using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sc2Toolbox;

public partial class App : System.Windows.Application
{
	private struct ICONINFO
	{
		public bool fIcon;

		public int xHotspot;

		public int yHotspot;

		public nint hbmMask;

		public nint hbmColor;
	}

	private struct BMPHDR
	{
		public uint biSize;

		public int biWidth;

		public int biHeight;

		public ushort biPlanes;

		public ushort biBitCount;

		public uint biCompression;

		public uint biSizeImage;

		public int biXPelsPerMeter;

		public int biYPelsPerMeter;

		public uint biClrUsed;

		public uint biClrImportant;
	}

	private struct BMPINFO
	{
		public BMPHDR hdr;

		public uint c0;

		public uint c1;

		public uint c2;
	}

	[Serializable]
	[CompilerGenerated]
	private sealed class _003C_003Ec
	{
		public static readonly _003C_003Ec _003C_003E9 = new _003C_003Ec();

		public static DispatcherUnhandledExceptionEventHandler _003C_003E9__29_0;

		public static RoutedEventHandler _003C_003E9__33_0;

		public static RoutedEventHandler _003C_003E9__36_2;

		internal void _003COnStartup_003Eb__29_0(object s, DispatcherUnhandledExceptionEventArgs a)
		{
			L.W("未处理异常: " + a.Exception);
			a.Handled = true;
		}

		internal void _003CHookWindowIcons_003Eb__33_0(object s, RoutedEventArgs e)
		{
			if (!(s is Window window))
			{
				return;
			}
			try
			{
				if (window.Icon == null)
				{
					ImageSource imageSource = AppIcon();
					if (imageSource != null)
					{
						window.Icon = imageSource;
					}
				}
			}
			catch
			{
			}
		}

		internal void _003CShowTrayMenu_003Eb__36_2(object _003Cp0_003E, RoutedEventArgs _003Cp1_003E)
		{
			if (Ctx.Keep.Running)
			{
				Ctx.Keep.PulseNow();
			}
		}
	}

	private const uint MOD_ALT = 1u;

	private const uint MOD_CONTROL = 2u;

	private const uint MOD_SHIFT = 4u;

	private const uint MOD_NOREPEAT = 16384u;

	private const int WM_HOTKEY = 786;

	private const uint VK_A = 65u;

	private const uint VK_T = 84u;

	private const uint VK_K = 75u;

	private static Mutex _mtx;

	private static ImageSource _appIcon;

	private static Icon _trayIcon;

	private TrayIcon _tray;

	private HwndSource _src;

	private bool _hkTest;

	private bool _hkOverlay;

	private bool _hkKeep;

	private uint _taskbarCreated;

	private bool _trayHintShown;

	internal static string IconPath
	{
		get
		{
			try
			{
				return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
			}
			catch
			{
				return "";
			}
		}
	}

	[DllImport("user32.dll")]
	private static extern bool RegisterHotKey(nint h, int id, uint mod, uint vk);

	[DllImport("user32.dll")]
	private static extern bool UnregisterHotKey(nint h, int id);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern uint RegisterWindowMessage(string name);

	[DllImport("gdi32.dll")]
	private static extern nint CreateDIBSection(nint hdc, ref BMPINFO pbmi, uint usage, out nint bits, nint section, uint offset);

	[DllImport("gdi32.dll")]
	private static extern nint CreateBitmap(int w, int h, uint planes, uint bpp, nint bits);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(nint o);

	[DllImport("user32.dll")]
	private static extern nint CreateIconIndirect(ref ICONINFO ii);

