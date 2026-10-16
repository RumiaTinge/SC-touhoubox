using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sc2Toolbox;

public static class TouhouApi
{
	public class StatIcon
	{
		public int Id;

		public int Num;

		public string Tip = "";
	}

	private class Snapshot
	{
		public TPlayer Me = new TPlayer();

		public List<TPlayer> Rank = new List<TPlayer>();

		public List<THero> Heroes = new List<THero>();

		public List<TMatch> Matches = new List<TMatch>();

		public string SavedAt = "";
	}

	public const string ApiBase = "https://www.aslucky.club/touhou-api/";

	public const string FileBase = "https://www.aslucky.club/file/touhou/";

	public const string ImgBase = "https://www.aslucky.club/touhou/img/";

	public static string Region = "5";

	public static string Aaa = "1";

	public static string HandleStr = "";

	public static string MyHandle = "";

	public static string LastError = "";

	public static DateTime LastOk = DateTime.MinValue;

	public static bool PartialFail = false;

	public static bool Busy = false;

	private static readonly HttpClient _local = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(4L)
	};

	public static TPlayer Me = new TPlayer();

	public static List<TPlayer> Rank = new List<TPlayer>();

	public static List<TMatch> Matches = new List<TMatch>();

	public static List<THero> MyHeroes = new List<THero>();

	public static Dictionary<string, string> NameOf = new Dictionary<string, string>();

	public static Dictionary<string, int> HeroOf = new Dictionary<string, int>();

	public static Dictionary<string, int> MmrOf = new Dictionary<string, int>();

	public static Dictionary<string, string> ClanOf = new Dictionary<string, string>();

	public static Dictionary<string, TPlayer> RoomProfiles = new Dictionary<string, TPlayer>();

	private static Dictionary<int, string> _heroNames = new Dictionary<int, string>();

	private static readonly HttpClient Http = CreateClient();

	private static readonly char[] AbilitySlots = new char[5] { 't', 'q', 'w', 'e', 'r' };

	private static readonly int[] InitStatIconIds = new int[6] { 4, 6, 0, 1, 2, 3 };

	private static readonly string[] HeroNameTable = new string[54]
	{
		"", "魂魄妖梦", "博丽灵梦", "琪露诺", "十六夜咲夜", "永江衣玖", "村纱水蜜", "铃仙", "爱丽丝", "蕾米莉亚",
		"芙兰朵露", "黑化琪露诺", "红魔蕾米莉亚", "再临妖梦", "雾雨魔理沙", "八云紫", "比那名居天子", "藤原妹红", "红美铃", "再临琪露诺",
		"犬走椛", "帕秋莉", "蕾蒂", "西行寺幽幽子", "灵乌路空", "射命丸文", "洩矢诹访子", "东风谷早苗", "小野冢小町", "八意永琳",
		"伊吹萃香", "圣白莲", "古明地觉", "黑化灵梦", "祸灵梦", "先代巫女", "蓬莱山辉夜", "封兽鵺", "黑暗天子", "黑暗妖梦",
		"风见幽香", "黑魅灵", "再临芙兰朵露", "四季映姬", "清兰", "黑化村纱", "依神女苑", "古明地恋", "再临犬走椛", "黑化辉夜",
		"绿衣红美玲", "因幡帝", "露米娅", "纯狐"
	};

	private static int _cycle = 0;

	private static DateTime _tMe = DateTime.MinValue;

	private static DateTime _tHeroes = DateTime.MinValue;

	private static DateTime _tMatches = DateTime.MinValue;

	private static DateTime _tBest = DateTime.MinValue;

	private static DateTime _tRank = DateTime.MinValue;

	private static long _lastGameId = 0L;

	public static int UpdatedLastRound = 0;

	public static string LastPlan = "还没刷新";

	public static bool SawNewGame = false;

	private static readonly Dictionary<string, DateTime> _profileTried = new Dictionary<string, DateTime>();

	private static DateTime _heroNamesAt = DateTime.MinValue;

	private static bool _heroNamesLoaded = false;

	private static readonly JsonSerializerOptions _jo = new JsonSerializerOptions
	{
		IncludeFields = true
	};

	private static bool _snapTried = false;

	public static string SavedAtText = "";

	public static string HeroNameFile
	{
		get
		{
			try
			{
				return Path.Combine(L.Dir, "heroes.tsv");
			}
			catch
			{
				return "";
			}
		}
	}

	private static string CachePath
	{
		get
		{
			try
			{
				return Path.Combine(L.Dir, "cache.json");
			}
			catch
			{
				return "";
			}
		}
	}

	public static event Action Changed;

	public static void Raise()
	{
		try
		{
			Changed?.Invoke();
		}
		catch
		{
		}
	}

	private static HttpClient CreateClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(90L);
		try
		{
			httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
		}
		catch
		{
		}
		return httpClient;
	}

	public static string HeroImg(int heroId)
	{
		return "https://www.aslucky.club/touhou/img/hero/" + heroId + ".jpg";
	}

	public static string RegionImg(int region)
	{
		return "https://www.aslucky.club/touhou/img/region/" + region + ".jpg";
	}

	public static string Pad2(int n)
	{
		string text = n.ToString();
		if (text.Length >= 2)
		{
			return text;
		}
		return "0" + text;
	}

	public static string AbilityImg(int heroId, int abilityId, int slot)
	{
		if (heroId <= 0 || slot < 0 || slot >= AbilitySlots.Length)
		{
			return "";
		}
		return "https://www.aslucky.club/touhou/img/ability/" + Pad2(heroId) + "_" + (abilityId + 1) + "_" + slot + AbilitySlots[slot] + ".jpg";
	}

	public static string SpellImg(int slot, int spellId)
	{
		if (spellId <= 0)
		{
			return "";
		}
		return "https://www.aslucky.club/touhou/img/spell/" + slot + "_" + spellId + ".jpg";
	}

	public static string RuneImg(int column, int rune)
	{
		if (rune <= 0)
		{
			return "";
		}
		return "https://www.aslucky.club/touhou/img/rune/" + column + "_" + rune + ".jpg";
	}

	public static string StatImg(int index)
	{
		return "https://www.aslucky.club/touhou/img/stat/" + index + ".jpg";
	}

	public static List<StatIcon> StatIcons(TMatchPlayer p)
	{
		List<StatIcon> list = new List<StatIcon>();
		if (p == null)
		{
			return list;
		}
		int[] array = Nums(p.InitStats);
		for (int i = 0; i < array.Length && i < InitStatIconIds.Length; i++)
		{
			int num = array[i];
			if (num > 0)
			{
				if (num > 8)
				{
					num = 8;
				}
				for (int j = 0; j < num; j++)
				{
					list.Add(new StatIcon
					{
						Id = InitStatIconIds[i],
						Num = 0,
						Tip = "初始属性碎片 " + InitStatIconIds[i]
					});
				}
			}
		}
		int[] array2 = Nums(p.StatSet);
		for (int k = 0; k < array2.Length; k++)
		{
			if (k != 8 && k != 9 && array2[k] > 0)
			{
				list.Add(new StatIcon
				{
					Id = k,
					Num = array2[k],
					Tip = "属性碎片 " + k + " ×" + array2[k]
				});
			}
		}
		return list;
	}

	public static int[] Nums(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
		{
			return new int[0];
		}
		string[] array = s.Split(new char[2] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
		List<int> list = new List<int>();
		string[] array2 = array;
		foreach (string text in array2)
		{
			list.Add(int.TryParse(text.Trim(), out var result) ? result : (-1));
		}
		return list.ToArray();
	}

	public static List<string> PlayerArtUrls(TMatchPlayer p)
	{
		List<string> list = new List<string>();
		if (p == null || p.HeroId <= 0)
		{
			return list;
		}
		list.Add(HeroImg(p.HeroId));
		int[] array = Nums(p.AbilitySet);
		for (int i = 0; i < array.Length && i < AbilitySlots.Length; i++)
		{
			string text = AbilityImg(p.HeroId, array[i], i);
			if (text.Length > 0)
			{
				list.Add(text);
			}
		}
		int[] array2 = Nums(p.Spells);
		for (int j = 0; j < array2.Length; j++)
		{
			string text2 = SpellImg(j, array2[j]);
			if (text2.Length > 0)
			{
				list.Add(text2);
			}
		}
		int[] array3 = Nums(p.PrimaryRunes);
		foreach (int rune in array3)
		{
			string text3 = RuneImg(p.PrimaryColumn, rune);
			if (text3.Length > 0)
			{
				list.Add(text3);
			}
		}
		if (p.SecondaryRune > 0)
		{
			list.Add(RuneImg(p.SecondaryColumn, p.SecondaryRune));
		}
		foreach (StatIcon item in StatIcons(p))
		{
			list.Add(StatImg(item.Id));
		}
		int[] array4 = Nums(p.AbilityUpSeq);
		foreach (int num in array4)
		{
			if (num >= 0 && num < array.Length)
			{
				string text4 = AbilityImg(p.HeroId, array[num], num);
				if (text4.Length > 0)
				{
					list.Add(text4);
				}
			}
		}
		return list;
	}

	public static List<string> MatchArtUrls(TMatch m)
	{
		List<string> list = new List<string>();
		if (m == null)
		{
			return list;
		}
		foreach (TMatchPlayer player in m.Players)
		{
			list.AddRange(PlayerArtUrls(player));
		}
		return list;
	}

	public static int PreloadMatchArt(TMatch m, int budgetMs = 0)
	{
		List<string> list = MatchArtUrls(m);
		if (list.Count == 0)
		{
			return 0;
		}
		int[] array = new int[1];
		Download(list, array, budgetMs, Environment.TickCount);
		return array[0];
	}

	public static string PfpImg(string handle, int pfp)
	{
		if (pfp <= 0 || string.IsNullOrEmpty(handle))
		{
			return "";
		}
		return "https://www.aslucky.club/file/touhou/pfp/" + handle + ".png?r=" + pfp;
	}

	public static string HeroName(int heroId)
	{
		if (_heroNames.TryGetValue(heroId, out var value) && !string.IsNullOrEmpty(value))
		{
			return value;
		}
		if (heroId >= 1 && heroId < HeroNameTable.Length)
		{
			return HeroNameTable[heroId];
		}
		return "英雄 " + heroId;
	}

	private static string Str(JsonElement e, string k)
	{
		try
		{
			if (e.TryGetProperty(k, out var value) && value.ValueKind == JsonValueKind.String)
			{
				return value.GetString();
			}
		}
		catch
		{
		}
		return "";
	}

	private static int Int(JsonElement e, string k)
	{
		try
		{
			if (!e.TryGetProperty(k, out var value))
			{
				return 0;
			}
			if (value.ValueKind == JsonValueKind.Number)
			{
				return (int)Math.Round(value.GetDouble());
			}
			if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
			{
				return (int)Math.Round(result);
			}
		}
		catch
		{
		}
		return 0;
	}

	private static double Dbl(JsonElement e, string k)
	{
		try
		{
			if (!e.TryGetProperty(k, out var value))
			{
				return 0.0;
			}
			if (value.ValueKind == JsonValueKind.Number)
			{
				return value.GetDouble();
			}
			if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
			{
				return result;
			}
		}
		catch
		{
		}
		return 0.0;
	}

	private static long Lng(JsonElement e, string k)
	{
		try
		{
			if (!e.TryGetProperty(k, out var value))
			{
				return 0L;
			}
			if (value.ValueKind == JsonValueKind.Number)
			{
				return value.GetInt64();
			}
			if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var result))
			{
				return result;
			}
		}
		catch
		{
		}
		return 0L;
	}

	private static void SetErr(ConcurrentBag<string> errs, string msg)
	{
		if (errs != null)
		{
			errs.Add(msg);
		}
		else
		{
			LastError = msg;
		}
	}

	private static JsonElement? GetData(string path, string why, int timeoutMs = 25000)
	{
		return GetData(path, why, timeoutMs, null);
	}

	private static JsonElement? GetData(string path, string why, int timeoutMs, ConcurrentBag<string> errs)
	{
		try
		{
			HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.aslucky.club/touhou-api/" + path);
			if (!string.IsNullOrEmpty(Region))
			{
				httpRequestMessage.Headers.TryAddWithoutValidation("region", Region);
			}
			if (!string.IsNullOrEmpty(Aaa))
			{
				httpRequestMessage.Headers.TryAddWithoutValidation("aaa", Aaa);
			}
			if (!string.IsNullOrEmpty(HandleStr))
			{
				httpRequestMessage.Headers.TryAddWithoutValidation("handleStr", HandleStr);
			}
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(Math.Max(2000, timeoutMs));
			byte[] result = Http.SendAsync(httpRequestMessage, cancellationTokenSource.Token).GetAwaiter().GetResult()
				.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
			JsonElement rootElement = JsonDocument.Parse(Encoding.UTF8.GetString(result)).RootElement;
			if (rootElement.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.Number && value.GetInt32() != 1)
			{
				SetErr(errs, why + "：接口返回 code=" + value.GetInt32());
				return null;
			}
			if (rootElement.TryGetProperty("data", out var value2))
			{
				return value2;
			}
			SetErr(errs, why + "：响应里没有 data");
			return null;
		}
		catch (Exception ex)
		{
			SetErr(errs, why + "：" + ex.Message);
			return null;
		}
	}

	public static void RefreshLocalIdentity()
	{
		try
		{
			string text;
			using (HttpResponseMessage httpResponseMessage = _local.GetAsync("http://127.0.0.1:8030/").GetAwaiter().GetResult())
			{
				text = httpResponseMessage.Content.ReadAsStringAsync().GetAwaiter().GetResult()
					.Trim();
			}
			string[] array = text.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length != 0 && array[0].Contains("-S2-"))
			{
				MyHandle = array[0];
				HandleStr = text;
				string[] array2 = MyHandle.Split('-');
				if (array2.Length != 0)
				{
					Region = array2[0];
				}
				Me.Handle = MyHandle;
			}
		}
		catch
		{
		}
	}

	private static bool Hit(ConcurrentBag<string> errs, string key)
	{
		foreach (string err in errs)
		{
			if (err != null && err.Contains(key))
			{
				return true;
			}
		}
		return false;
	}

	public static void Refresh(bool force = false)
	{
		if (Busy)
		{
			return;
		}
		Busy = true;
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			_cycle++;
			LastError = "";
			LoadSnapshot();
			RefreshLocalIdentity();
			if (string.IsNullOrEmpty(MyHandle))
			{
				LastError = "没拿到本机档案号（touhou_client 没在跑？）";
				L.W("对战数据刷新失败：" + LastError);
				return;
			}
			long num = 0L;
			JsonElement? data = GetData("player/lastGame", "变化探测", 20000);
			if (data.HasValue)
			{
				num = Lng(data.Value, "gameId");
			}
			bool flag = num != 0;
			bool flag2 = _lastGameId != 0;
			bool flag3 = !flag || num != _lastGameId;
			if (flag)
			{
				_lastGameId = num;
			}
			SawNewGame = flag & flag2 & flag3;
			DateTime now = DateTime.Now;
			bool flag4 = (force | flag3) || (now - _tMe).TotalSeconds > 300.0;
			bool flag5 = (force | flag3) || (now - _tHeroes).TotalSeconds > 600.0;
			bool flag6 = (force | flag3) || (now - _tMatches).TotalSeconds > 120.0;
			bool flag7 = force || (now - _tRank).TotalSeconds > 600.0;
			bool flag8 = (force | flag3) || (now - _tBest).TotalSeconds > 1800.0;
			ConcurrentBag<string> errs = new ConcurrentBag<string>();
			List<Task> list = new List<Task>();
			List<string> list2 = new List<string>();
			if (flag4)
			{
				list2.Add("我的资料");
				list.Add(Task.Run(() =>
				{
					RefreshMeProfile(errs);
				}));
			}
			if (flag7)
			{
				list2.Add("排行榜");
				list.Add(Task.Run(() =>
				{
					RefreshRank(errs);
				}));
			}
			if (flag5)
			{
				list2.Add("我的英雄");
				list.Add(Task.Run(() =>
				{
					RefreshMyHeroes(errs);
				}));
			}
			if (flag6)
			{
				list2.Add("对局记录");
				list.Add(Task.Run(() =>
				{
					RefreshMyMatches(errs);
				}));
			}
			RefreshHeroNames();
			Task.WaitAll(list.ToArray());
			bool flag9 = true;
			if (flag8)
			{
				list2.Add("单场最佳");
				RefreshMyBest();
				flag9 = LastError.Length == 0;
			}
			if (flag4 && !Hit(errs, "我的资料"))
			{
				_tMe = now;
			}
			if (flag7 && !Hit(errs, "排行榜"))
			{
				_tRank = now;
			}
			if (flag5 && !Hit(errs, "英雄"))
			{
				_tHeroes = now;
			}
			if (flag6 && !Hit(errs, "对局记录"))
			{
				_tMatches = now;
			}
			if (flag8 & flag9)
			{
				_tBest = now;
			}
			if (SawNewGame)
			{
				_tMe = (_tHeroes = (_tMatches = (_tBest = DateTime.MinValue)));
			}
			UpdatedLastRound = list2.Count;
			LastPlan = ((list2.Count == 0) ? "无变化，五块数据都用缓存" : string.Join("、", list2.ToArray()));
			if (!errs.IsEmpty)
			{
				LastError = ((LastError.Length > 0) ? (LastError + "；") : "") + string.Join("；", errs.ToArray());
			}
			PartialFail = list2.Count > 0 && !errs.IsEmpty;
			LastOk = DateTime.Now;
			SaveSnapshot();
			L.W("对战数据已刷新（" + stopwatch.ElapsedMilliseconds + " ms）：我=" + (string.IsNullOrEmpty(Me.DisplayName) ? MyHandle : Me.DisplayName) + " 排名#" + Me.Rank + " 英雄 " + MyHeroes.Count + " 个 对局 " + Matches.Count + " 场 MMR " + Me.Mmr + " 累计 " + Me.Games + " 场 胜率 " + Me.WinRate.ToString("0.#") + "% 榜单 " + Rank.Count + " 人；本轮更新 " + list2.Count + "/5 块（" + LastPlan + "）" + (SawNewGame ? "，探测到新对局" : "") + ((LastError.Length > 0) ? ("；⚠ " + LastError) : ""));
		}
		catch (Exception ex)
		{
			LastError = ex.Message;
			L.W("对战数据刷新异常：" + ex.Message);
		}
		finally
		{
			Busy = false;
			Raise();
		}
	}

	private static void RefreshMeProfile(ConcurrentBag<string> errs = null)
	{
		JsonElement? data = GetData("player/info/" + MyHandle + "?stat=1", "我的资料", 30000, errs);
		if (!data.HasValue)
		{
			return;
		}
		ApplyProfile(data.Value, Me, isMe: true);
		RoomProfiles[MyHandle] = Me;
		if (Me.Handle.Length > 0)
		{
			if (Me.Name.Length > 0)
			{
				NameOf[Me.Handle] = Me.Name;
			}
			ClanOf[Me.Handle] = Me.ClanTag ?? "";
			if (Me.HeroId > 0)
			{
				HeroOf[Me.Handle] = Me.HeroId;
			}
			MmrOf[Me.Handle] = Me.Mmr;
		}
	}

	private static void ApplyProfile(JsonElement p, TPlayer tp, bool isMe)
	{
		string text = Str(p, "handle");
		if (text.Length > 0 && tp.Handle.Length == 0)
		{
			tp.Handle = text;
		}
		string text2 = Str(p, "name");
		if (text2.Length > 0)
		{
			tp.Name = text2;
		}
		tp.ClanTag = Str(p, "clanTag");
		int num = Int(p, "region");
		if (num > 0)
		{
			tp.Region = num;
		}
		tp.Pfp = Int(p, "pfp");
		tp.Mmr = Int(p, "mmr");
		tp.MmrMax = Int(p, "mmrMax");
		tp.Wins = Int(p, "wins");
		tp.Losses = Int(p, "losses");
		tp.Games = tp.Wins + tp.Losses;
		tp.WinRate = ((tp.Games > 0) ? (100.0 * (double)tp.Wins / (double)tp.Games) : 0.0);
		tp.WinStreak = Int(p, "winStreak");
		tp.LoseStreak = Int(p, "loseStreak");
		tp.WinStreakMax = Int(p, "winStreakMax");
		tp.LoseStreakMax = Int(p, "loseStreakMax");
		tp.Mvp = Int(p, "mvp");
		tp.Svp = Int(p, "svp");
		tp.ExitCount = Int(p, "exitCount");
		if (isMe && p.TryGetProperty("commonHeroes", out var value) && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0 && value[0].ValueKind == JsonValueKind.Number)
		{
			tp.HeroId = value[0].GetInt32();
		}
	}

	public static void RefreshRoomProfiles(IEnumerable<string> handles)
	{
		if (handles == null)
		{
			return;
		}
		foreach (string handle in handles)
		{
			if (string.IsNullOrEmpty(handle) || handle == MyHandle || !handle.Contains("-S2-") || (_profileTried.TryGetValue(handle, out var value) && (DateTime.Now - value).TotalSeconds < 600.0))
			{
				continue;
			}
			_profileTried[handle] = DateTime.Now;
			try
			{
				JsonElement? data = GetData("player/info/" + handle + "?stat=1", "玩家资料", 6000);
				if (data.HasValue)
				{
					TPlayer tPlayer = new TPlayer
					{
						Handle = handle
					};
					ApplyProfile(data.Value, tPlayer, isMe: false);
					if (tPlayer.Name.Length == 0 && NameOf.ContainsKey(handle))
					{
						tPlayer.Name = NameOf[handle];
					}
					if (tPlayer.Name.Length > 0)
					{
						NameOf[handle] = tPlayer.Name;
					}
					ClanOf[handle] = tPlayer.ClanTag ?? "";
					if (tPlayer.Mmr > 0)
					{
						MmrOf[handle] = tPlayer.Mmr;
					}
					RoomProfiles[handle] = tPlayer;
					Raise();
				}
			}
			catch
			{
			}
		}
	}

	private static void LoadHeroNames()
	{
		_heroNamesLoaded = true;
		try
		{
			string path = Path.Combine(L.Dir, "heroes.tsv");
			if (!File.Exists(path))
			{
				return;
			}
			string[] array = File.ReadAllLines(path, Encoding.UTF8);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split('\t');
				if (array2.Length == 2 && int.TryParse(array2[0], out var result) && array2[1].Length > 0)
				{
					_heroNames[result] = array2[1];
				}
			}
			if (_heroNames.Count > 0)
			{
				_heroNamesAt = DateTime.Now.AddMinutes(-50.0);
			}
		}
		catch
		{
		}
	}

	private static void SaveHeroNames()
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<int, string> heroName in _heroNames)
			{
				stringBuilder.Append(heroName.Key).Append('\t').Append(heroName.Value)
					.Append('\n');
			}
			File.WriteAllText(Path.Combine(L.Dir, "heroes.tsv"), stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		catch
		{
		}
	}

	private static void RefreshHeroNames()
	{
		if (!_heroNamesLoaded)
		{
			LoadHeroNames();
		}
	}

	public static bool WriteHeroNameTemplate(bool overwrite = false)
	{
		try
		{
			string heroNameFile = HeroNameFile;
			if (heroNameFile.Length == 0)
			{
				return false;
			}
			if (File.Exists(heroNameFile) && !overwrite)
			{
				return false;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("# SC-touhoubox 英雄名表（每行：id + 制表符 + 名字；以 # 开头的行会被忽略）\n");
			stringBuilder.Append("# 站点接口只有英文名，这份是中文名。想用游戏里的官方写法就直接改这里，改完在面板上点一次「刷新数据」。\n");
			for (int i = 1; i < HeroNameTable.Length; i++)
			{
				stringBuilder.Append(i).Append('\t').Append(HeroNameTable[i])
					.Append('\n');
			}
			File.WriteAllText(heroNameFile, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static void ReloadHeroNames()
	{
		_heroNames.Clear();
		_heroNamesLoaded = false;
		LoadHeroNames();
	}

	private static void RefreshRank(ConcurrentBag<string> errs = null)
	{
		JsonElement? data = GetData("player/rank", "排行榜", 45000, errs);
		if (!data.HasValue)
		{
			return;
		}
		List<TPlayer> list = new List<TPlayer>();
		try
		{
			Me.Rank = Int(data.Value, "myRank");
			if (data.Value.TryGetProperty("list", out var value) && value.ValueKind == JsonValueKind.Array)
			{
				int num = 0;
				foreach (JsonElement item in value.EnumerateArray())
				{
					num++;
					TPlayer tPlayer = new TPlayer
					{
						Handle = Str(item, "handle"),
						Name = Str(item, "name"),
						ClanTag = Str(item, "clanTag"),
						Region = Int(item, "region"),
						Pfp = Int(item, "pfp"),
						Mmr = Int(item, "mmr"),
						WinStreak = Int(item, "winStreak"),
						Wins = Int(item, "wins"),
						Losses = Int(item, "losses"),
						Rank = num
					};
					tPlayer.Games = tPlayer.Wins + tPlayer.Losses;
					tPlayer.WinRate = ((tPlayer.Games > 0) ? (100.0 * (double)tPlayer.Wins / (double)tPlayer.Games) : 0.0);
					JsonElement jsonElement = ((item.TryGetProperty("commonHeroes", out var value2) && value2.ValueKind == JsonValueKind.Array && value2.GetArrayLength() > 0) ? value2[0] : default(JsonElement));
					if (jsonElement.ValueKind == JsonValueKind.Number)
					{
						tPlayer.HeroId = jsonElement.GetInt32();
					}
					if (tPlayer.Handle.Length > 0)
					{
						NameOf[tPlayer.Handle] = tPlayer.Name;
						if (tPlayer.ClanTag != null)
						{
							ClanOf[tPlayer.Handle] = tPlayer.ClanTag;
						}
						if (tPlayer.HeroId > 0)
						{
							HeroOf[tPlayer.Handle] = tPlayer.HeroId;
						}
						MmrOf[tPlayer.Handle] = tPlayer.Mmr;
					}
					list.Add(tPlayer);
				}
			}
		}
		catch (Exception ex)
		{
			LastError = "排行榜解析：" + ex.Message;
		}
		if (list.Count > 0)
		{
			Rank = list;
		}
	}

	private static void RefreshMyHeroes(ConcurrentBag<string> errs = null)
	{
		JsonElement? data = GetData("hero/list2/" + MyHandle, "我的英雄数据", 60000, errs);
		if (!data.HasValue)
		{
			return;
		}
		List<THero> list = new List<THero>();
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		double num6 = 0.0;
		int num7 = 0;
		try
		{
			foreach (JsonElement item in data.Value.EnumerateArray())
			{
				THero tHero = new THero
				{
					HeroId = Int(item, "heroId"),
					Total = Int(item, "total"),
					WinPercentage = Dbl(item, "winPercentage"),
					PickPercentage = Dbl(item, "pickPercentage"),
					BanPercentage = Dbl(item, "banPercentage"),
					KillAverage = Dbl(item, "killAverage"),
					DeathAverage = Dbl(item, "deathAverage"),
					AssistAverage = Dbl(item, "assistAverage"),
					DamageAverage = Dbl(item, "damageAverage"),
					DamagedAverage = Dbl(item, "damagedAverage"),
					Fighting = Dbl(item, "fighting"),
					Mmr = Int(item, "mmr")
				};
				if (tHero.Total > 0)
				{
					num7 += tHero.Total;
					num += tHero.WinPercentage * (double)tHero.Total;
					num2 += (double)tHero.Total;
					num3 += tHero.KillAverage * (double)tHero.Total;
					num4 += tHero.DeathAverage * (double)tHero.Total;
					num5 += tHero.AssistAverage * (double)tHero.Total;
					num6 += tHero.DamageAverage * (double)tHero.Total;
				}
				list.Add(tHero);
			}
			list.Sort((THero a, THero b) => b.Total.CompareTo(a.Total));
			if (num2 > 0.0)
			{
				Me.Games = num7;
				Me.WinRate = num / num2;
				Me.KillAvg = num3 / num2;
				Me.DeathAvg = num4 / num2;
				Me.AssistAvg = num5 / num2;
				Me.DamageAvg = num6 / num2;
			}
		}
		catch (Exception ex)
		{
			LastError = "英雄数据解析：" + ex.Message;
		}
		MyHeroes = list;
	}

	private static void RefreshMyMatches(ConcurrentBag<string> errs = null)
	{
		JsonElement? data = GetData("game/list?current=1&pageSize=8&playerHandle=" + MyHandle, "对局记录", 60000, errs);
		if (!data.HasValue)
		{
			return;
		}
		List<TMatch> list = new List<TMatch>();
		try
		{
			if (!data.Value.TryGetProperty("records", out var value) || value.ValueKind != JsonValueKind.Array)
			{
				return;
			}
			int num = 0;
			int num2 = 0;
			foreach (JsonElement item in value.EnumerateArray())
			{
				TMatch tMatch = new TMatch
				{
					GameId = ((Lng(item, "id") > 0) ? Lng(item, "id") : Lng(item, "gameId")),
					Mode = Int(item, "mode"),
					GameMode = Int(item, "gameMode"),
					Winner = Int(item, "winner"),
					Team1Wins = Int(item, "team1wins"),
					Team2Wins = Int(item, "team2wins"),
					EndTime = Lng(item, "endTime"),
					EndTimeText = Str(item, "endTime")
				};
				if (item.TryGetProperty("playerList", out var value2) && value2.ValueKind == JsonValueKind.Array)
				{
					foreach (JsonElement item2 in value2.EnumerateArray())
					{
						TMatchPlayer tMatchPlayer = new TMatchPlayer
						{
							GameId = Lng(item2, "gameId"),
							Handle = Str(item2, "playerHandle"),
							Name = Str(item2, "playerName"),
							ClanTag = Str(item2, "clanTag"),
							Team = Int(item2, "team"),
							HeroId = Int(item2, "heroId"),
							Win = Int(item2, "win"),
							Kill = Int(item2, "kill"),
							Death = Int(item2, "death"),
							Assist = Int(item2, "assist"),
							Money = Int(item2, "money"),
							Damage = Int(item2, "damage"),
							Damaged = Int(item2, "damaged"),
							Mmr = Int(item2, "mmr"),
							MmrChange = Int(item2, "mmrChange"),
							MmrHero = Int(item2, "mmrHero"),
							Mvp = Int(item2, "mvp"),
							HeroLevel = Int(item2, "heroLevel"),
							Healing = Int(item2, "healing"),
							SelectNum = Int(item2, "selectNum"),
							ExitFrame = Int(item2, "exitFrame"),
							AbilityUpSeq = Str(item2, "abilityUpSeq"),
							AbilitySet = Str(item2, "abilitySet"),
							Spells = Str(item2, "spells"),
							StatSet = Str(item2, "statSet"),
							InitStats = Str(item2, "initStats"),
							PrimaryColumn = Int(item2, "primaryColumn"),
							SecondaryColumn = Int(item2, "secondaryColumn"),
							SecondaryRune = Int(item2, "secondaryRune"),
							PrimaryRunes = Str(item2, "primaryRunes")
						};
						tMatch.Players.Add(tMatchPlayer);
						if (tMatchPlayer.Handle.Length > 0)
						{
							if (tMatchPlayer.Name.Length > 0)
							{
								NameOf[tMatchPlayer.Handle] = tMatchPlayer.Name;
							}
							if (tMatchPlayer.HeroId > 0)
							{
								HeroOf[tMatchPlayer.Handle] = tMatchPlayer.HeroId;
							}
							if (tMatchPlayer.Mmr > 0)
							{
								MmrOf[tMatchPlayer.Handle] = tMatchPlayer.Mmr;
							}
							if (!string.IsNullOrEmpty(tMatchPlayer.ClanTag))
							{
								ClanOf[tMatchPlayer.Handle] = tMatchPlayer.ClanTag;
							}
						}
						if (tMatchPlayer.Handle == MyHandle)
						{
							tMatch.MyTeam = tMatchPlayer.Team;
							tMatch.MyHeroId = tMatchPlayer.HeroId;
							tMatch.MyKill = tMatchPlayer.Kill;
							tMatch.MyDeath = tMatchPlayer.Death;
							tMatch.MyAssist = tMatchPlayer.Assist;
							tMatch.MyMoney = tMatchPlayer.Money;
							tMatch.MyDamage = tMatchPlayer.Damage;
							tMatch.MyDamaged = tMatchPlayer.Damaged;
							tMatch.MyMmr = tMatchPlayer.Mmr;
							tMatch.MyMmrChange = tMatchPlayer.MmrChange;
							tMatch.MyMmrHero = tMatchPlayer.MmrHero;
							tMatch.IsMvp = tMatchPlayer.Mvp > 0;
						}
					}
				}
				if (tMatch.GameId == 0L && tMatch.Players.Count > 0)
				{
					tMatch.GameId = tMatch.Players[0].GameId;
				}
				list.Add(tMatch);
			}
			TMatch tMatch2 = list.FirstOrDefault((TMatch x) => x.MyMmr > 0);
			if (tMatch2 != null)
			{
				Me.Mmr = tMatch2.MyMmr;
				Me.MmrChange = tMatch2.MyMmrChange;
			}
			foreach (TMatch item3 in list)
			{
				bool? win = item3.Win;
				if (win == true)
				{
					num++;
				}
				else if (win == false)
				{
					num2++;
				}
				if (num + num2 >= 10)
				{
					break;
				}
			}
			Me.Wins = num;
			Me.Losses = num2;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			foreach (TMatch item4 in list)
			{
				if (item4.MyHeroId > 0)
				{
					num3++;
					num4 += item4.MyKill;
					num5 += item4.MyDeath;
					num6 += item4.MyAssist;
					if (num3 >= 20)
					{
						break;
					}
				}
			}
			if (num3 > 0)
			{
				Me.KillAvg = (double)num4 / (double)num3;
				Me.DeathAvg = (double)num5 / (double)num3;
				Me.AssistAvg = (double)num6 / (double)num3;
				Me.Sample = num3;
			}
			if (list.Count > 0)
			{
				TMatch tMatch3 = list[0];
				if (tMatch3.MyHeroId > 0)
				{
					Me.HeroId = tMatch3.MyHeroId;
				}
				foreach (TMatchPlayer item5 in tMatch3.Players.Where((TMatchPlayer p) => p.Handle.Length > 0))
				{
					if (item5.Name.Length > 0)
					{
						NameOf[item5.Handle] = item5.Name;
					}
					if (item5.HeroId > 0)
					{
						HeroOf[item5.Handle] = item5.HeroId;
					}
				}
			}
			Matches = list;
		}
		catch (Exception ex)
		{
			LastError = "对局记录解析：" + ex.Message;
		}
	}

	private static void RefreshMyBest()
	{
		if (_cycle != 1 && _cycle % 5 != 0 && Me.BestKill > 0)
		{
			return;
		}
		JsonElement? data = GetData("player/max/" + MyHandle, "个人最佳", 20000);
		if (!data.HasValue)
		{
			return;
		}
		try
		{
			Me.BestDamage = Int(data.Value, "damage");
			Me.BestDamaged = Int(data.Value, "damaged");
			Me.BestDeath = Int(data.Value, "death");
			Me.BestMoney = Int(data.Value, "money");
			Me.BestAssist = Int(data.Value, "assist");
			Me.BestKill = Int(data.Value, "kill");
		}
		catch
		{
		}
	}

	public static string NameFor(string handle)
	{
		if (string.IsNullOrEmpty(handle))
		{
			return "";
		}
		if (NameOf.TryGetValue(handle, out var value) && !string.IsNullOrEmpty(value))
		{
			return value;
		}
		if (Ctx.Watcher != null && Ctx.Watcher.Nicks != null && Ctx.Watcher.Nicks.TryGetValue(handle, out value) && !string.IsNullOrEmpty(value))
		{
			return value;
		}
		return TPlayer.Short(handle);
	}

	public static int HeroFor(string handle)
	{
		if (!string.IsNullOrEmpty(handle) && HeroOf.TryGetValue(handle, out var value))
		{
			return value;
		}
		return 0;
	}

	public static int MmrFor(string handle)
	{
		if (!string.IsNullOrEmpty(handle) && MmrOf.TryGetValue(handle, out var value))
		{
			return value;
		}
		return 0;
	}

	public static int PreloadArt(IEnumerable<string> roomHandles = null, int budgetMs = 0)
	{
		int tickCount = Environment.TickCount;
		int[] array = new int[1];
		try
		{
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			if (roomHandles != null)
			{
				foreach (string item in roomHandles.Distinct())
				{
					if (string.IsNullOrEmpty(item))
					{
						continue;
					}
					int num = HeroFor(item);
					if (num > 0)
					{
						list.Add(HeroImg(num));
					}
					int num2 = RegionOf(item);
					if (num2 > 0)
					{
						list.Add(RegionImg(num2));
					}
					if (RoomProfiles.TryGetValue(item, out var value))
					{
						string text = PfpImg(item, value.Pfp);
						if (text.Length > 0)
						{
							list2.Add(text);
						}
					}
				}
			}
			if (Me.HeroId > 0)
			{
				list.Add(HeroImg(Me.HeroId));
			}
			foreach (THero myHero in MyHeroes)
			{
				list2.Add(HeroImg(myHero.HeroId));
			}
			foreach (TMatch item2 in Matches.Take(8))
			{
				if (item2.MyHeroId > 0)
				{
					list2.Add(HeroImg(item2.MyHeroId));
				}
			}
			foreach (TMatch item3 in Matches.Take(2))
			{
				list2.AddRange(MatchArtUrls(item3));
			}
			foreach (TPlayer item4 in Rank)
			{
				list2.Add(RegionImg((item4.Region > 0) ? item4.Region : 5));
				if (item4.HeroId > 0)
				{
					list2.Add(HeroImg(item4.HeroId));
				}
				string text2 = PfpImg(item4.Handle, item4.Pfp);
				if (text2.Length > 0)
				{
					list2.Add(text2);
				}
			}
			Download(list, array, budgetMs, tickCount);
			Download(list2, array, budgetMs, tickCount);
		}
		catch
		{
		}
		return array[0];
	}

	private static void Download(List<string> urls, int[] cnt, int budgetMs, int t0)
	{
		ParallelOptions parallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = 4
		};
		try
		{
			Parallel.ForEach(urls.Distinct(), parallelOptions, (string u) =>
			{
				if (budgetMs <= 0 || Environment.TickCount - t0 <= budgetMs)
				{
					int num = ImgCache.Preload(u);
					if (num > 0)
					{
						Interlocked.Add(ref cnt[0], num);
					}
				}
			});
		}
		catch
		{
		}
	}

	public static int RegionOf(string handle)
	{
		if (string.IsNullOrEmpty(handle))
		{
			return 0;
		}
		if (RoomProfiles.TryGetValue(handle, out var value) && value.Region > 0)
		{
			return value.Region;
		}
		string[] array = handle.Split('-');
		if (array.Length != 0 && int.TryParse(array[0], out var result))
		{
			return result;
		}
		return 0;
	}

	private static void SaveSnapshot()
	{
		try
		{
			string cachePath = CachePath;
			if (cachePath.Length != 0)
			{
				Snapshot value = new Snapshot
				{
					Me = Me,
					Rank = Rank,
					Heroes = MyHeroes,
					Matches = Matches,
					SavedAt = DateTime.Now.ToString("MM-dd HH:mm")
				};
				File.WriteAllText(cachePath, JsonSerializer.Serialize(value, _jo), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
		}
		catch
		{
		}
	}

	public static void LoadSnapshot()
	{
		if (_snapTried)
		{
			return;
		}
		_snapTried = true;
		try
		{
			string cachePath = CachePath;
			if (cachePath.Length == 0 || !File.Exists(cachePath))
			{
				return;
			}
			Snapshot snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(cachePath, Encoding.UTF8), _jo);
			if (snapshot != null)
			{
				if (snapshot.Me != null && (snapshot.Me.Mmr > 0 || snapshot.Me.Handle.Length > 0))
				{
					Me = snapshot.Me;
				}
				if (snapshot.Rank != null && snapshot.Rank.Count > 0)
				{
					Rank = snapshot.Rank;
				}
				if (snapshot.Heroes != null && snapshot.Heroes.Count > 0)
				{
					MyHeroes = snapshot.Heroes;
				}
				if (snapshot.Matches != null && snapshot.Matches.Count > 0)
				{
					Matches = snapshot.Matches;
				}
				SavedAtText = ((snapshot.SavedAt == null) ? "" : snapshot.SavedAt);
				if (!string.IsNullOrEmpty(MyHandle) && !string.IsNullOrEmpty(Me.Name))
				{
					NameOf[MyHandle] = Me.Name;
				}
			}
		}
		catch
		{
		}
	}
}
