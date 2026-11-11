using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Sc2Toolbox;

/// <summary>
/// 快捷施法（智能施法，仿《星际争霸 II》的「选择技能或 AI 目标」写法）：
/// 你按下技能键，助手在光标处补一下左键 —— 游戏里「选技能 → 再点目标」合成了一步，
/// 按住不放就按设定间隔连续补点，等于连发。
///
/// 为什么这样做，而不是自己合成「按键 + 左键」或者吞掉按键：
///   · 按键本身游戏已经收到了（Q/W/E/R 就是游戏自己的技能热键），我们只负责补最后那一下左键；
///   · 不吞键、不改写按键。就算注入被 Windows 的 UIPI 拦下，
///     最坏情况也只是回到「按 Q 再手动点左键」，不会出现按了没反应；
///   · 装的是「只看不改」的底层键盘钩子（永远放行给游戏），只为在按键的瞬间补点；
///     钩子装不上时自动退回 2ms 轮询，功能不受影响。
///
/// 只在这些条件下才动手：星际争霸 II 是前台窗口、光标在游戏窗口里、
/// 没按 Ctrl/Alt/Shift/Win、不在聊天框里（进游戏后按过回车就当作在打字）。
/// </summary>
public static class QuickCast
{
	/// <summary>默认参与快捷施法的技能键（东方月灵传的技能栏 t/q/w/e/r）。</summary>
	public const string DefaultKeys = "Q,W,E,R,T";

	/// <summary>是否正在接管。</summary>
	public static bool Enabled { get; private set; }

	/// <summary>当前生效的技能键，逗号分隔的大写形式。</summary>
	public static string KeysText { get; private set; } = DefaultKeys;

	/// <summary>当前补的是哪个鼠标键：left / right / middle。</summary>
	public static string ButtonText { get; private set; } = "left";

	/// <summary>游戏里聊天框是不是开着（开着就不补点，免得打字变成施法）。</summary>
	public static bool ChatOpen
	{
		get
		{
			return _chat;
		}
	}

	/// <summary>补完点之后要不要跟着点一下右键取消残留的技能选择状态。</summary>
	public static bool CancelWithRight { get; private set; } = true;

	/// <summary>最近一条状态/错误说明，直接显示在面板上。</summary>
	public static string LastNote { get; private set; } = "";

	/// <summary>本次运行一共补了多少次左键。</summary>
	public static int Casts { get; private set; }

	/// <summary>最近一次补左键的时间。</summary>
	public static DateTime LastCast { get; private set; } = DateTime.MinValue;

	/// <summary>状态有变化（给界面刷新用，可能来自后台线程）。</summary>
	public static event Action Changed;

	private static readonly object Gate = new object();
	private static readonly List<int> Keys = new List<int>();
	private static readonly Dictionary<int, long> LastByKey = new Dictionary<int, long>();
	private static readonly StringBuilder Cls = new StringBuilder(64);

	private static Thread _thread;
	private static volatile bool _run;
	private static volatile int _delayMs = 30;
	private static volatile int _gapMs = 100;
	private static bool _chat;
	private static long _lastNoteAt;
	private static string _lastHint = "";
	private static bool _timerFast;
	private static volatile bool _safeMouse = true;
	private static volatile bool _instantClick = true;
	private static long _injectUntil;
	private static long _lastUserRight;
	private static long _lastUserLeft;

	/// <summary>按下技能键的那一下由键盘钩子直接处理，轮询只负责「按住连发」。</summary>
	private static HookProc _hookKeepAlive;	private static nint _hook;
	private static Thread _hookThread;
	private static uint _hookThreadId;
	private static volatile bool _hookOk;
	private static long _lastHookAt;
	private static long _hookAll;
	private static long _hookHit;
	private static long _hookKeyAt;
	private static long _hookLogAt;

