using System;

namespace Sc2Toolbox;

/// <summary>
/// 「现在该不该补点」的判定。
///
/// 本地没有真正的「对局进行中」接口：touhou_client 只注册了
/// lobbyPlayerList / sendEmoji / setConf / setConf2 四条路由，
/// 房间列表是它唯一给出的实时信息（而且游戏没开时还会把上一次的列表返回回来），
/// 所以这里不去猜「是否开局」，只用它确定的一件事：
/// 房间列表里还有我 = 我还在房间里 —— 没开局、或者刚打完回到房间。
///
/// 规则：在房间里不补点，其余时候放行。
/// 这样开局后（房间列表里没我了）立刻能用，打完回房间（我又出现在列表里）立刻停。
///
/// 兜底：房间接口没跑（从没拿到过数据）、接口断掉超过 2 分钟，都按「不限制」处理，
/// 免得工具自己把快捷施法卡死。
/// </summary>
public static class MatchWatch
{
	/// <summary>是否判定为「还在房间里」（没开局 / 刚打完）。</summary>
	public static bool InRoom { get; private set; }

	/// <summary>最近一次「离开房间」的时间（大约等于本局开始的时间）。</summary>
	public static DateTime StartedAt { get; private set; } = DateTime.MinValue;

	/// <summary>最近一次拿到房间列表的时间。</summary>
	public static DateTime LastOk { get; private set; } = DateTime.MinValue;

	/// <summary>最近一次判定说明，直接显示在面板上。</summary>
	public static string LastNote { get; private set; } = "";

	/// <summary>状态有变化（可能来自后台线程）。</summary>
	public static event Action Changed;

	private static readonly object Gate = new object();
	private static DateTime _lastCheck = DateTime.MinValue;

	/// <summary>房间接口多久没数据就当作不限制（秒）。</summary>
	private const int StaleSeconds = 120;

	/// <summary>当前这一下补点该不该放行。</summary>
	public static bool CanCast
	{
		get
		{
			Settings s = Ctx.S;
			if (s == null || !s.QuickCastOnlyInMatch)
			{
				return true;
			}
			if (LastOk == DateTime.MinValue)
			{
				// 从来没拿到过房间数据（没开房间接口）：不做限制，免得整个功能用不了
				return true;
			}
			if ((DateTime.Now - LastOk).TotalSeconds > (double)StaleSeconds)
			{
				// 接口断了：也不限制
				return true;
			}
			return !InRoom;
		}
	}

	/// <summary>给面板看的一句话。</summary>
	public static string Text
	{
		get
		{
			Settings s = Ctx.S;
			if (s == null || !s.QuickCastOnlyInMatch)
			{
				return "不限制对局状态";
			}
			if (LastOk == DateTime.MinValue)
			{
				return "还没拿到房间数据，暂不限制";
			}
			if ((DateTime.Now - LastOk).TotalSeconds > (double)StaleSeconds)
			{
				return "房间接口没数据了，暂不限制";
			}
			return InRoom ? "在房间里（还没开局或刚打完），不补点" : "不在房间里，补点放行";
		}
	}

	/// <summary>每次房间列表刷新后调用（LobbyWatcher 的 UI 线程回调里）。</summary>
	public static void Feed(LobbyState st)
	{
		if (st == null || !st.Ok)
		{
			return;
		}
		DateTime now = DateTime.Now;
		lock (Gate)
		{
			LastOk = now;
		}
		string myHandle = st.MyHandle ?? "";
		if (myHandle.Length == 0 || st.Handles == null)
		{
			// 还不知道自己的档案名，判断不了，先不动
			return;
		}
		if (st.Handles.Contains(myHandle))
		{
			SetRoom(value: true, "你在房间列表里：还没开局或者刚打完，先不补点");
			return;
		}
		SetRoom(value: false, "房间列表里没有你了：按已经开图处理，补点放行");
	}

	/// <summary>快捷施法轮询时顺手调一下（自己限流到 1 秒一次）。</summary>
	public static void Tick()
	{
		DateTime now = DateTime.Now;
		if ((now - _lastCheck).TotalSeconds < 1.0)
		{
			return;
		}
		_lastCheck = now;
		if (!InRoom)
		{
			return;
		}
		if (LastOk == DateTime.MinValue || (now - LastOk).TotalSeconds > (double)StaleSeconds)
		{
			SetRoom(value: false, "房间接口没数据了，按不限制处理");
		}
	}

	/// <summary>手动复位（比如游戏重开、房间接口换了）。</summary>
	public static void Reset()
	{
		lock (Gate)
		{
			LastOk = DateTime.MinValue;
		}
		SetRoom(value: false, "已复位");
	}

	private static void SetRoom(bool value, string note)
	{
		bool flag = false;
		bool flag2 = false;
		lock (Gate)
		{
			if (InRoom != value)
			{
				InRoom = value;
				flag = true;
			}
			if (!value && flag)
			{
				StartedAt = DateTime.Now;
			}
			if (note != null && note != LastNote)
			{
				LastNote = note;
				flag2 = true;
			}
		}
		if (flag)
		{
			L.W("[对局] " + note);
		}
		if (!flag && !flag2)
		{
			return;
		}
		try
		{
			MatchWatch.Changed?.Invoke();
		}
		catch
		{
		}
	}
}
