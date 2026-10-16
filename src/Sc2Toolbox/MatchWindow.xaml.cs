using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace Sc2Toolbox;

public partial class MatchWindow : Window, IComponentConnector
{
	public class MIcon
	{
		public ImageSource Src { get; set; }

		public string Tip { get; set; }

		/// <summary>右下角角标：买了几级 / 用了几次。空字符串表示不显示角标。</summary>
		public string Num { get; set; }
	}

	public class MRow
	{
		public ImageSource Img { get; set; }

		public Brush RowBrush { get; set; }

		public Brush MmrBrush { get; set; }

		public string Name { get; set; }

		public string Sub { get; set; }

		public string Kda { get; set; }

		public string Mmr { get; set; }

		public List<MIcon> Abilities { get; set; }

		public List<MIcon> Spells { get; set; }

		public List<MIcon> Runes { get; set; }

		public List<MIcon> Stats { get; set; }

		public string SeqLabel { get; set; }

		public List<MIcon> Seq { get; set; }
	}

	private TMatch _m;

	private DispatcherTimer _timer;

	private bool _busy;

	private int _pumps;

	private int _sameRounds;

	private int _lastCached = -1;

	private static readonly string[] SlotKeys = new string[5] { "T", "Q", "W", "E", "R" };

	public MatchWindow(TMatch m)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected Obj, but got Unknown
		InitializeComponent();
		_m = m;
		try
		{
			Fill(m);
		}
		catch (Exception ex)
		{
			try
			{
				TxtNote.Text = "这一局的数据没能读出来：" + ex.Message;
			}
			catch
			{
			}
		}
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(4L)
		};
		_timer.Tick += (object? s, EventArgs e) =>
		{
			Pump();
		};
		_timer.Start();
		Pump();
		Closed += (object? s, EventArgs e) =>
		{
			try
			{
				_timer.Stop();
			}
			catch
			{
			}
		};
	}

	private void Pump()
	{
		if (_m == null || _busy)
		{
			return;
		}
		if (++_pumps > 90)
		{
			try
			{
				_timer.Stop();
				return;
			}
			catch
			{
				return;
			}
		}
		_busy = true;
		TMatch buf = _m;
		ThreadPool.QueueUserWorkItem((object? _) =>
		{
			int got = 0;
			try
			{
				got = TouhouApi.PreloadMatchArt(buf, 30000);
			}
			catch
			{
			}
			try
			{
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)(() =>
				{
					try
					{
						int num = TouhouApi.MatchArtUrls(buf).Count((string u) => ImgCache.Cached(u) != null);
						if (num == _lastCached)
						{
							if (++_sameRounds >= 6)
							{
								try
								{
									_timer.Stop();
								}
								catch
								{
								}
							}
						}
						else
						{
							_sameRounds = 0;
							_lastCached = num;
						}
						if (got > 0 || num != _lastCached)
						{
							Fill(buf);
						}
					}
					catch
					{
					}
					finally
					{
						_busy = false;
					}
				}), Array.Empty<object>());
			}
			catch
			{
				_busy = false;
			}
		});
	}

	private static string ModeName(TMatch m)
	{
		switch (m.Mode)
		{
		case 1:
			return "1v1";
		case 2:
			return "2v2";
		case 3:
			return "3v3";
		case 4:
			return "4v4";
		default:
			if (m.Mode <= 0)
			{
				return "对局";
			}
			return "模式 " + m.Mode;
		}
	}

	private static List<MIcon> MakeIcons(IEnumerable<string> urls, string prefix)
	{
		List<MIcon> list = new List<MIcon>();
		int num = 0;
		foreach (string url in urls)
		{
			if (!string.IsNullOrEmpty(url))
			{
				num++;
				list.Add(new MIcon
				{
					Src = ImgCache.Cached(url),
					Tip = prefix + " " + num
				});
			}
		}
		return list;
	}

	private static void BuildPlayerIcons(TMatchPlayer p, MRow row)
	{
		row.Abilities = new List<MIcon>();
		row.Spells = new List<MIcon>();
		row.Runes = new List<MIcon>();
		row.Stats = new List<MIcon>();
		row.Seq = new List<MIcon>();
		row.SeqLabel = "";
		if (p == null || p.HeroId <= 0)
		{
			return;
		}
		int[] array = TouhouApi.Nums(p.AbilitySet);
		for (int i = 0; i < array.Length && i < SlotKeys.Length; i++)
		{
			string text = TouhouApi.AbilityImg(p.HeroId, array[i], i);
			if (text.Length != 0)
			{
				row.Abilities.Add(new MIcon
				{
					Src = ImgCache.Cached(text),
					Tip = "技能 " + SlotKeys[i] + "（id " + array[i] + "）"
				});
			}
		}
		int[] array2 = TouhouApi.Nums(p.Spells);
		for (int j = 0; j < array2.Length; j++)
		{
			string text2 = TouhouApi.SpellImg(j, array2[j]);
			if (text2.Length != 0)
			{
				row.Spells.Add(new MIcon
				{
					Src = ImgCache.Cached(text2),
					Tip = "召唤技能 " + (j + 1) + "（id " + array2[j] + "）"
				});
			}
		}
		int[] array3 = TouhouApi.Nums(p.PrimaryRunes);
		// 同一个符文买多级会在数组里重复出现；按首次出现顺序去重，并把重复次数当角标显示。
		Dictionary<int, MIcon> dictionary = new Dictionary<int, MIcon>();
		List<int> list2 = new List<int>();
		foreach (int num2 in array3)
		{
			string text6 = TouhouApi.RuneImg(p.PrimaryColumn, num2);
			if (text6.Length == 0)
			{
				continue;
			}
			if (dictionary.ContainsKey(num2))
			{
				dictionary[num2].Num = (int.Parse(dictionary[num2].Num) + 1).ToString();
				dictionary[num2].Tip = TouhouRune.Tip(p.PrimaryColumn, num2, "天赋 · ") + " ×" + dictionary[num2].Num;
				continue;
			}
			MIcon mIcon = new MIcon
			{
				Src = ImgCache.Cached(text6),
				Tip = TouhouRune.Tip(p.PrimaryColumn, num2, "天赋 · "),
				Num = "1"
			};
			dictionary[num2] = mIcon;
			list2.Add(num2);
			row.Runes.Add(mIcon);
		}
		foreach (int num3 in list2)
		{
			MIcon mIcon2 = dictionary[num3];
			if (mIcon2.Num == "1")
			{
				mIcon2.Num = "";
			}
			else
			{
				mIcon2.Tip = TouhouRune.Tip(p.PrimaryColumn, num3, "天赋 · ") + " ×" + mIcon2.Num;
			}
		}
		// 站点的 primaryRunes 只给「点了哪几个」，不给各点几级，所以这里通常没有角标。
		// 只有它偶尔带上重复项时才显示数量。
		if (p.SecondaryRune > 0)
		{
			string text4 = TouhouApi.RuneImg(p.SecondaryColumn, p.SecondaryRune);
			if (text4.Length > 0)
			{
				row.Runes.Add(new MIcon
				{
					Src = ImgCache.Cached(text4),
					Tip = TouhouRune.Tip(p.SecondaryColumn, p.SecondaryRune, "天赋 · 副系 ")
				});
			}
		}
		foreach (TouhouApi.StatIcon item in TouhouApi.StatIcons(p))
		{
			row.Stats.Add(new MIcon
			{
				Src = ImgCache.Cached(TouhouApi.StatImg(item.Id)),
				Tip = item.Tip,
				Num = ((item.Num > 1) ? item.Num.ToString() : "")
			});
		}
		// 天赋的提示里也写了「×N」（买了几级），一样抠出来当角标。
		foreach (MIcon mIcon4 in row.Runes)
		{
			Match match2 = Regex.Match(mIcon4.Tip ?? "", "×\\s*(\\d+)");
			if (match2.Success && match2.Groups[1].Value != "1")
			{
				mIcon4.Num = match2.Groups[1].Value;
			}
		}
		int[] array4 = TouhouApi.Nums(p.AbilityUpSeq);
		List<MIcon> list = new List<MIcon>();
		array3 = array4;
		foreach (int num in array3)
		{
			if (num >= 0 && num < array.Length && num < SlotKeys.Length)
			{
				string text5 = TouhouApi.AbilityImg(p.HeroId, array[num], num);
				if (text5.Length != 0)
				{
					list.Add(new MIcon
					{
						Src = ImgCache.Cached(text5),
						Tip = "第 " + (list.Count + 1) + " 级加 " + SlotKeys[num]
					});
				}
			}
		}
		row.Seq = list;
		row.SeqLabel = ((list.Count > 0) ? ("加点顺序（" + list.Count + " 级）") : "");
	}

	private void Fill(TMatch m)
	{
		if (m == null)
		{
			TxtTitle.Text = "没有这一局的数据";
			return;
		}
		bool? win = m.Win;
		string text;
		if (win.HasValue)
		{
			text = (win.Value ? "胜" : "负");
		}
		else
		{
			text = "结果未知";
		}
		Brush foreground;
		if (win == true)
		{
			foreground = (Brush)FindResource("B.Ok");
		}
		else
		{
			foreground = ((win == false) ? ((Brush)FindResource("B.Danger")) : Brushes.Gray);
		}
		string text2 = ((m.When == DateTime.MinValue) ? "时间未知" : m.When.ToString("yyyy-MM-dd HH:mm"));
		TxtTitle.Text = ModeName(m) + "\u3000" + text + "\u3000" + text2;
		TxtTitle.Foreground = FindResource("B.Text") as Brush;
		string text3 = ((m.Team1Wins > 0 || m.Team2Wins > 0) ? ("\u3000比分 " + m.Team1Wins + " : " + m.Team2Wins) : "");
		string text4 = "我：" + ((m.MyHeroId > 0) ? TouhouApi.HeroName(m.MyHeroId) : "英雄未知") + "\u3000KDA " + m.MyKill + " / " + m.MyDeath + " / " + m.MyAssist + (m.IsMvp ? "\u3000★MVP" : "") + ((m.MyMmr > 0) ? ("\u3000MMR " + m.MyMmr + ((m.MyMmrChange != 0) ? ("（" + ((m.MyMmrChange > 0) ? "+" : "") + m.MyMmrChange + "）") : "")) : "") + ((m.MyMmrHero != 0) ? ("\u3000英雄分 " + ((m.MyMmrHero > 0) ? "+" : "") + m.MyMmrHero) : "");
		TxtSub.Text = "对局编号 " + m.GameId + text3 + "\n" + text4;
		TxtSub.Foreground = foreground;
		string myHandle = TouhouApi.MyHandle;
		List<MRow> list = new List<MRow>();
		List<MRow> list2 = new List<MRow>();
		foreach (TMatchPlayer player in m.Players)
		{
			MRow mRow = new MRow();
			mRow.Img = ((player.HeroId > 0) ? ImgCache.Cached(TouhouApi.HeroImg(player.HeroId)) : null);
			mRow.Name = ((player.SelectNum > 0) ? (player.SelectNum + ". ") : "") + player.DisplayName + (string.IsNullOrEmpty(player.ClanTag) ? "" : (" [" + player.ClanTag + "]")) + (player.IsMvp ? "\u3000★MVP" : "") + (string.Equals(player.Handle, myHandle, StringComparison.OrdinalIgnoreCase) ? "\u3000← 我" : "");
			mRow.Sub = ((player.HeroId > 0) ? TouhouApi.HeroName(player.HeroId) : "英雄未知") + ((player.HeroLevel > 0) ? (" · 等级 " + player.HeroLevel) : "") + " · 金钱 " + player.Money + " · 伤害 " + player.Damage + " · 承伤 " + player.Damaged + ((player.Healing > 0) ? (" · 治疗 " + player.Healing) : "");
			mRow.Kda = player.KdaText;
			mRow.Mmr = ((player.Mmr > 0) ? ("MMR " + player.Mmr + ((player.MmrChange != 0) ? ("（" + ((player.MmrChange > 0) ? "+" : "") + player.MmrChange + "）") : "")) : "");
			MRow mRow2 = mRow;
			Brush mmrBrush;
			if (player.MmrChange > 0)
			{
				mmrBrush = (Brush)FindResource("B.Ok");
			}
			else
			{
				mmrBrush = ((player.MmrChange < 0) ? ((Brush)FindResource("B.Danger")) : ((Brush)FindResource("B.Muted")));
			}
			mRow2.MmrBrush = mmrBrush;
			mRow.RowBrush = (string.Equals(player.Handle, myHandle, StringComparison.OrdinalIgnoreCase) ? ((Brush)FindResource("B.Card2")) : Brushes.Transparent);
			MRow mRow3 = mRow;
			BuildPlayerIcons(player, mRow3);
			if (player.Team == 2)
			{
				list2.Add(mRow3);
			}
			else
			{
				list.Add(mRow3);
			}
		}
		TxtTeam1.Text = "队伍 1" + ((m.MyTeam == 1) ? "（我方）" : "") + "\u3000" + list.Count + " 人";
		TxtTeam2.Text = "队伍 2" + ((m.MyTeam == 2) ? "（我方）" : "") + "\u3000" + list2.Count + " 人";
		ListTeam1.ItemsSource = list;
		ListTeam2.ItemsSource = list2;
		int count = TouhouApi.MatchArtUrls(m).Count;
		int num = TouhouApi.MatchArtUrls(m).Count((string u) => ImgCache.Cached(u) != null);
		TxtNote.Text = "数据来自 aslucky.club 的对局记录（game/list）。图标按站点前端的规则拼接：技能 ability/英雄id_技能id_槽位tqwer.jpg、召唤技能 spell/槽位_id.jpg、符文 rune/系_id.jpg、属性 stat/序号.jpg。\n站点图片接口很慢（一张 3~40 秒），图标会一张张补上：本局需要 " + count + " 张，已缓存 " + num + " 张（缓存目录 data\\img，下过一次以后就是本地的）。\n装备编号站点没给名称对照表，所以不显示。";
	}
}
