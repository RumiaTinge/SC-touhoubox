using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Sc2Toolbox;

/// <summary>一条直播弹幕。</summary>
public sealed class DanmakuLine
{
	public string User = "";

	public string Text = "";

	public bool Super;

	public DateTime At = DateTime.Now;

	public string Time => At.ToString("HH:mm");
}

/// <summary>
/// B 站直播弹幕：从 chat.laplace.live（LAPLACE）用的那套接口拿连接信息，再直连 B 站弹幕服务器。
/// 房间号 → /bilibili/uid-by-room/{房间} 拿真实 roomId；/bilibili/room-conn-info-v2/{roomId} 拿 token 和弹幕服务器地址；
/// 然后 wss://{host}:{wss_port}/sub 发认证包，收 protover=3（brotli）的消息流。
/// 注意：那两个接口在 Cloudflare 后面，.NET 的 HttpClient 会被拦（403 Just a moment），所以走 curl.exe 取 JSON。
/// </summary>
public static class LiveDanmaku
{
	private sealed class Line
	{
		public string User = "";

		public string Text = "";

		public bool Super;

		public DateTime At = DateTime.Now;
	}

	private const int MaxLines = 60;

	private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

	private const string Referer = "https://chat.laplace.live/";

	private static readonly object Gate = new object();

