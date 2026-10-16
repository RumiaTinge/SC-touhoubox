using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sc2Toolbox;

public partial class AlertWindow : Window, IComponentConnector
{
	private static readonly nint HWND_TOPMOST = new IntPtr(-1);

	private const uint SWP_NOSIZE = 1u;

	private const uint SWP_NOMOVE = 2u;

	private const uint SWP_NOACTIVATE = 16u;

	private const uint SWP_NOOWNERZORDER = 512u;

	private static AlertWindow _cur;

	private DispatcherTimer _hide;

	private DispatcherTimer _top;

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(nint h, nint after, int x, int y, int cx, int cy, uint flags);

	public AlertWindow()
	{
		InitializeComponent();
	}

	private void KeepTopmost()
	{
		try
		{
			if (IsVisible)
			{
				nint handle = new WindowInteropHelper(this).Handle;
				if (handle != IntPtr.Zero)
				{
					SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, 531u);
				}
			}
		}
		catch
		{
		}
	}

	private void Close2()
	{
		if (_hide != null)
		{
			_hide.Stop();
		}
		if (_top != null)
		{
			_top.Stop();
		}
		Hide();
	}

	public static void ShowIt(string title, string body)
	{
		if (_cur == null)
		{
			_cur = new AlertWindow();
		}
		_cur.Pop(title, body);
	}

	public static void HideIt()
	{
		try
		{
			if (_cur != null)
			{
				_cur.Hide();
			}
		}
		catch
		{
		}
	}

	public static bool IsShowing()
	{
		try
		{
			return _cur != null && _cur.IsVisible;
		}
		catch
		{
			return false;
		}
	}

	private void Pop(string title, string body)
	{
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Expected Obj, but got Unknown
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Expected Obj, but got Unknown
		Settings s = Ctx.S;
		TxtTitle.Text = title;
		TxtBody.Text = body;
		bool flag = false;
		if (s.AlertShowImage && !string.IsNullOrWhiteSpace(s.AlertImage) && File.Exists(s.AlertImage))
		{
			try
			{
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.UriSource = new Uri(s.AlertImage);
				bitmapImage.EndInit();
				Img.Source = bitmapImage;
				Img.Visibility = Visibility.Visible;
				flag = true;
			}
			catch (Exception ex)
			{
				L.W("加载警报图片失败: " + ex.Message);
			}
		}
		if (!flag)
		{
			Img.Source = null;
			Img.Visibility = Visibility.Collapsed;
		}
		try
		{
			nint num = Sc2.Find();
			Rectangle bounds = ((num != IntPtr.Zero) ? Screen.FromHandle(num) : Screen.PrimaryScreen).Bounds;
			double num2 = 1.0;
			try
			{
				num2 = VisualTreeHelper.GetDpi(this).DpiScaleX;
			}
			catch
			{
			}
			if (num2 <= 0.1)
			{
				num2 = 1.0;
			}
			Left = (double)bounds.Left / num2;
			Top = (double)bounds.Top / num2;
			Width = (double)bounds.Width / num2;
			Height = (double)bounds.Height / num2;
		}
		catch
		{
		}
		if (!IsVisible)
		{
			Show();
		}
		Topmost = false;
		Topmost = true;
		KeepTopmost();
		if (_top == null)
		{
			_top = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1L)
			};
			_top.Tick += delegate
			{
				KeepTopmost();
			};
		}
		_top.Start();
		if (_hide != null)
		{
			_hide.Stop();
		}
		if (s.AlertAutoHideSec > 0)
		{
			_hide = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(s.AlertAutoHideSec)
			};
			_hide.Tick += delegate
			{
				Close2();
			};
			_hide.Start();
		}
		L.W("警报横幅已显示：" + title + "（" + ((s.AlertAutoHideSec > 0) ? (s.AlertAutoHideSec + " 秒后自动隐藏") : "手动关闭") + "）");
	}

	private void Win_MouseDown(object sender, MouseButtonEventArgs e)
	{
		Close2();
	}
}