	private static Icon HIconFromBitmap(Bitmap bmp)
	{
		int width = bmp.Width;
		int height = bmp.Height;
		BMPINFO pbmi = new BMPINFO
		{
			hdr = 
			{
				biSize = (uint)Marshal.SizeOf(typeof(BMPHDR)),
				biWidth = width,
				biHeight = -height,
				biPlanes = 1,
				biBitCount = 32,
				biCompression = 0u
			}
		};
		nint num = CreateDIBSection(IntPtr.Zero, ref pbmi, 0u, out var bits, IntPtr.Zero, 0u);
		if (num == IntPtr.Zero || bits == IntPtr.Zero)
		{
			return null;
		}
		BitmapData bitmapData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		byte[] array = new byte[bitmapData.Stride * height];
		Marshal.Copy(bitmapData.Scan0, array, 0, array.Length);
		bmp.UnlockBits(bitmapData);
		Marshal.Copy(array, 0, bits, Math.Min(array.Length, width * height * 4));
		nint num2 = CreateBitmap(width, height, 1u, 1u, IntPtr.Zero);
		ICONINFO ii = new ICONINFO
		{
			fIcon = true,
			hbmColor = num,
			hbmMask = num2
		};
		nint num3 = CreateIconIndirect(ref ii);
		DeleteObject(num);
		DeleteObject(num2);
		if (num3 == IntPtr.Zero)
		{
			return null;
		}
		return Icon.FromHandle(num3);
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Expected Obj, but got Unknown
		base.OnStartup(e);
		bool flag = false;
		string[] args = e.Args;
		foreach (string text in args)
		{
			if (text == "-force" || text == "--force")
			{
				flag = true;
			}
		}
		L.W("启动尝试：pid=" + Environment.ProcessId + " 管理员=" + Admin.IsElevated + " 参数=[" + string.Join(" ", e.Args) + "]");
		_mtx = new Mutex(initiallyOwned: true, "SC-touhoubox_SingleInstance_v1", out var createdNew);
		L.W("单实例检查：created=" + createdNew + (flag ? "（-force：忽略已有实例）" : ""));
		if (!createdNew && !flag)
		{
			System.Windows.MessageBox.Show("SC-touhoubox已经在运行了（看右下角托盘图标）。", "SC-touhoubox", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			Shutdown();
			return;
		}
		DispatcherUnhandledExceptionEventHandler val = _003C_003Ec._003C_003E9__29_0;
		if (val == null)
		{
			DispatcherUnhandledExceptionEventHandler val2 = (object s, DispatcherUnhandledExceptionEventArgs a) =>
			{
				L.W("未处理异常: " + a.Exception);
				a.Handled = true;
			};
			_003C_003Ec._003C_003E9__29_0 = val2;
			val = val2;
		}
		DispatcherUnhandledException += val;
		L.W("================ SC-touhoubox启动 ================");
		HookWindowIcons();
		L.W("日志目录：" + L.Dir + ((L.LastError.Length > 0) ? ("  失败记录：" + L.LastError) : ""));
		L.ImportFromOtherDirs("settings.json", "cache.json");
		Ctx.S = Settings.Load();
		L.W("设置已载入：" + Settings.FilePath);
		Ctx.Main = new MainWindow();
		Ctx.Main.Title = "SC-touhoubox · 东方月灵传助手";
		Ctx.Main.Show();
		if (L.LastError.Length > 0)
		{
			L.W("注意：有路径写入失败 → " + L.LastError);
		}
		Ctx.Overlay = new OverlayWindow();
		if (Ctx.S.OverlayEnabled)
		{
			Ctx.Overlay.ShowOverlay();
		}
		Ctx.Watcher.Start();
		if (Ctx.S.KeepAliveEnabled)
		{
			Ctx.Keep.Start();
		}
		SetupTray();
		RegisterHotkeys();
	}

	internal static ImageSource AppIcon()
	{
		if (_appIcon != null)
		{
			return _appIcon;
		}
		try
		{
			string iconPath = IconPath;
			if (iconPath.Length > 0 && File.Exists(iconPath))
			{
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.UriSource = new Uri(iconPath);
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
				((Freezable)bitmapImage).Freeze();
				_appIcon = bitmapImage;
			}
		}
		catch
		{
		}
		return _appIcon;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

	/// <summary>给仍带系统标题栏的窗口套上深色标题栏，避免白条割裂。</summary>
	internal static void ApplyDarkTitleBar(Window window)
	{
		try
		{
			if (window == null)
			{
				return;
			}
			IntPtr handle = new WindowInteropHelper(window).Handle;
			if (handle == IntPtr.Zero)
			{
				return;
			}
			if (window.WindowStyle == WindowStyle.None)
			{
				return;
			}
			int value = 1;
			DwmSetWindowAttribute(handle, 20, ref value, 4);
			int caption = 0x00140E0A;
			DwmSetWindowAttribute(handle, 35, ref caption, 4);
			int text = 0x00F8EFE9;
			DwmSetWindowAttribute(handle, 36, ref text, 4);
			int border = 0x001F2A36;
			DwmSetWindowAttribute(handle, 34, ref border, 4);
		}
		catch
		{
		}
	}

	private static void HookWindowIcons()
	{
		EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, (RoutedEventHandler)((object s, RoutedEventArgs e) =>
		{
			if (!(s is Window window))
			{
				return;
			}
			try
			{
				if (window.Icon == null)
				{
					ImageSource imageSource = AppIcon();
					if (imageSource != null)
					{
						window.Icon = imageSource;
					}
				}
			}
			catch
			{
			}
			try
			{
				ApplyDarkTitleBar(window);
			}
			catch
			{
			}
		}));
	}

	private static Icon LoadTrayIcon()
	{
		try
		{
			string iconPath = IconPath;
			if (iconPath.Length > 0 && File.Exists(iconPath))
			{
				int num = SystemInformation.SmallIconSize.Width;
				if (num < 16)
				{
					num = 16;
				}
				if (num > 64)
				{
					num = 64;
				}
				try
				{
					Icon icon = new Icon(iconPath, num, num);
					if (icon != null && icon.Handle != IntPtr.Zero)
					{
						_trayIcon = icon;
						L.W("托盘图标：icon.ico 的 " + num + "x" + num + " 帧（HICON=0x" + ((IntPtr)icon.Handle).ToInt64().ToString("X") + "）→ " + iconPath);
						return icon;
					}
				}
				catch (Exception ex)
				{
					L.W("托盘图标：直接取 ico 帧失败（" + ex.Message + "），改自造 HICON");
				}
				try
				{
					using Icon icon2 = new Icon(iconPath, num, num);
					using Bitmap bmp = icon2.ToBitmap();
					Icon icon3 = HIconFromBitmap(bmp);
					if (icon3 != null)
					{
						_trayIcon = icon3;
						L.W("托盘图标：自造 HICON " + icon3.Width + "x" + icon3.Height + "（取自 icon.ico 的 " + num + "x" + num + " 帧）→ " + iconPath);
						return icon3;
					}
				}
				catch (Exception ex2)
				{
					L.W("托盘图标：自造 HICON 失败（" + ex2.Message + "），退回直接用 ico 帧");
				}
				_trayIcon = new Icon(iconPath, num, num);
				L.W("托盘图标：用 icon.ico 的 " + num + "x" + num + " 帧 → " + iconPath);
				return _trayIcon;
			}
			L.W("托盘图标：旁边没有 icon.ico（" + iconPath + "），改用 exe 内嵌图标");
		}
		catch (Exception ex3)
		{
			L.W("托盘图标：icon.ico 取帧失败（" + ex3.Message + "），改用 exe 内嵌图标");
		}
		try
		{
			string fileName = Process.GetCurrentProcess().MainModule.FileName;
			Icon icon4 = Icon.ExtractAssociatedIcon(fileName);
			if (icon4 != null)
			{
				_trayIcon = icon4;
				L.W("托盘图标：用 exe 内嵌图标 " + icon4.Width + "x" + icon4.Height + " → " + fileName);
				return icon4;
			}
		}
		catch (Exception ex4)
		{
			L.W("托盘图标：exe 取图标也失败（" + ex4.Message + "）");
		}
		L.W("托盘图标：退回系统默认盾牌图标");
		_trayIcon = SystemIcons.Shield;
		return _trayIcon;
	}

	private void SetupTray()
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Expected Obj, but got Unknown
		try
		{
			if (_tray != null)
			{
				try
				{
					_tray.Dispose();
				}
				catch
				{
				}
				_tray = null;
			}
			_tray = new TrayIcon(LoadTrayIcon(), "SC-touhoubox · 房间悬浮窗 / 挂机等房 / 开局警报");
			_tray.LeftClick = () =>
			{
				ShowPanel();
			};
			_tray.DoubleClick = () =>
			{
				ShowPanel();
			};
			_tray.RightClick = () =>
			{
				ShowTrayMenu();
			};
			bool flag = _tray.Add();
			L.W("托盘图标（自己调 Shell_NotifyIcon）：" + _tray.LastResult);
			if (flag)
			{
				return;
			}
			DispatcherTimer t = new DispatcherTimer();
			t.Interval = TimeSpan.FromSeconds(2.5);
			t.Tick += delegate
			{
				t.Stop();
				if (_tray != null && !_tray.Added)
				{
					_tray.Add();
					L.W("托盘图标重试：" + _tray.LastResult);
				}
			};
			t.Start();
		}
		catch (Exception ex)
		{
			L.W("托盘图标创建失败: " + ex.Message);
		}
	}

