using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Sc2Toolbox;

public class Settings
{
	private static readonly JsonSerializerOptions Opt = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public bool OverlayEnabled { get; set; } = true;

	public double OverlayOpacity { get; set; } = 0.94;

	public bool OverlayClickThrough { get; set; }

	public double OverlayLeft { get; set; } = -1.0;

	public double OverlayTop { get; set; } = -1.0;

	public bool OverlayShowNick { get; set; } = true;

	public bool OverlayOnlyFilled { get; set; }

	public bool OverlayCompact { get; set; }

	/// <summary>悬浮窗里显示聊天记录面板。</summary>
	public bool OverlayChat { get; set; } = true;

	/// <summary>聊天面板抓取最近多少局录像。</summary>
	public int OverlayChatReplays { get; set; } = 1;

	public bool KeepAliveEnabled { get; set; } = true;

	public string KeepMode { get; set; } = "Both";

	public int IntervalSec { get; set; } = 30;
	/// <summary>键鼠连续静止多少分钟就自动关掉挂机等房（0 = 不自动关）。</summary>
	public int AutoStopIdleMin { get; set; } = 10;

	public int FocusEverySec { get; set; } = 90;

	public bool DontInterruptTyping { get; set; } = true;

	public bool KeepKeyboard { get; set; } = true;

	public bool PauseInMatch { get; set; } = true;
	/// <summary>后台保活时用「回车开聊天框再 Esc」代替抖鼠标（不发消息）。</summary>
	public bool KeepChatPoke { get; set; } = true;

	public bool AlertEnabled { get; set; } = true;

	public bool AlertOnRoomFull { get; set; } = true;

	public bool AlertOnMatchStart { get; set; } = true;

	public int RoomSlots { get; set; } = 10;

	public bool AlertShowImage { get; set; }

	public string AlertImage { get; set; } = "";

	public bool AlertSoundEnabled { get; set; } = true;

	public string AlertSoundFull { get; set; } = "";

	public string AlertSoundStart { get; set; } = "";

	public string AlertTextFull { get; set; } = "";

	public string AlertTextStart { get; set; } = "";

	public int AlertAutoHideSec { get; set; } = 20;

	public bool NotifyJoin { get; set; } = true;

	public string NotifyJoinText { get; set; } = "进人了：{name}（{n}/{max}）";

	public string ToastCorner { get; set; } = "righttop";

	public int ToastSeconds { get; set; } = 4;

	public bool CloseToTray { get; set; } = true;

	public bool TrayHintOnClose { get; set; } = true;

	public bool AlertOnRoomEmpty { get; set; }

	public bool AlertOnSc2Foreground { get; set; }

	public string AlertSound { get; set; } = "";

	public string AlertText { get; set; } = "";

	public int PrewarnCount { get; set; }

	public int PollMs { get; set; } = 1000;

	public string LobbyPort { get; set; } = "8030";

	/// <summary>悬浮窗里房间列表是否折叠。</summary>
	public bool OverlayRoomFold { get; set; }

	/// <summary>悬浮窗里聊天记录是否折叠。</summary>
	public bool OverlayChatFold { get; set; }

	/// <summary>聊天记录里是否显示指令行（-vk / -save 之类）。默认不显示。</summary>
	public bool OverlayChatCmds { get; set; }

	/// <summary>主窗口里被折叠的卡片标题，一行一个。</summary>
	public string FoldCards { get; set; } = "";

	/// <summary>对战数据页显示「本地对局」而不是「在线对局」。</summary>
	public bool P5LocalTab { get; set; }

	public static string FilePath => Path.Combine(L.Dir, "settings.json");

	public static Settings Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath, Encoding.UTF8)) ?? new Settings();
			}
		}
		catch (Exception ex)
		{
			L.W("读设置失败，用默认值: " + ex.Message);
		}
		return new Settings();
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(L.Dir);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opt), Encoding.UTF8);
		}
		catch (Exception ex)
		{
			L.W("写设置失败: " + ex.Message);
		}
	}
}
