using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Sc2Toolbox;

/// <summary>
/// dbHeroNN_config 段里的一个加点方案。段的每个 Key 就是一条方案：Key 名是保存时间戳，
/// 值串是 105 个数字（7 段 × 15 位）后面跟一个方案名。
/// </summary>
public sealed class BankPreset
{
	/// <summary>方案键名，就是保存时间戳，如 2026:10:15:16:39:37。</summary>
	public string Key = "";

	/// <summary>值串原文。</summary>
	public string Raw = "";

	/// <summary>解析出的数值数组。</summary>
	public int[] Points = new int[0];

	/// <summary>方案名（值串末尾那段非数字文本）。</summary>
	public string Name = "";

	/// <summary>点数合计。</summary>
	public int Sum;

	/// <summary>方案所属英雄 id。</summary>
	public int HeroId;

	public string HeroName => TouhouApi.HeroName(HeroId);

	/// <summary>把点数数组还原成值串（105 个数字 + 方案名）。</summary>
	public static string Build(int[] points, string name)
	{
		List<string> list = new List<string>();
		foreach (int num in points)
		{
			list.Add(num.ToString(CultureInfo.InvariantCulture));
		}
		string text = string.Join(" ", list);
		if (!string.IsNullOrEmpty(name))
		{
			text = text + " " + name;
		}
		return text;
	}

	/// <summary>解析值串。</summary>
	public static void Parse(BankPreset preset)
	{
		preset.Points = new int[0];
		preset.Name = "";
		preset.Sum = 0;
		if (string.IsNullOrEmpty(preset.Raw))
		{
			return;
		}
		string[] array = preset.Raw.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		List<int> list = new List<int>(array.Length);
		List<string> list2 = new List<string>();
		int num = 0;
		foreach (string text in array)
		{
			int num2;
			if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out num2))
			{
				list.Add(num2);
				num += num2;
			}
			else
			{
				list2.Add(text);
			}
		}
		preset.Points = list.ToArray();
		preset.Name = string.Join(" ", list2);
		preset.Sum = num;
	}
}

/// <summary>
/// 单个英雄的本地存档战绩（对应 touhou.SC2Bank 里的一个 dbHeroNN 段）。
/// </summary>
public sealed class BankHeroStat
{
	public string Key = "";

	public int HeroId;

	public int Total;

	public int Win;

	public int Lose;

	public int Leave;

	/// <summary>dbHeroNN_config 段里的方案条数（每个时间戳一条）。</summary>
	public int ConfigCount;

	/// <summary>该英雄的全部加点方案（按时间戳从新到旧）。</summary>
	public List<BankPreset> Configs { get; } = new List<BankPreset>();

	/// <summary>最近一次加点方案的时间戳原文，如 2026:10:15:16:39:37。</summary>
	public string LastConfigAt = "";

	/// <summary>最近一次加点方案的原始串（空格分隔的数值，末尾是方案名）。</summary>
	public string ConfigRaw = "";

	/// <summary>最近一次加点方案解析出的数值数组（月灵传固定 105 项）。</summary>
	public int[] Points = new int[0];

	/// <summary>加点方案的名字，如「最后一次的配置」。</summary>
	public string ConfigName = "";

	/// <summary>Points 的合计。</summary>
	public int PointSum;

	public double WinRate => ((Total > 0) ? (Win * 100.0 / Total) : 0.0);

	public string Name => TouhouApi.HeroName(HeroId);
}

/// <summary>
/// 一份 touhou.SC2Bank 解析结果：玩家在东方月灵传里的本地存档战绩。
/// 这是游戏自己写在 Documents\StarCraft II\Accounts\...\Banks\... 下的，完全离线可得。
/// </summary>
public sealed class BankProfile
{
	public string BankPath = "";

	/// <summary>战网账号目录名，如 684772812。</summary>
	public string AccountId = "";

	/// <summary>档案目录名，如 5-S2-1-12345678（和 touhou_client 的 MyHandle 同格式）。</summary>
	public string ProfileId = "";

	/// <summary>地图目录名，如 5-S2-1-4028527。</summary>
	public string MapId = "";

	public DateTime ModifiedAt = DateTime.MinValue;

