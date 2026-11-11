using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Sc2Toolbox;

/// <summary>
/// 站点上「进行中」的对局：轮询 touhou-api/game/list2，找包含自己的那一局，
/// 拿比分（team1wins : team2wins）、回合数和帧数，帧数换算成对局时长。
/// </summary>
public static class LiveGame
{
	/// <summary>SC2 在「更快」速度下一秒钟跑 22.4 帧（站点时长也是按这个算的）。</summary>
	private const double FramesPerSecond = 22.4;

	private const int PollMs = 10000;

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(20)
	};

	private static Thread _thread;

	private static volatile bool _run;

	public static bool InGame { get; private set; }

	public static long GameId { get; private set; }

	public static int Round { get; private set; }

	public static int Team1 { get; private set; }

	public static int Team2 { get; private set; }

	public static int GameMode { get; private set; }

	public static DateTime LastOk { get; private set; } = DateTime.MinValue;

	public static string LastError { get; private set; } = "";

	/// <summary>站点上正在进行中的对局数量（不一定包含自己）。</summary>
	public static int LiveCount { get; private set; }

	/// <summary>站点上不仅有这局，而且数据还在动（接口断了、或者这局看完 5 分钟没更新，就当它已经结束）。</summary>
	public static bool FreshInGame
	{
		get
		{
			if (!InGame)
			{
				return false;
			}
			if (LastOk == DateTime.MinValue || (DateTime.Now - LastOk).TotalSeconds > 90.0)
			{
				return false;
			}
			if (_fetchedAt != DateTime.MinValue && (DateTime.Now - _fetchedAt).TotalMinutes > 5.0)
			{
				return false;
			}
			return true;
		}
	}

	private static int _frame;

	/// <summary>从本地存档认出来的档案号（client 没开时的兜底），5 分钟重新扫一次。</summary>
	private static string _localHandle;

	private static DateTime _localHandleAt = DateTime.MinValue;

	/// <summary>我是谁：优先 touhou_client，其次本地存档目录里的档案号。</summary>
	private static string ResolveHandle()
	{
		string text = TouhouApi.MyHandle;
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		text = Lobby.MyHandle;
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (string.IsNullOrEmpty(_localHandle) || (DateTime.Now - _localHandleAt).TotalMinutes > 5.0)
		{
			_localHandleAt = DateTime.Now;
			try
			{
				BankProfile bankProfile = TouhouBank.LoadBest();
				_localHandle = ((bankProfile == null) ? "" : bankProfile.ProfileId);
			}
			catch
			{
				_localHandle = "";
			}
		}
		return _localHandle;
	}

	private static DateTime _fetchedAt = DateTime.MinValue;

	/// <summary>上一次写进日志的错误，避免网络一直不通时每 10 秒刷一条。</summary>
	private static string _lastErrLogged;

	/// <summary>帧数 + 两次轮询之间的时间，换算成「22:11」这样的时长。</summary>
	public static string DurationText
	{
		get
		{
			double num = (double)_frame / FramesPerSecond;
			if (_fetchedAt != DateTime.MinValue)
			{
				// 站点只在对局数据更新时才刷新帧数，用「距上次拿到数据的时间」把秒数接着走；
				// 差值离谱（时钟偏差 / 拿到的不是本次更新）就干脆不加。
				double extra = (DateTime.Now - _fetchedAt).TotalSeconds;
				if (extra > 0.0 && extra < 120.0)
				{
					num += extra;
				}
			}
			if (num < 0.0)
			{
				num = 0.0;
			}
			int num2 = (int)num;
			return (num2 / 60) + ":" + (num2 % 60).ToString("00", CultureInfo.InvariantCulture);
		}
	}

	public static string ModeText
	{
		get
		{
			switch (GameMode)
			{
			case 1:
				return "普通模式（BP）";
			case 3:
				return "比赛模式";
			case 2:
				return "普通模式";
			default:
				return "对局";
			}
		}
	}

	/// <summary>悬浮窗底部那一行：进行中时给两句，否则空串。</summary>
	public static string Text
	{
		get
		{
			if (!InGame)
			{
				return "";
			}
			return "对局中 · " + ModeText + " · " + DurationText + "\n比分 " + Team1 + " : " + Team2 + " · 第 " + Round + " 回合";
		}
	}

	public static event Action Changed;

	public static void Start()
	{
		if (_run)
		{
			return;
		}
		_run = true;
		_thread = new Thread(Loop)
		{
			IsBackground = true,
			Name = "licgame"
		};
		_thread.Start();
	}

	public static void Stop()
	{
		if (!_run)
		{
			return;
		}
		_run = false;
		try
		{
			_thread?.Join(800);
		}
		catch
		{
		}
		_thread = null;
	}

	private static void Loop()
	{
		while (_run)
		{
			try
			{
				Refresh();
			}
			catch (Exception ex)
			{
				LastError = ex.Message;
			}
			int num = 0;
			while (_run && num < PollMs)
			{
				Thread.Sleep(200);
				num += 200;
			}
		}
	}

	public static void Refresh()
	{
		try
		{
			// 先把「我是谁」定下来：client 没开的时候也要能从本地存档认出档案号，否则这里永远认不出自己
			string text = ResolveHandle();
			string text2 = TouhouApi.Region;
			if (text2.Length == 0 && text.Length > 0)
			{
				string[] array0 = text.Split('-');
				if (array0.Length > 0)
				{
					text2 = array0[0];
				}
			}
			using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.aslucky.club/touhou-api/game/list2");
			if (!string.IsNullOrEmpty(text2))
			{
				httpRequestMessage.Headers.TryAddWithoutValidation("region", text2);
			}
			if (!string.IsNullOrEmpty(TouhouApi.Aaa))
			{
				httpRequestMessage.Headers.TryAddWithoutValidation("aaa", TouhouApi.Aaa);
			}
			httpRequestMessage.Headers.TryAddWithoutValidation("handleStr", string.IsNullOrEmpty(TouhouApi.HandleStr) ? text : TouhouApi.HandleStr);
			using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(18000);
			byte[] array = Http.SendAsync(httpRequestMessage, cancellationTokenSource.Token).GetAwaiter().GetResult()
				.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
			using JsonDocument jsonDocument = JsonDocument.Parse(Encoding.UTF8.GetString(array));
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.Number && value.GetInt32() != 1)
			{
				LastError = "接口返回 code=" + value.GetInt32();
				return;
			}
			if (!rootElement.TryGetProperty("data", out var value2) || value2.ValueKind != JsonValueKind.Array)
			{
				LastError = "响应里没有对局列表";
				return;
			}
			if (string.IsNullOrEmpty(text) && _lastErrLogged != "no-handle")
			{
				_lastErrLogged = "no-handle";
				L.W("[对局] 不知道本机档案号，没法在站点列表里认出你（等房就不会按对局暂停）");
			}
			int num = 0;
			bool flag = false;
			long num2 = 0L;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			int num7 = 0;
			DateTime dateTime = DateTime.MinValue;
			foreach (JsonElement item in value2.EnumerateArray())
			{
				num++;
				long num8 = Long(item, "id");
				if (num8 <= 0 || !HasMe(item, text))
				{
					continue;
				}
				if (num8 <= num2)
				{
					continue;
				}
				flag = true;
				num2 = num8;
				num3 = Int(item, "round");
				num4 = Int(item, "team1wins");
				num5 = Int(item, "team2wins");
				num6 = Int(item, "frame");
				num7 = Int(item, "gameMode");
				if (item.TryGetProperty("lastUpdateTime", out var value3) && value3.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value3.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
				{
					dateTime = result.LocalDateTime;
				}
			}
			LastOk = DateTime.Now;
			LastError = "";
			if (num != LiveCount)
			{
				LiveCount = num;
				L.W("[对局] 站点上正在进行中的对局：" + num + " 局");
			}
			if (flag)
			{
				if (!InGame || GameId != num2)
				{
					L.W("[对局] 站点上看到你的对局：id=" + num2 + "，" + num4 + " : " + num5 + "，第 " + num3 + " 回合");
				}
				InGame = true;
				GameId = num2;
				Round = num3;
				Team1 = num4;
				Team2 = num5;
				GameMode = num7;
				if (_frame != num6 || _fetchedAt == DateTime.MinValue)
				{
					_frame = num6;
					_fetchedAt = ((dateTime == DateTime.MinValue) ? DateTime.Now : dateTime);
				}
			}
			else if (InGame)
			{
				L.W("[对局] 站点上已经没有进行中的这局了（结束或掉线）");
				InGame = false;
				GameId = 0L;
				_frame = 0;
				_fetchedAt = DateTime.MinValue;
			}
			try
			{
				Changed?.Invoke();
			}
			catch
			{
			}
		}
		catch (Exception ex)
		{
			LastError = ex.Message;
			if (_lastErrLogged != LastError)
			{
				_lastErrLogged = LastError;
				L.W("[对局] 拉取进行中对局失败：" + LastError);
			}
		}
	}

	private static bool HasMe(JsonElement game, string myHandle)
	{
		if (string.IsNullOrEmpty(myHandle))
		{
			return false;
		}
		if (!game.TryGetProperty("playerList", out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return false;
		}
		foreach (JsonElement item in value.EnumerateArray())
		{
			if (item.TryGetProperty("playerHandle", out var value2) && value2.ValueKind == JsonValueKind.String && string.Equals(value2.GetString(), myHandle, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private static int Int(JsonElement e, string name)
	{
		if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
		{
			return 0;
		}
		if (!value.TryGetInt32(out var value2))
		{
			return 0;
		}
		return value2;
	}

	private static long Long(JsonElement e, string name)
	{
		if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
		{
			return 0L;
		}
		if (!value.TryGetInt64(out var value2))
		{
			return 0L;
		}
		return value2;
	}
}
