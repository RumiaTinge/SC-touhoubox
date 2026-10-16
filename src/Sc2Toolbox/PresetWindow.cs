using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Sc2Toolbox;

/// <summary>
/// 技能预设加点编辑器（可视化天赋树）。
///
/// 数据来自 touhou.SC2Bank 的 dbHeroNN_config 段：段的每个 Key 就是一条方案，
/// Key 名是保存时间戳，值串是 105 个数字。经与官网接口的真实构筑逐条比对后定下的版图：
///
///   [0..5]    初始属性 6 项（0~8，恰好一项为 8，位置即主属性）
///   [6]       主系 primaryColumn（0~4）
///   [7..96]   天赋网格 90 格 = 18 rune x 5 系，下标 = 7 + 5*rune + school
///   [97..104] 技能栏 / 辅助技能 / 技能组合，与天赋无关
///
/// 游戏规则（本窗口会强制）：
///   1. 一局只能选两个派系；
///   2. 同一派系每一行的选项互斥（一行只能点一个）；
///   3. 副系（不是主系的那一个）只能从「其他派系的基础天赋」里选一个，
///      基础天赋 = 第 1、2 行的左 / 中（rune 0、1、3、4）。
/// </summary>
public sealed class PresetWindow : Window
{
	/// <summary>基础天赋：副系只能在这 4 个里挑一个。</summary>
	private static readonly int[] BasicRunes = new int[4] { 0, 1, 3, 4 };

	private static readonly string[] SchoolName = new string[5] { "斗争", "灾难", "魔法", "荣誉", "祝福" };

	/// <summary>派系标题颜色，尽量贴近游戏面板。</summary>
	private static readonly Color[] SchoolColor = new Color[5]
	{
		Color.FromRgb(0xE8, 0xC8, 0x6A),
		Color.FromRgb(0xE0, 0x6A, 0x5A),
		Color.FromRgb(0xB8, 0x86, 0xE8),
		Color.FromRgb(0x6A, 0xD8, 0x6A),
		Color.FromRgb(0x6A, 0xC8, 0xE8)
	};

	private static readonly string[] InitStatName = new string[6] { "生命", "魔法值", "物伤", "法伤", "物抗", "法抗" };

	/// <summary>6 项初始属性在站点属性图里的图号（与 TouhouApi.InitStatIconIds 一致）。</summary>
	private static readonly int[] InitStatIcon = new int[6] { 4, 6, 0, 1, 2, 3 };

	private BankProfile _profile;

	private readonly ListBox _heroList = new ListBox();

	private readonly ListBox _presetList = new ListBox();

	private readonly TextBox _nameBox = new TextBox();

	private readonly TextBlock _info = new TextBlock();

	private BankHeroStat _hero;

	private BankPreset _preset;

	/// <summary>工作副本：105 位数字。</summary>
	private int[] _p = new int[105];

	private readonly TextBox[] _statBox = new TextBox[6];

	private readonly TextBox[] _tailBox = new TextBox[8];

	private readonly RadioButton[] _schoolRadio = new RadioButton[5];

	/// <summary>天赋格：下标 = rune*5 + school；不存在的格为 null。</summary>
	private readonly Border[] _cell = new Border[90];

	private readonly Image[] _cellImg = new Image[90];

	private readonly TextBlock[] _cellTxt = new TextBlock[90];

	private bool _loading;

	private readonly Brush _fg = new SolidColorBrush(Color.FromRgb(0xD6, 0xDE, 0xE8));

	private readonly Brush _muted = new SolidColorBrush(Color.FromRgb(0x8A, 0x96, 0xA8));

	private readonly Brush _bg = new SolidColorBrush(Color.FromRgb(0x14, 0x18, 0x20));

	private readonly Brush _panel = new SolidColorBrush(Color.FromRgb(0x1B, 0x21, 0x2B));

	private readonly Brush _pick = new SolidColorBrush(Color.FromRgb(0x7C, 0xE0, 0x7C));

