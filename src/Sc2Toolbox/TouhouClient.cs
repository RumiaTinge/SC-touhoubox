using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Sc2Toolbox;

/// <summary>
/// 内置 touhou_client（房间接口）的安装、启动和退出联动。
/// 只管理本程序安装到 LocalAppData 的副本；用户自己装到 ProgramData 的版本不启动、不结束。
/// </summary>
internal static class TouhouClient
{
	public static string ManagedDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SC-touhoubox", "client");

	public static string ManagedExe { get; } = Path.Combine(ManagedDir, "touhou_client.exe");

	public static string BundledExe { get; } = Path.Combine(AppContext.BaseDirectory, "client", "touhou_client.exe");

	public static string ExternalDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "touhou");

	public static string ExternalExe { get; } = Path.Combine(ExternalDir, "touhou_client.exe");

	public static string LastError { get; private set; } = "";

	public static bool ManagedInstalled => File.Exists(ManagedExe);

	public static bool ExternalInstalled => File.Exists(ExternalExe);

	public static bool BundledAvailable => File.Exists(BundledExe);

	private static Process? _proc;

	private static readonly object _lk = new();

	public static bool AnyRunning => FindAll().Count > 0;

	public static bool ManagedRunning
	{
		get
		{
			lock (_lk)
			{
				if (_proc != null)
				{
					try
					{
						if (!_proc.HasExited)
						{
							return true;
						}
					}
					catch
					{
					}
				}
				foreach (Process p in FindAll())
				{
					if (SamePath(p, ManagedExe))
					{
						return true;
					}
				}
				return false;
			}
		}
	}

	public static List<Process> FindAll()
	{
		List<Process> list = new();
		try
		{
			foreach (Process p in Process.GetProcessesByName("touhou_client"))
			{
				list.Add(p);
			}
		}
		catch
		{
		}
		return list;
	}

	public static bool Install(out string error)
	{
		lock (_lk)
		{
			try
			{
				if (ManagedInstalled)
				{
					error = "";
					return true;
				}
				if (!BundledAvailable)
				{
					error = "安装包内缺少 client\\touhou_client.exe。";
					LastError = error;
					return false;
				}
				Directory.CreateDirectory(ManagedDir);
				File.Copy(BundledExe, ManagedExe, overwrite: true);
				if (!ManagedInstalled)
				{
					error = "复制后仍找不到 " + ManagedExe;
					LastError = error;
					return false;
				}
				L.W("touhou_client 已安装：" + ManagedExe);
				error = "";
				return true;
			}
			catch (Exception ex)
			{
				error = ex.Message;
				LastError = error;
				L.W("touhou_client 安装失败：" + ex);
				return false;
			}
		}
	}

	public static bool StartManaged(out string error)
	{
		lock (_lk)
		{
			try
			{
				if (!ManagedInstalled)
				{
					error = "还没有安装内置的 touhou_client。";
					LastError = error;
					return false;
				}
				if (AnyRunning)
				{
					error = "touhou_client 已经在运行。";
					LastError = error;
					return false;
				}
				ProcessStartInfo info = new()
				{
					FileName = ManagedExe,
					WorkingDirectory = ManagedDir,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				Process? p = Process.Start(info);
				if (p == null)
				{
					error = "无法启动 touhou_client。";
					LastError = error;
					return false;
				}
				_proc = p;
				Thread.Sleep(450);
				try
				{
					if (p.HasExited && !PortAlive(8030))
					{
						error = "touhou_client 启动后立即退出（代码 " + p.ExitCode + "）。";
						LastError = error;
						_proc = null;
						return false;
					}
				}
				catch
				{
				}
				L.W("touhou_client 已启动（随本程序）：pid=" + p.Id);
				error = "";
				return true;
			}
			catch (Exception ex)
			{
				error = ex.Message;
				LastError = error;
				L.W("touhou_client 启动失败：" + ex);
				return false;
			}
		}
	}

	public static bool StartExternal(out string error)
	{
		try
		{
			if (!ExternalInstalled)
			{
				error = "没有找到外部安装的 touhou_client。";
				LastError = error;
				return false;
			}
			if (AnyRunning)
			{
				error = "touhou_client 已经在运行。";
				LastError = error;
				return false;
			}
			ProcessStartInfo info = new()
			{
				FileName = ExternalExe,
				WorkingDirectory = ExternalDir,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			Process.Start(info);
			Thread.Sleep(350);
			L.W("touhou_client 已启动（外部安装，不随本程序退出）。");
			error = "";
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			LastError = error;
			return false;
		}
	}

	public static bool StopManaged(out string error)
	{
		lock (_lk)
		{
			try
			{
				if (!ManagedInstalled)
				{
					error = "还没有安装内置的 touhou_client。";
					LastError = error;
					return false;
				}
				Process? target = _proc;
				if (target == null || SafeHasExited(target))
				{
					target = FirstManaged();
				}
				if (target == null)
				{
					error = "内置的 touhou_client 没有在运行。";
					LastError = error;
					return false;
				}
				target.Kill(entireProcessTree: true);
				target.WaitForExit(2500);
				_proc = null;
				L.W("touhou_client 已停止。");
				error = "";
				return true;
			}
			catch (Exception ex)
			{
				error = ex.Message;
				LastError = error;
				L.W("touhou_client 停止失败：" + ex);
				return false;
			}
		}
	}

	/// <summary>随本程序退出：只结束托管副本，外部安装的进程不动。</summary>
	public static void Shutdown()
	{
		lock (_lk)
		{
			try
			{
				Process? target = _proc;
				if (target != null && SafeHasExited(target))
				{
					target = null;
				}
				target ??= FirstManaged();
				_proc = null;
				if (target == null)
				{
					return;
				}
				target.Kill(entireProcessTree: true);
				target.WaitForExit(2500);
				L.W("touhou_client 已随程序退出。");
			}
			catch (Exception ex)
			{
				L.W("touhou_client 退出清理失败：" + ex.Message);
			}
		}
	}

	/// <summary>启动时：内置版本已安装且当前没有 client 在跑，就自动拉起。</summary>
	public static void Boot()
	{
		try
		{
			if (!ManagedInstalled || AnyRunning)
			{
				return;
			}
			if (!StartManaged(out string error))
			{
				LastError = error;
			}
		}
		catch (Exception ex)
		{
			LastError = ex.Message;
		}
	}

	private static Process? FirstManaged()
	{
		foreach (Process p in FindAll())
		{
			if (SamePath(p, ManagedExe))
			{
				return p;
			}
		}
		return null;
	}

	private static bool SamePath(Process p, string path)
	{
		try
		{
			string? fileName = p.MainModule?.FileName;
			if (string.IsNullOrEmpty(fileName))
			{
				return false;
			}
			return string.Equals(Path.GetFullPath(fileName), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static bool SafeHasExited(Process p)
	{
		try
		{
			return p.HasExited;
		}
		catch
		{
			return true;
		}
	}

	private static bool PortAlive(int port)
	{
		try
		{
			using TcpClient client = new();
			IAsyncResult result = client.BeginConnect(IPAddress.Loopback, port, null, null);
			if (!result.AsyncWaitHandle.WaitOne(250))
			{
				return false;
			}
			client.EndConnect(result);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
