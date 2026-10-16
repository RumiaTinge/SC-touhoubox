namespace Sc2Toolbox;

/// <summary>
/// 东方月灵传天赋（rune）表：名字、说明、以及 105 位存档里的下标换算。
///
/// 存档版图（touhou.SC2Bank 的 dbHeroNN_config 值串，105 个数字）：
///   [0..5]   初始属性（6 项，恰好一项为 8，位置即主属性）
///   [6]      主系 primaryColumn（0~4）
///   [7..96]  天赋网格 90 格 = 18 rune x 5 系，idx = 7 + 5*rune + school
///   [97..104] 技能栏 / 辅助技能 / 技能组合等，与天赋无关
///
/// rune = 行 * 3 + 位（a=0 左 / b=1 中 / c=2 右），行 0~5 自上而下。
/// 名字与说明 2026-10-16 由游戏面板截图 + 地图 zhCN GameStrings.txt 核对生成。
/// </summary>
public static class TouhouRune
{
	/// <summary>五大学派（对应主系/副系编号 0~4）。</summary>
	public static readonly string[] Schools = new string[5] { "斗争", "灾难", "魔法", "荣誉", "祝福" };

	/// <summary>天赋网格在存档里的起始下标。</summary>
	public const int GridStart = 7;

	/// <summary>天赋网格占 90 格（18 rune x 5 系）。</summary>
	public const int GridCount = 90;

	/// <summary>游戏面板的显示排布：Layout[系][行] = 该行从左到右的 rune 列表。</summary>
	public static readonly int[][][] Layout = new int[5][][]
	{
		new int[][] { new int[] { 0, 1 }, new int[] { 3, 4 }, new int[] { 6, 7 }, new int[] { 9, 10 }, new int[] { 12, 13 }, new int[] { 15, 16, 17 } },
		new int[][] { new int[] { 0, 1 }, new int[] { 3, 4 }, new int[] { 6, 7 }, new int[] { 9, 10 }, new int[] { 12, 13 }, new int[] { 15, 16, 17 } },
		new int[][] { new int[] { 0, 1 }, new int[] { 3, 4 }, new int[] { 6, 7 }, new int[] { 9, 10 }, new int[] { 12, 13 }, new int[] { 15, 16, 17 } },
		new int[][] { new int[] { 0, 1 }, new int[] { 3, 4 }, new int[] { 6 }, new int[] { 9, 10 }, new int[] { 12 }, new int[] { 15, 16, 17 } },
		new int[][] { new int[] { 0, 1 }, new int[] { 3, 4 }, new int[] { 6, 7, 8 }, new int[] { 9, 10 }, new int[] { 12 }, new int[] { 15, 16, 17 } }
	};

	private static readonly string[] Names = new string[90]
	{
		"白鸟的呼唤",
		"夜晚的猎人",
		"感悟",
		"阴阳调和",
		"准分子激光原地角膜消除术",
		"恢复的三叉戟",
		"卑劣的哥布林",
		"黑魔法",
		"阿瑞斯的头盔",
		"祝福之靴",
		null,
		null,
		null,
		null,
		null,
		"神奇的戒指",
		"撕咬",
		"熊猫人的火焰吐息",
		"坚固的墙",
		"神奇的古代魔法书",
		"致命子弹",
		"快乐冲锋",
		"魔法推进器",
		"魔法斗篷",
		"心情愉快",
		null,
		null,
		null,
		null,
		null,
		"渴望之剑",
		"战斗的基础",
		"战斗的基础",
		"强壮的肉体",
		"增加活力",
		"蓝色灵气",
		"蓝色灵气",
		"蓝色灵气",
		null,
		"鲁莽的攻击方式",
		null,
		null,
		null,
		null,
		"神秘的攻击方式",
		"护甲穿透",
		"护甲无效化",
		"护甲无效化",
		"神奇的盾",
		"经验值诈骗",
		"抗性穿透",
		"抗性无效化",
		"抗性无效化",
		"抵抗之盾",
		"金币小偷",
		null,
		null,
		null,
		null,
		null,
		"龙之愤怒（打击）",
		"龙之愤怒（物理）",
		"龙之愤怒（物理）",
		"最后的防御",
		"最后的防御",
		"龙之愤怒（魔法）",
		"龙之愤怒（魔法）",
		"龙之愤怒（魔法）",
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		"集中射击",
		"宙斯之怒",
		"魔法·神奇的流星",
		"熊掌",
		"魔法闪现",
		"无情连射",
		"奥德修斯的圣剑",
		"魔法之拳",
		"雷神之怒",
		"神之保佑",
		"幻像之击",
		"死线折光",
		"三相疾走",
		"蚀界环流",
		"适应之力"
	};

