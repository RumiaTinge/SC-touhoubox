using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Media;
using Nmpq;

namespace Sc2Toolbox;

/// <summary>一条聊天记录（悬浮窗用，WPF 绑定必须是属性）。</summary>
public sealed class ChatLine
{
	/// <summary>在录像里的字节位置，用来判断新旧。</summary>
	public long Offset { get; set; }

	/// <summary>形如 “12:34” 的时间（没有帧号时为 “—”）。</summary>
	public string Time { get; set; } = "—";

	/// <summary>发言者（拿不到时为 “玩家”）。</summary>
	public string Who { get; set; } = "玩家";

	/// <summary>正文。</summary>
	public string Text { get; set; } = "";

	/// <summary>悬浮窗里显示的一行（没有表情时用）。</summary>
	public string Display => string.IsNullOrEmpty(Who) ? Text : Who + "：" + Text;

	public Brush Color { get; set; } = Brushes.White;

	/// <summary>悬浮窗里实际渲染的控件（文字 + 内联表情图），由 OverlayWindow 构建。</summary>
	public System.Windows.FrameworkElement Visual { get; set; }

	/// <summary>这条是从 SC2 进程内存实时读到的（对局进行中），不是从录像读的。</summary>
	public bool Live { get; set; }

	/// <summary>内存里那条原始标记（去重与清除时用）。</summary>
	public string Raw { get; set; } = "";
}

/// <summary>
/// 从 SC2 录像里读聊天记录。
///
/// 原理：录像的 `replay.message.events` 里每条聊天都是
///     [比特打包头][0x02][1 字节 UTF-8 长度][正文]
/// 正文是字节对齐的，所以只要扫到「0x02 + 长度 + 合法 UTF-8」就能把整局聊天还原出来。
/// 这一点是实测确认的（一局 120+ 条，长度前缀和字节数完全吻合）。
/// </summary>
public static class TouhouChat
{
	private static readonly UTF8Encoding Strict = new UTF8Encoding(false, true);

	/// <summary>录像里的一个玩家（聊天说话人靠 userid 下标对应）。</summary>
	public sealed class ChatPlayer
	{
		public int Uid { get; set; }
		public string Name { get; set; }
		public Color Color { get; set; } = Colors.White;
	}

	/// <summary>
	/// 扫 event 字节流，抽出所有聊天。
	///
	/// 说话人：`0x02` 前那一个字节的低 5 位就是发言者的 userid（= replay.details 玩家表的下标）。
	/// 这是实测确认的：单人录像里本人发的 5 条全是 0，10 人局里解出来的 uid 全部 ≤ 9 且与名单吻合。
	/// 前面的时间戳仍是变长比特打包，暂不解析，所以没有精确时刻。
	/// </summary>
	public static List<ChatLine> ParseEvents(byte[] b, ChatPlayer[] players = null)
	{
		List<ChatLine> list = new List<ChatLine>();
		if (b == null || b.Length < 3)
		{
			return list;
		}
		for (int i = 0; i + 2 <= b.Length; i++)
		{
			if (b[i] != 0x02)
			{
				continue;
			}
			int len = b[i + 1];
			if (len == 0 || i + 2 + len > b.Length)
			{
				continue;
			}
			string text;
			try
			{
				text = Strict.GetString(b, i + 2, len);
			}
			catch
			{
				continue;
			}
			if (text.Length == 0)
			{
				continue;
			}
			bool clean = true;
			foreach (char c in text)
			{
				if (char.IsControl(c))
				{
					clean = false;
					break;
				}
			}
			if (!clean)
			{
				continue;
			}
			int uid = (i >= 1) ? (b[i - 1] & 0x1F) : -1;
			string who = "玩家";
			Color col = Colors.White;
			if (players != null && uid >= 0 && uid < players.Length && players[uid] != null)
			{
				who = players[uid].Name;
				col = players[uid].Color;
			}
			else if (uid > 0)
			{
				who = "玩家" + uid;
			}
			list.Add(new ChatLine
			{
				Offset = i,
				Time = "—",
				Who = who,
				Color = new SolidColorBrush(col),
				Text = text
			});
		}
		return list;
	}