	private const int WH_KEYBOARD_LL = 13;
	private const int WM_KEYDOWN = 0x100;
	private const int WM_KEYUP = 0x101;
	private const int WM_SYSKEYDOWN = 0x104;
	private const int WM_SYSKEYUP = 0x105;
	private const int WM_QUIT = 0x12;
	private const int WM_APP_CAST = 0x8001;
	private const uint LLKHF_INJECTED = 0x10;

	private const int VK_ENTER = 0x0D;
	private const int VK_ESCAPE = 0x1B;
	private const int VK_SPACE = 0x20;
	private const int VK_TAB = 0x09;
	private const int VK_SHIFT = 0x10;
	private const int VK_CONTROL = 0x11;
	private const int VK_MENU = 0x12;
	private const int VK_LWIN = 0x5B;
	private const int VK_RWIN = 0x5C;
	private const int VK_LBUTTON = 0x01;
	private const int VK_RBUTTON = 0x02;
	private const int VK_MBUTTON = 0x04;

	/// <summary>补完点之后隔多久才补右键取消。</summary>
	private const int CancelDelayMs = 30;

	/// <summary>钩子里记着哪些键正按着，用来把系统自动重复的「按下」认出来。</summary>
	private static readonly HashSet<int> HookDown = new HashSet<int>();
	private static readonly object HookGate = new object();

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int x;

		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RECTV
	{
		public int l;

		public int t;

		public int r;

		public int b;
	}

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vk);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(nint h, StringBuilder s, int n);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT p);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint h, out RECTV r);

	[DllImport("winmm.dll")]
	private static extern uint timeBeginPeriod(uint ms);

	[DllImport("winmm.dll")]
	private static extern uint timeEndPeriod(uint ms);

	private delegate nint HookProc(int nCode, nint wParam, nint lParam);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint SetWindowsHookEx(int idHook, HookProc proc, nint hMod, uint threadId);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnhookWindowsHookEx(nint hook);

	[DllImport("user32.dll")]
	private static extern nint CallNextHookEx(nint hook, int nCode, nint wParam, nint lParam);

	[DllImport("user32.dll")]
	private static extern int GetMessage(out MSGV m, nint h, uint min, uint max);

	[DllImport("user32.dll")]
	private static extern bool TranslateMessage(ref MSGV m);

	[DllImport("user32.dll")]
	private static extern nint DispatchMessage(ref MSGV m);

	[DllImport("user32.dll")]
	private static extern bool PostThreadMessage(uint threadId, uint msg, nint w, nint l);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[StructLayout(LayoutKind.Sequential)]
	private struct MSGV
	{
		public nint hwnd;

		public uint message;

		public nint wParam;

		public nint lParam;

		public uint time;

		public int x;

		public int y;

		public uint lPrivate;
	}

	/// <summary>界面改完设置后调用：读 Ctx.S 决定开/关、换键、改延迟。</summary>
	public static void Apply()
	{
		Settings s = Ctx.S;
		// 按键由钩子当场确认，不再需要落点延迟；「鼠标正按着时不确认」永远开着，
		// 免得把你的框选/走位打断。这几个开关已经收进代码，界面上不再暴露。
		_delayMs = 0;
		_instantClick = true;
		_safeMouse = true;
		_gapMs = Math.Clamp(s.QuickCastGapMs, 40, 1000);
		ButtonText = NormButton(s.QuickCastButton);
		CancelWithRight = s.QuickCastCancelWithRight;
		string text = Normalize(s.QuickCastKeys);
		bool changed = text != KeysText || Keys.Count == 0;
		if (changed)
		{
			KeysText = text;
			Parse(text);
		}
		if (s.QuickCastEnabled)
		{
			if (changed)
			{
				Stop(quiet: true);
			}
			Start();
		}
		else
		{
			Stop();
		}
	}

	public static void Start()
	{
		if (_run)
		{
			Enabled = true;
			return;
		}
		if (Keys.Count == 0)
		{
			Enabled = false;
			Note("没有可用的技能键，快捷施法没有启动（点这里改成比如 Q,W,E,R,T）");
			return;
		}
		_chat = false;
		lock (Gate)
		{
			LastByKey.Clear();
		}
		_run = true;
		Enabled = true;
		// 默认的 Sleep 精度只有 15.6ms 左右，轮询会把「按键 → 补点」拖到一帧以上；
		// 把计时器精度提到 1ms，轮询间隔才真的接近 2ms，右键插进来的窗口就小得多
		if (!_timerFast)
		{
			try
			{
				timeBeginPeriod(1u);
				_timerFast = true;
			}
			catch
			{
			}
		}
		_thread = new Thread(Loop)
		{
			IsBackground = true,
			Name = "quickcast"
		};
		_thread.Start();
		StartHook();
		string text = "已启用：" + KeysText + " · 确认键 " + ButtonName(ButtonText);
		if (CancelWithRight && ButtonText != "right")
		{
			text += " + 补右键取消";
		}
		Note(text + " · 连发间隔 " + _gapMs + "ms");
	}

	public static void Stop(bool quiet = false)
	{
		bool flag = _run;
		_run = false;
		Enabled = false;
		_chat = false;
		StopHook();
		Thread thread = _thread;
		_thread = null;
		try
		{
			if (thread != null && thread.IsAlive)
			{
				thread.Join(500);
			}
		}
		catch
		{
		}
		if (!_run && _timerFast)
		{
			try
			{
				timeEndPeriod(1u);
				_timerFast = false;
			}
			catch
			{
			}
		}
		if (flag && !quiet)
		{
			Note("已停止");
		}
	}

	/// <summary>鼠标键名统一成 left / right / middle。</summary>
	public static string NormButton(string raw)
	{
		string text = (raw ?? "").Trim().ToLowerInvariant();
		if (text == "right" || text == "r" || text == "右键")
		{
			return "right";
		}
		if (text == "middle" || text == "m" || text == "中键")
		{
			return "middle";
		}
		return "left";
	}

	/// <summary>鼠标键的中文名，给界面和日志用。</summary>
	public static string ButtonName(string raw)
	{
		string text = NormButton(raw);
		if (text == "right")
		{
			return "右键";
		}
		if (text == "middle")
		{
			return "中键";
		}
		return "左键";
	}

	/// <summary>鼠标键名 → 虚拟键码（左键 0x01 / 右键 0x02 / 中键 0x04），用来判断你有没有正在按它。</summary>
	private static int ButtonVk(string raw)
	{
		string text = NormButton(raw);
		if (text == "right")
		{
			return VK_RBUTTON;
		}
		if (text == "middle")
		{
			return VK_MBUTTON;
		}
		return VK_LBUTTON;
	}

	/// <summary>鼠标正忙着：任何键按着，或者右键刚按过（那一下自己就会取消技能，不用我们补）。</summary>
	private static bool MouseBusy()
	{
		if (Down(VK_LBUTTON) || Down(VK_RBUTTON) || Down(VK_MBUTTON))
		{
			return true;
		}
		return PressedSince(VK_RBUTTON);
	}

	/// <summary>把「这一下为什么没补」写到状态行；同一句话只记一次日志，免得按住不放时刷屏。</summary>
	private static void Hint(string text)
	{
		LastNote = text;
		if (_lastHint != text)
		{
			_lastHint = text;
			L.W("[快捷施法] " + text);
		}
		RaiseThrottled();
	}

	/// <summary>把「q, w、e; R T」这类输入整理成「Q,W,E,R,T」，认不出来的键直接丢掉。</summary>
	public static string Normalize(string raw)	{
		List<string> list = new List<string>();
		string[] array = (raw ?? "").Split(new char[5] { ',', '，', ' ', ';', '、' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			string text2 = text.Trim().ToUpperInvariant();
			if (text2.Length != 0 && Vk(text2) != 0 && !list.Contains(text2))
			{
				list.Add(text2);
			}
		}
		return string.Join(",", list);
	}

	private static void Parse(string text)
	{
		lock (Gate)
		{
			Keys.Clear();
			LastByKey.Clear();
			string[] array = text.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string text2 in array)
			{
				int num = Vk(text2.Trim().ToUpperInvariant());
				if (num != 0 && !Keys.Contains(num))
				{
					Keys.Add(num);
				}
			}
		}
	}

	/// <summary>键名 → 虚拟键码。只支持字母、数字、F1~F12 和几个常用键。</summary>
	private static int Vk(string name)
	{
		if (name.Length == 1)
		{
			char c = name[0];
			if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
			{
				return c;
			}
		}
		if (name.Length >= 2 && name[0] == 'F' && int.TryParse(name.Substring(1), out var result) && result >= 1 && result <= 12)
		{
			return 0x70 + (result - 1);
		}
		switch (name)
		{
		case "SPACE":
			return VK_SPACE;
		case "TAB":
			return VK_TAB;
		case "ENTER":
			return VK_ENTER;
		case "ESC":
		case "ESCAPE":
			return VK_ESCAPE;
		default:
			return 0;
		}
	}

	/// <summary>装一个「只看不改」的底层键盘钩子。
	/// 钩子回调在按键进消息队列的那一刻就会被调用，比 2ms 轮询还早一截，
	/// 补点就能紧贴着按键排队，右键没有空档插进来。我们不吞键、不拦截，永远放行。</summary>
	private static void StartHook()
	{
		if (_hookOk || (_hookThread != null && _hookThread.IsAlive))
		{
			return;
		}
		_hookThread = new Thread(HookLoop)
		{
			IsBackground = true,
			Name = "quickcast-hook"
		};
		_hookThread.Start();
	}

	private static void StopHook()
	{
		_hookOk = false;
		nint hook = _hook;
		_hook = IntPtr.Zero;
		if (hook != IntPtr.Zero)
		{
			try
			{
				UnhookWindowsHookEx(hook);
			}
			catch
			{
			}
		}
		uint hookThreadId = _hookThreadId;
		if (hookThreadId != 0)
		{
			try
			{
				PostThreadMessage(hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
			}
			catch
			{
			}
		}
		_hookThreadId = 0u;
		_hookThread = null;
	}

	private static void HookLoop()
	{
		try
		{
			_hookThreadId = GetCurrentThreadId();
			_hookKeepAlive = HookCallback;
			_hook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookKeepAlive, IntPtr.Zero, 0u);
			if (_hook == IntPtr.Zero)
			{
				L.W("[快捷施法] 键盘钩子装不上（错误 " + Marshal.GetLastWin32Error() + "），退回 2ms 轮询");
				return;
			}
			_hookOk = true;
			L.W("[快捷施法] 键盘钩子已装：按下技能键的瞬间就补点");
			MSGV mSV;
			while (GetMessage(out mSV, IntPtr.Zero, 0u, 0u) > 0)
			{
				if (mSV.message == WM_APP_CAST)
				{
					Cast((int)mSV.wParam, instant: true);
					continue;
				}
				TranslateMessage(ref mSV);
				DispatchMessage(ref mSV);
			}
		}
		catch (Exception ex)
		{
			_hookOk = false;
			L.W("[快捷施法] 键盘钩子出错：" + ex.Message);
		}
	}

	private static nint HookCallback(int nCode, nint wParam, nint lParam)	{
		try
		{
			if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
			{
				int num = Marshal.ReadInt32(lParam);
				uint num2 = (uint)Marshal.ReadInt32(lParam, 8);
				if ((num2 & LLKHF_INJECTED) == 0)
				{
					Interlocked.Increment(ref _hookAll);
					HookKey(num);
				}
			}
			else if (nCode >= 0 && (wParam == WM_KEYUP || wParam == WM_SYSKEYUP))
			{
				// 松开就清掉「按住」标记：系统自动重复的按下事件不算新按一次
				int num3 = Marshal.ReadInt32(lParam);
				uint num4 = (uint)Marshal.ReadInt32(lParam, 8);
				if ((num4 & LLKHF_INJECTED) == 0)
				{
					lock (HookGate)
					{
						HookDown.Remove(num3);
					}
				}
			}
		}
		catch
		{
		}
		return CallNextHookEx(_hook, nCode, wParam, lParam);
	}

	/// <summary>最近一次处理回车/Esc 的时刻：钩子和轮询都会看到同一次按键，用它去重。</summary>
	private static long _chatKeyAt;

	/// <summary>游戏里按回车：开 / 关聊天框（同一次按键只算一次）。</summary>
	private static void ChatToggle()
	{
		long num = Environment.TickCount64;
		if (num - _chatKeyAt < 300)
		{
			return;
		}
		_chatKeyAt = num;
		_chat = !_chat;
		L.W("[快捷施法] 聊天框" + (_chat ? "打开，打字期间不补点" : "关闭，恢复补点"));
		RaiseThrottled();
	}

	/// <summary>按 Esc：聊天框关掉。</summary>
	private static void ChatClose()
	{
		long num = Environment.TickCount64;
		if (num - _chatKeyAt < 300)
		{
			return;
		}
		_chatKeyAt = num;
		if (_chat)
		{
			_chat = false;
			L.W("[快捷施法] 聊天框关闭，恢复补点");
			RaiseThrottled();
		}
	}

	/// <summary>钩子里处理一次按下：聊天框开关照旧，命中的技能键当场补点。</summary>
	private static void HookKey(int vk)
	{
		if (!_run)
		{
			return;
		}
		nint foregroundWindow = GetForegroundWindow();
		if (!OnGame(foregroundWindow))
		{
			return;
		}
		if (vk == VK_ENTER)
		{
			ChatToggle();
			return;
		}
		if (vk == VK_ESCAPE)
		{
			ChatClose();
			return;
		}
		bool flag;
		lock (Gate)
		{
			flag = Keys.Contains(vk);
		}
		if (!flag || _chat)
		{
			return;
		}
		// 系统自动重复（按住不放）也会发「按下」，那不是新按一次；
		// 连发交给轮询按设定的间隔做，这里只管「刚按下去」这一下
		lock (HookGate)
		{
			if (!HookDown.Add(vk))
			{
				return;
			}
		}
		if (Down(VK_SHIFT) || Down(VK_CONTROL) || Down(VK_MENU) || Down(VK_LWIN) || Down(VK_RWIN))
		{
			return;
		}
		if (!MatchWatch.CanCast || !CursorInGame())
		{
			return;
		}
		long num = Environment.TickCount64;
		// 键盘的自动重复（按住不放）也会走这里，交给轮询按「连发间隔」处理
		long num2;
		lock (Gate)
		{
			LastByKey.TryGetValue(vk, out num2);
		}
		if (num - num2 < 30)
		{
			return;
		}
		lock (Gate)
		{
			LastByKey[vk] = num;
		}
		_lastHookAt = num;
		_hookKeyAt = num;
		Interlocked.Increment(ref _hookHit);
		// 关键：不能在钩子里直接注入鼠标。那一刻按键还没交给游戏，
		// 先注入的点击会排到按键前面去（等于「先点后按」，照样漏）。
		// 把活丢回自己的消息循环，等钩子返回、按键进了队列之后再补点
		if (!PostThreadMessage(_hookThreadId, WM_APP_CAST, vk, IntPtr.Zero))
		{
			Cast(vk, instant: true, "钩子");
		}
	}

	private static void Loop()
	{
		while (_run)
		{
			try
			{
				Tick();
			}
			catch (Exception ex)
			{
				Note("快捷施法出错：" + ex.Message);
				Thread.Sleep(500);
			}
			Thread.Sleep(2);
		}
	}

	private static void Tick()
	{
		// 对局状态自己限流到 1 秒一次，这里只是顺手驱动一下
		MatchWatch.Tick();
		long numNow = Environment.TickCount64;
		if (numNow - _hookLogAt >= 10000)
		{
			_hookLogAt = numNow;
			L.W("[快捷施法] 钩子状态：可用=" + _hookOk + "，收到按键 " + Interlocked.Read(ref _hookAll) + " 次，触发补点 " + Interlocked.Read(ref _hookHit) + " 次");
		}
		nint foregroundWindow = GetForegroundWindow();
		bool flag = OnGame(foregroundWindow);
		if (!flag)
		{
			// 切出游戏就把聊天框状态清掉，回来重新判断
			_chat = false;
			lock (HookGate)
			{
				HookDown.Clear();
			}
			// 在别的程序里按过的键，留着的「按过」标志要吃掉，否则切回游戏会立刻误补一下
			GetAsyncKeyState(VK_ENTER);
			GetAsyncKeyState(VK_ESCAPE);
			lock (Gate)
			{
				foreach (int item in Keys)
				{
					GetAsyncKeyState(item);
				}
			}
		}
		if (flag)
		{
			// 游戏里回车是开/关聊天框，Esc 是关聊天框（也是取消）。
			// 钩子和轮询都会看到同一次回车，所以两边都走 ChatToggle/ChatClose，内部按时间戳去重。
			if (PressedSince(VK_ENTER))
			{
				ChatToggle();
			}
			if (PressedSince(VK_ESCAPE))
			{
				ChatClose();
			}
			// 顺手记一下你自己按右键的时刻（排除我们自己注入的那一下），
			// 用来诊断「右键插进按键和补点之间」到底是谁先谁后
			long num0 = Environment.TickCount64;
			if (PressedSince(VK_RBUTTON) && num0 > _injectUntil)
			{
				_lastUserRight = num0;
			}
			if (PressedSince(VK_LBUTTON) && num0 > _injectUntil)
			{
				_lastUserLeft = num0;
			}		}
		if (!MatchWatch.CanCast)
		{
			// 房间里/菜单里不补点：读一下按住的键把一次性标志吃掉，免得回到对局时误触发
			lock (Gate)
			{
				foreach (int item in Keys)
				{
					GetAsyncKeyState(item);
				}
			}
			return;
		}
		bool flag2 = Down(VK_SHIFT) || Down(VK_CONTROL) || Down(VK_MENU) || Down(VK_LWIN) || Down(VK_RWIN);
		int[] array;
		lock (Gate)
		{
			array = Keys.ToArray();
		}
		long num = Environment.TickCount64;
		foreach (int item in array)
		{
			// 不管这次用不用得上都要读一下：低位的「自上次读取后按过」标志是一次性的
			short asyncKeyState = GetAsyncKeyState(item);
			bool flag3 = (asyncKeyState & 0x8000) != 0;
			bool flag4 = (asyncKeyState & 1) != 0;
			if ((!flag4 && !flag3) || !flag || _chat || flag2)
			{
				continue;
			}
			long num2 = 0L;
			lock (Gate)
			{
				LastByKey.TryGetValue(item, out num2);
			}
			if (flag4)
			{
				// 刚按下：钩子已经当场补过了，这里只负责钩子没装上的退路
				if (_hookOk && num - _lastHookAt < 200)
				{
					continue;
				}
				if (num - num2 < 30)
				{
					continue;
				}
			}
			else if (num - num2 < _gapMs)
			{
				// 按住不放：按设定的间隔连发
				continue;
			}
			if (!CursorInGame())
			{
				continue;
			}
			lock (Gate)
			{
				LastByKey[item] = num;
			}
			Cast(item, flag4, "轮询");
		}
	}

	/// <summary>补完点之后，过一小会儿在光标处补一下鼠标。
	/// instant = 这次是「刚按下」：按下瞬间就把点击排进消息队列，不睡落点延迟 ——
	/// 消息队列本身是有序的，游戏先收按键、后收点击，中间没有空档给右键插进来。</summary>
	private static void Cast(int vk, bool instant = false, string src = "轮询")
	{
		try
		{
			int delay = ((instant && _instantClick) ? 0 : _delayMs);
			if (delay > 0)
			{
				Thread.Sleep(delay);
			}
			if (!OnGame(GetForegroundWindow()) || !CursorInGame())
			{
				return;
			}
			if (!MatchWatch.CanCast)
			{
				return;
			}
			int buttonVk = ButtonVk(ButtonText);
			if (_safeMouse && buttonVk != 0 && Down(buttonVk))
			{
				// 你自己正按着要补的那个键（比如拖着左键框选、按着右键走位）：
				// 再叠一对模拟的按下/抬起，会把你的拖动打断，这一下就跳过
				Hint("你正按着" + ButtonName(ButtonText) + "，这一下不确认");
				return;
			}
			// 按下即点时按久一点（15ms）：万一游戏是按帧读鼠标状态而不是收消息，
			// 8ms 的点击可能整段落在两帧之间被漏掉，15ms 至少能占住一帧
			long num5 = Environment.TickCount64;
			string text3 = "[" + src + "]" + KeyName(vk);
			if (src == "钩子" && _hookKeyAt > 0)
			{
				text3 += " 按键→补点 " + (num5 - _hookKeyAt) + "ms";
			}
			if (_lastUserRight > 0 && num5 - _lastUserRight <= 300)
			{
				text3 += "，距你按右键 " + (num5 - _lastUserRight) + "ms";
			}
			L.W("[快捷施法] 诊断：" + text3);
			_injectUntil = num5 + 20;
			Sc2.ClickMouse(ButtonText, ((instant && _instantClick) ? 15 : 8));
			if (PressedSince(VK_RBUTTON))
			{
				L.W("[快捷施法] 诊断：" + KeyName(vk) + " 补点之后立刻看到右键按下（你的右键在补点之后 " + (Environment.TickCount64 - num5) + "ms 内到的）");
			}
			if (buttonVk != 0)
			{
				// 吃掉我们自己这次注入留下的「按过」标志，免得下一轮当成你按的
				GetAsyncKeyState(buttonVk);
			}
			if (CancelWithRight && ButtonText != "right")
			{
				// 等游戏把这个点处理完，再点一下右键，把还挂在光标上的技能选择状态取消掉
				Thread.Sleep(CancelDelayMs);
				if (!MouseBusy() && OnGame(GetForegroundWindow()) && CursorInGame())
				{
					Sc2.ClickMouse("right", 8);
					GetAsyncKeyState(VK_RBUTTON);
				}
			}
			Casts++;
			LastCast = DateTime.Now;
			RaiseThrottled();
		}
		catch (Exception ex)
		{
			Note("施法失败：" + ex.Message);
		}
	}

	/// <summary>键码 → 日志里好认的名字。</summary>
	private static string KeyName(int vk)
	{
		if ((vk >= 0x30 && vk <= 0x39) || (vk >= 0x41 && vk <= 0x5A))
		{
			return ((char)vk).ToString();
		}
		return "VK" + vk;
	}

	private static bool OnGame(nint h)
	{
		if (h == IntPtr.Zero)
		{
			return false;
		}
		Cls.Clear();
		GetClassName(h, Cls, Cls.Capacity);
		return Cls.ToString() == Sc2.WND_CLASS;
	}

	private static bool CursorInGame()
	{
		if (!GetCursorPos(out var p))
		{
			return true;
		}
		nint foregroundWindow = GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			return false;
		}
		if (!GetWindowRect(foregroundWindow, out var r))
		{
			return true;
		}
		if (p.x >= r.l && p.x < r.r && p.y >= r.t)
		{
			return p.y < r.b;
		}
		return false;
	}

	private static bool Down(int vk)
	{
		return (GetAsyncKeyState(vk) & 0x8000) != 0;
	}

	private static bool PressedSince(int vk)
	{
		return (GetAsyncKeyState(vk) & 1) != 0;
	}

	private static void RaiseThrottled()
	{
		long num = Environment.TickCount64;
		if (num - _lastNoteAt < 300)
		{
			return;
		}
		_lastNoteAt = num;
		try
		{
			QuickCast.Changed?.Invoke();
		}
		catch
		{
		}
	}

	private static void Note(string text)
	{
		LastNote = text;
		L.W("[快捷施法] " + text);
		try
		{
			QuickCast.Changed?.Invoke();
		}
		catch
		{
		}
	}
}
