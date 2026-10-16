using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sc2Toolbox;

public partial class OverlayWindow : Window, IComponentConnector
{
	private const int GWL_EXSTYLE = -20;

	private const int WS_EX_TRANSPARENT = 32;

	private const int WS_EX_LAYERED = 524288;

	private const int WS_EX_NOACTIVATE = 134217728;

	private const int WS_EX_TOOLWINDOW = 128;

	private static readonly nint HWND_TOPMOST = new IntPtr(-1);

	private const uint SWP_NOSIZE = 1u;

	private const uint SWP_NOMOVE = 2u;

	private const uint SWP_NOACTIVATE = 16u;

	private const uint SWP_NOOWNERZORDER = 512u;

	private DispatcherTimer _top;

	public readonly ObservableCollection<SlotRow> Rows = new ObservableCollection<SlotRow>();

	public readonly ObservableCollection<ChatLine> Chat = new ObservableCollection<ChatLine>();

	private readonly Brush _dim = new SolidColorBrush(Color.FromRgb(90, 102, 114));

	private readonly Brush _lit = new SolidColorBrush(Color.FromRgb(230, 237, 243));

	private readonly Brush _me = new SolidColorBrush(Color.FromRgb(127, 192, byte.MaxValue));

	private readonly Brush _okDot = new SolidColorBrush(Color.FromRgb(34, 197, 94));

	private readonly Brush _badDot = new SolidColorBrush(Color.FromRgb(239, 68, 68));

	private readonly Brush _idleDot = new SolidColorBrush(Color.FromRgb(107, 114, 128));

	[DllImport("user32.dll")]
	private static extern int GetWindowLong(nint h, int i);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(nint h, int i, int v);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(nint h, nint after, int x, int y, int cx, int cy, uint flags);