	/// <summary>读录像的玩家表（下标 = 聊天里的 userid）。失败返回 null。</summary>
	public static ChatPlayer[] LoadPlayers(IMpqArchive archive)
	{
		try
		{
			System.Collections.IDictionary d =
				Nmpq.Util.Starcraft2SerializedData.ReadSerializedData(archive, "replay.details", true) as System.Collections.IDictionary;
			if (d == null)
			{
				return null;
			}
			System.Array pl = d[0L] as System.Array;
			if (pl == null)
			{
				return null;
			}
			ChatPlayer[] list = new ChatPlayer[pl.Length];
			for (int i = 0; i < pl.Length; i++)
			{
				System.Collections.IDictionary pd = pl.GetValue(i) as System.Collections.IDictionary;
				string n = (pd == null) ? "" : CleanName(Convert.ToString(pd[0L]));
				ChatPlayer p = new ChatPlayer
				{
					Uid = i,
					Name = string.IsNullOrEmpty(n) ? ("玩家" + i) : n
				};
				try
				{
					System.Array c = (pd == null) ? null : (pd[3L] as System.Array);
					if (c != null && c.Length >= 4)
					{
						p.Color = Color.FromRgb(
							Convert.ToByte(c.GetValue(1)),
							Convert.ToByte(c.GetValue(2)),
							Convert.ToByte(c.GetValue(3)));
					}
				}
				catch
				{
				}
				list[i] = p;
			}
			return list;
		}
		catch (Exception ex)
		{
			L.W("聊天：读玩家表失败 → " + ex.GetType().Name + ": " + ex.Message);
			return null;
		}
	}

	/// <summary>把 “&amp;lt;战队&amp;gt;&amp;lt;sp/&amp;gt;名字” 这种名字清成可显示文本。</summary>
	public static string CleanName(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		s = s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&amp;", "&");
		s = Regex.Replace(s, "<sp\\s*/?>", " ", RegexOptions.IgnoreCase);
		s = Regex.Replace(s, "<[^>]*>", "");
		return s.Trim();
	}

	/// <summary>读一个录像文件的聊天。失败返回空表。</summary>
	public static List<ChatLine> FromReplay(string path)
	{
		try
		{
			IMpqArchive archive = MpqArchive.Open(path);
			byte[] b = archive.ReadFile("replay.message.events");
			ChatPlayer[] players = LoadPlayers(archive);
			List<ChatLine> list = ParseEvents(b, players);
			L.W("聊天：读 " + Path.GetFileName(path) + " → events=" + ((b == null) ? -1 : b.Length) + " 玩家=" + ((players == null) ? 0 : players.Length) + " 条数=" + list.Count);
			return list;
		}
		catch (Exception ex)
		{
			L.W("聊天：读录像失败 " + Path.GetFileName(path) + " → " + ex.GetType().Name + ": " + ex.Message);
			return new List<ChatLine>();
		}
	}

	/// <summary>录像目录（当前账号下）。找不到返回 null。</summary>
	public static string ReplayDir()
	{
		try
		{
			string root = Path.Combine(TouhouBank.Sc2Root(), "Accounts");
			if (!Directory.Exists(root))
			{
				return null;
			}
			string best = null;
			DateTime bestTime = DateTime.MinValue;
			foreach (string acc in Directory.GetDirectories(root))
			{
				foreach (string profile in Directory.GetDirectories(acc))
				{
					string dir = Path.Combine(profile, "Replays", "Multiplayer");
					if (!Directory.Exists(dir))
					{
						continue;
					}
					DateTime t = Directory.GetLastWriteTime(dir);
					if (t > bestTime)
					{
						bestTime = t;
						best = dir;
					}
				}
			}
			return best;
		}
		catch
		{
			return null;
		}
	}

