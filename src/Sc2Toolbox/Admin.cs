using System.Security.Principal;

namespace Sc2Toolbox;

public static class Admin
{
	public static bool IsElevated
	{
		get
		{
			try
			{
				return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
			}
			catch
			{
				return false;
			}
		}
	}

	public static string Why()
	{
		if (!IsElevated)
		{
			return "普通权限：游戏在前台时注入会被系统静默挡下";
		}
		return "管理员权限：注入可以送达游戏进程";
	}
}
