namespace Sc2Toolbox;

public class THero
{
	public int HeroId;

	public int Total;

	public double WinPercentage;

	public double PickPercentage;

	public double BanPercentage;

	public double KillAverage;

	public double DeathAverage;

	public double AssistAverage;

	public double DamageAverage;

	public double DamagedAverage;

	public double Fighting;

	public int Mmr;

	public string Name => TouhouApi.HeroName(HeroId);

	public string WinText => WinPercentage.ToString("0.#") + "%";

	public string KdaText => KillAverage.ToString("0.#") + "/" + DeathAverage.ToString("0.#") + "/" + AssistAverage.ToString("0.#");

	public string Url => TouhouApi.HeroImg(HeroId);
}
