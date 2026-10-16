using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sc2Toolbox;

public static class L
{
	private static string _dir;

	public static string LastError = "";

	private static readonly object _lk = new object();

	public static string Dir
	{
		get
		{
			if (_dir == null)
			{
				string[] array = Candidates();
				for (int i = 0; i < array.Length; i++)
				{
					_dir = TryDir(array[i]);
					if (_dir != null)
					{
						break;
					}
				}
				if (_dir == null)
				{
					_dir = Path.GetTempPath();
				}
			}
			return _dir;
		}
	}

	public static string File => Path.Combine(Dir, "toolbox.log");

	public static event Action<string> OnLine;

	public static string[] Candidates()
	{
		List<string> list = new List<string>();
		try
		{
			list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SC-touhoubox"));
		}
		catch
		{
		}
		try
		{
			list.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"));
		}
		catch
		{
		}
		try
		{
			list.Add(Path.GetTempPath());
		}
		catch
		{
		}
		return list.ToArray();
	}

	public static void ImportFromOtherDirs(params string[] names)
	{
		foreach (string text in names)
		{
			try
			{
				string text2 = Path.Combine(Dir, text);
				if (System.IO.File.Exists(text2))
				{
					continue;
				}
				string[] array = Candidates();
				foreach (string text3 in array)
				{
					if (!string.Equals(Path.GetFullPath(text3), Path.GetFullPath(Dir), StringComparison.OrdinalIgnoreCase))
					{
						string text4 = Path.Combine(text3, text);
						if (System.IO.File.Exists(text4))
						{
							System.IO.File.Copy(text4, text2, overwrite: false);
							W("数据目录里没有 " + text + "，已从 " + text3 + " 导入一份");
							break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				LastError = LastError + "导入 " + text + " 失败：" + ex.GetType().Name + ": " + ex.Message + " ; ";
			}
		}
	}

	private static string TryDir(string d)
	{
		try
		{
			Directory.CreateDirectory(d);
			string path = Path.Combine(d, "_wtest_" + Environment.ProcessId + ".tmp");
			System.IO.File.AppendAllText(path, "x");
			System.IO.File.Delete(path);
			return d;
		}
		catch (Exception ex)
		{
			try
			{
				string[] files = Directory.GetFiles(d, "_wtest*.tmp");
				for (int i = 0; i < files.Length; i++)
				{
					System.IO.File.Delete(files[i]);
				}
				string path2 = Path.Combine(d, "_wtest_" + Environment.ProcessId + ".tmp");
				System.IO.File.AppendAllText(path2, "x");
				System.IO.File.Delete(path2);
				return d;
			}
			catch (Exception ex2)
			{
				LastError = LastError + d + " → " + ex.GetType().Name + ": " + ex.Message + "（清理残file后仍失败：" + ex2.GetType().Name + ": " + ex2.Message + "） ; ";
				return null;
			}
		}
	}

	public static void W(string s)
	{
		string text = DateTime.Now.ToString("MM-dd HH:mm:ss") + "  " + s;
		try
		{
			lock (_lk)
			{
				Directory.CreateDirectory(Dir);
				if (System.IO.File.Exists(File) && new FileInfo(File).Length > 2097152)
				{
					System.IO.File.Move(File, File + ".old", overwrite: true);
				}
				System.IO.File.AppendAllText(File, text + "\r\n", Encoding.UTF8);
			}
		}
		catch (Exception ex)
		{
			LastError = ex.GetType().Name + ": " + ex.Message;
		}
		try
		{
			OnLine?.Invoke(text);
		}
		catch
		{
		}
	}

	public static string Tail(int lines)
	{
		try
		{
			if (!System.IO.File.Exists(File))
			{
				return "（还没有日志）";
			}
			string[] array = System.IO.File.ReadAllLines(File, Encoding.UTF8);
			int num = Math.Min(lines, array.Length);
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = array.Length - num; i < array.Length; i++)
			{
				stringBuilder.AppendLine(array[i]);
			}
			return stringBuilder.ToString();
		}
		catch (Exception ex)
		{
			return "读日志失败: " + ex.Message;
		}
	}
}