	private void ShowTrayMenu()
	{
		try
		{
			ContextMenu contextMenu = new ContextMenu();
			MenuItem menuItem = new MenuItem
			{
				Header = "打开面板"
			};
			menuItem.Click += delegate
			{
				ShowPanel();
			};
			MenuItem menuItem2 = new MenuItem
			{
				Header = "显示 / 隐藏 房间悬浮窗"
			};
			menuItem2.Click += delegate
			{
				ToggleOverlay();
			};
			MenuItem menuItem3 = new MenuItem
			{
				Header = "立即等房一次"
			};
			menuItem3.Click += delegate
			{
				if (Ctx.Keep.Running)
				{
					Ctx.Keep.PulseNow();
				}
			};
			MenuItem menuItem4 = new MenuItem
			{
				Header = "退出"
			};
			menuItem4.Click += delegate
			{
				QuitApp();
			};
			contextMenu.Items.Add(menuItem);
			contextMenu.Items.Add(menuItem2);
			contextMenu.Items.Add(menuItem3);
			contextMenu.Items.Add(new Separator());
			contextMenu.Items.Add(menuItem4);
			contextMenu.Placement = PlacementMode.MousePoint;
			contextMenu.IsOpen = true;
		}
		catch (Exception ex)
		{
			L.W("托盘菜单打开失败: " + ex.Message);
		}
	}

