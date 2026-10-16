using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;

namespace Sc2Toolbox;

public static class Lobby
{
	private static readonly HttpClient Http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(2L)
	};

	public static string MyHandle = "";

	private static string Url(string path)
	{
		return "http://127.0.0.1:" + Ctx.S.LobbyPort + "/" + path;
	}

	public static LobbyState Poll()
	{
		LobbyState lobbyState = new LobbyState();
		lobbyState.When = DateTime.Now;
		try
		{
			HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, Url("lobbyPlayerList"));
			httpRequestMessage.Content = new StringContent("", Encoding.UTF8, "text/plain");
			HttpResponseMessage result = Http.SendAsync(httpRequestMessage).GetAwaiter().GetResult();
			lobbyState.Raw = (result.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? "").Trim();
			lobbyState.Ok = true;
		}
		catch (Exception ex)
		{
			lobbyState.Error = ex.Message;
			lobbyState.Ok = false;
			return lobbyState;
		}
		if (lobbyState.Raw.IndexOf(' ') >= 0 && lobbyState.Raw.IndexOf(':') < 0)
		{
			lobbyState.Ok = false;
			lobbyState.Error = "接口返回的是身份信息而不是房间列表";
			return lobbyState;
		}
		string[] array = lobbyState.Raw.Split(':');
		for (int i = 0; i < 10; i++)
		{
			string text = ((i < array.Length) ? array[i].Trim() : "");
			if (text.IndexOf(' ') >= 0)
			{
				text = "";
			}
			lobbyState.Handles.Add(text);
			if (text.Length > 0)
			{
				lobbyState.Count++;
			}
		}
		lobbyState.MyHandle = MyHandle;
		return lobbyState;
	}

	public static void RefreshMyHandle()
	{
		try
		{
			string[] array = (Http.GetAsync(Url("")).GetAwaiter().GetResult()
				.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? "").Trim().Split(new char[4] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length >= 1 && array[0].Contains("-S2-"))
			{
				MyHandle = array[0];
			}
		}
		catch
		{
		}
	}

	public static Dictionary<string, string> LoadNicks()
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			Directory.CreateDirectory("C:\\ProgramData\\touhou");
			string[] files = Directory.GetFiles("C:\\ProgramData\\touhou");
			foreach (string path in files)
			{
				string fileName = Path.GetFileName(path);
				if (fileName.IndexOf("S2-", StringComparison.Ordinal) < 0)
				{
					continue;
				}
				try
				{
					string[] array = File.ReadAllText(path, Encoding.UTF8).Split(new char[4] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
					if (array.Length >= 2 && !string.IsNullOrWhiteSpace(array[1]))
					{
						dictionary[fileName] = array[1];
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return dictionary;
	}
}
