using System;
using System.Threading;

namespace Sc2Toolbox;

public class KeepAlive
{
	private Thread _th;

	private volatile bool _run;

	private ManualResetEventSlim _wake = new ManualResetEventSlim(initialState: false);

	private string _lastSkipLog = "";

	public string Status { get; private set; } = "已停止";

	public DateTime LastPulse { get; private set; } = DateTime.MinValue;

	public string LastKind { get; private set; } = "";

	public int Pulses { get; private set; }

	public int Skips { get; private set; }

	public string Note { get; private set; } = "";

	public bool Running => _run;

	public event Action Changed;

	private void SkipLog(string why)
	{
		if (!(why == _lastSkipLog))
		{
			_lastSkipLog = why;
			L.W("等房跳过：" + why);
		}
	}

	private void Raise()
	{
		try
		{
			Changed?.Invoke();
		}
		catch
		{
		}
	}

	public void Start()
	{
		if (!_run)
		{
			_run = true;
			_wake = new ManualResetEventSlim(initialState: false);
			_th = new Thread(Loop)
			{
				IsBackground = true,
				Name = "keepalive"
			};
			_th.Start();
			L.W("等房已启动：方式=" + Ctx.S.KeepMode + " 间隔=" + Ctx.S.IntervalSec + "s 抢焦点兜底=" + Ctx.S.FocusEverySec + "s");
		}
	}

	public void Stop()
	{
		if (_run)
		{
			_run = false;
			try
			{
				_wake.Set();
			}
			catch
			{
			}
			Status = "已停止";
			L.W("挂机等房已停止");
			Raise();
		}
	}

	public void PulseNow()
	{
		try
		{
			_wake.Set();
		}
		catch
		{
		}
	}

	private void Loop()
	{
		DateTime nextFocus = DateTime.MinValue;
		while (_run)
		{
			try
			{
				Tick(ref nextFocus);
			}
			catch (Exception ex)
			{
				Status = "异常：" + ex.Message;
				L.W("等房异常: " + ex);
			}
			Raise();
			int millisecondsTimeout = Math.Max(2, Ctx.S.IntervalSec) * 1000;
			try
			{
				if (_wake.Wait(millisecondsTimeout))
				{
					_wake.Reset();
				}
			}
			catch
			{
			}
		}
	}

	public static string PauseReason()
	{
		try
		{
			LobbyState lobbyState = ((Ctx.Watcher != null) ? Ctx.Watcher.Last : null);
			if (lobbyState == null || !lobbyState.Ok)
			{
				return "";
			}
			if (string.IsNullOrEmpty(lobbyState.MyHandle))
			{
				return "";
			}
			if (lobbyState.Handles != null && lobbyState.Handles.Contains(lobbyState.MyHandle))
			{
				return "";
			}
			return "侦测到对局已开始（你已不在房间列表里），防挂机自动暂停";
		}
		catch
		{
			return "";
		}
	}

