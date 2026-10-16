namespace Sc2Toolbox;

public class TMatchPlayer
{
	public string Handle = "";

	public string Name = "";

	public string ClanTag = "";

	public long GameId;

	public int Team;

	public int HeroId;

	public int Win;

	public int Kill;

	public int Death;

	public int Assist;

	public int Money;

	public int Damage;

	public int Damaged;

	public int Mmr;

	public int MmrChange;

	public int MmrHero;

	public int Mvp;

	public int HeroLevel;

	public int Healing;

	public int SelectNum;

	public int ExitFrame;

	public string AbilityUpSeq = "";

	public int PrimaryColumn;

	public int SecondaryColumn;

	public int SecondaryRune;

	public string PrimaryRunes = "";

	public string AbilitySet = "";

	public string Spells = "";

	public string StatSet = "";

	public string InitStats = "";

	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrEmpty(Name))
			{
				return Name;
			}
			return TPlayer.Short(Handle);
		}
	}

	public string KdaText => Kill + " / " + Death + " / " + Assist;

	public bool IsMvp => Mvp > 0;
}
