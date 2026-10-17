using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace Sc2Toolbox;

public static class Sc2
{
	private delegate bool EnumProc(nint h, nint p);

	private struct RECT
	{
		public int l;

		public int t;

		public int r;

		public int b;
	}

	private struct LASTINPUTINFO
	{
		public uint cbSize;

		public uint dwTime;
	}

	private struct MOUSEINPUT
	{
		public int dx;

		public int dy;

		public uint mouseData;

		public uint dwFlags;

		public uint time;

		public nint extra;
	}

	private struct KEYBDINPUT
	{
		public ushort wVk;

		public ushort wScan;

		public uint dwFlags;

		public uint time;

		public nint extra;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct INPUTUNION
	{
		[FieldOffset(0)]
		public MOUSEINPUT mi;

		[FieldOffset(0)]
		public KEYBDINPUT ki;
	}

	private struct INPUT
	{
		public uint type;

		public INPUTUNION u;
	}

	private const int SW_RESTORE = 9;

	private const int SW_MINIMIZE = 6;

	private const byte VK_MENU = 18;

	private const byte VK_RETURN = 13;

	private const byte VK_BACK = 8;

	private const byte VK_A = 65;

	private const uint KEYEVENTF_KEYUP = 2u;

	private const uint INPUT_MOUSE = 0u;

	private const uint INPUT_KEYBOARD = 1u;

	private const uint MOUSEEVENTF_MOVE = 1u;

	private const uint MOUSEEVENTF_ABSOLUTE = 32768u;

	private const uint MOUSEEVENTF_WHEEL = 2048u;

	private const uint WM_MOUSEMOVE = 512u;

	private const uint WM_NCMOUSEMOVE = 160u;

	public const string WND_CLASS = "StarCraft II";

	public static string LastDiag = "";

