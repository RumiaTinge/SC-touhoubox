using System;
using System.Collections.Generic;

namespace Sc2Toolbox;

public class LobbyState
{
	public bool Ok;

	public DateTime When = DateTime.MinValue;

	public string Error = "";

	public string Raw = "";

	public string MyHandle = "";

	public List<string> Handles = new List<string>();

	public int Count;
}
