using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Sc2Toolbox;

public class ToastWindow : Window
{
	private static ToastWindow _cur;

	private static DispatcherTimer _tm;

	public static void ShowText(string text)
	{
		ShowText(text, "righttop", 4);
	}

	public static void ShowText(string text, string corner, int seconds)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected Obj, but got Unknown
		try
		{
			if (_cur != null)
			{
				_cur.Close();
				_cur = null;
			}
		}
		catch
		{
		}
		ToastWindow toastWindow = (_cur = new ToastWindow(text));
		toastWindow.Show();
		try
		{
			toastWindow.UpdateLayout();
		}
		catch
		{
		}
		Place(toastWindow, corner);
		if (_tm != null)
		{
			_tm.Stop();
		}
		_tm = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(Math.Max(1.5, seconds))
		};
		_tm.Tick += delegate
		{
			_tm.Stop();
			try
			{
				if (_cur != null)
				{
					_cur.Close();
					_cur = null;
				}
			}
			catch
			{
			}
		};
		_tm.Start();
	}

	public static void Place(Window w, string corner)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Rect workArea = SystemParameters.WorkArea;
			double num = ((w.ActualWidth > 0.0) ? w.ActualWidth : 360.0);
			double num2 = ((w.ActualHeight > 0.0) ? w.ActualHeight : 80.0);
			double num3 = 18.0;
			bool flag = corner?.StartsWith("left") ?? false;
			bool flag2 = corner?.EndsWith("bottom") ?? false;
			w.Left = (flag ? (workArea.Left + num3) : (workArea.Right - num - num3));
			w.Top = (flag2 ? (workArea.Bottom - num2 - num3) : (workArea.Top + num3));
		}
		catch
		{
		}
	}

	private ToastWindow(string text)
	{
		WindowStyle = WindowStyle.None;
		AllowsTransparency = true;
		Background = Brushes.Transparent;
		Topmost = true;
		ShowInTaskbar = false;
		ShowActivated = false;
		SizeToContent = SizeToContent.WidthAndHeight;
		Opacity = 0.0;
		base.FontFamily = new FontFamily("Microsoft YaHei UI");
		Border border = new Border
		{
			CornerRadius = new CornerRadius(14.0),
			Background = new LinearGradientBrush(new GradientStopCollection
			{
				new GradientStop(Color.FromArgb(byte.MaxValue, 27, 35, 49), 0.0),
				new GradientStop(Color.FromArgb(byte.MaxValue, 13, 19, 28), 1.0)
			}, new Point(0.0, 0.0), new Point(0.5, 1.0)),
			BorderBrush = new SolidColorBrush(Color.FromArgb(38, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
			BorderThickness = new Thickness(1.0),
			Effect = new DropShadowEffect
			{
				BlurRadius = 26.0,
				ShadowDepth = 6.0,
				Opacity = 0.62,
				Direction = 270.0
			}
		};
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(3.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		Border accent = new Border
		{
			CornerRadius = new CornerRadius(13.0, 0.0, 0.0, 13.0),
			Background = new LinearGradientBrush(Color.FromRgb(76, 141, 255), Color.FromRgb(122, 184, 255), 90.0)
		};
		Grid.SetColumn(accent, 0);
		grid.Children.Add(accent);
		TextBlock textBlock = new TextBlock
		{
			Text = text,
			Foreground = new SolidColorBrush(Color.FromRgb(233, 239, 248)),
			FontSize = 13.0,
			LineHeight = 19.0,
			TextWrapping = TextWrapping.Wrap,
			MaxWidth = 380.0,
			Margin = new Thickness(15.0, 12.0, 17.0, 12.0)
		};
		Grid.SetColumn(textBlock, 1);
		grid.Children.Add(textBlock);
		border.Child = grid;
		Content = border;
		base.Loaded += delegate
		{
			BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(170.0))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				}
			});
		};
	}
}