	/// <summary>是否和本机 touhou_client 报出来的当前档案一致。</summary>
	public bool IsCurrentAccount;

	public int Total;

	public int Win;

	public int Lose;

	public int Leave;

	public int Xp;

	public int Money;

	public int Version;

	public int Pet;

	public int Last;

	public int WinStreak;

	public int LoseStreak;

	public int WinStreakHigh;

	public int LoseStreakHigh;

	/// <summary>首次胜利的 Unix 秒（东方月灵传自己写的 GetSystemTime 换算值）。</summary>
	public long FirstWinUnix;

	public string CurrentHero = "";

	public string Status = "";

	public string Hash = "";

	public List<BankHeroStat> Heroes { get; } = new List<BankHeroStat>();

	public double WinRate => ((Total > 0) ? (Win * 100.0 / Total) : 0.0);

	public int CurrentHeroId => ParseHeroId(CurrentHero);

	public DateTime? FirstWinTime
	{
		get
		{
			if (FirstWinUnix <= 0L)
			{
				return null;
			}
			try
			{
				return DateTimeOffset.FromUnixTimeSeconds(FirstWinUnix).LocalDateTime;
			}
			catch
			{
				return null;
			}
		}
	}

	public string Label => AccountId + " / " + ProfileId;

	/// <summary>把 dbHero48 这样的段名解析成英雄 id 48；解析不出来返回 0。</summary>
	public static int ParseHeroId(string key)
	{
		if (string.IsNullOrEmpty(key) || !key.StartsWith("dbHero", StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}
		string text = key.Substring("dbHero".Length);
		if (text.EndsWith("_config", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring(0, text.Length - "_config".Length);
		}
		int result;
		if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
		{
			return result;
		}
		return 0;
	}
}

/// <summary>
/// 定位并解析本机星际争霸 II 的东方月灵传银行文件（touhou.SC2Bank）。
/// 银行文件其实是纯 XML，用 XDocument 直接读即可，不需要任何游戏进程或联网。
/// </summary>
public static class TouhouBank
{
	public static string LastError = "";

	/// <summary>Documents\StarCraft II（文档目录被重定向也能正确取到）。</summary>
	public static string Sc2Root()
	{
		try
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			if (!string.IsNullOrEmpty(folderPath))
			{
				return Path.Combine(folderPath, "StarCraft II");
			}
		}
		catch
		{
		}
		return "";
	}

	public static string AccountsRoot()
	{
		string text = Sc2Root();
		if (text.Length == 0)
		{
			return "";
		}
		return Path.Combine(text, "Accounts");
	}

	/// <summary>本机所有账号/档案下的 touhou.SC2Bank 全路径。</summary>
	public static List<string> FindBanks()
	{
		List<string> list = new List<string>();
		string text = AccountsRoot();
		if (text.Length == 0 || !Directory.Exists(text))
		{
			return list;
		}
		try
		{
			foreach (string item in Directory.EnumerateDirectories(text))
			{
				try
				{
					foreach (string item2 in Directory.EnumerateDirectories(item))
					{
						string text2 = Path.Combine(item2, "Banks");
						if (!Directory.Exists(text2))
						{
							continue;
						}
						foreach (string item3 in Directory.EnumerateDirectories(text2))
						{
							string text3 = Path.Combine(item3, "touhou.SC2Bank");
							if (File.Exists(text3))
							{
								list.Add(text3);
							}
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public static BankProfile Load(string bankPath)
	{
		try
		{
			BankProfile bankProfile = new BankProfile();
			bankProfile.BankPath = bankPath;
			try
			{
				bankProfile.ModifiedAt = File.GetLastWriteTime(bankPath);
			}
			catch
			{
			}
			string dirMap = Path.GetDirectoryName(bankPath);
			string dirBanks = string.IsNullOrEmpty(dirMap) ? "" : Path.GetDirectoryName(dirMap);
			string dirProfile = string.IsNullOrEmpty(dirBanks) ? "" : Path.GetDirectoryName(dirBanks);
			string dirAccount = string.IsNullOrEmpty(dirProfile) ? "" : Path.GetDirectoryName(dirProfile);
			bankProfile.MapId = string.IsNullOrEmpty(dirMap) ? "" : Path.GetFileName(dirMap);
			bankProfile.ProfileId = string.IsNullOrEmpty(dirProfile) ? "" : Path.GetFileName(dirProfile);
			bankProfile.AccountId = string.IsNullOrEmpty(dirAccount) ? "" : Path.GetFileName(dirAccount);
			Dictionary<int, BankHeroStat> dictionary = new Dictionary<int, BankHeroStat>();
			XDocument xDocument = XDocument.Load(bankPath);
			XElement xElement = xDocument.Root;
			if (xElement == null)
			{
				LastError = "银行文件是空的";
				return null;
			}
			foreach (XElement item in xElement.Elements("Section"))
			{
				string text = (string)item.Attribute("name") ?? "";
				if (text == "global")
				{
					foreach (XElement item2 in item.Elements("Key"))
					{
						string text2 = (string)item2.Attribute("name") ?? "";
						int num = ReadInt(item2);
						switch (text2)
						{
						case "total":
							bankProfile.Total = num;
							break;
						case "win":
							bankProfile.Win = num;
							break;
						case "lose":
							bankProfile.Lose = num;
							break;
						case "leave":
							bankProfile.Leave = num;
							break;
						case "xp":
							bankProfile.Xp = num;
							break;
						case "money":
							bankProfile.Money = num;
							break;
						case "version":
							bankProfile.Version = num;
							break;
						case "pet":
							bankProfile.Pet = num;
							break;
						case "last":
							bankProfile.Last = num;
							break;
						case "winStreak":
							bankProfile.WinStreak = num;
							break;
						case "loseStreak":
							bankProfile.LoseStreak = num;
							break;
						case "winStreakHigh":
							bankProfile.WinStreakHigh = num;
							break;
						case "loseStreakHigh":
							bankProfile.LoseStreakHigh = num;
							break;
						case "firstWin":
							bankProfile.FirstWinUnix = ReadLong(item2);
							break;
						}
					}
				}
				else if (text == "record")
				{
					foreach (XElement item3 in item.Elements("Key"))
					{
						string text3 = (string)item3.Attribute("name") ?? "";
						string text4 = ReadString(item3);
						if (text3 == "hero")
						{
							bankProfile.CurrentHero = text4;
						}
						else if (text3 == "status")
						{
							bankProfile.Status = text4;
						}
					}
				}
				else if (text == "hash")
				{
					foreach (XElement item4 in item.Elements("Key"))
					{
						bankProfile.Hash = ReadString(item4);
					}
				}
				else
				{
					if (!text.StartsWith("dbHero", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					int num2 = BankProfile.ParseHeroId(text);
					if (num2 <= 0)
					{
						continue;
					}
					bool flag = text.EndsWith("_config", StringComparison.OrdinalIgnoreCase);
					BankHeroStat bankHeroStat;
					if (!dictionary.TryGetValue(num2, out bankHeroStat))
					{
						bankHeroStat = new BankHeroStat
						{
							HeroId = num2,
							Key = "dbHero" + num2.ToString("00", CultureInfo.InvariantCulture)
						};
						dictionary[num2] = bankHeroStat;
					}
					if (flag)
					{
						foreach (XElement item5 in item.Elements("Key"))
						{
							string text5 = (string)item5.Attribute("name") ?? "";
							bankHeroStat.ConfigCount++;
							BankPreset bankPreset = new BankPreset
							{
								Key = text5,
								Raw = ReadString(item5),
								HeroId = num2
							};
							BankPreset.Parse(bankPreset);
							bankHeroStat.Configs.Add(bankPreset);
							if (string.CompareOrdinal(text5, bankHeroStat.LastConfigAt) > 0)
							{
								bankHeroStat.LastConfigAt = text5;
								bankHeroStat.ConfigRaw = bankPreset.Raw;
							}
						}
						bankHeroStat.Configs.Sort((BankPreset a, BankPreset b) => string.CompareOrdinal(b.Key, a.Key));
						ParsePoints(bankHeroStat);
						continue;
					}
					foreach (XElement item6 in item.Elements("Key"))
					{
						string text6 = (string)item6.Attribute("name") ?? "";
						int num3 = ReadInt(item6);
						switch (text6)
						{
						case "total":
							bankHeroStat.Total = num3;
							break;
						case "win":
							bankHeroStat.Win = num3;
							break;
						case "lose":
							bankHeroStat.Lose = num3;
							break;
						case "leave":
							bankHeroStat.Leave = num3;
							break;
						}
					}
				}
			}
			bankProfile.Heroes.AddRange(dictionary.Values);
			bankProfile.Heroes.Sort((BankHeroStat a, BankHeroStat b) => (b.Total != a.Total) ? (b.Total - a.Total) : ((b.Win != a.Win) ? (b.Win - a.Win) : a.HeroId.CompareTo(b.HeroId)));
			LastError = "";
			return bankProfile;
		}
		catch (Exception ex)
		{
			LastError = ex.Message;
			return null;
		}
	}

	/// <summary>本机所有银行档案，当前账号优先、其次按修改时间倒序。</summary>
	public static List<BankProfile> LoadAll()
	{
		List<BankProfile> list = new List<BankProfile>();
		string myHandle = TouhouApi.MyHandle;
		foreach (string item in FindBanks())
		{
			BankProfile bankProfile = Load(item);
			if (bankProfile != null)
			{
				if (myHandle.Length > 0 && string.Equals(bankProfile.ProfileId, myHandle, StringComparison.OrdinalIgnoreCase))
				{
					bankProfile.IsCurrentAccount = true;
				}
				list.Add(bankProfile);
			}
		}
		list.Sort(delegate(BankProfile a, BankProfile b)
		{
			if (a.IsCurrentAccount != b.IsCurrentAccount)
			{
				return a.IsCurrentAccount ? (-1) : 1;
			}
			return b.ModifiedAt.CompareTo(a.ModifiedAt);
		});
		return list;
	}

	/// <summary>取最合适的一份：优先当前账号，否则取最近修改的那份。</summary>
	public static BankProfile LoadBest()
	{
		List<BankProfile> list = LoadAll();
		if (list.Count <= 0)
		{
			return null;
		}
		return list[0];
	}

	private static XElement ValueOf(XElement key)
	{
		if (key == null)
		{
			return null;
		}
		return key.Element("Value");
	}

	private static int ReadInt(XElement key)
	{
		XElement xElement = ValueOf(key);
		if (xElement == null)
		{
			return 0;
		}
		XAttribute xAttribute = xElement.Attribute("int");
		if (xAttribute != null)
		{
			int result;
			if (int.TryParse(xAttribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
			{
				return result;
			}
			return 0;
		}
		XAttribute xAttribute2 = xElement.Attribute("fixed");
		if (xAttribute2 != null)
		{
			double result2;
			if (double.TryParse(xAttribute2.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out result2))
			{
				return (int)Math.Round(result2);
			}
			return 0;
		}
		XAttribute xAttribute3 = xElement.Attribute("flag");
		if (xAttribute3 != null)
		{
			int result3;
			if (int.TryParse(xAttribute3.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result3))
			{
				return result3;
			}
			return 0;
		}
		if (xElement.Attribute("string") != null)
		{
			int result4;
			if (int.TryParse(xElement.Attribute("string").Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result4))
			{
				return result4;
			}
			return 0;
		}
		return 0;
	}

	private static long ReadLong(XElement key)
	{
		XElement xElement = ValueOf(key);
		if (xElement == null)
		{
			return 0L;
		}
		XAttribute xAttribute = xElement.Attribute("int");
		string text = ((xAttribute != null) ? xAttribute.Value : null);
		if (text == null)
		{
			XAttribute xAttribute2 = xElement.Attribute("string");
			if (xAttribute2 != null)
			{
				text = xAttribute2.Value;
			}
		}
		long result;
		if (text != null && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
		{
			return result;
		}
		return 0L;
	}

	/// <summary>把 dbHeroNN_config 的值串拆成数值数组 + 方案名。</summary>
	private static void ParsePoints(BankHeroStat stat)
	{
		stat.Points = new int[0];
		stat.ConfigName = "";
		stat.PointSum = 0;
		if (string.IsNullOrEmpty(stat.ConfigRaw))
		{
			return;
		}
		string[] array = stat.ConfigRaw.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		List<int> list = new List<int>(array.Length);
		List<string> list2 = new List<string>();
		int num = 0;
		foreach (string text in array)
		{
			int num2;
			if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out num2))
			{
				list.Add(num2);
				num += num2;
			}
			else
			{
				list2.Add(text);
			}
		}
		stat.Points = list.ToArray();
		stat.ConfigName = string.Join(" ", list2);
		stat.PointSum = num;
	}

	/// <summary>
	/// 往 touhou.SC2Bank 写一条加点方案：定位（或新建）dbHeroNN_config 段与指定时间戳的 Key。
	/// 写之前会先备份一份 .sctb.bak。注意：游戏运行中改银行会被游戏退出时回写覆盖，务必先关游戏。
	/// </summary>
	public static bool SavePreset(string bankPath, int heroId, string keyName, int[] points, string name, out string error)
	{
		error = "";
		try
		{
			if (string.IsNullOrEmpty(bankPath) || !File.Exists(bankPath))
			{
				error = "存档文件不存在";
				return false;
			}
			if (heroId <= 0 || string.IsNullOrEmpty(keyName))
			{
				error = "英雄或键名不合法";
				return false;
			}
			File.Copy(bankPath, bankPath + ".sctb.bak", true);
			XDocument xDocument = XDocument.Load(bankPath, LoadOptions.PreserveWhitespace);
			XElement xElement = xDocument.Root;
			if (xElement == null)
			{
				error = "XML 根节点为空";
				return false;
			}
			string sectionName = "dbHero" + heroId.ToString("00", CultureInfo.InvariantCulture) + "_config";
			XElement section = null;
			foreach (XElement item in xElement.Elements("Section"))
			{
				if ((string?)item.Attribute("name") == sectionName)
				{
					section = item;
					break;
				}
			}
			if (section == null)
			{
				section = new XElement("Section", new XAttribute("name", sectionName));
				xElement.Add(section);
			}
			XElement key = null;
			foreach (XElement item2 in section.Elements("Key"))
			{
				if ((string?)item2.Attribute("name") == keyName)
				{
					key = item2;
					break;
				}
			}
			if (key == null)
			{
				key = new XElement("Key", new XAttribute("name", keyName));
				section.Add(key);
			}
			string text = BankPreset.Build(points, name);
			XElement val = key.Element("Value");
			if (val == null)
			{
				val = new XElement("Value");
				key.Add(val);
			}
			val.SetAttributeValue("string", text);
			xDocument.Save(bankPath);
			LastError = "";
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			LastError = ex.Message;
			return false;
		}
	}

	/// <summary>删掉一条加点方案。</summary>
	public static bool DeletePreset(string bankPath, int heroId, string keyName, out string error)
	{
		error = "";
		try
		{
			if (string.IsNullOrEmpty(bankPath) || !File.Exists(bankPath))
			{
				error = "存档文件不存在";
				return false;
			}
			File.Copy(bankPath, bankPath + ".sctb.bak", true);
			XDocument xDocument = XDocument.Load(bankPath, LoadOptions.PreserveWhitespace);
			XElement xElement = xDocument.Root;
			if (xElement == null)
			{
				error = "XML 根节点为空";
				return false;
			}
			string sectionName = "dbHero" + heroId.ToString("00", CultureInfo.InvariantCulture) + "_config";
			foreach (XElement item in xElement.Elements("Section"))
			{
				if ((string?)item.Attribute("name") != sectionName)
				{
					continue;
				}
				foreach (XElement item2 in item.Elements("Key").ToList())
				{
					if ((string?)item2.Attribute("name") == keyName)
					{
						item2.Remove();
					}
				}
			}
			xDocument.Save(bankPath);
			LastError = "";
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			LastError = ex.Message;
			return false;
		}
	}

	private static string ReadString(XElement key)
	{
		XElement xElement = ValueOf(key);
		if (xElement == null)
		{
			return "";
		}
		XAttribute xAttribute = xElement.Attribute("string");
		if (xAttribute != null)
		{
			return xAttribute.Value;
		}
		XAttribute xAttribute2 = xElement.Attribute("int");
		if (xAttribute2 != null)
		{
			return xAttribute2.Value;
		}
		return "";
	}
}
