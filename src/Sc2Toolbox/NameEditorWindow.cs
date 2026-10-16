using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Sc2Toolbox;

public class NameEditorWindow : Window
{
	private readonly TextBox _box = new TextBox();

	private readonly TextBlock _state = new TextBlock();

	private readonly Action _onSaved;

	public NameEditorWindow(Action onSaved)
	{
		_onSaved = onSaved;
		Title = "英雄名表（每行：id + Tab + 中文名）";
		Width = 580.0;
		Height = 740.0;
		MinWidth = 420.0;
		MinHeight = 300.0;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		Background = new SolidColorBrush(Color.FromRgb(10, 14, 20));
		Foreground = new SolidColorBrush(Color.FromRgb(233, 239, 248));
		FontSize = 13.0;
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(14.0)
		};
		TextBlock element = new TextBlock
		{
			Text = "左边是 id，右边是中文名，中间用 Tab 隔开（# 开头的整行会被忽略）。改完点左上角蓝色的「保存并生效」（也可以按 Ctrl+S），回面板再点一次「刷新数据」就换过来了。",
			TextWrapping = TextWrapping.Wrap,
			Foreground = new SolidColorBrush(Color.FromRgb(137, 150, 168)),
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		DockPanel.SetDock(element, Dock.Top);
		dockPanel.Children.Add(element);
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		wrapPanel.Children.Add(Btn("保存并生效（Ctrl+S）", Save, primary: true));
		wrapPanel.Children.Add(Btn("保存并关闭", SaveAndClose, primary: true));
		wrapPanel.Children.Add(Btn("恢复内置名表", Reset));
		wrapPanel.Children.Add(Btn("用记事本打开", Note));
		wrapPanel.Children.Add(Btn("打开文件所在目录", OpenDir));
		wrapPanel.Children.Add(Btn("关闭", Close));
		DockPanel.SetDock(wrapPanel, Dock.Top);
		dockPanel.Children.Add(wrapPanel);
		_state.Text = TouhouApi.HeroNameFile;
		_state.Foreground = new SolidColorBrush(Color.FromRgb(102, 113, 127));
		_state.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		_state.TextWrapping = TextWrapping.Wrap;
		DockPanel.SetDock(_state, Dock.Bottom);
		dockPanel.Children.Add(_state);
		_box.AcceptsReturn = true;
		_box.AcceptsTab = true;
		_box.FontFamily = new FontFamily("Consolas, Microsoft YaHei UI");
		_box.FontSize = 12.5;
		_box.Background = new SolidColorBrush(Color.FromRgb(16, 22, 31));
		_box.Foreground = new SolidColorBrush(Color.FromRgb(221, 228, 238));
		_box.Padding = new Thickness(10.0, 8.0, 10.0, 8.0);
		_box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		_box.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
		try
		{
			if (Application.Current.TryFindResource("Tb") is Style tbStyle)
			{
				_box.Style = tbStyle;
			}
		}
		catch
		{
		}
		_box.Text = Load();
		dockPanel.Children.Add(_box);
		Content = dockPanel;
		PreviewKeyDown += (object s, KeyEventArgs e) =>
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Invalid comparison between Unknown and I4
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			if ((int)e.Key == 62 && ((int)Keyboard.Modifiers & 2) != 0)
			{
				Save();
				e.Handled = true;
			}
		};
		Loaded += (object s, RoutedEventArgs e) =>
		{
			ClampToScreen();
			try
			{
				_box.Focus();
				_box.CaretIndex = 0;
				_box.ScrollToHome();
			}
			catch
			{
			}
			_state.Text = TouhouApi.HeroNameFile + "   ——   按钮都在窗口顶部（保存并生效 / 保存并关闭 …）";
		};
	}

	private void ClampToScreen()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Rect workArea = SystemParameters.WorkArea;
			double num = (Height = Math.Min(Height, Math.Max(MinHeight, workArea.Height - 16.0)));
			if (Top + num > workArea.Bottom - 8.0)
			{
				Top = workArea.Bottom - num - 8.0;
			}
			if (Top < workArea.Top + 8.0)
			{
				Top = workArea.Top + 8.0;
			}
			if (Left + Width > workArea.Right - 8.0)
			{
				Left = workArea.Right - Width - 8.0;
			}
			if (Left < workArea.Left + 8.0)
			{
				Left = workArea.Left + 8.0;
			}
		}
		catch
		{
		}
	}

	private static Button Btn(string text, Action click, bool primary = false)
	{
		Button button = new Button
		{
			Content = text,
			Padding = new Thickness(14.0, 7.0, 14.0, 7.0),
			Margin = new Thickness(0.0, 0.0, 8.0, 6.0)
		};
		if (primary)
		{
			button.FontWeight = FontWeights.Bold;
		}
		try
		{
			string resourceKey = (primary ? "BtnPrimary" : "Btn");
			if (Application.Current.TryFindResource(resourceKey) is Style style)
			{
				button.Style = style;
			}
		}
		catch
		{
		}
		button.Click += (object s, RoutedEventArgs e) =>
		{
			try
			{
				click();
			}
			catch
			{
			}
		};
		return button;
	}

	private string Load()
	{
		try
		{
			string heroNameFile = TouhouApi.HeroNameFile;
			if (heroNameFile.Length == 0)
			{
				return "";
			}
			if (!File.Exists(heroNameFile))
			{
				TouhouApi.WriteHeroNameTemplate();
			}
			if (!File.Exists(heroNameFile))
			{
				TouhouApi.WriteHeroNameTemplate(overwrite: true);
			}
			return File.Exists(heroNameFile) ? File.ReadAllText(heroNameFile, Encoding.UTF8) : "";
		}
		catch (Exception ex)
		{
			return "# 读取失败：" + ex.Message;
		}
	}

	private void Save()
	{
		try
		{
			string heroNameFile = TouhouApi.HeroNameFile;
			if (heroNameFile.Length == 0)
			{
				_state.Text = "找不到数据目录，存不了。";
				return;
			}
			File.WriteAllText(heroNameFile, _box.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			_state.Text = "已保存：" + heroNameFile;
			if (_onSaved != null)
			{
				_onSaved();
			}
		}
		catch (Exception ex)
		{
			_state.Text = "保存失败：" + ex.Message;
		}
	}

	private void SaveAndClose()
	{
		Save();
		string text = _state.Text;
		if (text != null && text.StartsWith("已保存"))
		{
			try
			{
				Close();
			}
			catch
			{
			}
		}
	}

	private void Reset()
	{
		try
		{
			if (!TouhouApi.WriteHeroNameTemplate(overwrite: true))
			{
				_state.Text = "恢复失败，看看日志。";
				return;
			}
			_box.Text = Load();
			Save();
			_state.Text = "已恢复内置名表并保存。";
		}
		catch (Exception ex)
		{
			_state.Text = "恢复失败：" + ex.Message;
		}
	}

	private void Note()
	{
		try
		{
			string heroNameFile = TouhouApi.HeroNameFile;
			if (heroNameFile.Length == 0)
			{
				_state.Text = "没有文件可打开。";
				return;
			}
			Process.Start(new ProcessStartInfo("notepad.exe", "\"" + heroNameFile + "\"")
			{
				UseShellExecute = true
			});
			_state.Text = "已叫记事本打开（打不开就用上面的编辑框改，一样生效）。";
		}
		catch (Exception ex)
		{
			_state.Text = "记事本没拉起来：" + ex.Message + " —— 直接用上面的编辑框改就行。";
		}
	}

	private void OpenDir()
	{
		try
		{
			string heroNameFile = TouhouApi.HeroNameFile;
			if (heroNameFile.Length == 0)
			{
				_state.Text = "没有目录可打开。";
				return;
			}
			Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + heroNameFile + "\"")
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			_state.Text = "打开目录失败：" + ex.Message;
		}
	}
}
