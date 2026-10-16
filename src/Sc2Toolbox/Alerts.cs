using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Sc2Toolbox;

public class Alerts
{
	private int _prevCount = -1;

	private int _goneSeen;

	private bool _wasFull;

	private bool _hasPrev;

	private DateTime _lastBig = DateTime.MinValue;

	private HashSet<string> _prevHandles = new HashSet<string>();

	private static MediaPlayer _mp;

	public void Feed(LobbyState st)
	{
		Settings s = Ctx.S;
		if (!st.Ok)
		{
			return;
		}
		int num = Math.Max(2, s.RoomSlots);
		HashSet<string> hashSet = new HashSet<string>();
		try
		{
			if (st.Handles != null)
			{
				foreach (string handle in st.Handles)
				{
					if (!string.IsNullOrEmpty(handle))
					{
						hashSet.Add(handle);
					}
				}
			}
		}
		catch
		{
		}
		bool flag = st.Count >= num;
		if ((s.AlertOnRoomFull & flag) && !_wasFull && _hasPrev)
		{
			Big("房间满了（" + st.Count + "/" + num + "），马上要开了", s.AlertTextFull, AlertKind.Full);
		}
		_wasFull = flag;
		if (Lobby.MyHandle.Length > 0 && !hashSet.Contains(Lobby.MyHandle))
		{
			_goneSeen++;
		}
		else
		{
			_goneSeen = 0;
		}
		if (s.AlertOnMatchStart && _goneSeen == 2)
		{
			Big("开局了！快回到星际争霸 II", s.AlertTextStart, AlertKind.Start);
		}
		if (_hasPrev && s.NotifyJoin && hashSet.Count > _prevHandles.Count)
		{
			foreach (string item in hashSet)
			{
				if (!_prevHandles.Contains(item) && !(item == Lobby.MyHandle))
				{
					Toast(Fill(s.NotifyJoinText, TouhouApi.NameFor(item), st.Count, num));
					break;
				}
			}
		}
		_prevHandles = hashSet;
		_hasPrev = true;
		_prevCount = st.Count;
	}

	public static string Fill(string tpl, string name, int n, int max)
	{
		if (string.IsNullOrEmpty(tpl))
		{
			tpl = "进人了：{name}（{n}/{max}）";
		}
		return tpl.Replace("{name}", name).Replace("{n}", n.ToString()).Replace("{max}", max.ToString());
	}

	public void Big(string title, string body)
	{
		Big(title, body, AlertKind.Start);
	}

	public void Big(string title, string body, AlertKind kind)
	{
		Settings s = Ctx.S;
		if (!s.AlertEnabled)
		{
			L.W("（警报已关闭）" + title);
		}
		else
		{
			if ((DateTime.Now - _lastBig).TotalSeconds < 5.0)
			{
				return;
			}
			_lastBig = DateTime.Now;
			L.W("★ 警报（" + ((kind == AlertKind.Full) ? "满人" : "开局") + "）：" + title);
			PlaySound(kind);
			if (s.AlertShowImage || !string.IsNullOrWhiteSpace(body))
			{
				try
				{
					AlertWindow.ShowIt(title, body);
					return;
				}
				catch (Exception ex)
				{
					L.W("弹警报失败: " + ex.Message);
					return;
				}
			}
			Toast(title);
		}
	}

	public void Test()
	{
		_lastBig = DateTime.MinValue;
		L.W("手动试响");
		PlaySound(AlertKind.Start);
		Settings s = Ctx.S;
		if (s.AlertShowImage || !string.IsNullOrWhiteSpace(s.AlertTextStart))
		{
			try
			{
				AlertWindow.ShowIt("【自检】这就是开局警报的样子", s.AlertTextStart);
				return;
			}
			catch (Exception ex)
			{
				L.W("试响失败: " + ex.Message);
				return;
			}
		}
		Toast("【自检】开局警报（满人 / 开局 是两种不同的声音）");
	}

	public void TestSound(AlertKind kind)
	{
		SyncNow();
		L.W("试听声音：" + ((kind == AlertKind.Full) ? "满人" : "开局"));
		PlaySound(kind);
	}

	private static void SyncNow()
	{
		try
		{
			if (Ctx.S != null)
			{
				Ctx.S.Save();
			}
		}
		catch
		{
		}
	}

	public void PlaySound()
	{
		PlaySound(AlertKind.Start);
	}

	public void PlaySound(AlertKind kind)
	{
		Settings s = Ctx.S;
		if (!s.AlertSoundEnabled)
		{
			return;
		}
		string text = ((kind == AlertKind.Full) ? s.AlertSoundFull : s.AlertSoundStart);
		if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
		{
			try
			{
				if (_mp == null)
				{
					_mp = new MediaPlayer();
				}
				_mp.Open(new Uri(text));
				_mp.Volume = 1.0;
				_mp.Play();
				return;
			}
			catch (Exception ex)
			{
				L.W("播放自定义声音失败，改用内置提示音: " + ex.Message);
			}
		}
		Task.Run(() =>
		{
			try
			{
				if (kind == AlertKind.Full)
				{
					Console.Beep(680, 130);
					Thread.Sleep(70);
					Console.Beep(680, 220);
				}
				else
				{
					Console.Beep(880, 150);
					Thread.Sleep(90);
					Console.Beep(1180, 150);
					Thread.Sleep(90);
					Console.Beep(1560, 260);
				}
			}
			catch
			{
				try
				{
					SystemSounds.Exclamation.Play();
				}
				catch
				{
				}
			}
		});
	}

	public void Toast(string text)
	{
		if (!Ctx.S.AlertEnabled)
		{
			return;
		}
		L.W("提示：" + text);
		try
		{
			ToastWindow.ShowText(text, Ctx.S.ToastCorner, Ctx.S.ToastSeconds);
		}
		catch
		{
		}
	}
}