	public static nint Foreground => GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumProc cb, nint p);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(nint h, StringBuilder s, int n);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint h);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(nint h);

	[DllImport("user32.dll")]
	private static extern bool GetClientRect(nint h, out RECT r);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint h);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint h, int cmd);

	[DllImport("user32.dll")]
	private static extern bool BringWindowToTop(nint h);

	[DllImport("user32.dll")]
	private static extern nint SetActiveWindow(nint h);

	[DllImport("user32.dll")]
	private static extern void SwitchToThisWindow(nint h, bool alt);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint h, nint pid);

	[DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
	private static extern uint GetWindowPid(nint h, out uint pid);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("user32.dll")]
	private static extern bool AttachThreadInput(uint a, uint b, bool attach);

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte vk, byte scan, uint flags, nuint extra);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vk);

	[DllImport("user32.dll")]
	private static extern bool PostMessage(nint h, uint msg, nint w, nint l);

	[DllImport("user32.dll")]
	private static extern bool GetLastInputInfo(ref LASTINPUTINFO li);

	[DllImport("user32.dll")]
	private static extern uint SendInput(uint n, INPUT[] inputs, int size);

	[DllImport("user32.dll")]
	private static extern bool SetCursorPos(int x, int y);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out Point p);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint h, out RECT r);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int i);

	public static nint Find()
	{
		nint best = IntPtr.Zero;
		long bestArea = long.MinValue;
		EnumWindows((nint h, nint p) =>
		{
			StringBuilder stringBuilder = new StringBuilder(256);
			GetClassName(h, stringBuilder, stringBuilder.Capacity);
			if (stringBuilder.ToString() != "StarCraft II")
			{
				return true;
			}
			if (!IsWindowVisible(h))
			{
				return true;
			}
			GetClientRect(h, out var r);
			long num = (long)(r.r - r.l) * (long)(r.b - r.t);
			if (IsIconic(h))
			{
				num = -1L;
			}
			if (num > bestArea)
			{
				bestArea = num;
				best = h;
			}
			return true;
		}, IntPtr.Zero);
		return best;
	}

	public static bool Visible(nint h)
	{
		if (h != IntPtr.Zero && IsWindowVisible(h))
		{
			return !IsIconic(h);
		}
		return false;
	}

	public static bool Fg(nint h)
	{
		if (Visible(h))
		{
			return GetForegroundWindow() == h;
		}
		return false;
	}

	public static uint IdleMs()
	{
		LASTINPUTINFO li = new LASTINPUTINFO
		{
			cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO))
		};
		if (!GetLastInputInfo(ref li))
		{
			return 0u;
		}
		return (uint)Environment.TickCount - li.dwTime;
	}

	public static bool AnyMouseDown()
	{
		if ((GetAsyncKeyState(1) & 0x8000) == 0 && (GetAsyncKeyState(2) & 0x8000) == 0)
		{
			return (GetAsyncKeyState(4) & 0x8000) != 0;
		}
		return true;
	}

	private static bool WaitFg(nint h, int ms)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < ms)
		{
			if (GetForegroundWindow() == h)
			{
				return true;
			}
			Thread.Sleep(20);
		}
		return GetForegroundWindow() == h;
	}

	public static bool EnsureForeground(nint h)
	{
		if (Fg(h))
		{
			return true;
		}
		if (IsIconic(h) || !IsWindowVisible(h))
		{
			ShowWindow(h, 9);
			if (WaitFg(h, 900))
			{
				return true;
			}
		}
		keybd_event(18, 0, 0u, UIntPtr.Zero);
		keybd_event(18, 0, 2u, UIntPtr.Zero);
		Thread.Sleep(60);
		SetForegroundWindow(h);
		if (WaitFg(h, 500))
		{
			return true;
		}
		uint currentThreadId = GetCurrentThreadId();
		uint windowThreadProcessId = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
		uint windowThreadProcessId2 = GetWindowThreadProcessId(h, IntPtr.Zero);
		bool flag = false;
		bool flag2 = false;
		try
		{
			if (windowThreadProcessId != 0 && windowThreadProcessId != currentThreadId)
			{
				flag = AttachThreadInput(windowThreadProcessId, currentThreadId, attach: true);
			}
			if (windowThreadProcessId2 != 0 && windowThreadProcessId2 != currentThreadId)
			{
				flag2 = AttachThreadInput(windowThreadProcessId2, currentThreadId, attach: true);
			}
			BringWindowToTop(h);
			SetForegroundWindow(h);
			SetActiveWindow(h);
		}
		finally
		{
			if (flag)
			{
				AttachThreadInput(windowThreadProcessId, currentThreadId, attach: false);
			}
			if (flag2)
			{
				AttachThreadInput(windowThreadProcessId2, currentThreadId, attach: false);
			}
		}
		if (WaitFg(h, 500))
		{
			return true;
		}
		SwitchToThisWindow(h, alt: true);
		return WaitFg(h, 400);
	}

	private static void SendMouse(int dx, int dy, uint flags, uint data = 0u)
	{
		INPUT[] array = new INPUT[1];
		array[0].type = 0u;
		array[0].u.mi.dx = dx;
		array[0].u.mi.dy = dy;
		array[0].u.mi.dwFlags = flags;
		array[0].u.mi.mouseData = data;
		SendInput(1u, array, Marshal.SizeOf(typeof(INPUT)));
	}

	private static void SendKey(byte vk)
	{
		INPUT[] array = new INPUT[1];
		array[0].type = 1u;
		array[0].u.ki.wVk = vk;
		INPUT[] array2 = new INPUT[1];
		array2[0].type = 1u;
		array2[0].u.ki.wVk = vk;
		array2[0].u.ki.dwFlags = 2u;
		SendInput(1u, array, Marshal.SizeOf(typeof(INPUT)));
		Thread.Sleep(45);
		SendInput(1u, array2, Marshal.SizeOf(typeof(INPUT)));
	}

	public static void Jiggle()
	{
		int[][] array = new int[4][]
		{
			new int[2] { 2, 0 },
			new int[2] { -2, 0 },
			new int[2] { 2, 0 },
			new int[2] { -2, 0 }
		};
		foreach (int[] array2 in array)
		{
			SendMouse(array2[0], array2[1], 1u);
			Thread.Sleep(35);
		}
	}

	public static bool Counted()
	{
		return IdleMs() < 900;
	}

	public static string GameExePath()
	{
		try
		{
			nint num = Find();
			if (num != IntPtr.Zero)
			{
				GetWindowPid(num, out var pid);
				if (pid != 0)
				{
					string fileName = Process.GetProcessById((int)pid).MainModule.FileName;
					if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
					{
						return fileName;
					}
				}
			}
		}
		catch
		{
		}
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "StarCraft II", "ExecuteInfo.txt");
			if (File.Exists(path))
			{
				string[] array = File.ReadAllLines(path);
				foreach (string text in array)
				{
					int num2 = text.IndexOf('=');
					if (num2 > 0 && text.Substring(0, num2).Trim().Equals("executable", StringComparison.OrdinalIgnoreCase))
					{
						string text2 = text.Substring(num2 + 1).Trim();
						if (File.Exists(text2))
						{
							return text2;
						}
					}
				}
			}
		}
		catch
		{
		}
		return "";
	}

	public static bool DisableFullscreenOptimizations(out string info)
	{
		info = "";
		string text = GameExePath();
		if (string.IsNullOrEmpty(text))
		{
			info = "没找到星际争霸 II 的可执行文件路径";
			return false;
		}
		try
		{
			using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers"))
			{
				string obj = (registryKey.GetValue(text) as string) ?? "";
				List<string> list = new List<string>();
				string[] array = obj.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string text2 in array)
				{
					if (text2 != "~" && text2 != "DISABLEDXMAXIMIZEDWINDOWEDMODE")
					{
						list.Add(text2);
					}
				}
				list.Insert(0, "DISABLEDXMAXIMIZEDWINDOWEDMODE");
				registryKey.SetValue(text, "~ " + string.Join(" ", list.ToArray()));
			}
			info = text;
			return true;
		}
		catch (Exception ex)
		{
			info = ex.Message;
			return false;
		}
	}

	public static bool Inject(nint h, bool allowKeyboard, out string how, out bool verified)
	{
		how = "";
		verified = false;
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = IdleMs() >= 1500;
		try
		{
			Jiggle();
		}
		catch
		{
		}
		Thread.Sleep(120);
		if (Counted())
		{
			how = "鼠标微动";
			verified = flag;
			LastDiag = "微动 " + (flag ? "✓" : "· 未自检");
			return true;
		}
		if (!flag)
		{
			how = "鼠标微动";
			LastDiag = "已注入（无法自检：你刚动过键鼠）";
			return true;
		}
		stringBuilder.Append("微动✗ ");
		GetCursorPos(out var p);
		try
		{
			GetWindowRect(h, out var r);
			int num = Math.Max(2, GetSystemMetrics(0));
			int num2 = Math.Max(2, GetSystemMetrics(1));
			int num3 = (r.l + r.r) / 2;
			SendMouse(dy: (int)((long)((r.t + r.b) / 2) * 65535L / (num2 - 1)), dx: (int)((long)num3 * 65535L / (num - 1)), flags: 32769u);
			Thread.Sleep(150);
			if (Counted())
			{
				how = "光标移动到游戏窗口";
				SetCursorPos(p.X, p.Y);
				LastDiag = stringBuilder.ToString() + "绝对移动 ✓";
				verified = true;
				return true;
			}
			stringBuilder.Append("绝对移动✗ ");
		}
		catch
		{
			stringBuilder.Append("绝对移动! ");
		}
		try
		{
			SetCursorPos(p.X, p.Y);
		}
		catch
		{
		}
		if (allowKeyboard)
		{
			try
			{
				SendKey(13);
				Thread.Sleep(240);
				SendKey(65);
				Thread.Sleep(150);
				SendKey(8);
				Thread.Sleep(150);
				SendKey(13);
				Thread.Sleep(150);
				if (Counted())
				{
					how = "房间聊天框按键";
					LastDiag = stringBuilder.ToString() + "键盘 ✓";
					verified = true;
					return true;
				}
				stringBuilder.Append("键盘✗ ");
			}
			catch
			{
				stringBuilder.Append("键盘! ");
			}
		}
		try
		{
			keybd_event(18, 0, 0u, UIntPtr.Zero);
			keybd_event(18, 0, 2u, UIntPtr.Zero);
			Thread.Sleep(150);
			if (Counted())
			{
				how = "Alt 轻敲";
				LastDiag = stringBuilder.ToString() + "Alt ✓";
				verified = true;
				return true;
			}
			stringBuilder.Append("Alt✗");
		}
		catch
		{
			stringBuilder.Append("Alt!");
		}
		LastDiag = stringBuilder.ToString();
		return false;
	}

	/// <summary>玩家最近 2.5 秒内还在动键鼠 —— 说明他很可能正在聊天框里打字，
	/// 这时候绝对不能往游戏里发回车（会把他打的字直接发出去）。</summary>
	public const int ChatPokeGuardMs = 2500;


	/// <summary>大厅/对局里「敲一下回车打开聊天框，再按 Esc 关掉」——不发任何消息。
	/// 大厅等房时比抖鼠标更不容易被当成挂机，而且不抢焦点、不闪屏、最小化也生效。
	/// ★ Esc 在 SC2 里是取消/关闭键（框没开时按会关房间、弹菜单），所以：
	///   ① 只在「我们确定框是开的」时候才发 Esc；
	///   ② 玩家最近 2.5 秒内有输入（可能在打字）就整轮跳过。</summary>
	/// <summary>true = 用方向键保活（默认）；false = 旧的聊天框 Enter/Esc 方案。</summary>
	public static bool UseArrowPoke { get; set; } = true;

	public static bool ChatPoke(nint h, out string how)
	{
		uint idle = IdleMs();
		if (idle < ChatPokeGuardMs)
		{
			how = "你正在操作，跳过聊天框（" + idle + "ms 前有输入）";
			return false;
		}
		how = "回车开聊天框";
		try
		{
			// ★ 草稿闸门已彻底移除。实测那两个「固定槽位」其实是「上一次输入」的缓冲，
			//   聊天框关掉之后里面仍然留着上次打的字，所以它不能代表「框现在开着」。
			//   拿它当依据去按 Esc，就会在框其实关着时把房间关掉 / 弹出菜单（已多次踩到）。
			//   现在唯一的 Esc 就是下面紧跟在 Enter 之后那一下 —— 那时框必定是开的。
			//   顺便记一下槽位内容，方便观察它在关闭后是否残留。
			string draft = TouhouChat.DraftAtFixed();
			if (!string.IsNullOrEmpty(draft))
			{
				L.W("聊天框槽位残留：" + draft + "（仅记录，不做动作）");
			}
			// ★ 现在改成「只按方向键」，完全不碰 Enter / Esc。
			//   理由：回车会打开聊天框，如果玩家那边框本来就开着，这一下回车就把草稿发出去了；
			//   而 Esc 在框没开时会关房间 / 弹菜单。两个键都有副作用，方向键没有。
			//   在大厅房间里方向键只是转视角/无操作，在任何界面都不会打开输入框。
			if (UseArrowPoke)
			{
				nint[] keys = new nint[4] { 0x25, 0x26, 0x27, 0x28 };
				for (int i = 0; i < keys.Length; i++)
				{
					PostMessage(h, 0x100u, keys[i], (nint)0x014B0001);
					Thread.Sleep(30);
					PostMessage(h, 0x101u, keys[i], (nint)0xC14B0001u);
					Thread.Sleep(40);
				}
				how = "方向键";
				return true;
			}
			// 旧方案（聊天框）：Enter 开框 → 等框出来 → Esc 关框。
			//    ★ 这里绝不再补任何「前导 Esc」—— 框没开时按 Esc 会关房间/弹菜单。
			bool flag = PostMessage(h, 0x100u, (nint)13, (nint)0x001C0001);
			Thread.Sleep(40);
			PostMessage(h, 0x101u, (nint)13, (nint)0xC01C0001u);
			Thread.Sleep(260);
			bool flag2 = PostMessage(h, 0x100u, (nint)27, (nint)0x00010001);
			Thread.Sleep(40);
			PostMessage(h, 0x101u, (nint)27, (nint)0xC0010001u);
			Thread.Sleep(40);
			return flag | flag2;
		}
		catch (Exception ex)
		{
			// 出异常时保留 _chatBoxOpen，下一轮会先补一个安全的 Esc 再继续。
			how = "回车失败：" + ex.GetType().Name;
			return false;
		}
	}

	public static bool PostJiggle(nint h)
	{
		GetClientRect(h, out var r);
		int num = Math.Max(1, (r.r - r.l) / 2);
		int num2 = Math.Max(1, (r.b - r.t) / 2);
		nint l = (num2 << 16) | (num & 0xFFFF);
		nint l2 = (num2 << 16) | ((num + 2) & 0xFFFF);
		bool flag = PostMessage(h, 512u, IntPtr.Zero, l);
		Thread.Sleep(30);
		bool num3 = flag | PostMessage(h, 512u, IntPtr.Zero, l2);
		Thread.Sleep(30);
		return num3 | PostMessage(h, 512u, IntPtr.Zero, l) | PostMessage(h, 160u, 2, l);
	}

	public static bool FocusPulse(nint h, bool allowKeyboard, out string how, out bool verified)
	{
		how = "";
		verified = false;
		nint foregroundWindow = GetForegroundWindow();
		bool flag = IsIconic(h);
		if (!EnsureForeground(h))
		{
			return false;
		}
		try
		{
			return Inject(h, allowKeyboard, out how, out verified);
		}
		finally
		{
			if (flag)
			{
				ShowWindow(h, 6);
			}
			if (foregroundWindow != IntPtr.Zero && foregroundWindow != h)
			{
				try
				{
					SetForegroundWindow(foregroundWindow);
				}
				catch
				{
				}
			}
		}
	}
}