	private static readonly List<Line> Lines = new List<Line>();

	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(20L)
	};

	private static Thread _thread;

	private static volatile bool _run;

	private static ClientWebSocket _ws;

	private static long _roomId;

	private static string _room = "";

	private static bool _connected;

	private static string _status = "没设直播间号";

	private static int _count;

	/// <summary>弹幕有变化（新来一条、连接状态变了）。</summary>
	public static event Action Changed;

	/// <summary>当前设置的直播间号。</summary>
	public static string Room => _room;

	/// <summary>是否已经连上弹幕服务器。</summary>
	public static bool Connected => _connected;

	/// <summary>一句话状态，给界面显示。</summary>
	public static string Status
	{
		get
		{
			lock (Gate)
			{
				return _status;
			}
		}
	}

	/// <summary>本次已收到多少条弹幕。</summary>
	public static int Count => Volatile.Read(ref _count);

	/// <summary>当前缓存快照（旧→新）。</summary>
	public static DanmakuLine[] Snapshot()
	{
		lock (Gate)
		{
			DanmakuLine[] array = new DanmakuLine[Lines.Count];
			for (int i = 0; i < Lines.Count; i++)
			{
				Line line = Lines[i];
				array[i] = new DanmakuLine
				{
					User = line.User,
					Text = line.Text,
					Super = line.Super,
					At = line.At
				};
			}
			return array;
		}
	}

	/// <summary>读设置：房间号变了就重连，没填就断开。</summary>
	public static void Apply()
	{
		string text = NormalizeRoom(Ctx.S.DanmakuRoom);
		lock (Gate)
		{
			if (_run && _room == text)
			{
				// 同一个直播间，已经连着了就别重复连（面板上改别的设置也会走到这儿）
				return;
			}
			_room = text;
		}
		Stop();
		if (text.Length == 0)
		{
			SetStatus("没设直播间号");
			return;
		}
		Start(text);
	}

	/// <summary>把用户填的东西整理成纯房间号：接受 5440、bilibili.com/5440 这种。</summary>
	public static string NormalizeRoom(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return "";
		}
		string text = raw.Trim();
		int num = text.LastIndexOf('/');
		if (num >= 0 && num < text.Length - 1)
		{
			text = text.Substring(num + 1);
		}
		int num2 = text.IndexOf('?');
		if (num2 > 0)
		{
			text = text.Substring(0, num2);
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in text)
		{
			if (!char.IsDigit(c))
			{
				break;
			}
			stringBuilder.Append(c);
		}
		return stringBuilder.ToString();
	}

	public static void Start(string room)
	{
		if (_run)
		{
			return;
		}
		_run = true;
		lock (Gate)
		{
			Lines.Clear();
		}
		Interlocked.Exchange(ref _count, 0);
		SetStatus($"正在获取直播间 {room} 的信息…");
		_thread = new Thread(Loop)
		{
			IsBackground = true,
			Name = "live-danmaku"
		};
		_thread.Start();
	}

	public static void Stop()
	{
		if (!_run)
		{
			return;
		}
		_run = false;
		try
		{
			_ws?.Dispose();
		}
		catch
		{
		}
		_ws = null;
		try
		{
			_thread?.Join(1500);
		}
		catch
		{
		}
		_thread = null;
		_connected = false;
	}

	/// <summary>重新连一次（界面上点「重连」用）。</summary>
	public static void Reconnect()
	{
		Stop();
		Apply();
	}

	private static void Loop()
	{
		string room = _room;
		while (_run)
		{
			try
			{
				ConnectOnce(room);
			}
			catch (Exception ex)
			{
				L.W("[直播弹幕] 出错了：" + ex.Message);
				SetStatus("连接失败：" + Short(ex.Message));
			}
			_connected = false;
			Raise();
			if (_run)
			{
				Thread.Sleep(5000);
				SetStatus("5 秒后重连…");
			}
		}
	}

	private static void ConnectOnce(string room)
	{
		SetStatus("正在获取房间信息…");
		string text = CurlText("https://workers.laplace.cn/bilibili/uid-by-room/" + room);
		long num = ReadRoomId(text);
		if (num <= 0)
		{
			throw new InvalidOperationException("这个直播间号好像不对");
		}
		_roomId = num;
		SetStatus($"直播间 {room}（{num}），正在取弹幕服务器…");
		string text2 = CurlText("https://workers.laplace.cn/bilibili/room-conn-info-v2/" + num, "{}");
		using JsonDocument jsonDocument = JsonDocument.Parse(text2);
		if (!jsonDocument.RootElement.TryGetProperty("data", out var value))
		{
			throw new InvalidOperationException("拿不到弹幕服务器信息");
		}
		string text3 = value.GetProperty("token").GetString() ?? "";
		string text4 = value.TryGetProperty("ack", out var value2) ? (value2.GetString() ?? "") : "";
		long num2 = ((value.TryGetProperty("fetcher", out var value3) && value3.ValueKind == JsonValueKind.Number) ? value3.GetInt64() : 0L);
		if (!value.TryGetProperty("host_list", out var value4) || value4.GetArrayLength() == 0)
		{
			throw new InvalidOperationException("弹幕服务器列表是空的");
		}
		JsonElement jsonElement = value4[0];
		string text5 = jsonElement.GetProperty("host").GetString() ?? "";
		int num3 = (jsonElement.TryGetProperty("wss_port", out var value5) ? value5.GetInt32() : 443);
		string provider = value.TryGetProperty("fetchMode", out var value6) ? value6.GetString() : "";
		L.W($"[直播弹幕] 房间 {room}（{num}）→ wss://{text5}:{num3}，模式 {provider}，fetcher={num2}");
		SetStatus($"正在连接弹幕服务器（{text5}）…");
		ClientWebSocket clientWebSocket = new ClientWebSocket();
		clientWebSocket.Options.SetRequestHeader("User-Agent", UserAgent);
		clientWebSocket.Options.SetRequestHeader("Origin", "https://live.bilibili.com");
		_ws = clientWebSocket;
		clientWebSocket.ConnectAsync(new Uri($"wss://{text5}:{num3}/sub"), CancellationToken.None).GetAwaiter().GetResult();
		string text7 = "{\"uid\":" + num2 + ",\"roomid\":" + num + ",\"protover\":3,\"buvid\":\"" + text4 + "\",\"support_ack\":true,\"queue_uuid\":\"" + Guid.NewGuid().ToString("N").Substring(0, 8) + "\",\"scene\":\"room\",\"platform\":\"web\",\"type\":2,\"key\":\"" + text3 + "\"}";
		clientWebSocket.SendAsync(new ArraySegment<byte>(Pack(7, Encoding.UTF8.GetBytes(text7))), WebSocketMessageType.Binary, true, CancellationToken.None).GetAwaiter().GetResult();
		_connected = true;
		SetStatus($"已连接 直播间 {room}");
		L.W($"[直播弹幕] 已连接 直播间 {room}（{num}）");
		using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		Thread thread = new Thread((ThreadStart)delegate
		{
			Heartbeat(clientWebSocket, cancellationTokenSource.Token);
		})
		{
			IsBackground = true,
			Name = "live-danmaku-hb"
		};
		thread.Start();
		byte[] array = new byte[65536];
		MemoryStream memoryStream = new MemoryStream();
		try
		{
			while (_run && clientWebSocket.State == WebSocketState.Open)
			{
				WebSocketReceiveResult webSocketReceiveResult = clientWebSocket.ReceiveAsync(new ArraySegment<byte>(array), CancellationToken.None).GetAwaiter().GetResult();
				if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
				{
					break;
				}
				memoryStream.Write(array, 0, webSocketReceiveResult.Count);
				if (!webSocketReceiveResult.EndOfMessage)
				{
					continue;
				}
				byte[] array2 = memoryStream.ToArray();
				memoryStream.SetLength(0L);
				Parse(array2);
			}
		}
		finally
		{
			cancellationTokenSource.Cancel();
			_connected = false;
		}
	}

	private static void Heartbeat(ClientWebSocket ws, CancellationToken token)
	{
		byte[] array = Pack(2, Encoding.UTF8.GetBytes("[object Object]"));
		while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
		{
			try
			{
				ws.SendAsync(new ArraySegment<byte>(array), WebSocketMessageType.Binary, true, token).GetAwaiter().GetResult();
			}
			catch
			{
				return;
			}
			if (token.WaitHandle.WaitOne(30000))
			{
				return;
			}
		}
	}

	private static void Parse(byte[] data)
	{
		int num = 0;
		while (num + 16 <= data.Length)
		{
			int num2 = (data[num] << 24) | (data[num + 1] << 16) | (data[num + 2] << 8) | data[num + 3];
			int num3 = (data[num + 4] << 8) | data[num + 5];
			int num4 = (data[num + 6] << 8) | data[num + 7];
			int num5 = (data[num + 8] << 24) | (data[num + 9] << 16) | (data[num + 10] << 8) | data[num + 11];
			if (num2 <= 0 || num3 < 16 || num + num2 > data.Length)
			{
				break;
			}
			byte[] array = new byte[num2 - num3];
			Buffer.BlockCopy(data, num + num3, array, 0, array.Length);
			if (num4 == 2 || num4 == 3)
			{
				try
				{
					Parse(num4 == 2 ? Inflate(array) : Brotli(array));
				}
				catch
				{
				}
			}
			else if (num5 == 5)
			{
				Handle(array);
			}
			num += num2;
		}
	}

	private static void Handle(byte[] body)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(body);
			JsonElement rootElement = jsonDocument.RootElement;
			if (!rootElement.TryGetProperty("cmd", out var value))
			{
				return;
			}
			string text = value.GetString() ?? "";
			if (text.StartsWith("DANMU_MSG"))
			{
				JsonElement info = rootElement.GetProperty("info");
				string text2 = info[1].GetString() ?? "";
				string user = ((info[2].ValueKind == JsonValueKind.Array && info[2].GetArrayLength() > 1) ? (info[2][1].GetString() ?? "") : "");
				Add(user, text2, super: false);
			}
			else if (text.StartsWith("SUPER_CHAT_MESSAGE"))
			{
				JsonElement data = rootElement.GetProperty("data");
				string text3 = data.GetProperty("message").GetString() ?? "";
				string user2 = ((data.TryGetProperty("user_info", out var value2) && value2.TryGetProperty("uname", out var value3)) ? (value3.GetString() ?? "") : "");
				Add(user2, text3, super: true);
			}
		}
		catch
		{
		}
	}

	private static void Add(string user, string text, bool super)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		lock (Gate)
		{
			Lines.Add(new Line
			{
				User = user,
				Text = text.Trim(),
				Super = super,
				At = DateTime.Now
			});
			if (Lines.Count > MaxLines)
			{
				Lines.RemoveRange(0, Lines.Count - MaxLines);
			}
		}
		Interlocked.Increment(ref _count);
		Raise();
	}

	private static void SetStatus(string text)
	{
		lock (Gate)
		{
			if (_status == text)
			{
				return;
			}
			_status = text;
		}
		Raise();
	}

	private static void Raise()
	{
		try
		{
			Changed?.Invoke();
		}
		catch
		{
		}
	}

	private static string Short(string text)
	{
		if (text.Length <= 60)
		{
			return text;
		}
		return text.Substring(0, 60) + "…";
	}

	private static byte[] Pack(int op, byte[] body)
	{
		byte[] array = new byte[16 + body.Length];
		int num = array.Length;
		array[0] = (byte)(num >> 24);
		array[1] = (byte)(num >> 16);
		array[2] = (byte)(num >> 8);
		array[3] = (byte)num;
		array[5] = 16;
		array[7] = 1;
		array[8] = (byte)(op >> 24);
		array[9] = (byte)(op >> 16);
		array[10] = (byte)(op >> 8);
		array[11] = (byte)op;
		Buffer.BlockCopy(body, 0, array, 16, body.Length);
		return array;
	}

	private static byte[] Brotli(byte[] data)
	{
		using MemoryStream stream = new MemoryStream(data);
		using BrotliStream brotliStream = new BrotliStream(stream, CompressionMode.Decompress);
		using MemoryStream memoryStream = new MemoryStream();
		brotliStream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	private static byte[] Inflate(byte[] data)
	{
		using MemoryStream stream = new MemoryStream(data);
		using ZLibStream zLibStream = new ZLibStream(stream, CompressionMode.Decompress);
		using MemoryStream memoryStream = new MemoryStream();
		zLibStream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	private static long ReadRoomId(string json)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			if (jsonDocument.RootElement.TryGetProperty("roomId", out var value) && value.ValueKind == JsonValueKind.Number)
			{
				return value.GetInt64();
			}
		}
		catch
		{
		}
		return 0L;
	}

	/// <summary>
	/// 取 JSON：优先 curl.exe（Cloudflare 放行 curl 的 TLS 指纹，拦 .NET 的），没有再退回 HttpClient。
	/// </summary>
	private static string CurlText(string url, string postJson = null)
	{
		try
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo("curl.exe")
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};
			processStartInfo.ArgumentList.Add("-s");
			processStartInfo.ArgumentList.Add("--max-time");
			processStartInfo.ArgumentList.Add("25");
			processStartInfo.ArgumentList.Add("-H");
			processStartInfo.ArgumentList.Add("User-Agent: " + UserAgent);
			processStartInfo.ArgumentList.Add("-H");
			processStartInfo.ArgumentList.Add("Accept: application/json, text/plain, */*");
			processStartInfo.ArgumentList.Add("-H");
			processStartInfo.ArgumentList.Add("Accept-Language: zh-CN,zh;q=0.9");
			processStartInfo.ArgumentList.Add("-H");
			processStartInfo.ArgumentList.Add("Referer: " + Referer);
			processStartInfo.ArgumentList.Add("-H");
			processStartInfo.ArgumentList.Add("Origin: https://chat.laplace.live");
			if (postJson != null)
			{
				processStartInfo.ArgumentList.Add("-X");
				processStartInfo.ArgumentList.Add("POST");
				processStartInfo.ArgumentList.Add("-H");
				processStartInfo.ArgumentList.Add("Content-Type: application/json");
				processStartInfo.ArgumentList.Add("--data-binary");
				processStartInfo.ArgumentList.Add(postJson);
			}
			processStartInfo.ArgumentList.Add(url);
			using Process process = Process.Start(processStartInfo);
			if (process != null)
			{
				string text = process.StandardOutput.ReadToEnd();
				process.WaitForExit(30000);
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text;
				}
			}
		}
		catch
		{
		}
		return HttpGet(url, postJson);
	}

	private static string HttpGet(string url, string postJson)
	{
		try
		{
			using HttpRequestMessage httpRequestMessage = new HttpRequestMessage(postJson == null ? HttpMethod.Get : HttpMethod.Post, url);
			httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
			httpRequestMessage.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
			httpRequestMessage.Headers.TryAddWithoutValidation("Referer", Referer);
			if (postJson != null)
			{
				httpRequestMessage.Content = new StringContent(postJson, Encoding.UTF8, "application/json");
			}
			using HttpResponseMessage httpResponseMessage = Http.SendAsync(httpRequestMessage).GetAwaiter().GetResult();
			return httpResponseMessage.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		}
		catch
		{
			return "";
		}
	}
}
