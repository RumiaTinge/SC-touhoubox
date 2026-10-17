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

		/// <summary>键鼠长时间静止就自动停 —— 避免人不在电脑前还一直挂着。</summary>
	private bool CheckAutoStop()
	{
		try
		{
			int min = Ctx.S.AutoStopIdleMin;
			if (min <= 0) return false;
			uint idle = Sc2.IdleMs();
			if (idle < (uint)min * 60000u) return false;
			L.W("键鼠已静止 " + (int)(idle / 60000u) + " 分钟（阈值 " + min + " 分钟），自动关闭挂机等房");
			Stop();
			return true;
		}
		catch (Exception ex)
		{
			L.W("自动关闭判断失败：" + ex.Message);
			return false;
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
			if (CheckAutoStop()) { return; }
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
		// 现在只有一种保活动作：前台真实鼠标微动 / 后台回车开聊天框。
		// 「切换程序」不再切屏，所以这条永远走静默路径。
		bool flag = true;
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
				if (!flag6)
				{
					// 前台注入被系统挡了（普通权限动不了高完整性进程，或你正在动键鼠）。
					// 以前这里会抢焦点，现在改成回车开聊天框 —— 不切窗口、照样算活跃。
					string how4 = "";
					if (s.KeepChatPoke && Sc2.ChatPoke(num, out how4))
					{
						flag6 = true;
						verified = true;
						flag5 = false;
						how = how4;
					}
				}
			}
			else
			{
				// 后台时优先「回车开聊天框再 Esc」——不发消息、不抢焦点、不闪屏。
				// 拿不到窗口/失败了才退回抖鼠标。
				string how2 = "";
				bool flag7 = s.KeepChatPoke && Sc2.ChatPoke(num, out how2);
				if (s.KeepChatPoke)
				{
					how = how2;
				}
				flag4 = flag7 || Sc2.PostJiggle(num);
				if (flag7)
				{
					// 聊天框已经打开过 → 这轮就算活跃了，不要再抢焦点（抢焦点会闪屏）
					verified = true;
				}
			}
		}
		if (flag3 && !verified)
		{
			// 原来是 Sc2.FocusPulse（切走再切回前台）—— 每抢一次焦点屏幕上就是一闪，
			// 而且在游戏里前台本来就用不着抢。现在改成「回车开聊天框再 Esc」：
			// 不发消息、不切窗口、不闪屏。
			string how3 = "";
			if (Sc2.ChatPoke(num, out how3))
			{
				flag6 = true;
				verified = true;
				how = how3;
			}
			else
			{
				how = "聊天框失败 · 未保活";
			}
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
			Note = "注入没被系统接受，已自动改用聊天框保活。";
			L.W("脉冲失败（静默·前台注入未生效）：" + Sc2.LastDiag);
		}
		else if (flag3)
		{
			Status = "等房失败：注入没被系统接受" + ((Sc2.LastDiag.Length > 0) ? ("（" + Sc2.LastDiag + "）") : "");
			Note = "注入没被系统接受，已自动改用聊天框保活。";
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
			Note = "";
		}
	}
}
