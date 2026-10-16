namespace Sc2Toolbox;

public class TPlayer
{
	public string Handle = "";

	public string Name = "";

	public string ClanTag = "";

	public int Region = 5;

	public int Pfp;

	public int Mmr;

	public int MmrChange;

	public int Rank;

	public int Wins;

	public int Losses;

	public int WinStreak;

	public int LoseStreak;

	public int Games;

	public double WinRate;

	public double KillAvg;

	public double DeathAvg;

	public double AssistAvg;

	public double DamageAvg;

	public int HeroId;

	public int Sample;

	public int MmrMax;

	public int WinStreakMax;

	public int LoseStreakMax;

	public int Mvp;

	public int Svp;

	public int ExitCount;

	public int BestKill;

	public int BestDamage;

	public int BestMoney;

	public int BestAssist;

	public int BestDamaged;

	public int BestDeath;

	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrEmpty(Name))
			{
				return Name;
			}
			return Short(Handle);
		}
	}

	public string ShortName
	{
		get
		{
			string displayName = DisplayName;
			if (displayName.Length > 8)
			{
				return displayName.Substring(0, 8) + "…";
			}
			return displayName;
		}
	}

	public static string Short(string handle)
	{
		if (string.IsNullOrEmpty(handle))
		{
			return "？";
		}
		int num = handle.LastIndexOf('-');
		if (num < 0 || num + 1 >= handle.Length)
		{
			return handle;
		}
		return "玩家 " + handle.Substring(num + 1);
	}
}
