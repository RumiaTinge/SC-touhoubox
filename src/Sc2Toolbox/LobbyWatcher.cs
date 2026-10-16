using System;
using System.Collections.Generic;
using System.Threading;

namespace Sc2Toolbox;

public class LobbyWatcher
{
	private Timer _t;

	private DateTime _lastPoll = DateTime.MinValue;

	private DateTime _lastNick = DateTime.MinValue;

	private DateTime _lastApi = DateTime.Now.AddSeconds(-235.0);

	private bool _apiBusy;

	private bool _artBusy;

	public LobbyState Last = new LobbyState();

	public Dictionary<string, string> Nicks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public bool InterfaceOk => Last.Ok;

	public string Error => Last.Error;

	public void RefreshTouhou()
	{
		RefreshTouhou(force: false);
	}

	public void RefreshTouhou(bool force)
	{
		if (_apiBusy)
		{
			return;
		}
		_apiBusy = true;
		try
		{
			TouhouApi.LoadSnapshot();
			Ctx.UI.BeginInvoke((Delegate)(Action)(() =>
			{
				try
				{
					if (Ctx.Overlay != null)
					{
						Ctx.Overlay.Apply(Last, Nicks);
					}
				}
				catch
				{
				}
				try
				{
					if (Ctx.Main != null)
					{
						Ctx.Main.OnTouhou();
					}
				}
				catch
				{
				}
			}), Array.Empty<object>());
		}
		catch
		{
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				TouhouApi.Refresh(force);
				if (TouhouApi.PartialFail)
				{
					_lastApi = DateTime.Now.AddSeconds(-180.0);
				}
			}
			catch (Exception ex)
			{
				TouhouApi.LastError = ex.Message;
			}
			finally
			{
				_apiBusy = false;
				try
				{
					Ctx.UI.BeginInvoke((Delegate)(Action)(() =>
					{
						try
						{
							if (Ctx.Overlay != null)
							{
								Ctx.Overlay.Apply(Last, Nicks);
							}
						}
						catch
						{
						}
						try
						{
							if (Ctx.Main != null)
							{
								Ctx.Main.OnTouhou();
							}
						}
						catch
						{
						}
					}), Array.Empty<object>());
				}
				catch
				{
				}
			}
			try
			{
				if (_artBusy)
				{
					return;
				}
				List<string> list = new List<string>();
				try
				{
					LobbyState last = Last;
					if (last != null && last.Handles != null)
					{
						list.AddRange(last.Handles);
					}
				}
				catch
				{
				}
				_artBusy = true;
				try
				{
					TouhouApi.RefreshRoomProfiles(list);
					int num = TouhouApi.PreloadArt(list, 40000);
					L.W("美术资源预取：这轮新下载 " + num + " 张，磁盘上一共 " + ImgCache.CountOnDisk() + " 张（房间 " + list.Count + " 人）" + ((num == 0 && ImgCache.LastError.Length > 0) ? ("；最近一次失败：" + ImgCache.LastError) : ""));
					if (num > 0)
					{
						try
						{
							Ctx.UI.BeginInvoke((Delegate)(Action)(() =>
							{
								try
								{
									if (Ctx.Overlay != null)
									{
										Ctx.Overlay.Apply(Last, Nicks);
									}
								}
								catch
								{
								}
								try
								{
									if (Ctx.Main != null)
									{
										Ctx.Main.OnTouhou();
									}
								}
								catch
								{
								}
							}), Array.Empty<object>());
							return;
						}
						catch
						{
							return;
						}
					}
				}
				finally
				{
					_artBusy = false;
				}
			}
			catch
			{
			}
		});
	}

	public void Start()
	{
		Lobby.RefreshMyHandle();
		Nicks = Lobby.LoadNicks();
		_t = new Timer(Tick, null, 300, 250);
		L.W("房间接口监听已启动：POST http://127.0.0.1:" + Ctx.S.LobbyPort + "/lobbyPlayerList（我的档案 " + ((Lobby.MyHandle.Length > 0) ? Lobby.MyHandle : "未知") + "，昵称表 " + Nicks.Count + " 条）");
	}

	private void Tick(object _)
	{
		try
		{
			DateTime now = DateTime.Now;
			if ((now - _lastNick).TotalSeconds > 60.0)
			{
				_lastNick = now;
				Nicks = Lobby.LoadNicks();
				Lobby.RefreshMyHandle();
			}
			if ((now - _lastApi).TotalSeconds > 90.0)
			{
				_lastApi = now;
				RefreshTouhou();
			}
			if ((now - _lastPoll).TotalMilliseconds < (double)Math.Max(200, Ctx.S.PollMs))
			{
				return;
			}
			_lastPoll = now;
			LobbyState st = Lobby.Poll();
			Last = st;
			Ctx.UI.BeginInvoke((Delegate)(Action)(() =>
			{
				if (Ctx.Overlay != null)
				{
					Ctx.Overlay.Apply(st, Nicks);
				}
				Ctx.Alerts.Feed(st);
				if (Ctx.Main != null)
				{
					Ctx.Main.OnLobby(st);
				}
			}), Array.Empty<object>());
		}
		catch (Exception)
		{
		}
	}
}
