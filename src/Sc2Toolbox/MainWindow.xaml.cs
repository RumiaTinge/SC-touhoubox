using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Sc2Toolbox;

public partial class MainWindow : Window, IComponentConnector, IStyleConnector
{
	private bool _ready;

	private bool _sync;

	private bool _exit;

	private readonly ConcurrentQueue<string> _pending = new ConcurrentQueue<string>();

	private readonly DispatcherTimer _flush;

	private int _lines;

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint h, int attr, ref int val, int size);

	public MainWindow()
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected Obj, but got Unknown
		InitializeComponent();
		Ctx.Keep.Changed += () =>
		{
			try
			{
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(RefreshKeepStatus), Array.Empty<object>());
			}
			catch
			{
			}
		};
		L.OnLine += (string line) =>
		{
			_pending.Enqueue(line);
		};
		_flush = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(400L, 0L)
		};
		_flush.Tick += delegate
		{
			FlushLog();
		};
		_flush.Start();
		SyncFromSettings();
		_ready = true;
		RefreshKeepStatus();
		TxtAboutPaths.Text = "设置文件：" + Settings.FilePath + "\n日志文件：" + L.File + "\n房间接口：POST http://127.0.0.1:" + Ctx.S.LobbyPort + "/lobbyPlayerList\n提示：如果「静默」保活一直失败，把本工具改成以管理员身份运行。";
		TbLog.Text = L.Tail(200);
		RefreshBank();
		_bankWatch = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(8.0)
		};
		_bankWatch.Tick += delegate
		{
			CheckBankChanged();
		};
		_bankWatch.Start();
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		try
		{
			nint handle = new WindowInteropHelper(this).Handle;
			int val = 1;
			DwmSetWindowAttribute(handle, 20, ref val, 4);
		}
		catch
		{
		}
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ButtonState != MouseButtonState.Pressed)
		{
			return;
		}
		try
		{
			DragMove();
		}
		catch
		{
		}
	}

	private void BtnMin_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void BtnMax_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = ((base.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized);
	}

	private void BtnClose_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	protected override void OnStateChanged(EventArgs e)
	{
		base.OnStateChanged(e);
		try
		{
			bool flag = base.WindowState == WindowState.Maximized;
			GlyphMax.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			GlyphRestore.Visibility = (flag ? Visibility.Visible : Visibility.Collapsed);
			base.BorderThickness = (flag ? new Thickness(7.0) : new Thickness(0.0));
		}
		catch
		{
		}
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!_exit)
		{
			if (Ctx.S.CloseToTray)
			{
				e.Cancel = true;
				Hide();
				L.W("面板已隐藏到托盘（程序还在后台跑，托盘图标右键可以退出）");
				if (!Ctx.S.TrayHintOnClose)
				{
					return;
				}
				try
				{
					if (Application.Current is App app)
					{
						app.ShowTrayHint();
					}
					return;
				}
				catch
				{
					return;
				}
			}
			e.Cancel = true;
			L.W("关闭面板：设置里没有勾「关闭后隐藏到托盘」，直接退出程序");
			try
			{
				if (Application.Current is App app2)
				{
					app2.QuitFromUi();
					return;
				}
			}
			catch
			{
			}
			_exit = true;
		}
		Ctx.S.Save();
		base.OnClosing(e);
	}

	public void RealClose()
	{
		_exit = true;
		try
		{
			Close();
		}
		catch
		{
		}
	}

	public void SyncFromSettings()
	{
		_sync = true;
		Settings s = Ctx.S;
		try
		{
			SwOverlay.IsChecked = s.OverlayEnabled;
			SwClickThrough.IsChecked = s.OverlayClickThrough;
			SwNick.IsChecked = s.OverlayShowNick;
			SwOnlyFilled.IsChecked = s.OverlayOnlyFilled;
			SldOpacity.Value = Math.Max(SldOpacity.Minimum, Math.Min(SldOpacity.Maximum, s.OverlayOpacity));
			TxtOpacity.Text = (int)Math.Round(SldOpacity.Value * 100.0) + "%";
			SwKeep.IsChecked = s.KeepAliveEnabled;
			RbHidden.IsChecked = s.KeepMode == "Hidden";
			RbFocus.IsChecked = s.KeepMode == "Focus";
			RbBoth.IsChecked = s.KeepMode == "Both";
			if (!RbHidden.IsChecked.Value && !RbFocus.IsChecked.Value && !RbBoth.IsChecked.Value)
			{
				RbBoth.IsChecked = true;
			}
			TbInterval.Text = s.IntervalSec.ToString();
			TbFocusEvery.Text = s.FocusEverySec.ToString();
			SwNoInterrupt.IsChecked = s.DontInterruptTyping;
			SwPauseInMatch.IsChecked = s.PauseInMatch;
			SwIdleGuard.IsChecked = s.IdleGuard;
			TbIdleSec.Text = s.IdleGuardSec.ToString();
			SwAlert.IsChecked = s.AlertEnabled;
			SwOnFull.IsChecked = s.AlertOnRoomFull;
			SwOnStart.IsChecked = s.AlertOnMatchStart;
			TbSlots.Text = s.RoomSlots.ToString();
			SwJoin.IsChecked = s.NotifyJoin;
			SwImg.IsChecked = s.AlertShowImage;
			SwSound.IsChecked = s.AlertSoundEnabled;
			TbTextFull.Text = s.AlertTextFull;
			TbTextStart.Text = s.AlertTextStart;
			TbAutoHide.Text = s.AlertAutoHideSec.ToString();
			TxtImgPath.Text = (string.IsNullOrWhiteSpace(s.AlertImage) ? "（未选择，用文字横幅）" : s.AlertImage);
			TxtSoundFull.Text = (string.IsNullOrWhiteSpace(s.AlertSoundFull) ? "（未选择，用内置「短促双响」）" : s.AlertSoundFull);
			TxtSoundStart.Text = (string.IsNullOrWhiteSpace(s.AlertSoundStart) ? "（未选择，用内置「上扬三响」）" : s.AlertSoundStart);
			TbJoinText.Text = s.NotifyJoinText;
			TbToastSec.Text = s.ToastSeconds.ToString();
			RbCornerRT.IsChecked = s.ToastCorner == "righttop";
			RbCornerRB.IsChecked = s.ToastCorner == "rightbottom";
			RbCornerLT.IsChecked = s.ToastCorner == "lefttop";
			RbCornerLB.IsChecked = s.ToastCorner == "leftbottom";
			if (!RbCornerRT.IsChecked.Value && !RbCornerRB.IsChecked.Value && !RbCornerLT.IsChecked.Value && !RbCornerLB.IsChecked.Value)
			{
				RbCornerRT.IsChecked = true;
			}
			SwCloseToTray.IsChecked = s.CloseToTray;
			SwTrayHint.IsChecked = s.TrayHintOnClose;
			LoadPreview();
		}
		finally
		{
			_sync = false;
		}
	}

	private void SyncToSettings()
	{
		Settings s = Ctx.S;
		s.OverlayEnabled = SwOverlay.IsChecked == true;
		s.OverlayClickThrough = SwClickThrough.IsChecked == true;
		s.OverlayShowNick = SwNick.IsChecked == true;
		s.OverlayOnlyFilled = SwOnlyFilled.IsChecked == true;
		s.OverlayOpacity = SldOpacity.Value;
		s.KeepAliveEnabled = SwKeep.IsChecked == true;
		Settings settings = s;
		string keepMode;
		if (RbHidden.IsChecked == true)
		{
			keepMode = "Hidden";
		}
		else
		{
			keepMode = ((RbFocus.IsChecked == true) ? "Focus" : "Both");
		}
		settings.KeepMode = keepMode;
		s.DontInterruptTyping = SwNoInterrupt.IsChecked == true;
		s.PauseInMatch = SwPauseInMatch.IsChecked == true;
		s.IdleGuard = SwIdleGuard.IsChecked == true;
		s.AlertEnabled = SwAlert.IsChecked == true;
		s.AlertOnRoomFull = SwOnFull.IsChecked == true;
		s.AlertOnMatchStart = SwOnStart.IsChecked == true;
		s.NotifyJoin = SwJoin.IsChecked == true;
		s.AlertShowImage = SwImg.IsChecked == true;
		s.AlertSoundEnabled = SwSound.IsChecked == true;
		s.AlertTextFull = TbTextFull.Text ?? "";
		s.AlertTextStart = TbTextStart.Text ?? "";
		s.NotifyJoinText = TbJoinText.Text ?? "";
		Settings settings2 = s;
		string toastCorner;
		if (RbCornerRB.IsChecked == true)
		{
			toastCorner = "rightbottom";
		}
		else if (RbCornerLT.IsChecked == true)
		{
			toastCorner = "lefttop";
		}
		else
		{
			toastCorner = ((RbCornerLB.IsChecked == true) ? "leftbottom" : "righttop");
		}
		settings2.ToastCorner = toastCorner;
		s.CloseToTray = SwCloseToTray.IsChecked == true;
		s.TrayHintOnClose = SwTrayHint.IsChecked == true;
		ApplyNums();
	}

	private void ApplyRuntimes()
	{
		Settings s = Ctx.S;
		OverlayWindow overlay = Ctx.Overlay;
		if (overlay != null)
		{
			if (s.OverlayEnabled && !overlay.IsVisible)
			{
				overlay.ShowOverlay();
			}
			else if (!s.OverlayEnabled && overlay.IsVisible)
			{
				overlay.Hide();
			}
			overlay.Opacity = Math.Max(0.25, Math.Min(1.0, s.OverlayOpacity));
			overlay.ReapplyStyle();
		}
		if (s.KeepAliveEnabled && !Ctx.Keep.Running)
		{
			Ctx.Keep.Start();
		}
		else if (!s.KeepAliveEnabled && Ctx.Keep.Running)
		{
			Ctx.Keep.Stop();
		}
		RefreshKeepStatus();
	}

	private void Persist()
	{
		Ctx.S.Save();
	}

	private void ApplyNums()
	{
		Settings s = Ctx.S;
		s.IntervalSec = ParseNum(TbInterval, 5, 600, 30);
		s.FocusEverySec = ParseNum(TbFocusEvery, 10, 3600, 90);
		s.IdleGuardSec = ParseNum(TbIdleSec, 1, 3600, 15);
		s.RoomSlots = ParseNum(TbSlots, 2, 10, 10);
		s.ToastSeconds = ParseNum(TbToastSec, 1, 60, 4);
		s.AlertAutoHideSec = ParseNum(TbAutoHide, 0, 3600, 30);
	}

	private static int ParseNum(TextBox tb, int min, int max, int fallback)
	{
		if (!int.TryParse((tb.Text ?? "").Trim(), out var result))
		{
			result = fallback;
		}
		if (result < min)
		{
			result = min;
		}
		if (result > max)
		{
			result = max;
		}
		string text = result.ToString();
		if (tb.Text != text)
		{
			tb.Text = text;
		}
		return result;
	}

	private void Nav_Checked(object sender, RoutedEventArgs e)
	{
		if (_ready)
		{
			P1.Visibility = ((Nav1.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
			P2.Visibility = ((Nav2.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
			P3.Visibility = ((Nav3.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
			P5.Visibility = ((Nav5.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
			P4.Visibility = ((Nav4.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void Any_Changed(object sender, RoutedEventArgs e)
	{
		if (_ready && !_sync)
		{
			SyncToSettings();
			Persist();
			ApplyRuntimes();
		}
	}

	private void SldOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (TxtOpacity != null)
		{
			TxtOpacity.Text = (int)Math.Round(e.NewValue * 100.0) + "%";
		}
		if (_ready && !_sync)
		{
			SyncToSettings();
			Persist();
			if (Ctx.Overlay != null)
			{
				Ctx.Overlay.Opacity = Math.Max(0.25, Math.Min(1.0, e.NewValue));
			}
		}
	}

	private void Num_LostFocus(object sender, RoutedEventArgs e)
	{
		if (_ready && !_sync)
		{
			ApplyNums();
			Persist();
			RefreshKeepStatus();
		}
	}

	private void Num_KeyDown(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)e.Key == 6)
		{
			ApplyNums();
			Persist();
			RefreshKeepStatus();
			Keyboard.ClearFocus();
		}
	}

	private void TbText_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_ready && !_sync)
		{
			Ctx.S.AlertTextFull = TbTextFull.Text ?? "";
			Ctx.S.AlertTextStart = TbTextStart.Text ?? "";
			Persist();
		}
	}

	private void TbJoin_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_ready && !_sync)
		{
			Ctx.S.NotifyJoinText = TbJoinText.Text ?? "";
			Persist();
		}
	}

	private void BtnShowOverlay_Click(object sender, RoutedEventArgs e)
	{
		Ctx.S.OverlayEnabled = true;
		Ctx.S.Save();
		SyncFromSettings();
		ApplyRuntimes();
	}

	private void BtnResetPos_Click(object sender, RoutedEventArgs e)
	{
		Ctx.S.OverlayLeft = -1.0;
		Ctx.S.OverlayTop = -1.0;
		Ctx.S.Save();
		if (Ctx.Overlay != null)
		{
			Ctx.Overlay.ShowOverlay();
		}
	}

	private void BtnOpenDir_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			Process.Start("explorer.exe", L.Dir);
		}
		catch (Exception ex)
		{
			L.W("打开目录失败: " + ex.Message);
		}
	}

	private void BtnFixFs_Click(object sender, RoutedEventArgs e)
	{
		if (Sc2.DisableFullscreenOptimizations(out var info))
		{
			L.W("已为游戏关闭全屏优化：" + info + "（需要完全退出游戏再进才生效）");
			TxtFsState.Text = "已关闭全屏优化：" + info + "\n请完全退出星际争霸 II（连战网一起退出更保险）再重新进入游戏，悬浮窗就会显示在游戏画面上了。\n想还原：把上面这个程序从「兼容性」设置里去掉「禁用全屏优化」即可。";
			try
			{
				MessageBox.Show("已为下面这个程序关闭 Windows 全屏优化：\n\n" + info + "\n\n请完全退出星际争霸 II（连战网一起退出更保险）再重新进入游戏，悬浮窗就会显示在游戏画面上。", "SC2 工具箱");
				return;
			}
			catch
			{
				return;
			}
		}
		L.W("关闭全屏优化失败：" + info);
		TxtFsState.Text = "没能自动设置：" + info + "\n可以手动来：右键星际争霸 II 的快捷方式 → 属性 → 兼容性 → 勾选「禁用全屏优化」→ 确定，然后重启游戏。";
	}

	private void BtnKeepStart_Click(object sender, RoutedEventArgs e)
	{
		SwKeep.IsChecked = true;
		SyncToSettings();
		Persist();
		Ctx.Keep.Start();
		RefreshKeepStatus();
	}

	private void BtnKeepStop_Click(object sender, RoutedEventArgs e)
	{
		SwKeep.IsChecked = false;
		SyncToSettings();
		Persist();
		Ctx.Keep.Stop();
		RefreshKeepStatus();
	}

	private void BtnPulseNow_Click(object sender, RoutedEventArgs e)
	{
		if (!Ctx.Keep.Running)
		{
			L.W("还没启动保活，先点「启动保活」");
			RefreshKeepStatus();
		}
		else
		{
			L.W("手动脉冲一次");
			Ctx.Keep.PulseNow();
		}
	}

	private void BtnTestAlert_Click(object sender, RoutedEventArgs e)
	{
		SyncToSettings();
		Persist();
		Ctx.Alerts.Test();
	}

	private void BtnPickImg_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "选一张警报图片",
			Filter = "图片|*.png;*.jpg;*.jpeg;*.gif;*.bmp|所有文件|*.*"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			Ctx.S.AlertImage = openFileDialog.FileName;
			Ctx.S.AlertShowImage = true;
			Ctx.S.Save();
			SyncFromSettings();
			L.W("警报图片已设置：" + openFileDialog.FileName);
		}
	}

	private void BtnClearImg_Click(object sender, RoutedEventArgs e)
	{
		Ctx.S.AlertImage = "";
		Ctx.S.Save();
		SyncFromSettings();
	}

	private void PickSound(string kind)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = ((kind == "full") ? "选一个「房间满人」的警报声音" : "选一个「确认开局」的警报声音"),
			Filter = "声音|*.wav;*.mp3;*.m4a;*.aac;*.wma|所有文件|*.*"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			if (kind == "full")
			{
				Ctx.S.AlertSoundFull = openFileDialog.FileName;
			}
			else
			{
				Ctx.S.AlertSoundStart = openFileDialog.FileName;
			}
			Ctx.S.AlertSoundEnabled = true;
			Ctx.S.Save();
			SyncFromSettings();
			L.W("警报声音已设置（" + ((kind == "full") ? "满人" : "开局") + "）：" + openFileDialog.FileName);
		}
	}

	private void BtnPickSoundFull_Click(object sender, RoutedEventArgs e)
	{
		PickSound("full");
	}

	private void BtnPickSoundStart_Click(object sender, RoutedEventArgs e)
	{
		PickSound("start");
	}

	private void BtnClearSoundFull_Click(object sender, RoutedEventArgs e)
	{
		Ctx.S.AlertSoundFull = "";
		Ctx.S.Save();
		SyncFromSettings();
	}

	private void BtnClearSoundStart_Click(object sender, RoutedEventArgs e)
	{
		Ctx.S.AlertSoundStart = "";
		Ctx.S.Save();
		SyncFromSettings();
	}

	private void BtnTestSoundFull_Click(object sender, RoutedEventArgs e)
	{
		SyncToSettings();
		Persist();
		Ctx.Alerts.PlaySound(AlertKind.Full);
	}

	private void BtnTestSoundStart_Click(object sender, RoutedEventArgs e)
	{
		SyncToSettings();
		Persist();
		Ctx.Alerts.PlaySound(AlertKind.Start);
	}

	private void BtnTestToast_Click(object sender, RoutedEventArgs e)
	{
		SyncToSettings();
		Persist();
		Ctx.Alerts.Toast(Alerts.Fill(Ctx.S.NotifyJoinText, "势魏延 测试用", 3, Math.Max(2, Ctx.S.RoomSlots)));
	}

	private void BtnReTray_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (Application.Current is App app)
			{
				app.RestoreTray();
			}
			else
			{
				L.W("重新显示托盘图标失败：拿不到 App");
			}
		}
		catch (Exception ex)
		{
			L.W("重新显示托盘图标失败：" + ex.Message);
		}
	}

	private void MatchRow_Click(object sender, MouseButtonEventArgs e)
	{
		try
		{
			TouhouRow touhouRow = ((sender is FrameworkElement frameworkElement) ? (frameworkElement.DataContext as TouhouRow) : null);
			if (touhouRow != null && touhouRow.Match != null)
			{
				MatchWindow matchWindow = new MatchWindow(touhouRow.Match);
				matchWindow.Owner = this;
				matchWindow.Show();
			}
		}
		catch (Exception ex)
		{
			L.W("打开对局详情失败：" + ex.Message);
		}
	}

	private void BtnRefreshLog_Click(object sender, RoutedEventArgs e)
	{
		TbLog.Text = L.Tail(400);
		_lines = TbLog.LineCount;
		TbLog.ScrollToEnd();
	}

	private void BtnClearLogView_Click(object sender, RoutedEventArgs e)
	{
		TbLog.Clear();
		_lines = 0;
	}

	private void FlushLog()
	{
		bool flag = false;
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		string result;
		while (num++ < 200 && _pending.TryDequeue(out result))
		{
			stringBuilder.AppendLine(result);
			flag = true;
		}
		if (!flag)
		{
			return;
		}
		try
		{
			TbLog.AppendText(stringBuilder.ToString());
			_lines += num;
			if (_lines > 800)
			{
				TbLog.Text = L.Tail(400);
				_lines = TbLog.LineCount;
			}
			if (P4.Visibility == Visibility.Visible)
			{
				TbLog.ScrollToEnd();
			}
		}
		catch
		{
		}
	}

	public void RefreshKeepStatus()
	{
		try
		{
			KeepAlive keep = Ctx.Keep;
			string text = (keep.Running ? "保活：运行中" : "保活：已停止");
			if (keep.Running)
			{
				text = text + " · 方式 " + ModeName(Ctx.S.KeepMode) + " · 间隔 " + Ctx.S.IntervalSec + " 秒";
				if (keep.LastPulse != DateTime.MinValue)
				{
					double totalSeconds = (DateTime.Now - keep.LastPulse).TotalSeconds;
					text = text + "\n上次脉冲：" + ((totalSeconds < 60.0) ? ((int)totalSeconds + " 秒前") : ((int)(totalSeconds / 60.0) + " 分钟前")) + "（" + keep.LastKind + "）";
				}
				text = text + "\n累计脉冲 " + keep.Pulses + " 次，跳过 " + keep.Skips + " 次";
				text = text + "\n当前状态：" + keep.Status;
			}
			LobbyState last = Ctx.Watcher.Last;
			text = text + "\n房间接口：" + ((last != null && last.Ok) ? ("正常 · 当前 " + last.Count + " / 10 人") : ("无响应" + ((last != null && last.Error.Length > 0) ? ("（" + last.Error + "）") : "（游戏或 touhou_client 没在跑？）")));
			if (last != null && last.When != DateTime.MinValue)
			{
				text = text + "\n最后刷新：" + last.When.ToString("HH:mm:ss");
			}
			if (last != null && last.Ok)
			{
				text = text + "\n原始串：" + last.Raw;
			}
			TxtKeepStatus.Text = text;
			TxtKeepNote.Text = (string.IsNullOrEmpty(keep.Note) ? "" : keep.Note);

			// 侧边栏底部状态卡
			if (SideKeep != null)
			{
				SideKeep.Text = (keep.Running ? ("保活运行中 · " + ModeName(Ctx.S.KeepMode)) : "保活已停止");
				LobbyState side = Ctx.Watcher.Last;
				if (side != null && side.Ok)
				{
					SideRoom.Text = "房间 " + side.Count + " / 10 人 · " + side.When.ToString("HH:mm:ss");
				}
				else
				{
					SideRoom.Text = "房间接口：无响应";
				}
				SideDot.Fill = (Brush)FindResource(keep.Running ? (side != null && side.Ok ? "B.Ok" : "B.Warn") : "B.Dim");
			}
		}
		catch
		{
		}
	}

	private static string ModeName(string m)
	{
		if (m == "Hidden")
		{
			return "静默";
		}
		if (m == "Focus")
		{
			return "抢焦点";
		}
		return "静默+抢焦点";
	}

	public void OnLobby(LobbyState st)
	{
		if (P2.Visibility == Visibility.Visible)
		{
			RefreshKeepStatus();
			return;
		}
		try
		{
			// 侧边栏底部状态卡始终跟着房间接口走
			if (SideRoom != null && st != null)
			{
				SideRoom.Text = (st.Ok ? ("房间 " + st.Count + " / 10 人 · " + st.When.ToString("HH:mm:ss")) : "房间接口：无响应");
				SideDot.Fill = (Brush)FindResource(st.Ok ? "B.Ok" : "B.Warn");
			}
		}
		catch
		{
		}
	}

	private void BtnRefreshTouhou_Click(object sender, RoutedEventArgs e)
	{
		TxtTouhouState.Text = "正在刷新…（按钮会把五块数据全部重拉一遍）";
		try
		{
			Ctx.Watcher.RefreshTouhou(force: true);
		}
		catch
		{
		}
	}

	private void BtnHeroNames_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			TouhouApi.WriteHeroNameTemplate();
			NameEditorWindow nameEditorWindow = new NameEditorWindow(() =>
			{
				try
				{
					TouhouApi.ReloadHeroNames();
					OnTouhou();
					TxtTouhouState.Text = "英雄名表已保存并生效（想重新拉数据就点「刷新数据」）。";
				}
				catch
				{
				}
			});
			nameEditorWindow.Owner = this;
			nameEditorWindow.ShowDialog();
			TxtTouhouState.Text = "英雄名表：" + TouhouApi.HeroNameFile;
		}
		catch (Exception ex)
		{
			TxtTouhouState.Text = "打开英雄名表失败：" + ex.Message;
		}
	}

	private Brush Res(string key, Brush fallback)
	{
		try
		{
			return (Application.Current.TryFindResource(key) as Brush) ?? fallback;
		}
		catch
		{
			return fallback;
		}
	}

	public void OnTouhou()
	{
		try
		{
			TPlayer me = TouhouApi.Me;
			bool meOnline = me.Games > 0 || me.Mmr > 0;
			BankProfile bankProfile = (meOnline ? null : LocalBank());
			MeName.Text = (string.IsNullOrEmpty(me.DisplayName) ? ((bankProfile == null) ? "（还没读到数据）" : "本地存档") : (me.DisplayName + (string.IsNullOrEmpty(me.ClanTag) ? "" : ("  [" + me.ClanTag + "]"))));
			MeHandle.Text = ((TouhouApi.MyHandle.Length > 0) ? ("档案 " + TouhouApi.MyHandle + "\u3000区服 " + TouhouApi.Region) : ((bankProfile == null) ? "（没连上本机 touhou_client）" : ("档案 " + bankProfile.ProfileId + "\u3000（站点没读到，区服未知）")));
			if (me.Games > 0 || me.Mmr > 0)
			{
				MeStats.Text = "MMR " + me.Mmr + ((me.MmrChange != 0) ? ("（最近 " + ((me.MmrChange > 0) ? "+" : "") + me.MmrChange + "）") : "") + "\u3000\u3000排名 " + ((me.Rank > 0) ? ("#" + me.Rank) : "—") + "\n对局 " + me.Games + " 场\u3000\u3000胜率 " + me.WinRate.ToString("0.#") + "%\n场均 KDA " + me.KillAvg.ToString("0.#") + " / " + me.DeathAvg.ToString("0.#") + " / " + me.AssistAvg.ToString("0.#") + "\u3000\u3000场均伤害 " + me.DamageAvg.ToString("0");
				MeBest.Text = "单场最高：击杀 " + me.BestKill + " · 伤害 " + me.BestDamage + " · 金钱 " + me.BestMoney + " · 助攻 " + me.BestAssist + " · 承伤 " + me.BestDamaged;
			}
			else if (bankProfile != null)
			{
				MeStats.Text = "站点没读到，下面是本机存档（离线也能看）\n对局 " + bankProfile.Total + " 场\u3000\u3000胜率 " + bankProfile.WinRate.ToString("0.#") + "%\n" + bankProfile.Win + " 胜 · " + bankProfile.Lose + " 负 · " + bankProfile.Leave + " 离开\u3000\u3000最高连胜 " + bankProfile.WinStreakHigh + "\n经验 " + bankProfile.Xp + "\u3000\u3000金币 " + bankProfile.Money;
				MeBest.Text = "本地存档 " + bankProfile.ProfileId + "\u3000" + bankProfile.ModifiedAt.ToString("MM-dd HH:mm") + " 更新";
			}
			else
			{
				MeStats.Text = "还没有读到战绩数据。";
				MeBest.Text = "";
			}
			MeImg.Source = ((me.HeroId > 0) ? ImgCache.Cached(TouhouApi.HeroImg(me.HeroId)) : ((bankProfile == null || bankProfile.CurrentHeroId <= 0) ? null : ImgCache.Cached(TouhouApi.HeroImg(bankProfile.CurrentHeroId))));
			TxtTouhouState.Text = ((TouhouApi.LastError.Length > 0) ? ("部分数据没读到（站点很慢，正在重试）：" + TouhouApi.LastError + ((TouhouApi.SavedAtText.Length > 0) ? ("\n显示的是 " + TouhouApi.SavedAtText + " 缓存到的数据") : "")) : ("更新于 " + ((TouhouApi.LastOk == DateTime.MinValue) ? "—" : TouhouApi.LastOk.ToString("HH:mm:ss")) + "\n本轮更新 " + TouhouApi.UpdatedLastRound + "/5 块：" + TouhouApi.LastPlan + (TouhouApi.SawNewGame ? "\n（探测到新对局，已把整份数据重新拉一遍）" : "") + "\n（站点很慢：只拉有变化或超过缓存时间的块，其余直接用上一次的快照）"));
			BuildMatches();
			BuildHeroes();
			BuildRank();
		}
		catch (Exception ex)
		{
			try
			{
				TxtTouhouState.Text = "界面刷新失败：" + ex.Message;
			}
			catch
			{
			}
		}
	}

	private void BuildMatches()
	{
		List<TouhouRow> list = new List<TouhouRow>();
		foreach (TMatch match in TouhouApi.Matches)
		{
			bool? win = match.Win;
			list.Add(new TouhouRow
			{
				Img = ((match.MyHeroId > 0) ? ImgCache.Cached(TouhouApi.HeroImg(match.MyHeroId)) : null),
				Result = match.ResultText,
				ResultBrush = ((win == true) ? Res("B.Ok", Brushes.LimeGreen) : ((win == false) ? Res("B.Danger", Brushes.IndianRed) : Brushes.Gray)),
				Title = ((match.MyHeroId > 0) ? TouhouApi.HeroName(match.MyHeroId) : "对局") + "\u3000KDA " + match.KdaText + (match.IsMvp ? "\u3000★MVP" : ""),
				Sub = match.WhenText + ((match.MyHeroLevel > 0) ? ("\u3000等级 " + match.MyHeroLevel) : "") + ((match.MyMmr > 0) ? ("\u3000MMR " + match.MyMmr) : "") + "\u3000伤害 " + match.MyDamage,
				Right = ((match.MyMmrChange != 0) ? (((match.MyMmrChange > 0) ? "+" : "") + match.MyMmrChange) : ""),
				Match = match
			});
		}
		if (list.Count == 0)
		{
			BankProfile bankProfile = LocalBank();
			MatchesSub.Text = ((bankProfile == null) ? "站点没读到，本机也没有存档。点右上角「刷新数据」重试。" : ("站点没读到。本机存档 " + bankProfile.ProfileId + " 只有累计战绩、没有逐场记录，所以这里空着。"));
			list.Add(new TouhouRow
			{
				Title = "还没读到对局记录",
				Sub = "点右上角「刷新数据」，或确认 touhou_client 在跑、站点接口可达。",
				Result = "",
				Right = ""
			});
		}
		else
		{
			MatchesSub.Text = "";
		}
		ListMatches.ItemsSource = list;
	}

	private void BuildHeroes()
	{
		List<TouhouRow> list = new List<TouhouRow>();
		foreach (THero item in TouhouApi.MyHeroes.Where((THero x) => x.Total > 0))
		{
			list.Add(new TouhouRow
			{
				Img = ImgCache.Cached(item.Url),
				Title = item.Name,
				Sub = item.Total + " 场 · 胜率 " + item.WinText,
				Right = "KDA " + item.KdaText + " · MMR " + item.Mmr
			});
		}
		if (list.Count == 0)
		{
			BankProfile bankProfile = LocalBank();
			if (bankProfile != null)
			{
				List<BankHeroStat> list2 = new List<BankHeroStat>(bankProfile.Heroes);
				list2.Sort(delegate(BankHeroStat a, BankHeroStat b)
				{
					return b.Total.CompareTo(a.Total);
				});
				foreach (BankHeroStat item in list2)
				{
					if (item.Total > 0)
					{
						list.Add(new TouhouRow
						{
							Img = ((item.HeroId > 0) ? ImgCache.Cached(TouhouApi.HeroImg(item.HeroId)) : null),
							Title = item.Name,
							Sub = string.Format("{0} 场 · {1} 胜 {2} 负", item.Total, item.Win, item.Lose),
							Right = string.Format("胜率 {0:0.#}%", item.WinRate)
						});
					}
				}
			}
			if (list.Count > 0)
			{
				HeroesSub.Text = "站点没读到，下面是本机存档 " + bankProfile.ProfileId + " 的英雄战绩（只有场次和胜负，没有 KDA / MMR）。";
			}
			else
			{
				HeroesSub.Text = "站点没读到，本机也没有可用的存档。";
				list.Add(new TouhouRow
				{
					Title = "还没读到英雄数据",
					Sub = "点右上角「刷新数据」。",
					Result = "",
					Right = ""
				});
			}
		}
		else
		{
			HeroesSub.Text = "";
		}
		ListHeroes.ItemsSource = list;
	}

	private void BuildRank()
	{
		List<TouhouRow> list = new List<TouhouRow>();
		string myHandle = TouhouApi.MyHandle;
		foreach (TPlayer item in TouhouApi.Rank)
		{
			bool flag = myHandle.Length > 0 && string.Equals(item.Handle, myHandle, StringComparison.OrdinalIgnoreCase);
			list.Add(new TouhouRow
			{
				Img = ((item.HeroId > 0) ? ImgCache.Cached(TouhouApi.HeroImg(item.HeroId)) : null),
				Region = ImgCache.Cached(TouhouApi.RegionImg((item.Region > 0) ? item.Region : 5)),
				No = "#" + item.Rank,
				Title = item.DisplayName + (string.IsNullOrEmpty(item.ClanTag) ? "" : (" [" + item.ClanTag + "]")) + (flag ? "  ← 我" : ""),
				Right = "MMR " + item.Mmr + "\u3000" + item.Wins + "胜" + item.Losses + "负" + ((item.WinStreak > 1) ? ("\u3000" + item.WinStreak + "连胜") : ""),
				RowBrush = (flag ? Res("B.Card2", Brushes.Transparent) : Brushes.Transparent)
			});
		}
		if (list.Count == 0)
		{
			list.Add(new TouhouRow
			{
				Title = "还没读到排行榜",
				Sub = "点右上角「刷新数据」。",
				Result = "",
				Right = ""
			});
		}
		ListRank.ItemsSource = list;
	}

	private void LoadPreview()
	{
		try
		{
			string alertImage = Ctx.S.AlertImage;
			if (Ctx.S.AlertShowImage && !string.IsNullOrWhiteSpace(alertImage) && File.Exists(alertImage))
			{
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.UriSource = new Uri(alertImage);
				bitmapImage.EndInit();
				ImgPreview.Source = bitmapImage;
			}
			else
			{
				ImgPreview.Source = null;
			}
		}
		catch
		{
			ImgPreview.Source = null;
		}
	}

	// ================= 本地存档战绩（读 touhou.SC2Bank，完全离线） =================

	private List<BankProfile> _bankProfiles = new List<BankProfile>();

	private bool _bankFilling;

	private BankProfile _bankCur;

	private DispatcherTimer _bankWatch;

	private DateTime _bankStamp = DateTime.MinValue;

	/// <summary>离线兜底用的本地存档：优先用户在下拉框选的那份，否则取最合适的一份。</summary>
	private BankProfile LocalBank()
	{
		if (_bankCur != null)
		{
			return _bankCur;
		}
		if (_bankProfiles.Count > 0)
		{
			return _bankProfiles[0];
		}
		return TouhouBank.LoadBest();
	}

	private void BtnBankLoad_Click(object sender, RoutedEventArgs e)
	{
		RefreshBank(true);
	}

	private void CbBank_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_bankFilling)
		{
			return;
		}
		int num = CbBank.SelectedIndex;
		if (num >= 0 && num < _bankProfiles.Count)
		{
			BankProfile bankProfile = _bankProfiles[num];
			if (bankProfile != _bankCur)
			{
				ShowBank(bankProfile);
			}
		}
	}

	/// <summary>扫描本机所有账号 / 档案的 touhou.SC2Bank，填下拉框并展示选中的那份。</summary>
	public void RefreshBank(bool force = false)
	{
		try
		{
			string text = ((_bankCur == null) ? "" : _bankCur.BankPath);
			if (force || _bankProfiles.Count == 0)
			{
				_bankProfiles = TouhouBank.LoadAll();
			}
			int num = 0;
			_bankFilling = true;
			try
			{
				CbBank.Items.Clear();
				int num2 = 0;
				foreach (BankProfile item in _bankProfiles)
				{
					string text2 = item.ProfileId + "（" + item.AccountId + "）";
					if (item.IsCurrentAccount)
					{
						text2 += " ★当前";
					}
					CbBank.Items.Add(text2 + " · " + item.ModifiedAt.ToString("MM-dd HH:mm") + " · " + item.Total + " 场");
					if (text.Length > 0 && string.Equals(item.BankPath, text, StringComparison.OrdinalIgnoreCase))
					{
						num = num2;
					}
					num2++;
				}
				if (CbBank.Items.Count > 0)
				{
					CbBank.SelectedIndex = Math.Min(num, CbBank.Items.Count - 1);
				}
			}
			finally
			{
				_bankFilling = false;
			}
			ShowBank((_bankProfiles.Count > 0) ? _bankProfiles[Math.Min(num, _bankProfiles.Count - 1)] : null);
			RememberBankStamp();
			if (_bankCur != null)
			{
				L.W(string.Format("本地存档：扫到 {0} 份档案，显示 {1}（{2} 场 · {3:0.0}%）", _bankProfiles.Count, _bankCur.ProfileId, _bankCur.Total, _bankCur.WinRate));
			}
		}
		catch (Exception ex)
		{
			BankStats.Text = "读取本地存档失败：" + ex.Message;
		}
	}

	private void ShowBank(BankProfile p)
	{
		_bankCur = p;
		List<TouhouRow> list = new List<TouhouRow>();
		if (p == null)
		{
			BankStats.Text = "没找到本地存档（touhou.SC2Bank）。\n查找位置：" + TouhouBank.AccountsRoot() + ((TouhouBank.LastError.Length > 0) ? ("\n最后错误：" + TouhouBank.LastError) : "") + "\n提示：至少要进过一次东方月灵传，游戏才会写出这份存档。";
			BankBest.Text = "";
			BankWhere.Text = "";
			BankHeroSub.Text = "没读到存档。";
			ListBankHeroes.ItemsSource = list;
			BuildBankConfigs(null);
			return;
		}
		string text = string.Format("总场次 {0}（{1} 胜 · {2} 负 · {3} 离开）　胜率 {4:0.0}%", p.Total, p.Win, p.Lose, p.Leave, p.WinRate);
		if (p.Total != p.Win + p.Lose + p.Leave)
		{
			text += string.Format("\n注意：胜+负+离开 = {0}，和 total = {1} 不一致。", p.Win + p.Lose + p.Leave, p.Total);
		}
		text += string.Format("\n最高连胜 {0}　最高连败 {1}　当前连胜 {2}　当前连败 {3}", p.WinStreakHigh, p.LoseStreakHigh, p.WinStreak, p.LoseStreak);
		text += string.Format("\n经验 {0}　金币 {1}　存档版本 {2}", p.Xp, p.Money, p.Version);
		DateTime? firstWinTime = p.FirstWinTime;
		if (firstWinTime.HasValue)
		{
			text += "\n首次胜利 " + firstWinTime.Value.ToString("yyyy-MM-dd HH:mm");
		}
		BankStats.Text = text;
		string text2 = "";
		if (p.CurrentHeroId > 0)
		{
			text2 = "存档记录英雄 " + p.CurrentHero + "（" + TouhouApi.HeroName(p.CurrentHeroId) + "）";
		}
		if (p.Status.Length > 0)
		{
			text2 += ((text2.Length > 0) ? "　" : "") + "状态 " + p.Status;
		}
		if (p.Hash.Length > 0)
		{
			text2 += "\n存档校验 hash：" + p.Hash;
		}
		BankBest.Text = text2;
		BankWhere.Text = "存档文件：" + p.BankPath + "（" + p.ModifiedAt.ToString("yyyy-MM-dd HH:mm") + " 更新）";
		List<BankHeroStat> list2 = new List<BankHeroStat>(p.Heroes);
		list2.Sort(delegate(BankHeroStat a, BankHeroStat b)
		{
			return b.Total.CompareTo(a.Total);
		});
		int num = 0;
		int num2 = 0;
		foreach (BankHeroStat item in list2)
		{
			if (item.Total > 0)
			{
				list.Add(new TouhouRow
				{
					Img = ((item.HeroId > 0) ? ImgCache.Cached(TouhouApi.HeroImg(item.HeroId)) : null),
					Title = "dbHero" + TouhouApi.Pad2(item.HeroId) + " " + item.Name,
					Sub = string.Format("{0} 场 · {1} 胜 {2} 负{3}", item.Total, item.Win, item.Lose, (item.Leave > 0) ? (" " + item.Leave + " 离开") : ""),
					Right = string.Format("胜率 {0:0.#}%", item.WinRate)
				});
				num += item.Total;
				num2++;
			}
		}
		if (list.Count == 0)
		{
			list.Add(new TouhouRow
			{
				Title = "这份存档里还没有英雄战绩",
				Sub = "进过一局东方月灵传之后就会有。",
				Right = ""
			});
		}
		ListBankHeroes.ItemsSource = list;
		BankHeroSub.Text = string.Format("共 {0} 个英雄、合计 {1} 场（全局 total = {2}）。数据来自 touhou.SC2Bank 的 dbHeroNN 段，按场次从多到少。", num2, num, p.Total);
		BuildBankConfigs(p);
	}

	/// <summary>站点 TouhouApi.InitStatIconIds 的副本：方案串前 6 项（初始属性）到属性图号的映射。</summary>
	private static readonly int[] InitStatIconIds = new int[6] { 4, 6, 0, 1, 2, 3 };

	private static readonly Brush CfgZero = FreezeBrush(42, 47, 58);

	private static readonly Brush CfgOne = FreezeBrush(59, 130, 246);

	private static readonly Brush CfgTwo = FreezeBrush(34, 211, 238);

	private static readonly Brush CfgThree = FreezeBrush(52, 211, 153);

	private static readonly Brush CfgFour = FreezeBrush(251, 191, 36);

	private static readonly Brush CfgBig = FreezeBrush(239, 68, 68);

	private static Brush FreezeBrush(byte r, byte g, byte b)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb(r, g, b));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	/// <summary>加点的数值 → 热力条颜色（0 是暗格，8 这种大值是红色主属性）。</summary>
	private static Brush PointBrush(int v)
	{
		switch (v)
		{
		case 0:
			return CfgZero;
		case 1:
			return CfgOne;
		case 2:
			return CfgTwo;
		case 3:
			return CfgThree;
		case 4:
			return CfgFour;
		default:
			return CfgBig;
		}
	}

	/// <summary>把 2026:10:15:20:21:47 显示成 10-15 20:21。</summary>
	private static string ConfigTimeText(string stamp)
	{
		if (stamp.Length >= 16 && stamp[4] == ':')
		{
			return stamp.Substring(5, 5) + " " + stamp.Substring(11, 5);
		}
		return stamp;
	}

	/// <summary>
	/// 用 touhou.SC2Bank 里 dbHeroNN_config 段的明文加点串画热力条。
	/// </summary>
	/// <summary>
	/// 方案串每段 15 位 -> 图标。前 6 位经实测确认就是站点接口的 initStats（取值恒为 8，与站点
	/// StatIcons() 里 if (num > 8) num = 8 的上限一致；各位置的选中率也一致），
	/// 图号走 InitStatIconIds；后 9 位的取值只到 1~4（位 6 是 1~4、位 7~14 恒为 1），
	/// 和站点 abilityUpSeq 的取值域相同，属于技能/天赋那一侧，所以统一用补进来的
	/// 「天赋表」(图号 8) 与「英雄技能表」(图号 9) 图标表示。
	/// </summary>
	private static readonly int[] StatSlotIconIds = new int[15]
	{
		4, 6, 0, 1, 2, 3, 8, 9, 9, 9, 9, 9, 9, 9, 9
	};

	/// <summary>每段 15 位各自对应的名字。</summary>
	private static string StatName(int i)
	{
		switch (i)
		{
		case 0:
			return "生命";
		case 1:
			return "魔法值";
		case 2:
			return "物伤";
		case 3:
			return "法伤";
		case 4:
			return "物抗";
		case 5:
			return "法抗";
		case 6:
			return "天赋/技能套装";
		case 7:
			return "技能位 1";
		case 8:
			return "技能位 2";
		case 9:
			return "技能位 3";
		case 10:
			return "技能位 4";
		case 11:
			return "技能位 5";
		case 12:
			return "技能位 6";
		case 13:
			return "技能位 7";
		case 14:
			return "技能位 8";
		default:
			return "第 " + (i + 1) + " 位";
		}
	}

	/// <summary>
	/// 每段第 7 位存的是「天赋系」：0~4 对应游戏天赋表的五栏 斗争 / 灾难 / 魔法 / 荣誉 / 祝福
	/// （由用户确认；并且对得上帮助里的定位——琪露诺是 AP 法师=魔法、咲夜是 CC 坦克=荣誉）。
	/// </summary>
	private static string TalentSchoolName(int v)
	{
		switch (v)
		{
		case 0:
			return "斗争";
		case 1:
			return "灾难";
		case 2:
			return "魔法";
		case 3:
			return "荣誉";
		case 4:
			return "祝福";
		default:
			return "未知";
		}
	}

	private static string TalentSchoolShort(int v)
	{
		switch (v)
		{
		case 0:
			return "斗";
		case 1:
			return "灾";
		case 2:
			return "魔";
		case 3:
			return "荣";
		case 4:
			return "祝";
		default:
			return "?";
		}
	}

	/// <summary>打开技能预设加点编辑器。</summary>
	private void BtnPresetEdit_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			BankProfile bankProfile = TouhouBank.LoadBest();
			if (bankProfile == null)
			{
				MessageBox.Show("没读到 touhou.SC2Bank。", "技能预设加点", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			new PresetWindow(bankProfile).ShowDialog();
			BuildBankConfigs(bankProfile);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "技能预设加点", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void BuildBankConfigs(BankProfile p)
	{
		List<BankCfgRow> list = new List<BankCfgRow>();
		if (p == null)
		{
			BankCfgSub.Text = "没读到存档。";
			ListBankConfigs.ItemsSource = list;
			return;
		}
		List<BankHeroStat> list2 = new List<BankHeroStat>();
		foreach (BankHeroStat item in p.Heroes)
		{
			if (item.Points.Length > 0)
			{
				list2.Add(item);
			}
		}
		list2.Sort(delegate(BankHeroStat a, BankHeroStat b)
		{
			return string.CompareOrdinal(b.LastConfigAt, a.LastConfigAt);
		});
		foreach (BankHeroStat item2 in list2)
		{
			BankCfgRow bankCfgRow = new BankCfgRow();
			bankCfgRow.Title = "dbHero" + TouhouApi.Pad2(item2.HeroId) + " " + item2.Name;
			bankCfgRow.Sub = ConfigTimeText(item2.LastConfigAt) + " · " + item2.PointSum + " 点";
			int[] array = new int[15];
			for (int i = 0; i < 15 && i < item2.Points.Length; i++)
			{
				array[i] = item2.Points[i];
			}
			for (int j = 0; j < 15; j++)
			{
				int num2 = array[j];
				int iconId = StatSlotIconIds[j];
				BitmapSource bitmapSource = ImgCache.Ensure(TouhouApi.StatImg(iconId));
				string text;
				string label;
				if (j < 6)
				{
					label = ((num2 > 0) ? num2.ToString() : "");
					text = string.Format("初始属性 · {0}：{1} 个碎片（站点 initStats 的上限就是 8）", StatName(j), (num2 > 0) ? num2.ToString() : "0");
				}
				else if (j == 6)
				{
					label = TalentSchoolShort(num2);
					text = string.Format("天赋系：{0}（天赋表五栏 = 斗争 / 灾难 / 魔法 / 荣誉 / 祝福）", TalentSchoolName(num2));
				}
				else
				{
					label = ((num2 > 0) ? num2.ToString() : "");
					text = string.Format("技能位 {0}：{1}（取值 0/1）", j - 6, (num2 > 0) ? "有" : "空");
				}
				bankCfgRow.Cells.Add(new BankCfgCell
				{
					Icon = bitmapSource,
					Num = label,
					Alpha = ((num2 > 0 || j == 6) ? 1.0 : 0.25),
					Tip = text
				});
			}
			list.Add(bankCfgRow);
		}
		if (list.Count == 0)
		{
			list.Add(new BankCfgRow
			{
				Title = "这份存档里还没有加点方案",
				Sub = "在游戏里给英雄加过点之后就会出现。"
			});
		}
		ListBankConfigs.ItemsSource = list;
		BankCfgSub.Text = string.Format("共 {0} 个英雄存过加点方案（含没打过场的）。每行 15 格 = 前 6 格「初始属性」（生命 / 魔法值 / 物伤 / 法伤 / 物抗 / 法抗，格里的数字是碎片个数，上限 8）+ 第 7 格「天赋系」（斗 / 灾 / 魔 / 荣 / 祝，对应游戏天赋表的五栏）+ 后 8 格「技能位」（这个英雄哪几个技能栏有技能）。灰的是没用到的位。整串 105 项 = 7 段 x 15 位，存档里只写「最后一次的配置」那一段。", list2.Count);
	}

	private void RememberBankStamp()
	{
		DateTime dateTime = DateTime.MinValue;
		foreach (BankProfile item in _bankProfiles)
		{
			if (item.ModifiedAt > dateTime)
			{
				dateTime = item.ModifiedAt;
			}
		}
		_bankStamp = dateTime;
	}

	/// <summary>游戏每打完一局都会重写 touhou.SC2Bank；发现文件变新就自动重读，不用手点。</summary>
	private void CheckBankChanged()
	{
		try
		{
			if (_bankProfiles.Count == 0)
			{
				return;
			}
			DateTime dateTime = DateTime.MinValue;
			foreach (BankProfile item in _bankProfiles)
			{
				try
				{
					DateTime lastWriteTime = File.GetLastWriteTime(item.BankPath);
					if (lastWriteTime > dateTime)
					{
						dateTime = lastWriteTime;
					}
				}
				catch
				{
				}
			}
			if (dateTime <= _bankStamp)
			{
				return;
			}
			BankProfile bankCur = _bankCur;
			RefreshBank(true);
			L.W("本地存档有更新，已自动重读本地战绩（原来显示 " + ((bankCur == null) ? "无" : bankCur.ProfileId) + "）");
		}
		catch
		{
		}
	}
}