	private static readonly string[] Descs = new string[90]
	{
		"每第3次攻击在0.45~0.75秒(+系数)内提高〔数值〕%移动速度.\n\n系数: 0.05%物伤点数 0.05%暴击点数 0.05%冷却点数 0.06%法伤点数\n\n持续时间: 〔数值〕秒\n\n冷却时间: 〔数值〕秒",
		"夜晚第2次提示时不会显示自己的方位,并提高0.05移动速度.剩余时间只剩1分钟时恢复35%视野,如果是刺客角色则恢复100%视野",
		null,
		null,
		null,
		"每第3次攻击恢复5~20(+系数)生命值.\n\n系数: 25%物伤点数 25%暴击点数 25%冷却点数 30%法伤点数\n\n恢复: 〔数值〕\n\n冷却时间: 〔数值〕秒",
		"攻击敌人时恢复9~35(+系数)生命值.\n远程英雄只恢复65%生命值\n\n系数: 90%物伤点数 90%暴击点数 90%冷却点数 105%法伤点数\n\n恢复: 〔数值〕\n\n冷却时间: 〔数值〕秒(24~19)",
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		"提高0.1~0.4基础武器射程。对敌人造成攻击伤害后进入战斗状态，持续3秒；持续攻击会刷新持续时间，3秒未攻击后进入冷却，冷却期间效果不会发动。\n\n射程提高: 〔数值〕\n\n冷却时间: 〔数值〕秒\n\n※近战英雄无法选择该天赋",
		"对被控制的敌人(僵直,定身,眩晕等无法行动的状态)造成伤害时额外造成5~40(+系数)的转化伤害.\n\n系数: 70%物伤点数 70%暴击点数 70%冷却点数 81%法伤点数\n\n伤害: 〔数值〕\n\n冷却时间: 〔数值〕秒(25~20)",
		null,
		null,
		null,
		null,
		"脱离战斗后提高0.05~0.1移动速度.\n(在5, 9, 13, 17, 21级时提升)\n\n当前移动速度: 〔数值〕",
		"使用D、F、G技能(立场除外)后在1.4秒内提高0.14移动速度.\n\n若拥有魔法恢复则持续1.9秒",
		"受到法术伤害时在〔数值〕秒内减少受到的法术伤害9.2%+((物理抗性加点 * 0.17)% + (法术抗性加点 * 0.17)%).\n\n减伤: 〔数值〕%\n\n冷却时间: 〔数值〕秒",
		null,
		null,
		null,
		null,
		null,
		null,
		"攻击力提高7，每级额外提高1点，最多提高30点。\n\n攻击速度加点上限提高6点。",
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		"增加4攻击力且每级增加1攻击力(总计27)\n增加10.3生命值且每级增加5.6生命值(总计139.1)",
		null,
		null,
		null,
		null,
		"增加8法术强度且每级增加2法术强度(总计54)\n增加10.3生命值且每级增加5.6生命值(总计139.1)",
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		"每次攻击给予对方一个持续〔数值〕秒的集中射击标志,叠加了3个标记的敌人会在〔数值〕秒内暴露弱点,对集中目标的敌人进行攻击时每次造成10~30(+系数)伤害.\n\n伤害类型为普通攻击,可以触发大多数攻击特效.\n队友也可触发，伤害来源为你，但不触发攻击特效.\n此技能的伤害不会造成暴击.\n每点暴击和冷却加点提高2%伤害.\n\n系数: 60%物伤点数 70%法伤点数\n\n伤害: 〔数值〕\n\n冷却时间: 〔数值〕秒 - (17~11)",
		"在短时间内对一个敌人造成3次不同伤害后落下雷电造成\n25~150(+系数)点转化伤害.\n\n不是刺客角色只造成75%的伤害.刺客角色参与击杀后结束冷却.\n\n系数: 200%物伤点数 200%暴击点数 200%攻速点数 200%冷却点数 232%法伤点数\n\n伤害: 〔数值〕\n\n冷却时间: 〔数值〕秒",
		"对敌人造成技能伤害时制造神奇的流星向敌人发射,造成15~80(+系数)魔法伤害.\n伤害越近越高,最高100%,最低75%.\n\n若拥有D则减少1秒冷却.\n\n系数: 100%物伤点数 100%暴击点数 100%攻速点数 120%法伤点数\n\n伤害: 〔数值〕\n\n冷却时间: 〔数值〕秒(13~7)\n升级 +++ 合计每达到10点时提升一次抛射速度,最多提升10次.",
		"对敌人造成伤害时造成10~32+((生命值加点 * 0.24) + (物理抗性加点 * 1.2) + (法术抗性加点 * 1.2))点物理伤害.\n然后自身恢复10~32+((生命值加点 * 0.24) + (物理抗性加点 * 1.2) + (法术抗性加点 * 1.2))点生命值.\n\n输出角色只造成40%伤害,只恢复55%生命值\n\n恢复: 〔数值〕\n\n伤害: 〔数值〕\n\n冷却时间: 〔数值〕秒",
		"闪现距离提高30%且冷却减少50秒,5, 9, 13, 17级时额外减少15秒\n闪现后〔数值〕秒内对敌人伤害提高6~11%(+系数). (5, 9, 13, 17级时提升)\n\n系数: 14%物伤点数 14%暴击点数 14%攻速点数 17%法伤点数\n\n伤害加深: 〔数值〕%",
		"攻击敌人〔数值〕秒后提高10~19%(+系数)攻击速度.\n攻击速度增加持续〔数值〕秒, 每次攻击延长〔数值〕秒最多延长〔数值〕秒.\n\n系数: 20%物伤点数 20%暴击点数 20%冷却点数 24%法伤点数\n\n攻击速度: 〔数值〕%\n\n冷却时间: 〔数值〕秒(15~9)",
		"攻击距离常驻增加0.5.\n\n短时间内普通攻击一个敌人3次时额外进行一次快速的普通攻击,额外攻击的伤害为75~100%.\n\n伤害系数: 〔数值〕%\n\n冷却时间: 〔数值〕秒(30~15)\n\n※远程英雄和四季映姬无法选择该天赋",
		null,
		null,
		"按V键使用,使用时5~9秒内获得神之保佑,提高8%移动速度且每1秒消耗固定魔法治疗友方单位.\n\n魔法消耗: 4~27(+0.5%魔法值上限)\n生命恢复: 4~20(+1%基础魔法值上限)\n\n魔法值低于1%时该效果立刻解除.\n\n恢复: 〔数值〕\n\n持续时间: 〔数值〕秒\n\n冷却时间: 〔数值〕秒 - (60~34)",
		"对敌人进行攻击时恢复8~46(+系数)生命值并在0.8~1.3(+系数)秒内提高〔数值〕%移动速度.\n拥有此技能时白鸟的呼唤额外提高4%移动速度.\n\n系数: 56%物伤点数 56%暴击点数 56%攻速点数56%冷却点数 66%法伤点数\n\n恢复: 〔数值〕\n\n持续时间: 〔数值〕秒\n\n冷却时间: 〔数值〕秒(16~8)",
		"每回合首次受到伤害时，获得相当于该次伤害75%的护盾，持续到回合结束。\n\n每回合首次受到致命伤害时，保留1点生命并免疫伤害〔数值〕秒（0.74~1.2）；非刺客角色无敌结束后死亡。",
		"短时间内造成3次独立伤害后，移动速度提高1.5点，持续〔数值〕秒（2~2.92），并获得控制免疫，持续〔数值〕秒（1~1.46），并清除大部分控制效果。\n\n冷却时间：〔数值〕秒。",
		"每0.5秒对周围3.9码内的敌人造成魔法伤害，距离越远伤害越低，最远造成50%伤害。\n\n每秒伤害：〔数值〕\n基础伤害：5~28\n物抗与法抗加点系数：0.7\n生命加点系数：0.2",
		"你造成物理或法术伤害时，若目标对应抗性高于另一项抗性，则按较低抗性补足穿透差额。"
	};

