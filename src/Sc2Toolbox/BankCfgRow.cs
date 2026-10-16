using System.Collections.Generic;
using System.Windows.Media;

namespace Sc2Toolbox;

/// <summary>加点条里的一格（对应站点属性图 stat/&lt;图号&gt;.jpg）。</summary>
public sealed class BankCfgCell
{
	public ImageSource Icon { get; set; }

	public string Num { get; set; } = "";

	public double Alpha { get; set; } = 1.0;

	public string Tip { get; set; } = "";
}

/// <summary>「英雄加点 · 本地存档」卡片里的一行。</summary>
public sealed class BankCfgRow
{
	public ImageSource Img { get; set; }

	public string Title { get; set; } = "";

	public string Sub { get; set; } = "";

	public List<BankCfgCell> Cells { get; set; } = new List<BankCfgCell>();
}
