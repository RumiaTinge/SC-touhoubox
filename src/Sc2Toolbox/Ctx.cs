using System.Windows;
using System.Windows.Threading;

namespace Sc2Toolbox;

public static class Ctx
{
	public static Settings S = new Settings();

	public static KeepAlive Keep = new KeepAlive();

	public static Alerts Alerts = new Alerts();

	public static LobbyWatcher Watcher = new LobbyWatcher();

	public static MainWindow Main;

	public static OverlayWindow Overlay;

	public static Dispatcher UI => ((DispatcherObject)Application.Current).Dispatcher;
}