	/// <summary>该系是否有这个天赋。</summary>
	public static bool Valid(int school, int rune)
	{
		return IdxOf(school, rune) >= 0 && Names[rune * 5 + school] != null;
	}

	/// <summary>天赋中文名；空槽或越界返回空串。</summary>
	public static string Name(int school, int rune)
	{
		if (school < 0 || school > 4 || rune < 0 || rune > 17) { return ""; }
		string s = Names[rune * 5 + school];
		return (s == null) ? "" : s;
	}

	/// <summary>天赋说明；没有说明时返回空串。</summary>
	public static string Desc(int school, int rune)
	{
		if (school < 0 || school > 4 || rune < 0 || rune > 17) { return ""; }
		string s = Descs[rune * 5 + school];
		return (s == null) ? "" : s;
	}

	/// <summary>取学派中文名；越界返回空串。</summary>
	public static string SchoolName(int school)
	{
		if (school < 0 || school >= Schools.Length) { return ""; }
		return Schools[school];
	}

	/// <summary>(系, rune) -> 105 位存档下标；越界返回 -1。</summary>
	public static int IdxOf(int school, int rune)
	{
		if (school < 0 || school > 4 || rune < 0 || rune > 17) { return -1; }
		return GridStart + rune * 5 + school;
	}

	/// <summary>105 位存档下标 -> (系, rune)；不在天赋区返回 false。</summary>
	public static bool Decode(int idx, out int school, out int rune)
	{
		school = 0;
		rune = 0;
		if (idx < GridStart || idx >= GridStart + GridCount) { return false; }
		int t = idx - GridStart;
		rune = t / 5;
		school = t % 5;
		return true;
	}

	/// <summary>rune 号翻成「第 N 行左/中/右」。</summary>
	public static string SlotText(int rune)
	{
		if (rune < 0 || rune > 17) { return ""; }
		int row = rune / 3;
		int slot = rune % 3;
		string where = (slot == 0) ? "左" : ((slot == 1) ? "中" : "右");
		return "第 " + (row + 1) + " 行" + where;
	}

	/// <summary>拼一条完整提示，例如「天赋 · 荣誉系 · 熊掌（第 5 行左）」。</summary>
	public static string Tip(int school, int rune, string prefix)
	{
		string s = SchoolName(school);
		string s2 = Name(school, rune);
		string s3 = SlotText(rune);
		string s4 = (s.Length == 0) ? ("系 " + school) : (s + "系");
		string s5 = (s2.Length == 0) ? ("rune " + rune) : s2;
		string s6 = (s3.Length == 0) ? "" : ("（" + s3 + "）");
		return prefix + s4 + " · " + s5 + s6;
	}

	/// <summary>每系天赋个数（用于校验）。</summary>
	public static int CountOf(int school)
	{
		int n = 0;
		for (int r = 0; r < 18; r++) { if (Valid(school, r)) { n++; } }
		return n;
	}
}
