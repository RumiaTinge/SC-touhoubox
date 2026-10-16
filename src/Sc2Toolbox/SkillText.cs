using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sc2Toolbox;

/// <summary>
/// 英雄技能 / 辅助技能的文案（从游戏的 GameStrings 里提出来，随程序打包成 ability_text.tsv）。
/// key 规律：
///   英雄技能   dbbu&lt;英雄号两位&gt;_&lt;技能组 1..3&gt;_&lt;槽位 0..4&gt;&lt;热键 t/q/w/e/r&gt;[后缀]
///   辅助技能   btn_spell_&lt;栏位 0..2&gt;_&lt;下标 0..&gt;
/// </summary>
internal static class SkillText
{
	private static Dictionary<string, string[]> _map;

	private static string[] Item(string key)
	{
		if (key.Length == 0)
		{
			return null;
		}
		if (_map == null)
		{
			Dictionary<string, string[]> dictionary = new Dictionary<string, string[]>(StringComparer.Ordinal);
			try
			{
				string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ability_text.tsv");
				if (!File.Exists(path))
				{
					path = Path.Combine(L.Dir, "ability_text.tsv");
				}
				if (File.Exists(path))
				{
					string[] array = File.ReadAllLines(path, Encoding.UTF8);
					for (int i = 1; i < array.Length; i++)
					{
						string[] array2 = array[i].Split('\t');
						if (array2.Length >= 8 && array2[0].Length > 0)
						{
							dictionary[array2[0]] = new string[2] { array2[6], array2[8] };
						}
					}
				}
			}
			catch (Exception ex)
			{
				L.W("读技能文案失败：" + ex.Message);
			}
			_map = dictionary;
		}
		string[] array3;
		if (!_map.TryGetValue(key, out array3))
		{
			return null;
		}
		return array3;
	}

	/// <summary>英雄技能名；找不到返回空串。</summary>
	public static string HeroName(int heroId, int set, int slot)
	{
		if (heroId <= 0 || set <= 0 || slot < 0 || slot > 4)
		{
			return "";
		}
		string[] array = Item("dbbu" + TouhouApi.Pad2(heroId) + "_" + set + "_" + slot + "tqwer"[slot]);
		return (array == null) ? "" : array[0];
	}

	/// <summary>辅助技能名；找不到返回空串。</summary>
	public static string SpellName(int spellSlot, int index)
	{
		if (spellSlot < 0 || spellSlot > 2 || index < 0)
		{
			return "";
		}
		string[] array = Item("btn_spell_" + spellSlot + "_" + index);
		return (array == null) ? "" : array[0];
	}
}