	public void RestoreTray()
	{
		if (_tray == null)
		{
			SetupTray();
			return;
		}
		_tray.Remove();
		_tray.Add();
		L.W("托盘图标手动重挂：" + _tray.LastResult);
	}

	public void ShowTrayHint()
	{
		if (_trayHintShown)
		{
			return;
		}
		_trayHintShown = true;
		try
		{
			if (_tray != null)
			{
				_tray.ShowBalloon("SC-touhoubox 还在后台运行", "面板只是收起来了：点托盘图标可以再打开，右键托盘图标 → 退出 才真的关掉。\n（这个提示可以在设置里关掉）");
			}
		}
		catch
		{
		}
	}

	public void QuitFromUi()
	{
		QuitApp();
	}

	private void RegisterHotkeys()
	{
		try
		{
			nint handle = new WindowInteropHelper(Ctx.Main).Handle;
			_src = HwndSource.FromHwnd(handle);
			if (_src != null)
			{
				_src.AddHook(WndProc);
			}
			_hkTest = RegisterHotKey(handle, 1, 16391u, 65u);
			_hkOverlay = RegisterHotKey(handle, 2, 16391u, 84u);
			_hkKeep = RegisterHotKey(handle, 3, 16391u, 75u);
			_taskbarCreated = RegisterWindowMessage("TaskbarCreated");
			L.W("热键：" + (_hkTest ? "Ctrl+Alt+Shift+A=试响/关闭警报" : "Ctrl+Alt+Shift+A=注册失败（被占用）") + "，" + (_hkOverlay ? "Ctrl+Alt+Shift+T=显示/隐藏悬浮窗" : "Ctrl+Alt+Shift+T=注册失败（被占用）") + "，" + (_hkKeep ? "Ctrl+Alt+Shift+K=保活开关" : "Ctrl+Alt+Shift+K=注册失败（被占用）"));
		}
		catch (Exception ex)
		{
			L.W("注册热键失败: " + ex.Message);
		}
	}