	public OverlayWindow()
	{
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Expected Obj, but got Unknown
		InitializeComponent();
		List.ItemsSource = Rows;
		ChatList.ItemsSource = Chat;
		MiChat.IsChecked = Ctx.S.OverlayChat;
		MiChatCmds.IsChecked = Ctx.S.OverlayChatCmds;
		Loaded += delegate
		{
			RefreshChat();
		};
		Ctx.Keep.Changed += () =>
		{
			try
			{
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(RefreshKeep), Array.Empty<object>());
			}
			catch
			{
			}
		};
		_top = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1L)
		};
		_top.Tick += delegate
		{
			KeepTopmost();
		};
		_top.Start();
		_chat = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(2L)
		};
		_chat.Tick += delegate
		{
			// 对局进行中：每隔几秒扫一次 SC2 内存，有新聊天才重建面板（RefreshChat 内部有特征串比对）
			try
			{
				if (Ctx.S.OverlayChat && TouhouChat.Sc2Pid() != 0)
				{
					TouhouChat.KickLive(4);
					RefreshChat();
				}
			}
			catch
			{
			}
		};
		_chat.Start();
	}

	private DispatcherTimer _chat;

	public void KeepTopmost()
	{
		try
		{
			if (!IsVisible)
			{
				return;
			}
			nint handle = new WindowInteropHelper(this).Handle;
			if (handle != IntPtr.Zero)
			{
				if (!Topmost)
				{
					Topmost = true;
				}
				SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, 531u);
			}
		}
		catch
		{
		}
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		ApplyExStyle();
		RestorePos();
		RefreshKeep();
		ApplyFold();
	}

	private void ApplyExStyle()
	{
		try
		{
			nint handle = new WindowInteropHelper(this).Handle;
			int windowLong = GetWindowLong(handle, -20);
			windowLong |= 0x8080080;
			windowLong = ((!Ctx.S.OverlayClickThrough) ? (windowLong & -33) : (windowLong | 0x20));
			SetWindowLong(handle, -20, windowLong);
			MiClickThrough.IsChecked = Ctx.S.OverlayClickThrough;
		}
		catch
		{
		}
	}

	public void ReapplyStyle()
	{
		ApplyExStyle();
		Opacity = Math.Max(0.25, Math.Min(1.0, Ctx.S.OverlayOpacity));
	}

	private void RestorePos()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		Settings s = Ctx.S;
		double num = s.OverlayLeft;
		double num2 = s.OverlayTop;
		if (num < 0.0 || num2 < 0.0)
		{
			Rect workArea = SystemParameters.WorkArea;
			num = workArea.Right - Width - 24.0;
			workArea = SystemParameters.WorkArea;
			num2 = workArea.Top + 24.0;
		}
		Rect workArea2 = SystemParameters.WorkArea;
		if (num > workArea2.Right - 60.0)
		{
			num = workArea2.Right - 60.0;
		}
		if (num2 > workArea2.Bottom - 40.0)
		{
			num2 = workArea2.Bottom - 40.0;
		}
		if (num < workArea2.Left - 20.0)
		{
			num = workArea2.Left + 8.0;
		}
		if (num2 < workArea2.Top - 10.0)
		{
			num2 = workArea2.Top + 8.0;
		}
		Left = num;
		Top = num2;
	}

	public void SavePos()
	{
		try
		{
			Ctx.S.OverlayLeft = Left;
			Ctx.S.OverlayTop = Top;
			Ctx.S.Save();
		}
		catch
		{
		}
	}

	public void Apply(LobbyState st, Dictionary<string, string> nicks)
	{
		Settings s = Ctx.S;
		Opacity = Math.Max(0.25, Math.Min(1.0, s.OverlayOpacity));
		Rows.Clear();
		for (int i = 0; i < 10; i++)
		{
			string text = ((i < st.Handles.Count) ? st.Handles[i] : "");
			bool flag = !string.IsNullOrEmpty(text);
			if (!flag && s.OverlayOnlyFilled)
			{
				continue;
			}
			string value = "";
			if (flag)
			{
				nicks?.TryGetValue(text, out value);
			}
			bool flag2 = flag && text == Lobby.MyHandle;
			string name;
			if (!flag)
			{
				name = "（空位）";
			}
			else
			{
				string text2 = TouhouApi.NameFor(text);
				if (s.OverlayShowNick && !string.IsNullOrEmpty(text2))
				{
					name = text2;
				}
				else
				{
					name = ((!s.OverlayShowNick || string.IsNullOrEmpty(value)) ? Short(text) : value);
				}
			}
			int num = (flag ? TouhouApi.HeroFor(text) : 0);
			int num2 = (flag ? TouhouApi.MmrFor(text) : 0);
			string text3 = "";
			string text4 = "";
			if (flag)
			{
				if (TouhouApi.RoomProfiles.TryGetValue(text, out var value2))
				{
					text3 = ((value2.Mmr > 0) ? ("MMR " + value2.Mmr) : "");
					if (value2.Games > 0)
					{
						text3 = text3 + ((text3.Length > 0) ? " · " : "") + value2.WinRate.ToString("0.#") + "% · " + value2.Games + "场";
					}
					text4 = "游戏ID " + text + ((value2.ClanTag == null || value2.ClanTag.Length == 0) ? "" : ("\u3000战队 [" + value2.ClanTag + "]")) + "\n对局 " + value2.Games + " 场\u3000胜率 " + value2.WinRate.ToString("0.#") + "%（" + value2.Wins + "胜" + value2.Losses + "负）\nMMR " + value2.Mmr + ((value2.MmrMax > 0) ? ("\u3000最高 " + value2.MmrMax) : "") + "\nMVP " + value2.Mvp + " · SVP " + value2.Svp + ((value2.WinStreak > 0) ? ("\u3000" + value2.WinStreak + " 连胜") : ((value2.LoseStreak > 0) ? ("\u3000" + value2.LoseStreak + " 连败") : ""));
				}
				else
				{
					text3 = ((num2 > 0) ? ("MMR " + num2) : "");
					text4 = "游戏ID " + text + "\n（站点还没有这个玩家资料，或正在读取）";
				}
				if (num > 0)
				{
					text4 = text4 + "\n常用英雄 " + TouhouApi.HeroName(num);
				}
			}
			Rows.Add(new SlotRow
			{
				No = i + 1,
				Handle = text,
				Nick = (value ?? ""),
				Name = name,
				Tag = (flag2 ? "我" : ""),
				Filled = flag,
				Color = ((!flag) ? _dim : (flag2 ? _me : _lit)),
				Avatar = ((num > 0) ? ImgCache.Cached(TouhouApi.HeroImg(num)) : null),
				Mmr = text3,
				HeroName = ((num > 0) ? TouhouApi.HeroName(num) : ""),
				Tip = text4
			});
		}
		TxtTitle.Text = "房间";
		TxtCount.Text = (st.Ok ? (st.Count + " / 10 人") : "接口未连接");
		Border dot = Dot;
		Brush background;
		if (st.Ok)
		{
			background = ((st.Count > 0) ? _okDot : _idleDot);
		}
		else
		{
			background = _badDot;
		}
		dot.Background = background;
		if (st.Ok)
		{
			TxtData.Text = ((st.Count > 0) ? ("房间接口正常 · " + DateTime.Now.ToString("HH:mm:ss") + " 更新") : "接口正常 · 你当前不在房间里（或房间是空的）");
			TxtData.Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128));
		}
		else
		{
			TxtData.Text = "读不到房间列表：" + (string.IsNullOrEmpty(st.Error) ? "接口无响应" : st.Error);
			TxtData.Foreground = new SolidColorBrush(Color.FromRgb(239, 123, 123));
		}
		try
		{
			TPlayer me = TouhouApi.Me;
			if (TouhouApi.MyHandle.Length > 0 && (me.Mmr > 0 || me.Games > 0 || me.Rank > 0))
			{
				TxtMe.Visibility = Visibility.Visible;
				TxtMe.Text = "我：" + (string.IsNullOrEmpty(me.DisplayName) ? TouhouApi.MyHandle : me.DisplayName) + "\u3000MMR " + me.Mmr + "\u3000排名 " + ((me.Rank > 0) ? ("#" + me.Rank) : "—") + "\u3000胜率 " + me.WinRate.ToString("0.#") + "%\u3000" + me.Games + " 场";
			}
			else
			{
				TxtMe.Visibility = Visibility.Collapsed;
			}
		}
		catch
		{
			TxtMe.Visibility = Visibility.Collapsed;
		}
	}

	private static string Short(string handle)
	{
		int num = handle.LastIndexOf('-');
		if (num <= 0 || num + 1 >= handle.Length)
		{
			return handle;
		}
		return "玩家 " + handle.Substring(num + 1);
	}

	public void RefreshKeep()
	{
		KeepAlive keep = Ctx.Keep;
		string text;
		if (!keep.Running)
		{
			text = "保活：关";
		}
		else
		{
			string text2 = ((keep.LastKind.Length > 0) ? keep.LastKind : "等待中");
			text = "保活：开 · " + text2;
			if (keep.LastPulse != DateTime.MinValue)
			{
				double totalSeconds = (DateTime.Now - keep.LastPulse).TotalSeconds;
				text = text + " · " + ((totalSeconds < 60.0) ? ((int)totalSeconds + " 秒前") : ((int)(totalSeconds / 60.0) + " 分钟前"));
			}
			if (keep.Status.Contains("已确认收到"))
			{
				text += " · 已确认";
			}
			else if (keep.Status.Contains("自动暂停"))
			{
				text += " · 对局中暂停";
			}
		}
		TxtKeep.Text = text;
		TxtKeep.Foreground = (keep.Running ? new SolidColorBrush(Color.FromRgb(154, 167, 180)) : new SolidColorBrush(Color.FromRgb(245, 158, 11)));
		bool flag = !string.IsNullOrEmpty(keep.Note);
		TxtNote.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		TxtNote.Text = (flag ? keep.Note : "");
	}

	private void Head_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ButtonState == MouseButtonState.Pressed)
		{
			try
			{
				DragMove();
			}
			catch
			{
			}
			SavePos();
		}
	}

	private void BtnTest_Click(object sender, RoutedEventArgs e)
	{
		Ctx.Alerts.Test();
	}

	private void BtnCfg_Click(object sender, RoutedEventArgs e)
	{
		if (Ctx.Main != null)
		{
			Ctx.Main.Show();
			Ctx.Main.WindowState = WindowState.Normal;
			Ctx.Main.Activate();
		}
	}

	private void BtnHide_Click(object sender, RoutedEventArgs e)
	{
		HideOverlay();
	}

	private void Menu_Hide(object sender, RoutedEventArgs e)
	{
		HideOverlay();
	}

	private void Menu_Settings(object sender, RoutedEventArgs e)
	{
		BtnCfg_Click(sender, e);
	}

	private void Menu_ClickThrough(object sender, RoutedEventArgs e)
	{
		Ctx.S.OverlayClickThrough = MiClickThrough.IsChecked;
		Ctx.S.Save();
		ApplyExStyle();
		if (Ctx.Main != null)
		{
			Ctx.Main.SyncFromSettings();
		}
	}

	private void Menu_Chat(object sender, RoutedEventArgs e)
	{
		Ctx.S.OverlayChat = MiChat.IsChecked;
		Ctx.S.Save();
		RefreshChat();
	}

	private void Menu_ChatCmds(object sender, RoutedEventArgs e)
	{
		Ctx.S.OverlayChatCmds = MiChatCmds.IsChecked;
		Ctx.S.Save();
		_chatSig = "";
		RefreshChat();
	}

	/// <summary>房间列表 / 聊天记录各自的折叠状态刷到界面上。</summary>
	private void ApplyFold()
	{
		bool roomFold = Ctx.S.OverlayRoomFold;
		RoomBody.Visibility = (roomFold ? Visibility.Collapsed : Visibility.Visible);
		RoomCaret.Text = (roomFold ? "▸" : "▾");
		bool chatFold = Ctx.S.OverlayChatFold;
		ChatBody.Visibility = (chatFold ? Visibility.Collapsed : Visibility.Visible);
		ChatCaret.Text = (chatFold ? "▸" : "▾");
	}

	private void RoomHead_Click(object sender, MouseButtonEventArgs e)
	{
		Ctx.S.OverlayRoomFold = !Ctx.S.OverlayRoomFold;
		Ctx.S.Save();
		ApplyFold();
		e.Handled = true;
	}

	private void ChatHead_Click(object sender, MouseButtonEventArgs e)
	{
		Ctx.S.OverlayChatFold = !Ctx.S.OverlayChatFold;
		Ctx.S.Save();
		ApplyFold();
		e.Handled = true;
	}

	/// <summary>已经尝试下载过的表情 id（避免下载失败后无限重试）。</summary>
	private readonly HashSet<int> _emojiTried = new HashSet<int>();

	/// <summary>上一次渲染聊天时的特征串，用来避免定时器每次都重建控件。</summary>
	private string _chatSig = "";

	/// <summary>按设置决定聊天面板是否显示；对局进行中从 SC2 内存实时读，否则读最近一局录像。</summary>
	public void RefreshChat()
	{
		try
		{
			if (!Ctx.S.OverlayChat)
			{
				ChatPanel.Visibility = Visibility.Collapsed;
				return;
			}
			List<ChatLine> lines = TouhouChat.LoadCurrent(400);
			if (!Ctx.S.OverlayChatCmds)
			{
				List<ChatLine> keep = new List<ChatLine>();
				foreach (ChatLine cl in lines)
				{
					// 指令行（-vk 3 / -save 1 / -u …）不算聊天，默认不显示
					if (cl.Text != null && cl.Text.TrimStart().StartsWith("-"))
					{
						continue;
					}
					keep.Add(cl);
				}
				lines = keep;
			}
			string sig = lines.Count + "|" + (TouhouChat.LiveSource ? "L" : "R") + "|" + TouhouChat.CurrentGame + "|" + TouhouChat.CurrentWhen.Ticks
				+ "|" + ((lines.Count > 0) ? (lines[lines.Count - 1].Raw ?? lines[lines.Count - 1].Text) : "");
			if (sig == _chatSig)
			{
				return;
			}
			_chatSig = sig;
			ChatPanel.Visibility = Visibility.Visible;
			Chat.Clear();
			List<int> missing = new List<int>();
			int emojiLines = 0;
			foreach (ChatLine line in lines)
			{
				Chat.Add(line);
				if (TouhouChat.HasEmoji(line.Text))
				{
					emojiLines++;
					foreach (TouhouChat.ChatPart part in TouhouChat.Split(line.Text))
					{
						if (part.EmojiId > 0 && !System.IO.File.Exists(TouhouChat.EmojiFile(part.EmojiId)) && !_emojiTried.Contains(part.EmojiId))
						{
							_emojiTried.Add(part.EmojiId);
							missing.Add(part.EmojiId);
						}
					}
				}
			}
			if (Chat.Count == 0)
			{
				if (TouhouChat.LiveSource)
				{
					TxtChatHint.Text = "本局进行中 · 还没有人说话";
				}
				else
				{
					TxtChatHint.Text = ((TouhouChat.CurrentGame.Length > 0) ? ("上一局（" + TouhouChat.CurrentWhen.ToString("HH:mm") + "）没有聊天") : "没读到（录像目录为空？）");
				}
			}
			else if (TouhouChat.LiveSource)
			{
				TxtChatHint.Text = "本局进行中 · 实时 " + Chat.Count + " 条" + ((emojiLines > 0) ? (" · 表情 " + emojiLines) : "");
			}
			else
			{
				TxtChatHint.Text = "上一局 " + TouhouChat.CurrentWhen.ToString("HH:mm") + " · " + Chat.Count + " 条" + ((emojiLines > 0) ? (" · 表情 " + emojiLines) : "");
			}
			foreach (ChatLine line in lines)
			{
				line.Visual = BuildChatLine(line);
			}
			if (missing.Count > 0)
			{
				L.W("表情：需要下载 " + missing.Count + " 张");
				System.Threading.Tasks.Task.Run(delegate
				{
					foreach (int id in missing)
					{
						TouhouChat.EnsureEmoji(id);
					}
					Dispatcher.BeginInvoke(new Action(delegate
					{
						RefreshChat();
					}));
				});
			}
		}
		catch (Exception ex)
		{
			TxtChatHint.Text = "读取失败：" + ex.Message;
		}
	}

	/// <summary>玩家色在深底上往往太暗，按亮度往白里混一点再显示。</summary>
	private static Color Bright(Brush b)
	{
		SolidColorBrush s = b as SolidColorBrush;
		if (s == null)
		{
			return Color.FromRgb(0xCF, 0xD8, 0xE4);
		}
		Color c = s.Color;
		double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
		double k = (lum < 0.6) ? (0.6 - lum) / 0.6 : 0.0;
		return Color.FromRgb(
			(byte)(c.R + (255 - c.R) * k),
			(byte)(c.G + (255 - c.G) * k),
			(byte)(c.B + (255 - c.B) * k));
	}

	/// <summary>把一行聊天做成 TextBlock（说话人 + 文字 + 内联表情图）。</summary>
	private FrameworkElement BuildChatLine(ChatLine line)
	{
		TextBlock tb = new TextBlock
		{
			TextWrapping = TextWrapping.Wrap,
			FontSize = 11.5,
			Foreground = new SolidColorBrush(Color.FromRgb(0xD6, 0xDE, 0xE8))
		};
		if (!string.IsNullOrEmpty(line.Who))
		{
			tb.Inlines.Add(new Run(line.Who + "：")
			{
				Foreground = new SolidColorBrush(Bright(line.Color)),
				FontWeight = FontWeights.SemiBold
			});
		}
		List<TouhouChat.ChatPart> parts = TouhouChat.Split(line.Text);
		if (parts.Count == 0)
		{
			tb.Inlines.Add(new Run(line.Text));
			return tb;
		}
		foreach (TouhouChat.ChatPart part in parts)
		{
			if (part.EmojiId > 0)
			{
				Image img = LoadEmojiImage(part.EmojiId);
				if (img != null)
				{
					tb.Inlines.Add(new InlineUIContainer(img)
					{
						BaselineAlignment = BaselineAlignment.Center
					});
				}
				else
				{
					tb.Inlines.Add(new Run("[表情" + part.EmojiId + "]"));
				}
			}
			else
			{
				tb.Inlines.Add(new Run(part.Text));
			}
		}
		return tb;
	}

	/// <summary>读本地缓存的表情图（没有就返回 null，交给后台下载后再重刷）。</summary>
	private Image LoadEmojiImage(int id)
	{
		try
		{
			string f = TouhouChat.EmojiFile(id);
			if (!System.IO.File.Exists(f))
			{
				return null;
			}
			BitmapImage bi = new BitmapImage();
			bi.BeginInit();
			bi.CacheOption = BitmapCacheOption.OnLoad;
			bi.UriSource = new Uri(f);
			bi.EndInit();
			bi.Freeze();
			return new Image
			{
				Source = bi,
				Width = 22,
				Height = 22,
				Margin = new Thickness(1, 0, 1, 0),
				Stretch = Stretch.Uniform
			};
		}
		catch
		{
			return null;
		}
	}

	private void HideOverlay()
	{
		SavePos();
		Ctx.S.OverlayEnabled = false;
		Ctx.S.Save();
		Hide();
		if (Ctx.Main != null)
		{
			Ctx.Main.SyncFromSettings();
		}
	}

	public void ShowOverlay()
	{
		Show();
		ApplyExStyle();
		RestorePos();
		RefreshChat();
		KeepTopmost();
		L.W("悬浮窗已显示：位置 " + (int)Left + "," + (int)Top + "，鼠标穿透=" + (Ctx.S.OverlayClickThrough ? "开" : "关") + "（游戏里看不到就检查游戏显示模式是否为「窗口化(全屏)」）");
	}
}