	private void Tick(ref DateTime nextFocus)
	{
		Settings s = Ctx.S;
		nint num = Sc2.Find();
		if (num == IntPtr.Zero)
		{
			Status = "没找到游戏窗口（游戏没运行？）";
			Note = "";
			SkipLog("没找到游戏窗口（游戏没运行？）");
			return;
		}
		if (Sc2.AnyMouseDown())
		{
			Skips++;
			Status = "你正按着鼠标键，本次跳过";
			SkipLog("你正按着鼠标键，本次跳过");
			return;
		}
		if (s.PauseInMatch)
		{
			string text = PauseReason();
			if (text.Length > 0)
			{
				Skips++;
				Status = text;
				Note = "";
				SkipLog(text);
				return;
			}
		}
		bool flag = s.KeepMode == "Hidden" || s.KeepMode == "Both";
		bool flag2 = s.KeepMode == "Focus" || s.KeepMode == "Both";
		bool flag3 = flag2 && DateTime.Now >= nextFocus;
		if (flag3 && s.DontInterruptTyping)
		{
			for (int i = 0; i < 8; i++)
			{
				if (!_run)
				{
					break;
				}
				if (Sc2.IdleMs() >= 1500)
				{
					break;
				}
				Thread.Sleep(1000);
			}
			if (Sc2.AnyMouseDown())
			{
				Skips++;
				Status = "你正按着鼠标键，本次跳过";
				return;
			}
		}
		Sc2.LastDiag = "";
		bool flag4 = false;
		bool flag5 = false;
		bool flag6 = false;
		string how = "";
		bool verified = false;
		if (flag)
		{
			// 游戏只要在前台就走真实输入（能被系统确认），不管选的是哪种模式。
			// 这样「两者都用」在前台也能确认成功，就不会再白抢一次焦点 —— 游戏里也就不会闪屏了。
			if (Sc2.Fg(num))
			{
				flag5 = true;
				flag6 = Sc2.Inject(num, s.KeepKeyboard, out how, out verified);
			}
			else
			{
				flag4 = Sc2.PostJiggle(num);
			}
		}
		if (flag3 && !verified)
		{
			// 只有「游戏在前台但这轮投递没能被系统确认」才需要抢焦点兜底。
			// 前台真实输入已经生效时（verified）绝不抢焦点 —— 抢焦点会把游戏切出去再切回来，屏幕上就是一闪。
			flag6 = Sc2.FocusPulse(num, s.KeepKeyboard, out how, out verified);
		}
		if (flag6)
		{
			// 抢焦点兜底成功、或游戏本来就在前台且输入已生效 —— 两种都说明
			// 这段时间没必要再抢焦点，把兜底时间往后推，避免在游戏里反复闪屏。
			nextFocus = DateTime.Now.AddSeconds(flag ? Math.Max(10, s.FocusEverySec) : Math.Max(5, s.IntervalSec));
		}
		if (flag6)
		{
			Pulses++;
			LastPulse = DateTime.Now;
			KeepAlive keepAlive = this;
			string lastKind;
			if (flag5)
			{
				lastKind = "静默·前台注入";
			}
			else
			{
				lastKind = (flag4 ? "抢焦点+静默" : "抢焦点");
			}
			keepAlive.LastKind = lastKind;
			string text2 = ((how.Length > 0) ? (" / " + how) : "");
			text2 += (verified ? " · 系统已确认收到" : " · 未自检（你刚才也在动键鼠）");
			Status = "已等房（" + LastKind + text2 + "）";
			Note = "";
			_lastSkipLog = "";
			L.W("脉冲 #" + Pulses + "（" + LastKind + text2 + "）" + ((Sc2.LastDiag.Length > 0) ? ("  [" + Sc2.LastDiag + "]") : ""));
		}
		else if (flag5)
		{
			Status = "等房失败：游戏在前台，注入没被系统接受" + ((Sc2.LastDiag.Length > 0) ? ("（" + Sc2.LastDiag + "）") : "");
			Note = (Admin.IsElevated ? "已经用管理员权限运行，但注入仍未生效。可以试试把等房方式改成「抢焦点」。" : "当前不是管理员身份运行！星际争霸 II 是高完整性进程，普通权限注入的鼠标/键盘会被 Windows 静默挡下。请关掉本工具，右键「以管理员身份运行」。");
			L.W("脉冲失败（静默·前台注入未生效）：" + Sc2.LastDiag);
		}
		else if (flag3)
		{
			Status = "等房失败：注入没被系统接受" + ((Sc2.LastDiag.Length > 0) ? ("（" + Sc2.LastDiag + "）") : "");
			Note = (Admin.IsElevated ? "已经用管理员权限运行，但注入仍未生效。可以试试把等房方式改成「抢焦点」，或确认游戏窗口没有被别的东西挡住。" : "当前不是管理员身份运行！星际争霸 II 是高完整性进程，普通权限注入的鼠标/键盘会被 Windows 静默挡下（已实测：游戏在前台时注入无效，5 分钟必被踢）。请关掉本工具，右键「以管理员身份运行」。");
			L.W("脉冲失败：注入未生效 " + Sc2.LastDiag);
		}
		else if (flag4)
		{
			Pulses++;
			LastPulse = DateTime.Now;
			LastKind = "静默·投递消息";
			Status = "已投递静默消息（无法确认游戏是否记账）";
			Note = "「静默」= 绝不抢你的焦点：只往游戏窗口投递鼠标消息。游戏本来就在前台时它会自动改用真实输入（能自检），但游戏在后台/最小化时，这种消息投递是没法确认游戏记不记账的（系统空闲计时器不会变化，也就无法自检）。要保证一定生效，请把等房方式改成「抢焦点」或「两者都用」。";
			_lastSkipLog = "";
			L.W("脉冲 #" + Pulses + "（静默 · 无法自检）" + ((Sc2.LastDiag.Length > 0) ? ("  [" + Sc2.LastDiag + "]") : ""));
		}
		else if (flag)
		{
			Status = "静默投递被系统拒绝";
			Note = "静默模式靠 PostMessage，如果游戏是管理员进程会被系统挡下（返回值 False）。解决办法：右键本工具「以管理员身份运行」，或把等房方式改成「抢焦点」。";
		}
	}
}