	private nint WndProc(nint hwnd, int msg, nint w, nint l, ref bool handled)
	{
		if (_taskbarCreated != 0 && msg == (int)_taskbarCreated)
		{
			RestoreTray();
			handled = true;
			return IntPtr.Zero;
		}
		if (msg == 786)
		{
			switch (((IntPtr)w).ToInt32())
			{
			case 1:
				if (AlertWindow.IsShowing())
				{
					AlertWindow.HideIt();
					L.W("热键：关闭警报");
				}
				else
				{
					Ctx.Alerts.Test();
				}
				handled = true;
				break;
			case 2:
				ToggleOverlay();
				handled = true;
				break;
			case 3:
				if (Ctx.Keep.Running)
				{
					Ctx.S.KeepAliveEnabled = false;
					Ctx.Keep.Stop();
				}
				else
				{
					Ctx.S.KeepAliveEnabled = true;
					Ctx.Keep.Start();
				}
				Ctx.S.Save();
				ShowPanel();
				Ctx.Main.SyncFromSettings();
				Ctx.Main.RefreshKeepStatus();
				handled = true;
				break;
			}
		}
		return IntPtr.Zero;
	}

	private void ShowPanel()
	{
		try
		{
			if (Ctx.Main != null)
			{
				Ctx.Main.Show();
				if (Ctx.Main.WindowState == WindowState.Minimized)
				{
					Ctx.Main.WindowState = WindowState.Normal;
				}
				Ctx.Main.Activate();
			}
		}
		catch
		{
		}
	}

	private void ToggleOverlay()
	{
		try
		{
			OverlayWindow overlay = Ctx.Overlay;
			if (overlay != null)
			{
				bool flag = !overlay.IsVisible;
				Ctx.S.OverlayEnabled = flag;
				Ctx.S.Save();
				if (flag)
				{
					overlay.ShowOverlay();
				}
				else
				{
					overlay.Hide();
				}
				if (Ctx.Main != null)
				{
					Ctx.Main.SyncFromSettings();
				}
				L.W("悬浮窗：" + (flag ? "显示" : "隐藏"));
			}
		}
		catch
		{
		}
	}

	private void QuitApp()
	{
		try
		{
			if (Ctx.Overlay != null)
			{
				Ctx.Overlay.SavePos();
				Ctx.Overlay.Hide();
			}
			Ctx.Keep.Stop();
			Ctx.S.Save();
			if (_tray != null)
			{
				try
				{
					_tray.Dispose();
				}
				catch
				{
				}
				_tray = null;
			}
			if (Ctx.Main != null)
			{
				Ctx.Main.RealClose();
			}
		}
		catch
		{
		}
		L.W("============ SC-touhoubox退出 ============");
		Shutdown();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		try
		{
			if (_src != null)
			{
				nint handle = new WindowInteropHelper(Ctx.Main).Handle;
				if (_hkTest)
				{
					UnregisterHotKey(handle, 1);
				}
				if (_hkOverlay)
				{
					UnregisterHotKey(handle, 2);
				}
				if (_hkKeep)
				{
					UnregisterHotKey(handle, 3);
				}
				_src.RemoveHook(WndProc);
			}
		}
		catch
		{
		}
		try
		{
			if (_tray != null)
			{
				_tray.Dispose();
			}
		}
		catch
		{
		}
		try
		{
			Ctx.Keep.Stop();
		}
		catch
		{
		}
		try
		{
			Ctx.S.Save();
		}
		catch
		{
		}
		base.OnExit(e);
	}
}
