using System.Windows.Media;

namespace Sc2Toolbox;

public class TouhouRow
{
	public ImageSource Img { get; set; }

	public ImageSource Region { get; set; }

	public Brush ResultBrush { get; set; }

	public Brush RowBrush { get; set; }

	public string Result { get; set; }

	public string No { get; set; }

	public string Title { get; set; }

	public string Sub { get; set; }

	public string Right { get; set; }

	public TMatch Match { get; set; }
}
