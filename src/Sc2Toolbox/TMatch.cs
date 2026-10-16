using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sc2Toolbox;

public class TMatch
{
	public long GameId;

	public int Mode;

	public int GameMode;

	public int Winner;

	public int Team1Wins;

	public int Team2Wins;

	public long EndTime;

	public string EndTimeText = "";

	public int MyTeam;

	public int MyHeroId;

	public int MyKill;

	public int MyDeath;

	public int MyAssist;

	public int MyMoney;

	public int MyDamage;

	public int MyDamaged;

	public int MyMmr;

	public int MyMmrChange;

	public int MyMmrHero;

	public int MyHeroLevel;

	public bool IsMvp;

	public List<TMatchPlayer> Players = new List<TMatchPlayer>();

	public DateTime When
	{
		get
		{
			try
			{
				if (EndTime > 0)
				{
					long num = EndTime;
					if (num > 100000000000000L)
					{
						num /= 10000;
					}
					return DateTimeOffset.FromUnixTimeMilliseconds(num).LocalDateTime;
				}
				if (!string.IsNullOrEmpty(EndTimeText) && DateTime.TryParse(EndTimeText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
				{
					return result;
				}
			}
			catch
			{
			}
			return DateTime.MinValue;
		}
	}

	public bool? Win
	{
		get
		{
			if (Winner > 0 && MyTeam > 0)
			{
				return Winner == MyTeam;
			}
			foreach (TMatchPlayer player in Players)
			{
				if (player.Handle == MyHandleOf(this) && player.Win > 0)
				{
					return player.Win == 1;
				}
			}
			return null;
		}
	}

	public string ResultText
	{
		get
		{
			bool? win = Win;
			if (win.HasValue)
			{
				if (!win.Value)
				{
					return "负";
				}
				return "胜";
			}
			return "—";
		}
	}

	public string KdaText => MyKill + " / " + MyDeath + " / " + MyAssist;

	public string WhenText
	{
		get
		{
			DateTime when = When;
			if (!(when == DateTime.MinValue))
			{
				return when.ToString("MM-dd HH:mm");
			}
			return "";
		}
	}

	private string MyHandleOf(TMatch m)
	{
		return TouhouApi.MyHandle;
	}
}
