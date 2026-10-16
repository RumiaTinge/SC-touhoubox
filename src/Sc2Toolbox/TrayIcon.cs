using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Sc2Toolbox;

internal sealed class TrayIcon : IDisposable
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct NOTIFYICONDATA
	{
		public uint cbSize;

		public nint hWnd;

		public uint uID;

		public uint uFlags;

		public uint uCallbackMessage;

		public nint hIcon;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string szTip;

		public uint dwState;

		public uint dwStateMask;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string szInfo;

		public uint uTimeoutOrVersion;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
		public string szInfoTitle;

		public uint dwInfoFlags;

		public Guid guidItem;

		public nint hBalloonIcon;
	}

	private struct NIDID
	{
		public uint cbSize;

		public nint hWnd;

		public uint uID;

		public Guid guidItem;
	}

	private struct RECT
	{
		public int L;

		public int T;

		public int R;

		public int B;
	}

	private const uint NIM_ADD = 0u;

	private const uint NIM_MODIFY = 1u;

	private const uint NIM_DELETE = 2u;

	private const uint NIM_SETVERSION = 4u;

	private const uint NIF_MESSAGE = 1u;

	private const uint NIF_ICON = 2u;

	private const uint NIF_TIP = 4u;

	private const uint NIF_INFO = 16u;

	private const uint NIF_SHOWTIP = 128u;

	private const uint WM_APP = 32768u;

	public const uint WM_TRAY = 32769u;

	private const uint WM_LBUTTONUP = 514u;

	private const uint WM_LBUTTONDBLCLK = 515u;

	private const uint WM_RBUTTONUP = 517u;

	private const uint NIIF_INFO = 1u;

	private readonly HwndSource _src;

	private readonly Icon _icon;

	private readonly uint _id = 1u;

	private readonly uint _taskbarCreated;

	private readonly string _tip;

	public Action LeftClick;

	public Action DoubleClick;

	public Action RightClick;

	public bool Added { get; private set; }

	public string LastResult { get; private set; }

	public nint Handle
	{
		get
		{
			if (_src != null)
			{
				return _src.Handle;
			}
			return IntPtr.Zero;
		}
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA d);

	[DllImport("shell32.dll")]
	private static extern int Shell_NotifyIconGetRect(ref NIDID id, out RECT rc);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern uint RegisterWindowMessage(string name);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern nint CreateWindowExW(uint ex, string cls, string name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint p);

	[DllImport("user32.dll")]
	private static extern bool DestroyWindow(nint h);

	public TrayIcon(Icon icon, string tip)
	{
		LastResult = "";
		_icon = icon;
		_tip = tip ?? "";
		HwndSourceParameters parameters = new HwndSourceParameters("Sc2TouhouboxTray")
		{
			WindowStyle = int.MinValue,
			Width = 1,
			Height = 1,
			PositionX = -30000,
			PositionY = -30000
		};
		_src = new HwndSource(parameters);
		_src.AddHook(WndProc);
		_taskbarCreated = RegisterWindowMessage("TaskbarCreated");
	}

	public bool Add()
	{
		try
		{
			bool flag = TryAdd(_src.Handle, out var diag);
			LastResult = "结构体 " + Marshal.SizeOf(typeof(NOTIFYICONDATA)) + " 字节；WPF 窗口(hWnd=0x" + ((IntPtr)_src.Handle).ToInt64().ToString("X") + ")： " + diag;
			if (!flag)
			{
				nint num = CreateWindowExW(0u, "STATIC", "Sc2TouhouboxTrayRaw", 0u, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
				if (num != IntPtr.Zero)
				{
					TryAdd(num, out var diag2);
					LastResult = LastResult + "；自造消息窗口(hWnd=0x" + ((IntPtr)num).ToInt64().ToString("X") + ")： " + diag2;
					NOTIFYICONDATA d = new NOTIFYICONDATA
					{
						cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
						hWnd = num,
						uID = _id
					};
					Shell_NotifyIcon(2u, ref d);
					DestroyWindow(num);
				}
				else
				{
					LastResult = LastResult + "；自造消息窗口失败（Win32 错误 " + Marshal.GetLastWin32Error() + "）";
				}
			}
			Added = flag;
			return flag;
		}
		catch (Exception ex)
		{
			LastResult = "挂托盘图标异常: " + ex.Message;
			return false;
		}
	}

	private bool TryAdd(nint hwnd, out string diag)
	{
		NOTIFYICONDATA d = new NOTIFYICONDATA
		{
			cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
			hWnd = hwnd,
			uID = _id,
			uFlags = 135u,
			uCallbackMessage = 32769u,
			hIcon = ((_icon == null) ? IntPtr.Zero : _icon.Handle),
			szTip = ((_tip.Length > 120) ? _tip.Substring(0, 120) : _tip)
		};
		Marshal.SetLastPInvokeError(0);
		bool flag = Shell_NotifyIcon(0u, ref d);
		int lastPInvokeError = Marshal.GetLastPInvokeError();
		if (flag)
		{
			NOTIFYICONDATA d2 = d;
			d2.uTimeoutOrVersion = 4u;
			Shell_NotifyIcon(4u, ref d2);
			diag = "NIM_ADD 成功(err=" + lastPInvokeError + ")，hIcon=0x" + ((IntPtr)d.hIcon).ToInt64().ToString("X") + "，" + Where(hwnd);
		}
		else
		{
			diag = "NIM_ADD 返回 false，Win32 错误 " + lastPInvokeError + "（0x" + lastPInvokeError.ToString("X8") + "），hIcon=0x" + ((IntPtr)d.hIcon).ToInt64().ToString("X");
		}
		return flag;
	}

	public string Where()
	{
		return Where(_src.Handle);
	}

	public string Where(nint hwnd)
	{
		try
		{
			NIDID id = new NIDID
			{
				cbSize = (uint)Marshal.SizeOf(typeof(NIDID)),
				hWnd = hwnd,
				uID = _id
			};
			int num = Shell_NotifyIconGetRect(ref id, out var rc);
			if (num == 0)
			{
				return "外壳认得它，位置 " + rc.L + "," + rc.T + " " + (rc.R - rc.L) + "x" + (rc.B - rc.T) + ((rc.T < 0 || rc.L < 0) ? "（负坐标 = 收在溢出面板里）" : "");
			}
			return "外壳查不到它（0x" + num.ToString("X8") + "）";
		}
		catch (Exception ex)
		{
			return "自查失败: " + ex.Message;
		}
	}

	public void ShowBalloon(string title, string text)
	{
		try
		{
			NOTIFYICONDATA d = new NOTIFYICONDATA
			{
				cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
				hWnd = _src.Handle,
				uID = _id,
				uFlags = 16u,
				szInfoTitle = (title ?? ""),
				szInfo = (text ?? ""),
				dwInfoFlags = 1u
			};
			Shell_NotifyIcon(1u, ref d);
		}
		catch
		{
		}
	}

	public void Remove()
	{
		try
		{
			NOTIFYICONDATA d = new NOTIFYICONDATA
			{
				cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
				hWnd = _src.Handle,
				uID = _id
			};
			Shell_NotifyIcon(2u, ref d);
			Added = false;
		}
		catch
		{
		}
	}

	private nint WndProc(nint hwnd, int msg, nint w, nint l, ref bool handled)
	{
		if (_taskbarCreated != 0 && msg == (int)_taskbarCreated)
		{
			Added = false;
			Add();
			handled = true;
			return IntPtr.Zero;
		}
		if (msg == 32769)
		{
			uint num = (uint)(((IntPtr)l).ToInt64() & 0xFFFF);
			try
			{
				switch (num)
				{
				case 514u:
					if (LeftClick != null)
					{
						LeftClick();
					}
					break;
				case 515u:
					if (DoubleClick != null)
					{
						DoubleClick();
					}
					break;
				case 517u:
					if (RightClick != null)
					{
						RightClick();
					}
					break;
				}
			}
			catch
			{
			}
			handled = true;
			return IntPtr.Zero;
		}
		return IntPtr.Zero;
	}

	public void Dispose()
	{
		try
		{
			Remove();
		}
		catch
		{
		}
		try
		{
			if (_src != null)
			{
				_src.RemoveHook(WndProc);
				_src.Dispose();
			}
		}
		catch
		{
		}
	}
}