	// ================= 实时聊天：读 SC2 进程内存 =================
	//
	// 录像只在对局结束时才写盘，所以「读录像」永远只能看到上一局。
	// 对局进行中，SC2 把每条聊天按显示标记放在内存里，形如：
	//     [所有玩家] <c val="00BB00"><w>玩家甲</w></c>: 一句话
	// 实测（pid 22928 一局进行中）全地址空间扫 2.2 GB 只要 5 秒，稳定命中。
	// 因此这里按 `</w></c>: ` 全盘搜，向前找最近的 `[`（频道），向后取到控制字符。

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(int access, bool inherit, int pid);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(IntPtr h);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr VirtualQueryEx(IntPtr h, IntPtr addr, out MemBasicInfo m, IntPtr len);

	[StructLayout(LayoutKind.Sequential)]
	private struct MemBasicInfo
	{
		public IntPtr Base;
		public IntPtr AllocBase;
		public uint AllocProtect;
		public IntPtr Size;
		public uint State;
		public uint Protect;
		public uint Type;
	}

	private const int MemChunk = 8 * 1024 * 1024;
	private const int MemOverlap = 2048;

	// 普通聊天是 "</w></c>: "，但表情消息没有 </w>（见下面 LiveRx2），
	// 所以只认 "</c>: " —— 它是前者的子串，两种写法都能扫到。
	private static readonly byte[] ChatNeedle = Encoding.UTF8.GetBytes("</c>: ");
	// 真·聊天行的形状（与「聊天历史缓冲」里那些拼接串区分开：颜色标记必须紧跟频道）：
	//   [所有玩家] <c val="00BB00"><w>玩家甲</w></c>: 正文
	//   [所有玩家] <c val="FFFF00"><w>&lt;大力出奇迹&gt;<sp/>幾把刀</w></c>: 正文
	// 后者带战队名：名字里是 HTML 转义过的尖括号 + <sp/> 分隔。
	private static readonly Regex LiveRx = new Regex(
		"^\\[(?<ch>[^\\]]*)\\]\\s*<c val=\"(?<col>[0-9A-Fa-f]{6})\"><w>(?<who>.*?)</w></c>:\\s?(?<msg>.*)$",
		RegexOptions.Compiled);

	// 表情消息走的是另一套更旧的写法：名字外面没有 <w></w>，直接包在 <c> 里。
	//   [盟友] <c val="E55BB0"><喵呜喵呜> 樱羽艾玛</c>: <s val="No_Border"><img path="emoji/636.png"/></s>
	// 之前的 needle 是 "</w></c>: "，这类消息一条都匹配不到，所以表情全丢了。
	private static readonly Regex LiveRx2 = new Regex(
		"^\\[(?<ch>[^\\]]*)\\]\\s*<c val=\"(?<col>[0-9A-Fa-f]{6})\">(?<who>[^<]*(?:<(?!/c>)[^>]*>[^<]*)*?)</c>:\\s?(?<msg>.*)$",
		RegexOptions.Compiled);

	/// <summary>累积的实时聊天（按首次出现的先后）。</summary>
	private static readonly List<ChatLine> LiveLines = new List<ChatLine>();

	/// <summary>已经见过的原始行，用来去重。</summary>
	private static readonly HashSet<string> LiveRaw = new HashSet<string>(StringComparer.Ordinal);

	private static readonly object LiveLock = new object();
	private static DateTime LiveAt = DateTime.MinValue;
	private static DateTime LiveLastHit = DateTime.MinValue;
	private static int LiveEmptyPolls;
	private static bool LiveBusy;

	/// <summary>正在进行的这一局的聊天（可能为空）。不会阻塞 UI。</summary>
	public static List<ChatLine> SnapshotLive()
	{
		lock (LiveLock)
		{
			return new List<ChatLine>(LiveLines);
		}
	}

