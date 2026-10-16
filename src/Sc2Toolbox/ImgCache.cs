using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Sc2Toolbox;

public static class ImgCache
{
	private static readonly Dictionary<string, BitmapSource> _c = new Dictionary<string, BitmapSource>();

	private static readonly Dictionary<string, DateTime> _fail = new Dictionary<string, DateTime>();

	private static readonly object _lk = new object();

	private static readonly HttpClient _hc = MakeHttp();

	private static string _dir = "";

	public static string LastError = "";

	private static string StoreDir
	{
		get
		{
			if (_dir.Length == 0)
			{
				try
				{
					_dir = Path.Combine(L.Dir, "img");
				}
				catch
				{
					_dir = Path.GetTempPath();
				}
			}
			return _dir;
		}
	}

	public static string BundleDir
	{
		get
		{
			try
			{
				return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "art");
			}
			catch
			{
				return "";
			}
		}
	}

	private static HttpClient MakeHttp()
	{
		HttpClient httpClient = new HttpClient
		{
			Timeout = TimeSpan.FromSeconds(90L)
		};
		try
		{
			httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
		}
		catch
		{
		}
		return httpClient;
	}

	private static string PathOf(string url)
	{
		try
		{
			using MD5 mD = MD5.Create();
			byte[] array = mD.ComputeHash(Encoding.UTF8.GetBytes(url));
			StringBuilder stringBuilder = new StringBuilder(array.Length * 2);
			byte[] array2 = array;
			foreach (byte b in array2)
			{
				stringBuilder.Append(b.ToString("x2"));
			}
			return Path.Combine(StoreDir, stringBuilder.ToString() + ".img");
		}
		catch
		{
			return "";
		}
	}

	private static string BundleOf(string url)
	{
		try
		{
			if (string.IsNullOrEmpty(url))
			{
				return "";
			}
			string bundleDir = BundleDir;
			if (bundleDir.Length == 0)
			{
				return "";
			}
			int num = url.IndexOf("/touhou/img/", StringComparison.OrdinalIgnoreCase);
			if (num < 0)
			{
				return "";
			}
			string text = url.Substring(num + 12);
			int num2 = text.IndexOf('?');
			if (num2 >= 0)
			{
				text = text.Substring(0, num2);
			}
			text = text.Replace('/', '\\');
			string text2 = Path.Combine(bundleDir, text);
			if (File.Exists(text2))
			{
				return text2;
			}
			string text3 = Path.Combine(bundleDir, Path.GetFileNameWithoutExtension(text));
			string[] array = new string[4] { ".jpg", ".png", ".jpeg", ".webp" };
			foreach (string text4 in array)
			{
				if (File.Exists(text3 + text4))
				{
					return text3 + text4;
				}
			}
			return "";
		}
		catch
		{
			return "";
		}
	}

	public static int CountInBundle()
	{
		try
		{
			string bundleDir = BundleDir;
			return Directory.Exists(bundleDir) ? Directory.GetFiles(bundleDir, "*.*", SearchOption.AllDirectories).Length : 0;
		}
		catch
		{
			return 0;
		}
	}

	private static bool LooksLikeImage(byte[] bytes)
	{
		if (bytes == null || bytes.Length < 12)
		{
			return false;
		}
		if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
		{
			return true;
		}
		if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
		{
			return true;
		}
		if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
		{
			return true;
		}
		if (bytes[0] == 0x42 && bytes[1] == 0x4D)
		{
			return true;
		}
		return bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;
	}

	private static BitmapImage FromBytes(byte[] bytes)
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.StreamSource = new MemoryStream(bytes);
		bitmapImage.EndInit();
		((Freezable)bitmapImage).Freeze();
		return bitmapImage;
	}

	public static BitmapSource Get(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return null;
		}
		lock (_lk)
		{
			if (_c.TryGetValue(url, out var value))
			{
				return value;
			}
		}
		return null;
	}

	public static BitmapSource Ensure(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return null;
		}
		lock (_lk)
		{
			if (_c.TryGetValue(url, out var value))
			{
				return value;
			}
			if (_fail.TryGetValue(url, out var value2) && (DateTime.Now - value2).TotalMinutes < 3.0)
			{
				return null;
			}
		}
		string text = BundleOf(url);
		if (text.Length > 0)
		{
			try
			{
				BitmapImage bitmapImage = FromBytes(File.ReadAllBytes(text));
				lock (_lk)
				{
					_c[url] = bitmapImage;
					_fail.Remove(url);
				}
				return bitmapImage;
			}
			catch
			{
			}
		}
		string text2 = PathOf(url);
		if (text2.Length > 0)
		{
			try
			{
				if (File.Exists(text2))
				{
					byte[] array = File.ReadAllBytes(text2);
					if (!LooksLikeImage(array))
					{
						throw new Exception("本地缓存不是图片（" + array.Length + " 字节）");
					}
					BitmapImage bitmapImage2 = FromBytes(array);
					lock (_lk)
					{
						_c[url] = bitmapImage2;
						_fail.Remove(url);
					}
					return bitmapImage2;
				}
			}
			catch
			{
				try
				{
					File.Delete(text2);
				}
				catch
				{
				}
			}
		}
		Exception ex = null;
		for (int i = 1; i <= 2; i++)
		{
			try
			{
				byte[] result;
				using (HttpResponseMessage httpResponseMessage = _hc.GetAsync(url, HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult())
				{
					if (!httpResponseMessage.IsSuccessStatusCode)
					{
						throw new Exception("HTTP " + (int)httpResponseMessage.StatusCode);
					}
					result = httpResponseMessage.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
				}
				if (!LooksLikeImage(result))
				{
					throw new Exception("返回的不是图片（" + result.Length + " 字节，多半是站点首页 HTML）");
				}
				BitmapImage bitmapImage3 = FromBytes(result);
				lock (_lk)
				{
					_c[url] = bitmapImage3;
					_fail.Remove(url);
				}
				if (text2.Length > 0)
				{
					try
					{
						Directory.CreateDirectory(StoreDir);
						File.WriteAllBytes(text2, result);
					}
					catch
					{
					}
				}
				return bitmapImage3;
			}
			catch (Exception ex2)
			{
				ex = ex2;
				if (i < 2)
				{
					try
					{
						Thread.Sleep(1200);
					}
					catch
					{
					}
				}
			}
		}
		LastError = url + " → " + ((ex != null) ? ex.Message : "下载失败");
		lock (_lk)
		{
			_fail[url] = DateTime.Now;
		}
		return null;
	}

	public static int CountOnDisk()
	{
		try
		{
			return Directory.Exists(StoreDir) ? Directory.GetFiles(StoreDir, "*.img").Length : 0;
		}
		catch
		{
			return 0;
		}
	}

	public static int Preload(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return 0;
		}
		if (Get(url) != null)
		{
			return 0;
		}
		if (Cached(url) != null)
		{
			return 0;
		}
		return (Ensure(url) != null) ? 1 : 0;
	}

	public static BitmapSource Cached(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return null;
		}
		lock (_lk)
		{
			if (_c.TryGetValue(url, out var value))
			{
				return value;
			}
		}
		string text = BundleOf(url);
		if (text.Length > 0)
		{
			try
			{
				BitmapImage bitmapImage = FromBytes(File.ReadAllBytes(text));
				lock (_lk)
				{
					_c[url] = bitmapImage;
				}
				return bitmapImage;
			}
			catch
			{
			}
		}
		string text2 = PathOf(url);
		if (text2.Length == 0)
		{
			return null;
		}
		try
		{
			if (!File.Exists(text2))
			{
				return null;
			}
			BitmapImage bitmapImage2 = FromBytes(File.ReadAllBytes(text2));
			lock (_lk)
			{
				_c[url] = bitmapImage2;
				_fail.Remove(url);
			}
			return bitmapImage2;
		}
		catch
		{
			return null;
		}
	}
}