	private readonly Brush _pickBg = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x24));

	private readonly Brush _empty = new SolidColorBrush(Color.FromRgb(0x2A, 0x32, 0x40));

	public PresetWindow(BankProfile profile)
	{
		_profile = profile;
		base.Title = "技能预设加点 · " + ((profile != null) ? profile.Label : "无存档");
		base.Width = 1380.0;
		base.Height = 820.0;
		base.Background = _bg;
		base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		BuildUi();
		FillHeroes();
	}

	private void BuildUi()
	{
		DockPanel dockPanel = new DockPanel();
		base.Content = dockPanel;
		Border border = new Border
		{
			Width = 300.0,
			Background = _panel,
			Padding = new Thickness(10.0)
		};
		DockPanel.SetDock(border, Dock.Left);
		StackPanel stackPanel = new StackPanel();
		border.Child = stackPanel;
		stackPanel.Children.Add(Label("英雄（有加点方案的）"));
		_heroList.Height = 240.0;
		_heroList.Background = _bg;
		_heroList.Foreground = _fg;
		_heroList.BorderThickness = new Thickness(0.0);
		_heroList.SelectionChanged += Hero_Changed;
		stackPanel.Children.Add(_heroList);
		stackPanel.Children.Add(Label("该英雄的方案（key = 保存时间）"));
		_presetList.Background = _bg;
		_presetList.Foreground = _fg;
		_presetList.BorderThickness = new Thickness(0.0);
		_presetList.SelectionChanged += Preset_Changed;
		stackPanel.Children.Add(_presetList);
		stackPanel.Children.Add(Label("方案名"));
		_nameBox.Background = _bg;
		_nameBox.Foreground = _fg;
		_nameBox.BorderBrush = _muted;
		_nameBox.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
		stackPanel.Children.Add(_nameBox);
		TextBlock textBlock = new TextBlock
		{
			Text = "规则：一局只能选两个派系；同一派系每行互斥；副系只能从其他派系的 4 个基础天赋"
				+ "（第 1、2 行的左 / 中）里选一个。点「主属性」那一栏可以换主系。",
			Foreground = _muted,
			FontSize = 11.0,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(textBlock);
		dockPanel.Children.Add(border);

		Border border2 = new Border
		{
			Background = _panel,
			Padding = new Thickness(10.0)
		};
		DockPanel.SetDock(border2, Dock.Bottom);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		border2.Child = stackPanel2;
		stackPanel2.Children.Add(Button("清空加点", Clear_Click));
		stackPanel2.Children.Add(Button("新建方案", New_Click));
		stackPanel2.Children.Add(Button("复制为新方案", Copy_Click));
		stackPanel2.Children.Add(Button("删除方案", Delete_Click));
		stackPanel2.Children.Add(Button("保存到存档", Save_Click));
		_info.Foreground = _muted;
		_info.Margin = new Thickness(12.0, 6.0, 0.0, 0.0);
		_info.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(_info);
		dockPanel.Children.Add(border2);

		ScrollViewer scrollViewer = new ScrollViewer
		{
			Padding = new Thickness(12.0),
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		StackPanel stackPanel3 = new StackPanel();
		scrollViewer.Content = stackPanel3;
		dockPanel.Children.Add(scrollViewer);

		stackPanel3.Children.Add(BuildHeader());
		stackPanel3.Children.Add(BuildTree());
		stackPanel3.Children.Add(BuildTail());

		TextBlock textBlock2 = new TextBlock
		{
			Text = "提示：游戏运行中改银行会在退出时被游戏回写覆盖，请先关游戏再保存。保存前会自动备份一份 .sctb.bak。\n"
				+ "天赋格上的提示里有该天赋的游戏内说明；没有说明的天赋（游戏里没写文案）只显示名字。",
			Foreground = _muted,
			FontSize = 11.0,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		stackPanel3.Children.Add(textBlock2);
	}

	// ---------------------------------------------------------------- 顶部：初始属性 + 主系

	private FrameworkElement BuildHeader()
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		TextBlock textBlock = Label("初始属性");
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		stackPanel.Children.Add(textBlock);
		for (int i = 0; i < 6; i++)
		{
			int idx = i;
			StackPanel stackPanel2 = new StackPanel
			{
				Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
			};
			Image image = new Image
			{
				Width = 24.0,
				Height = 24.0,
				Stretch = Stretch.Uniform,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			try
			{
				System.Windows.Media.Imaging.BitmapSource bitmapSource = ImgCache.Cached(TouhouApi.StatImg(InitStatIcon[i]));
				if (bitmapSource != null)
				{
					image.Source = bitmapSource;
				}
			}
			catch
			{
			}
			stackPanel2.Children.Add(image);
			TextBlock textBlock2 = new TextBlock
			{
				Text = InitStatName[i],
				Foreground = _muted,
				FontSize = 10.0,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			stackPanel2.Children.Add(textBlock2);
			TextBox textBox = new TextBox
			{
				Width = 40.0,
				TextAlignment = TextAlignment.Center,
				Background = _bg,
				Foreground = _fg,
				BorderBrush = _muted,
				ToolTip = "存档下标 " + i + "（0~8；恰好一项为 8，位置就是主属性）"
			};
			textBox.TextChanged += delegate
			{
				StatBox_Changed(idx);
			};
			_statBox[i] = textBox;
			stackPanel2.Children.Add(textBox);
			stackPanel.Children.Add(stackPanel2);
		}
		StackPanel stackPanel3 = new StackPanel
		{
			Margin = new Thickness(16.0, 0.0, 0.0, 0.0)
		};
		TextBlock textBlock3 = new TextBlock
		{
			Text = "主系",
			Foreground = _muted,
			FontSize = 10.0,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		stackPanel3.Children.Add(textBlock3);
		StackPanel stackPanel4 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		for (int j = 0; j < 5; j++)
		{
			int s = j;
			RadioButton radioButton = new RadioButton
			{
				Content = SchoolName[j],
				GroupName = "primary",
				Foreground = new SolidColorBrush(SchoolColor[j]),
				FontSize = 12.0,
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			radioButton.Checked += delegate
			{
				Radio_Changed(s);
			};
			_schoolRadio[j] = radioButton;
			stackPanel4.Children.Add(radioButton);
		}
		stackPanel3.Children.Add(stackPanel4);
		stackPanel.Children.Add(stackPanel3);
		return stackPanel;
	}

	// ---------------------------------------------------------------- 天赋树

	private FrameworkElement BuildTree()
	{
		Border border = new Border
		{
			Background = _panel,
			Padding = new Thickness(8.0),
			CornerRadius = new CornerRadius(4.0)
		};
		Grid grid = new Grid();
		border.Child = grid;
		for (int s = 0; s < 5; s++)
		{
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(192.0)
			});
		}
		for (int row = 0; row < 7; row++)
		{
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
		}
		for (int s2 = 0; s2 < 5; s2++)
		{
			TextBlock textBlock = new TextBlock
			{
				Text = SchoolName[s2],
				Foreground = new SolidColorBrush(SchoolColor[s2]),
				FontSize = 14.0,
				FontWeight = FontWeights.Bold,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
			};
			Grid.SetRow(textBlock, 0);
			Grid.SetColumn(textBlock, s2);
			grid.Children.Add(textBlock);

			for (int row2 = 0; row2 < 6; row2++)
			{
				FrameworkElement frameworkElement = BuildRow(s2, row2);
				Grid.SetRow(frameworkElement, row2 + 1);
				Grid.SetColumn(frameworkElement, s2);
				grid.Children.Add(frameworkElement);
			}
		}
		return border;
	}

	/// <summary>一个派系的一行：按游戏面板的位置把该行 1~3 个天赋摆进 3 个槽位。</summary>
	private FrameworkElement BuildRow(int school, int row)
	{
		int[] array = TouhouRune.Layout[school][row];
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 1.0, 0.0, 1.0)
		};
		for (int i = 0; i < 3; i++)
		{
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
		}
		int num = array.Length;
		for (int j = 0; j < num; j++)
		{
			int rune = array[j];
			int col = ((num == 1) ? 1 : ((num == 2) ? (j * 2) : j));
			Border border = new Border
			{
				Background = _bg,
				BorderBrush = _empty,
				BorderThickness = new Thickness(2.0),
				CornerRadius = new CornerRadius(4.0),
				Padding = new Thickness(3.0),
				Margin = new Thickness(2.0),
				Cursor = System.Windows.Input.Cursors.Hand
			};
			StackPanel stackPanel = new StackPanel();
			border.Child = stackPanel;
			Image image = new Image
			{
				Height = 42.0,
				Stretch = Stretch.Uniform
			};
			BitmapSourceRun(image, school, rune);
			stackPanel.Children.Add(image);
			TextBlock textBlock = new TextBlock
			{
				Text = TouhouRune.Name(school, rune),
				Foreground = _fg,
				FontSize = 9.5,
				TextWrapping = TextWrapping.Wrap,
				TextAlignment = TextAlignment.Center,
				Width = 54.0,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
			};
			stackPanel.Children.Add(textBlock);
			string text = TouhouRune.Tip(school, rune, "");
			string text2 = TouhouRune.Desc(school, rune);
			border.ToolTip = ((text2.Length == 0) ? (text + "\n（游戏里没有这条天赋的说明文案）") : (text + "\n\n" + text2));
			int key = rune * 5 + school;
			_cell[key] = border;
			_cellImg[key] = image;
			_cellTxt[key] = textBlock;
			int capturedRune = rune;
			border.MouseLeftButtonUp += delegate
			{
				Cell_Click(school, capturedRune);
			};
			Grid.SetColumn(border, col);
			grid.Children.Add(border);
		}
		return grid;
	}

	private static void BitmapSourceRun(Image img, int school, int rune)
	{
		try
		{
			System.Windows.Media.Imaging.BitmapSource bitmapSource = ImgCache.Cached("https://www.aslucky.club/touhou/img/rune/" + school + "_" + rune + ".jpg");
			if (bitmapSource != null)
			{
				img.Source = bitmapSource;
			}
		}
		catch
		{
		}
	}

	// ---------------------------------------------------------------- 底部：其余 8 项

	private FrameworkElement BuildTail()
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		TextBlock textBlock = Label("技能栏 / 辅助技能（下标 97~104，与天赋无关）");
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		stackPanel.Children.Add(textBlock);
		for (int i = 0; i < 8; i++)
		{
			int idx = 97 + i;
			TextBox textBox = new TextBox
			{
				Width = 34.0,
				Margin = new Thickness(0.0, 0.0, 4.0, 0.0),
				TextAlignment = TextAlignment.Center,
				Background = _bg,
				Foreground = _fg,
				BorderBrush = _muted,
				ToolTip = "存档下标 " + idx
			};
			textBox.TextChanged += delegate
			{
				TailBox_Changed(idx);
			};
			_tailBox[i] = textBox;
			stackPanel.Children.Add(textBox);
		}
		return stackPanel;
	}

	// ---------------------------------------------------------------- 规则与刷新

	private static TextBlock Label(string s)
	{
		return new TextBlock
		{
			Text = s,
			Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x96, 0xA8)),
			FontSize = 11.0,
			Margin = new Thickness(0.0, 6.0, 0.0, 4.0)
		};
	}

	private Button Button(string text, RoutedEventHandler handler)
	{
		Button button = new Button
		{
			Content = text,
			Padding = new Thickness(10.0, 3.0, 10.0, 3.0),
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
		};
		button.Click += handler;
		return button;
	}

	private static bool IsBasic(int rune)
	{
		for (int i = 0; i < BasicRunes.Length; i++)
		{
			if (BasicRunes[i] == rune)
			{
				return true;
			}
		}
		return false;
	}

	private int Picked(int school, int rune)
	{
		int num = TouhouRune.IdxOf(school, rune);
		if (num < 0 || num >= _p.Length)
		{
			return 0;
		}
		return _p[num];
	}

	/// <summary>当前选中的派系列表（按选中数量降序）。</summary>
	private List<int> PickedSchools()
	{
		List<int> list = new List<int>();
		for (int s = 0; s < 5; s++)
		{
			if (CountOf(s) > 0)
			{
				list.Add(s);
			}
		}
		return list;
	}

	private int CountOf(int school)
	{
		int num = 0;
		for (int rune = 0; rune < 18; rune++)
		{
			if (Picked(school, rune) > 0)
			{
				num++;
			}
		}
		return num;
	}

	/// <summary>用户手工点过「主属性」那一栏之后，主系就不再自动跟随。</summary>
	private bool _primaryManual;

	/// <summary>这个系有没有点过高级天赋（第 3 排往后）。</summary>
	private bool HasAdvanced(int school)
	{
		if (school < 0)
		{
			return false;
		}
		for (int rune = 6; rune < 18; rune++)
		{
			if (Picked(school, rune) > 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// 主系：默认跟着「点得最多的那个系」自动走，点数反超才换主系（并列时保持不变）。
	/// 已经点过高级天赋的系不能被降级成副系，否则那局就不合法了。
	/// 用户点过「主属性」那一栏之后改为手工指定，直到下一次点天赋为止。
	/// </summary>
	private int PrimarySchool()
	{
		int num = Math.Max(0, Math.Min(4, _p[6]));
		if (_primaryManual)
		{
			return num;
		}
		int num2 = CountOf(num);
		if (num2 == 0)
		{
			int num3 = -1;
			int num4 = 0;
			for (int s = 0; s < 5; s++)
			{
				int num5 = CountOf(s);
				if (num5 > num4)
				{
					num4 = num5;
					num3 = s;
				}
			}
			return (num3 >= 0) ? num3 : num;
		}
		if (HasAdvanced(num))
		{
			return num;
		}
		int num6 = num;
		int num7 = num2;
		for (int t = 0; t < 5; t++)
		{
			if (t != num)
			{
				int num8 = CountOf(t);
				if (num8 > num7)
				{
					num7 = num8;
					num6 = t;
				}
			}
		}
		return num6;
	}

	/// <summary>载入存档后修一次主系：优先有高级天赋的系，其次点数最多的系。</summary>
	private void FixPrimaryAfterLoad()
	{
		_primaryManual = false;
		int num = Math.Max(0, Math.Min(4, _p[6]));
		if (CountOf(num) > 0 && HasAdvanced(num))
		{
			return;
		}
		for (int s = 0; s < 5; s++)
		{
			if (CountOf(s) > 0 && HasAdvanced(s))
			{
				_p[6] = s;
				return;
			}
		}
		int num2 = -1;
		int num3 = 0;
		for (int t = 0; t < 5; t++)
		{
			int num4 = CountOf(t);
			if (num4 > num3)
			{
				num3 = num4;
				num2 = t;
			}
		}
		if (num2 >= 0)
		{
			_p[6] = num2;
		}
	}

	private void Cell_Click(int school, int rune)
	{
		if (_loading)
		{
			return;
		}
		_primaryManual = false;
		int num = TouhouRune.IdxOf(school, rune);
		if (num < 0)
		{
			return;
		}
		if (_p[num] > 0)
		{
			_p[num] = 0;
			RefreshTree();
			SetInfo("取消 " + TouhouRune.Tip(school, rune, ""));
			return;
		}
		int num2 = PrimarySchool();
		int num3 = CountOf(school);
		if (school != num2)
		{
			int num4 = num2;
			if (num3 == 0 && CountOf(num4) == 0)
			{
				_p[6] = school;
				num2 = school;
			}
			else
			{
				if (!IsBasic(rune))
				{
					SetInfo(string.Format("不能这样点：{0} 不是主系，副系只能从其他派系的 4 个基础天赋（第 1、2 行的左 / 中）里选一个。", SchoolName[school]));
					return;
				}
				foreach (int item in PickedSchools())
				{
					if (item != school && item != num2)
					{
						SetInfo(string.Format("不能这样点：已经选了 {0} 和 {1} 两个派系，一局最多只能选两个。", SchoolName[num2], SchoolName[item]));
						return;
					}
				}
			}
		}
		_p[num] = 1;
		int row = rune / 3;
		int[] array = TouhouRune.Layout[school][row];
		foreach (int num5 in array)
		{
			if (num5 != rune)
			{
				int num6 = TouhouRune.IdxOf(school, num5);
				if (num6 >= 0)
				{
					_p[num6] = 0;
				}
			}
		}
		_p[6] = PrimarySchool();
		RefreshTree();
		string text = Validate();
		SetInfo((text.Length == 0) ? TouhouRune.Tip(school, rune, "已点 ") : text);
	}

	/// <summary>检查当前加点是否合法，返回问题描述（空串 = 合法）。</summary>
	private string Validate()
	{
		List<int> list = PickedSchools();
		if (list.Count > 2)
		{
			return string.Format("当前选了 {0} 个派系，超过 2 个。", list.Count);
		}
		int num = PrimarySchool();
		foreach (int item in list)
		{
			if (item == num)
			{
				continue;
			}
			for (int rune = 0; rune < 18; rune++)
			{
				if (Picked(item, rune) > 0 && !IsBasic(rune))
				{
					return string.Format("{0} 不是主系，却点了非基础天赋「{1}」。", SchoolName[item], TouhouRune.Name(item, rune));
				}
			}
		}
		return "";
	}

	private void StatBox_Changed(int i)
	{
		if (_loading)
		{
			return;
		}
		int num;
		if (int.TryParse(_statBox[i].Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out num))
		{
			_p[i] = Math.Max(0, Math.Min(8, num));
		}
	}

	private void TailBox_Changed(int idx)
	{
		if (_loading)
		{
			return;
		}
		int num;
		if (int.TryParse(_tailBox[idx - 97].Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out num))
		{
			_p[idx] = num;
		}
	}

	private void Radio_Changed(int school)
	{
		if (_loading)
		{
			return;
		}
		_primaryManual = true;
		_p[6] = school;
		RefreshTree();
		string text = Validate();
		SetInfo((text.Length == 0) ? ("主系设为 " + SchoolName[school] + "。") : text);
	}

	/// <summary>把 _p 刷到界面上（不再触发事件）。</summary>
	private void RefreshTree()
	{
		_loading = true;
		try
		{
			for (int i = 0; i < 6; i++)
			{
				_statBox[i].Text = _p[i].ToString(CultureInfo.InvariantCulture);
			}
			for (int j = 0; j < 8; j++)
			{
				_tailBox[j].Text = _p[97 + j].ToString(CultureInfo.InvariantCulture);
			}
			for (int k = 0; k < 5; k++)
			{
				_schoolRadio[k].IsChecked = (k == Math.Max(0, Math.Min(4, _p[6])));
			}
			for (int l = 0; l < 5; l++)
			{
				for (int m = 0; m < 18; m++)
				{
					int num = m * 5 + l;
					Border border = _cell[num];
					if (border != null)
					{
						bool flag = Picked(l, m) > 0;
						border.Background = (flag ? _pickBg : _bg);
						border.BorderBrush = (flag ? _pick : _empty);
						border.BorderThickness = new Thickness(flag ? 2.0 : 1.0);
					}
					Image image = _cellImg[num];
					if (image != null)
					{
						image.Opacity = ((Picked(l, m) > 0) ? 1.0 : 0.42);
						image.Effect = ((Picked(l, m) > 0) ? new DropShadowEffect
						{
							Color = Color.FromRgb(0x7C, 0xE0, 0x7C),
							BlurRadius = 14.0,
							ShadowDepth = 0.0,
							Opacity = 0.9
						} : null);
					}
					TextBlock textBlock = _cellTxt[num];
					if (textBlock != null)
					{
						textBlock.Foreground = ((Picked(l, m) > 0) ? _pick : _fg);
						textBlock.Opacity = ((Picked(l, m) > 0) ? 1.0 : 0.42);
					}
				}
			}
		}
		finally
		{
			_loading = false;
		}
	}

	// ---------------------------------------------------------------- 列表与方案

	private void FillHeroes()
	{
		_heroList.Items.Clear();
		if (_profile == null)
		{
			SetInfo("没读到存档。");
			return;
		}
		foreach (BankHeroStat hero in _profile.Heroes)
		{
			if (hero.Configs.Count > 0)
			{
				_heroList.Items.Add(string.Format("{0} {1}（{2} 个方案）", TouhouApi.Pad2(hero.HeroId), hero.Name, hero.Configs.Count));
			}
		}
		if (_heroList.Items.Count > 0)
		{
			_heroList.SelectedIndex = 0;
		}
		else
		{
			SetInfo("这份存档里没有任何加点方案。");
		}
	}

	private List<BankHeroStat> HeroesWithConfig()
	{
		List<BankHeroStat> list = new List<BankHeroStat>();
		if (_profile != null)
		{
			foreach (BankHeroStat hero in _profile.Heroes)
			{
				if (hero.Configs.Count > 0)
				{
					list.Add(hero);
				}
			}
		}
		return list;
	}

	private void Hero_Changed(object sender, SelectionChangedEventArgs e)
	{
		List<BankHeroStat> list = HeroesWithConfig();
		int num = _heroList.SelectedIndex;
		if (num < 0 || num >= list.Count)
		{
			return;
		}
		_hero = list[num];
		_presetList.Items.Clear();
		foreach (BankPreset preset in _hero.Configs)
		{
			_presetList.Items.Add(string.Format("{0}  ·  {1}  ·  {2} 点", preset.Key, string.IsNullOrEmpty(preset.Name) ? "(无名)" : preset.Name, preset.Sum));
		}
		if (_presetList.Items.Count > 0)
		{
			_presetList.SelectedIndex = 0;
		}
	}

	private void Preset_Changed(object sender, SelectionChangedEventArgs e)
	{
		int num = _presetList.SelectedIndex;
		if (_hero == null || num < 0 || num >= _hero.Configs.Count)
		{
			return;
		}
		_preset = _hero.Configs[num];
		_nameBox.Text = _preset.Name;
		_p = new int[105];
		for (int i = 0; i < 105; i++)
		{
			_p[i] = ((i < _preset.Points.Length) ? _preset.Points[i] : 0);
		}
		FixPrimaryAfterLoad();
		RefreshTree();
		string text = Validate();
		SetInfo((text.Length == 0) ? string.Format("{0} · {1} · 合计 {2} 点{3}", _hero.Key, _preset.Key, _preset.Sum, DescribeNow()) : text);
	}

	/// <summary>把当前加点念一遍，用于状态栏。</summary>
	private string DescribeNow()
	{
		int num = PrimarySchool();
		List<int> list = PickedSchools();
		string text = "";
		foreach (int item in list)
		{
			string text2 = "";
			for (int rune = 0; rune < 18; rune++)
			{
				if (Picked(item, rune) > 0)
				{
					text2 = text2 + (text2.Length == 0 ? "" : "、") + ((TouhouRune.Name(item, rune).Length == 0) ? ("rune" + rune) : TouhouRune.Name(item, rune));
				}
			}
			text = text + "  " + ((item == num) ? "【主】" : "【副】") + SchoolName[item] + "：" + text2;
		}
		return text;
	}

	private bool GameRunning()
	{
		try
		{
			return Process.GetProcessesByName("SC2_x64").Length > 0;
		}
		catch
		{
			return false;
		}
	}

	private bool ConfirmGameClosed()
	{
		if (!GameRunning())
		{
			return true;
		}
		return MessageBox.Show(this, "星际争霸II 正在运行，游戏退出时会把银行文件整个回写，覆盖掉这次的修改。\n\n仍要继续写入吗？", "游戏还在运行", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK;
	}

	private void Clear_Click(object sender, RoutedEventArgs e)
	{
		for (int i = 7; i < 97; i++)
		{
			_p[i] = 0;
		}
		RefreshTree();
		SetInfo("已清空天赋（初始属性、主系和技能栏保留）。");
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		if (_hero == null || _preset == null)
		{
			SetInfo("先选一个方案。");
			return;
		}
		string text = Validate();
		if (text.Length > 0 && MessageBox.Show(this, text + "\n\n这不符合游戏规则，游戏里可能不会生效。仍要保存吗？", "加点不合法", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) != MessageBoxResult.OK)
		{
			return;
		}
		if (!ConfirmGameClosed())
		{
			return;
		}
		int[] array = new int[105];
		Array.Copy(_p, array, 105);
		string text2;
		if (TouhouBank.SavePreset(_profile.BankPath, _hero.HeroId, _preset.Key, array, _nameBox.Text.Trim(), out text2))
		{
			SetInfo("已写入 " + _profile.BankPath);
			Reload();
		}
		else
		{
			SetInfo("写入失败：" + text2);
		}
	}

	private void New_Click(object sender, RoutedEventArgs e)
	{
		if (_hero == null)
		{
			SetInfo("先选一个英雄。");
			return;
		}
		int[] array = new int[105];
		Array.Copy(_p, array, 105);
		string text = DateTime.Now.ToString("yyyy:MM:dd:HH:mm:ss", CultureInfo.InvariantCulture);
		string text2;
		if (!TouhouBank.SavePreset(_profile.BankPath, _hero.HeroId, text, array, string.IsNullOrEmpty(_nameBox.Text.Trim()) ? ("方案 " + text) : _nameBox.Text.Trim(), out text2))
		{
			SetInfo("写入失败：" + text2);
			return;
		}
		Reload();
		_selectPresetKey = text;
		SetInfo("已新建方案 " + text);
	}

	private void Copy_Click(object sender, RoutedEventArgs e)
	{
		New_Click(sender, e);
	}

	private void Delete_Click(object sender, RoutedEventArgs e)
	{
		if (_hero == null || _preset == null)
		{
			SetInfo("先选一个方案。");
			return;
		}
		if (MessageBox.Show(this, "删除方案 " + _preset.Key + " ？", "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
		{
			return;
		}
		string text;
		if (TouhouBank.DeletePreset(_profile.BankPath, _hero.HeroId, _preset.Key, out text))
		{
			SetInfo("已删除 " + _preset.Key);
			Reload();
		}
		else
		{
			SetInfo("删除失败：" + text);
		}
	}

	private string _selectPresetKey = "";

	/// <summary>重新读盘，并尽量停在原来选中的英雄/方案上。</summary>
	private void Reload()
	{
		int num = ((_hero != null) ? _hero.HeroId : 0);
		string selectPresetKey = _selectPresetKey;
		_selectPresetKey = "";
		BankProfile bankProfile = TouhouBank.Load(_profile.BankPath);
		if (bankProfile == null)
		{
			SetInfo("重新读取存档失败：" + TouhouBank.LastError);
			return;
		}
		_profile = bankProfile;
		_hero = null;
		_preset = null;
		_heroList.Items.Clear();
		_presetList.Items.Clear();
		FillHeroes();
		List<BankHeroStat> list = HeroesWithConfig();
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].HeroId == num)
			{
				_heroList.SelectedIndex = i;
				break;
			}
		}
		if (!string.IsNullOrEmpty(selectPresetKey) && _hero != null)
		{
			for (int j = 0; j < _hero.Configs.Count; j++)
			{
				if (_hero.Configs[j].Key == selectPresetKey)
				{
					_presetList.SelectedIndex = j;
					break;
				}
			}
		}
	}

	private void SetInfo(string s)
	{
		_info.Text = s;
	}
}