	/// <summary>本局第一条聊天记到的时刻。</summary>
	public static DateTime LiveWhen
	{
		get
		{
			lock (LiveLock)
			{
				return LiveLastHit;
			}
		}
	}

	/// <summary>当前有几个 SC2 进程在跑（没在跑就说明不在局内）。</summary>
	public static int Sc2Pid()
	{
		foreach (string nm in new string[] { "SC2_x64", "SC2" })
		{
			try
			{
				Process[] ps = Process.GetProcessesByName(nm);
				if (ps.Length > 0)
				{
					int id = ps[0].Id;
					foreach (Process p in ps)
					{
						p.Dispose();
					}
					return id;
				}
			}
			catch
			{
			}
		}
		return 0;
	}

	/// <summary>
	/// 若距上次扫描已超过 minGap 秒，就在后台线程扫一次 SC2 内存，结果并入 LiveLines。
	/// 这个方法是给 UI 定时器调的，绝不能阻塞。
	/// </summary>
	public static void KickLive(int minGap)
	{
		lock (LiveLock)
		{
			if (LiveBusy || (DateTime.UtcNow - LiveAt).TotalSeconds < minGap)
			{
				return;
			}
			LiveBusy = true;
			LiveAt = DateTime.UtcNow;
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				ScanLiveOnce();
			}
			catch (Exception ex)
			{
				L.W("实时聊天：扫描失败 " + ex.GetType().Name + ": " + ex.Message);
			}
			finally
			{
				lock (LiveLock)
				{
					LiveBusy = false;
				}
			}
		});
	}

	/// <summary>清空累积的实时聊天（换局时用）。</summary>
	public static void ResetLive()
	{
		lock (LiveLock)
		{
			LiveLines.Clear();
			LiveRaw.Clear();
			LiveLastHit = DateTime.MinValue;
			LiveEmptyPolls = 0;
		}
	}

	private static void ScanLiveOnce()
	{
		int pid = Sc2Pid();
		if (pid == 0)
		{
			lock (LiveLock)
			{
				LiveEmptyPolls++;
				if (LiveEmptyPolls >= 3)
				{
					LiveLines.Clear();
					LiveRaw.Clear();
					LiveLastHit = DateTime.MinValue;
				}
			}
			return;
		}
		List<string> raws = ScanChat(pid);
		lock (LiveLock)
		{
			if (raws.Count == 0)
			{
				LiveEmptyPolls++;
				// 连着几次都读不到，认为已经出局，清空累积，避免下一局接在上一局后面
				if (LiveEmptyPolls >= 3)
				{
					LiveLines.Clear();
					LiveRaw.Clear();
					LiveLastHit = DateTime.MinValue;
				}
				return;
			}
			LiveEmptyPolls = 0;
			// 整批都不认识 → 说明聊天缓冲换过一轮（新的一局），先清掉旧的
			bool any = false;
			foreach (string r in raws)
			{
				if (LiveRaw.Contains(r))
				{
					any = true;
					break;
				}
			}
			if (!any && LiveRaw.Count > 0)
			{
				LiveLines.Clear();
				LiveRaw.Clear();
			}
			bool added = false;
			foreach (string r in raws)
			{
				if (!LiveRaw.Add(r))
				{
					continue;
				}
				ChatLine cl = ParseLive(r);
				if (cl == null)
				{
					continue;
				}
				// 内存里是按地址扫出来的，顺序不等于说话顺序。
				// 用「第一次看到它的时刻」当时间戳，加完再按它重排。
				cl.Offset = DateTime.Now.Ticks;
				LiveLines.Add(cl);
				added = true;
			}
			if (added || LiveLastHit == DateTime.MinValue)
			{
				LiveLastHit = DateTime.Now;
				LiveLines.Sort(delegate(ChatLine a, ChatLine b)
				{
					return a.Offset.CompareTo(b.Offset);
				});
			}
			while (LiveLines.Count > 400)
			{
				LiveRaw.Remove(RawOf(LiveLines[0]));
				LiveLines.RemoveAt(0);
			}
		}
	}

	/// <summary>把一条内存里的聊天标记解析成 ChatLine。分不开就返回 null。</summary>
	private static ChatLine ParseLive(string raw)
	{
		Match m = LiveRx.Match(raw);
		if (!m.Success)
		{
			m = LiveRx2.Match(raw);
		}
		if (!m.Success)
		{
			return null;
		}
		string who = StripTags(m.Groups["who"].Value);
		string msg = m.Groups["msg"].Value;
		if (who.Length == 0)
		{
			who = "玩家";
		}
		Color col = Colors.White;
		try
		{
			string h = m.Groups["col"].Value;
			if (h.Length == 6)
			{
				col = Color.FromRgb(Convert.ToByte(h.Substring(0, 2), 16), Convert.ToByte(h.Substring(2, 2), 16), Convert.ToByte(h.Substring(4, 2), 16));
			}
		}
		catch
		{
		}
		ChatLine cl = new ChatLine();
		cl.Offset = 0L;
		cl.Time = m.Groups["ch"].Value;
		cl.Who = who;
		cl.Text = msg;
		// 这条是在后台线程上建的，画刷必须 Freeze 才能被 UI 线程用
		SolidColorBrush br = new SolidColorBrush(col);
		br.Freeze();
		cl.Color = br;
		cl.Live = true;
		cl.Raw = raw;
		return cl;
	}

	private static string RawOf(ChatLine c)
	{
		return c.Raw;
	}

	private static string StripTags(string s)
	{
		string t = Regex.Replace(s, "<sp\\s*/?>", " ");
		t = Regex.Replace(t, "<[^>]*>", "");
		t = t.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");
		return Regex.Replace(t, "\\s+", " ").Trim();
	}

	/// <summary>扫一遍进程内存，返回所有形如「[频道] …&lt;/w&gt;&lt;/c&gt;: 正文」的原始行（按地址排序）。</summary>
	private static List<string> ScanChat(int pid)
	{
		List<string> found = new List<string>();
		IntPtr h = OpenProcess(0x0410, false, pid);
		if (h == IntPtr.Zero)
		{
			L.W("实时聊天：OpenProcess 失败 err=" + Marshal.GetLastWin32Error());
			return found;
		}
		byte[] buffer = new byte[MemChunk + MemOverlap + 4096];
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		List<KeyValuePair<long, string>> hits = new List<KeyValuePair<long, string>>();
		int mbiSize = Marshal.SizeOf(typeof(MemBasicInfo));
		IntPtr addr = IntPtr.Zero;
		MemBasicInfo m;
		try
		{
			while (VirtualQueryEx(h, addr, out m, (IntPtr)mbiSize) != IntPtr.Zero)
			{
				long size = m.Size.ToInt64();
				bool ok = m.State == 0x1000 && (m.Protect & 0x100) == 0 && (m.Protect & 0x01) == 0 && (m.Protect & 0x04) != 0 && size > 0;
				if (ok)
				{
					long off = 0;
					while (off < size)
					{
						int want = (int)Math.Min((long)MemChunk, size - off);
						IntPtr got;
						if (!ReadProcessMemory(h, new IntPtr(m.Base.ToInt64() + off), buffer, (IntPtr)want, out got))
						{
							break;
						}
						int have = got.ToInt32();
						if (have <= 0)
						{
							break;
						}
						long chunkBase = m.Base.ToInt64() + off;
						int limit = have - ChatNeedle.Length;
						for (int i = 0; i <= limit; i++)
						{
							if (buffer[i] != ChatNeedle[0])
							{
								continue;
							}
							bool hit = true;
							for (int j = 1; j < ChatNeedle.Length; j++)
							{
								if (buffer[i + j] != ChatNeedle[j])
								{
									hit = false;
									break;
								}
							}
							if (!hit)
							{
								continue;
							}
							int a = i - 1;
							int guard = 0;
							while (a >= 0 && guard < 400 && buffer[a] != (byte)'[')
							{
								a--;
								guard++;
							}
							if (a < 0 || buffer[a] != (byte)'[')
							{
								continue;
							}
							int e = i + ChatNeedle.Length;
							while (e < have && e - i < 400)
							{
								byte c = buffer[e];
								if (c < 0x20 || c == 0x7F)
								{
									break;
								}
								e++;
							}
							string s;
							try
							{
								s = Strict.GetString(buffer, a, e - a);
							}
							catch
							{
								continue;
							}
							if (s.Length < 8 || s.Length > 400 || s.IndexOf('<') < 0)
							{
								continue;
							}
							if (!seen.Add(s))
							{
								continue;
							}
							hits.Add(new KeyValuePair<long, string>(chunkBase + i, s));
						}
						if (have < want || off + want >= size)
						{
							break;
						}
						off += want - MemOverlap;
					}
				}
				long next = m.Base.ToInt64() + size;
				if (next <= addr.ToInt64())
				{
					next = addr.ToInt64() + 0x1000;
				}
				addr = new IntPtr(next);
				if (addr.ToInt64() > 0x7FFFFFFFFFFF)
				{
					break;
				}
			}
		}
		finally
		{
			CloseHandle(h);
		}
		hits.Sort(delegate (KeyValuePair<long, string> x, KeyValuePair<long, string> y)
		{
			return x.Key.CompareTo(y.Key);
		});
		foreach (KeyValuePair<long, string> kv in hits)
		{
			found.Add(kv.Value);
		}
		return found;
	}

	/// <summary>最近一次读到的「本局」是哪个录像（没有时为空串）。</summary>
	public static string CurrentGame = "";

	/// <summary>当前这批聊天是从 SC2 内存实时读的（而不是从录像读的）。</summary>
	public static bool LiveSource;

	/// <summary>那个录像的写盘时间（约等于本局结束的时刻）。</summary>
	public static DateTime CurrentWhen = DateTime.MinValue;

	/// <summary>
	/// 只读「本局」的聊天：取录像目录里最新的一个东方月灵传录像。
	///
	/// 和 LoadRecent 的区别：**不往回找**。哪怕这一局一条聊天都没有，也只显示空，
	/// 不会把好几天前那局的聊天翻出来冒充本局。
	/// </summary>
	public static List<ChatLine> LoadCurrent(int maxLines)
	{
		List<ChatLine> list = new List<ChatLine>();
		CurrentGame = "";
		CurrentWhen = DateTime.MinValue;

		// 先看能不能实时读（对局进行中）。SC2 在跑就每隔几秒扫一次内存。
		if (Sc2Pid() != 0)
		{
			KickLive(6);
			List<ChatLine> live = SnapshotLive();
			if (live.Count > 0)
			{
				LiveSource = true;
				CurrentGame = "（进行中）";
				CurrentWhen = LiveWhen;
				if (live.Count > maxLines)
				{
					live = live.GetRange(live.Count - maxLines, maxLines);
				}
				return live;
			}
			LiveSource = true;
			CurrentGame = "（进行中）";
			CurrentWhen = LiveWhen == DateTime.MinValue ? DateTime.Now : LiveWhen;
			return list;
		}
		LiveSource = false;

		string dir = ReplayDir();
		if (dir == null)
		{
			L.W("聊天：录像目录为空");
			return list;
		}
		try
		{
			string[] files = Directory.GetFiles(dir, "*.SC2Replay");
			Array.Sort(files, (string a, string c) => File.GetLastWriteTime(a).CompareTo(File.GetLastWriteTime(c)));
			string text = null;
			for (int i = files.Length - 1; i >= 0; i--)
			{
				if (Path.GetFileName(files[i]).StartsWith("东方月灵传"))
				{
					text = files[i];
					break;
				}
			}
			if (text == null && files.Length != 0)
			{
				text = files[files.Length - 1];
			}
			if (text == null)
			{
				return list;
			}
			CurrentGame = Path.GetFileNameWithoutExtension(text);
			CurrentWhen = File.GetLastWriteTime(text);
			L.W("聊天：本局 = " + CurrentGame + " @" + CurrentWhen.ToString("HH:mm:ss"));
			list = FromReplay(text);
		}
		catch (Exception ex)
		{
			L.W("聊天：载入失败 " + ex.GetType().Name + ": " + ex.Message);
		}
		if (list.Count > maxLines)
		{
			list = list.GetRange(list.Count - maxLines, maxLines);
		}
		return list;
	}

	/// <summary>取最近若干局录像里的聊天，按时间正序拼起来。</summary>
	public static List<ChatLine> LoadRecent(int replays, int maxLines)
	{
		List<ChatLine> all = new List<ChatLine>();
		string dir = ReplayDir();
		L.W("聊天：录像目录 = " + (dir ?? "(null)"));
		if (dir == null)
		{
			return all;
		}
		try
		{
			string[] files = Directory.GetFiles(dir, "*.SC2Replay");
			L.W("聊天：目录里 " + files.Length + " 个录像");
			Array.Sort(files, (string a, string c) => File.GetLastWriteTime(a).CompareTo(File.GetLastWriteTime(c)));
			int taken = 0;
			for (int i = files.Length - 1; i >= 0 && taken < replays; i--)
			{
				List<ChatLine> one = FromReplay(files[i]);
				if (one.Count == 0)
				{
					continue;
				}
				taken++;
				all.AddRange(one);
			}
		}
		catch (Exception ex)
		{
			L.W("聊天：载入失败 " + ex.GetType().Name + ": " + ex.Message);
		}
		if (all.Count > maxLines)
		{
			all = all.GetRange(all.Count - maxLines, maxLines);
		}
		return all;
	}

	// ───────────────────────── 表情包 ─────────────────────────
	//
	// 站点（aslucky.club）的表情包最终是被写成 SC2 的富文本塞进聊天的，实测从内存里抠出来的原文是：
	//     <s val="No_Border"><img path="emoji/702.png"/></s>
	// 有时是反斜杠写法 <img path="emoji\632.png"/>，外面也可能包别的 <s val="…">。
	// 图片 id 和站点 emoji 列表一致（592~830），文件名就是 <id>.png。

	/// <summary>聊天正文切成「文字 / 表情」片段。</summary>
	public sealed class ChatPart
	{
		/// <summary>文字片段（表情片段时为 null）。</summary>
		public string Text { get; set; }

		/// <summary>表情 id（文字片段时为 0）。</summary>
		public int EmojiId { get; set; }
	}

	private static readonly Regex EmojiTagRx = new Regex(
		"<img\\s+path\\s*=\\s*[\"']?emoji[\\\\/](\\d+)\\.png[\"']?[^>]*>",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex EmojiBareRx = new Regex(
		"emoji[\\\\/](\\d+)\\.png",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex StyleTagRx = new Regex(
		"</?s(\\s+[^>]*)?>",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	/// <summary>颜色壳：<c val="B4141E">…</c>。</summary>
	private static readonly Regex ColorTagRx = new Regex(
		"</?c(\\s+[^>]*)?>",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	/// <summary>其余富文本壳（n / img 之外的成对标签）。</summary>
	private static readonly Regex MiscTagRx = new Regex(
		"</?(n|b|i|u|font|span|img)(\\s+[^>]*)?/?>",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	/// <summary>正文里是否带表情包。</summary>
	public static bool HasEmoji(string text)
	{
		return !string.IsNullOrEmpty(text) && EmojiBareRx.IsMatch(text);
	}

	/// <summary>把一行正文切成文字与表情片段（顺序保持）。</summary>
	public static List<ChatPart> Split(string text)
	{
		List<ChatPart> parts = new List<ChatPart>();
		if (string.IsNullOrEmpty(text))
		{
			return parts;
		}
		MatchCollection ms = EmojiTagRx.Matches(text);
		if (ms.Count == 0)
		{
			ms = EmojiBareRx.Matches(text);
		}
		if (ms.Count == 0)
		{
			string only = Strip(text);
			if (only.Length > 0)
			{
				parts.Add(new ChatPart { Text = only });
			}
			return parts;
		}
		int pos = 0;
		foreach (Match m in ms)
		{
			if (m.Index > pos)
			{
				string seg = Strip(text.Substring(pos, m.Index - pos));
				if (seg.Length > 0)
				{
					parts.Add(new ChatPart { Text = seg });
				}
			}
			int id;
			if (int.TryParse(m.Groups[1].Value, out id) && id > 0)
			{
				parts.Add(new ChatPart { EmojiId = id });
			}
			pos = m.Index + m.Length;
		}
		if (pos < text.Length)
		{
			string tail = Strip(text.Substring(pos));
			if (tail.Length > 0)
			{
				parts.Add(new ChatPart { Text = tail });
			}
		}
		return parts;
	}

	/// <summary>去掉 &lt;s val="…"&gt; 这类样式壳。</summary>
	private static string Strip(string s)
	{
		s = StyleTagRx.Replace(s, "");
		s = ColorTagRx.Replace(s, "");
		s = MiscTagRx.Replace(s, "");
		return s.Trim();
	}

	/// <summary>表情图片的本地缓存目录。</summary>
	public static string EmojiDir()
	{
		// 优先放在程序目录的 art\emoji 下，跟技能图一个地方，方便整包发出去；
		// 程序目录不可写（比如装在 Program Files）才退回 %LOCALAPPDATA%。
		try
		{
			string a = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "art", "emoji");
			Directory.CreateDirectory(a);
			string probe = Path.Combine(a, ".w");
			File.WriteAllText(probe, "");
			File.Delete(probe);
			return a;
		}
		catch
		{
		}
		string d = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"SC-touhoubox", "emoji");
		Directory.CreateDirectory(d);
		return d;
	}

	/// <summary>某个表情 id 的本地文件路径（不保证存在）。</summary>
	public static string EmojiFile(int id)
	{
		return Path.Combine(EmojiDir(), id + ".png");
	}

	/// <summary>确保表情图片已缓存；缺失时从站点下载。返回是否可用。</summary>
	public static bool EnsureEmoji(int id)
	{
		string f = EmojiFile(id);
		try
		{
			if (File.Exists(f) && new FileInfo(f).Length > 0)
			{
				return true;
			}
			string url = "https://www.aslucky.club/file/touhou/emoji/" + id + ".png";
			string tmp = f + ".tmp";
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.Timeout = 5000;
			req.ReadWriteTimeout = 5000;
			req.UserAgent = "SC-touhoubox";
			using (WebResponse resp = req.GetResponse())
			using (Stream src = resp.GetResponseStream())
			using (FileStream dst = File.Create(tmp))
			{
				src.CopyTo(dst);
			}
			if (File.Exists(f))
			{
				File.Delete(f);
			}
			File.Move(tmp, f);
			L.W("表情：已缓存 id=" + id + " → " + new FileInfo(f).Length + " B");
			return true;
		}
		catch (Exception ex)
		{
			L.W("表情：下载失败 id=" + id + " → " + ex.GetType().Name + ": " + ex.Message);
			try
			{
				if (File.Exists(f + ".tmp"))
				{
					File.Delete(f + ".tmp");
				}
			}
			catch
			{
			}
			return false;
		}
	}
}
