using System.Windows.Media;

namespace Sc2Toolbox;

public class SlotRow
{
	public int No { get; set; }

	public string Handle { get; set; }

	public string Nick { get; set; }

	public string Name { get; set; }

	public string Tag { get; set; }

	public Brush Color { get; set; }

	public bool Filled { get; set; }

	public ImageSource Avatar { get; set; }

	public string Mmr { get; set; }

	public string HeroName { get; set; }

	public string Tip { get; set; }
}
