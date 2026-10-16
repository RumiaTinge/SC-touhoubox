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
		"每第3次攻击在0.45~0.75秒(+系数)内提高〔数值〕%移动速度.\n\n系数: 0.05%物伤点数 0.05%暴击点数 0.05%冷却点数 0.06%法伤点数\n\n持续时间: 〔数值〕秒\n\n冷却时间: 〔数值〕秒", // [0] r00 DouZheng
		"夜晚第2次提示时不会显示自己的方位,并提高0.05移动速度.剩余时间只剩1分钟时恢复35%视野,如果是刺客角色则恢复100%视野", // [1] r00 ZaiNan
		"冷却最大加点提高5点.\n\n 1点时免费获得一点.\n如果主天赋是魔法则9点时再获得一点.\n\n免费冷却加点时返还300块.", // [2] r00 MoFa  <<< NEW from mod; 感悟
		"生命值低于35%时在5秒内恢复30~150生命值.\n每点生命值加点提升1点,每点抗性加点提升5点.\n\n恢复量:  〔数值〕\n\n每回合一次", // [3] r00 RongYu  <<< NEW from mod; 阴阳调和
		"魔法值低于40%时在6秒内恢复50~180+(魔法值加点 * 11)魔法值.\n拥有魔法恢复D则额外恢复魔法值加点*3的魔法值.\n\n恢复量:  〔数值〕\n\n每回合一次", // [4] r00 ZhuFu  <<< NEW from mod; 准分子激光原地角膜消除术
		"每第3次攻击恢复5~20(+系数)生命值.\n\n系数: 25%物伤点数 25%暴击点数 25%冷却点数 30%法伤点数\n\n恢复: 〔数值〕\n\n冷却时间: 〔数值〕秒", // [5] r01 DouZheng
		"攻击敌人时恢复9~35(+系数)生命值.\n远程英雄只恢复65%生命值\n\n系数: 90%物伤点数 90%暴击点数 90%冷却点数 105%法伤点数\n\n恢复: 〔数值〕\n\n冷却时间: 〔数值〕秒(24~19)", // [6] r01 ZaiNan
		"4、7、10、13、16、19回合时增加法强 (累计: 50)", // [7] r01 MoFa  <<< NEW from mod; 黑魔法
		"回合开始时获得15~95+(生命值加点 * 1) + (物理抗性加点 * 3.5) + (法术抗性加点 * 3.5)点护盾\n\n护盾值:  〔数值〕", // [8] r01 RongYu  <<< NEW from mod; 阿瑞斯的头盔
		"16级时免费获得1点移速升级.\n\n16级之前最多4点移速升级.\n\n如果祝福不是主天赋则16级之前最多3点移速升级", // [9] r01 ZhuFu  <<< NEW from mod; 祝福之靴
		null, // [10] r02 DouZheng  <<< illegal slot
		null, // [11] r02 ZaiNan  <<< illegal slot
		null, // [12] r02 MoFa  <<< illegal slot
		null, // [13] r02 RongYu  <<< illegal slot
		null, // [14] r02 ZhuFu  <<< illegal slot
		"提高0.1~0.4基础武器射程。对敌人造成攻击伤害后进入战斗状态，持续3秒；持续攻击会刷新持续时间，3秒未攻击后进入冷却，冷却期间效果不会发动。\n\n射程提高: 〔数值〕\n\n冷却时间: 〔数值〕秒\n\n※近战英雄无法选择该天赋", // [15] r03 DouZheng
		"对被控制的敌人(僵直,定身,眩晕等无法行动的状态)造成伤害时额外造成5~40(+系数)的转化伤害.\n\n系数: 70%物伤点数 70%暴击点数 70%冷却点数 81%法伤点数\n\n伤害: 〔数值〕\n\n冷却时间: 〔数值〕秒(25~20)", // [16] r03 ZaiNan
		"对敌人造成技能伤害时射出火球,造成5~35(+70%法伤点数, +55%物伤点数)法术伤害.\n\n伤害:  〔数值〕\n\n冷却时间:  〔数值〕秒- (14~8)", // [17] r03 MoFa  <<< NEW from mod; 熊猫人的火焰吐息
		"受到的物理伤害降低5%.(计算护甲后的)", // [18] r03 RongYu  <<< NEW from mod; 坚固的墙
		"升级G技能.\n\n雷达持续时间增加3秒.\n兴奋剂持续时间增加3秒.\n力场使用次数+2.\n侦测持续时间增加8秒.", // [19] r03 ZhuFu  <<< NEW from mod; 神奇的古代魔法书
		"暴击时恢复伤害值13%的生命值", // [20] r04 DouZheng  <<< NEW from mod; 致命子弹
		"脱离战斗后提高0.05~0.1移动速度.\n(在5, 9, 13, 17, 21级时提升)\n\n当前移动速度: 〔数值〕", // [21] r04 ZaiNan
		"使用D、F、G技能(立场除外)后在1.4秒内提高0.14移动速度.\n\n若拥有魔法恢复则持续1.9秒", // [22] r04 MoFa
		"受到法术伤害时在〔数值〕秒内减少受到的法术伤害9.2%+((物理抗性加点 * 0.17)% + (法术抗性加点 * 0.17)%).\n\n减伤: 〔数值〕%\n\n冷却时间: 〔数值〕秒", // [23] r04 RongYu
		"回合开始的移速增加时间延长1.25~3.75秒", // [24] r04 ZhuFu  <<< NEW from mod; 心情愉快
		null, // [25] r05 DouZheng  <<< illegal slot
		null, // [26] r05 ZaiNan  <<< illegal slot
		null, // [27] r05 MoFa  <<< illegal slot
		null, // [28] r05 RongYu  <<< illegal slot
		null, // [29] r05 ZhuFu  <<< illegal slot
		"攻击力提高7，每级额外提高1点，最多提高30点。\n\n攻击速度加点上限提高6点。", // [30] r06 DouZheng
		"提高6攻击力且每级提高1.5攻击力", // [31] r06 ZaiNan  <<< NEW from mod; 战斗的基础
		"提升攻击力6，每级额外增加1.5(最大 40.5)", // [32] r06 MoFa  <<< NEW from mod; 战斗的基础
		"9-12-15-18-21-24级时生命值增加67", // [33] r06 RongYu  <<< NEW from mod; 强壮的肉体
		"增加魔法值70，且每级额外增加22. (最大 576)", // [34] r06 ZhuFu  <<< NEW from mod; 增加活力
		"基础法强上升12，每级额外增加 3 （最大 81)", // [35] r07 DouZheng  <<< NEW from mod; 蓝色灵气
		"提升12点法强, 每级额外增加3点 (最大 81)", // [36] r07 ZaiNan  <<< NEW from mod; 蓝色灵气
		"提升12点法强, 每级额外增加3点 (最大 81)", // [37] r07 MoFa  <<< NEW from mod; 蓝色灵气
		null, // [38] r07 RongYu  <<< illegal slot
		"增加4攻击力且每级增加1攻击力(总计27)\n增加10.3生命值且每级增加5.6生命值(总计139.1)", // [39] r07 ZhuFu
		null, // [40] r08 DouZheng  <<< illegal slot
		null, // [41] r08 ZaiNan  <<< illegal slot
		null, // [42] r08 MoFa  <<< illegal slot
		null, // [43] r08 RongYu  <<< illegal slot
		"增加8法术强度且每级增加2法术强度(总计54)\n增加10.3生命值且每级增加5.6生命值(总计139.1)", // [44] r08 ZhuFu
		"提高8.8% ~ 18%物抗穿透", // [45] r09 DouZheng  <<< NEW from mod; 护甲穿透
		"提高8.8% ~ 18%物抗穿透", // [46] r09 ZaiNan  <<< NEW from mod; 护甲无效化
		"提高8.8% ~ 18%物抗穿透", // [47] r09 MoFa  <<< NEW from mod; 护甲无效化
		"5, 9, 13, 17级时提高1.2物理抗性.\n物理抗性加点额外获得0.1物理抗性.", // [48] r09 RongYu  <<< NEW from mod; 神奇的盾
		"每回合额外获得4点经验值.\n(1, 2回合除外)", // [49] r09 ZhuFu  <<< NEW from mod; 经验值诈骗
		"提高8.8% ~ 18%法抗穿透", // [50] r10 DouZheng  <<< NEW from mod; 抗性穿透
		"提高8.8% ~ 18%法抗穿透", // [51] r10 ZaiNan  <<< NEW from mod; 抗性无效化
		"提高8.8% ~ 18%法抗穿透", // [52] r10 MoFa  <<< NEW from mod; 抗性无效化
		"5, 9, 13, 17级时提高1.2法术抗性.\n法术抗性加点额外获得0.1法术抗性.", // [53] r10 RongYu  <<< NEW from mod; 抵抗之盾
		"每回合额外获得90金币(1,2回合除外）\n\n参与击杀额外获得20金币", // [54] r10 ZhuFu  <<< NEW from mod; 金币小偷
		null, // [55] r11 DouZheng  <<< illegal slot
		null, // [56] r11 ZaiNan  <<< illegal slot
		null, // [57] r11 MoFa  <<< illegal slot
		null, // [58] r11 RongYu  <<< illegal slot
		null, // [59] r11 ZhuFu  <<< illegal slot
		"普通攻击伤害提升12%", // [60] r12 DouZheng  <<< NEW from mod; 龙之愤怒 (打击)
		"所有物理伤害增加8%", // [61] r12 ZaiNan  <<< NEW from mod; 龙之愤怒 (物理)
		"所有物理伤害增加8%", // [62] r12 MoFa  <<< NEW from mod; 龙之愤怒 (物理)
		"收到的伤害减少9%", // [63] r12 RongYu  <<< NEW from mod; 最后的防御
		"受到伤害减少9%", // [64] r12 ZhuFu  <<< NEW from mod; 最后的防御
		"魔法伤害提高8%", // [65] r13 DouZheng  <<< NEW from mod; 龙之愤怒 (魔法)
		"所有魔法伤害增加8%", // [66] r13 ZaiNan  <<< NEW from mod; 龙之愤怒（魔法）
		"所有魔法伤害增加8%", // [67] r13 MoFa  <<< NEW from mod; 龙之愤怒（魔法）
		null, // [68] r13 RongYu  <<< illegal slot
		null, // [69] r13 ZhuFu  <<< illegal slot
		null, // [70] r14 DouZheng  <<< illegal slot
		null, // [71] r14 ZaiNan  <<< illegal slot
		null, // [72] r14 MoFa  <<< illegal slot
		null, // [73] r14 RongYu  <<< illegal slot
		null, // [74] r14 ZhuFu  <<< illegal slot
		"每次攻击给予对方一个持续〔数值〕秒的集中射击标志,叠加了3个标记的敌人会在〔数值〕秒内暴露弱点,对集中目标的敌人进行攻击时每次造成10~30(+系数)伤害.\n\n伤害类型为普通攻击,可以触发大多数攻击特效.\n队友也可触发，伤害来源为你，但不触发攻击特效.\n此技能的伤害不会造成暴击.\n每点暴击和冷却加点提高2%伤害.\n\n系数: 60%物伤点数 70%法伤点数\n\n伤害: 〔数值〕\n\n冷却时间: 〔数值〕秒 - (17~11)", // [75] r15 DouZheng
		"在短时间内对一个敌人造成3次不同伤害后落下雷电造成\n25~150(+系数)点转化伤害.\n\n不是刺客角色只造成75%的伤害.刺客角色参与击杀后结束冷却.\n\n系数: 200%物伤点数 200%暴击点数 200%攻速点数 200%冷却点数 232%法伤点数\n\n伤害: 〔数值〕\n\n冷却时间: 〔数值〕秒", // [76] r15 ZaiNan
		"对敌人造成技能伤害时制造神奇的流星向敌人发射,造成15~80(+系数)魔法伤害.\n伤害越近越高,最高100%,最低75%.\n\n若拥有D则减少1秒冷却.\n\n系数: 100%物伤点数 100%暴击点数 100%攻速点数 120%法伤点数\n\n伤害: 〔数值〕\n\n冷却时间: 〔数值〕秒(13~7)\n升级 +++ 合计每达到10点时提升一次抛射速度,最多提升10次.", // [77] r15 MoFa
		"对敌人造成伤害时造成10~32+((生命值加点 * 0.24) + (物理抗性加点 * 1.2) + (法术抗性加点 * 1.2))点物理伤害.\n然后自身恢复10~32+((生命值加点 * 0.24) + (物理抗性加点 * 1.2) + (法术抗性加点 * 1.2))点生命值.\n\n输出角色只造成40%伤害,只恢复55%生命值\n\n恢复: 〔数值〕\n\n伤害: 〔数值〕\n\n冷却时间: 〔数值〕秒", // [78] r15 RongYu
		"闪现距离提高30%且冷却减少50秒,5, 9, 13, 17级时额外减少15秒\n闪现后〔数值〕秒内对敌人伤害提高6~11%(+系数). (5, 9, 13, 17级时提升)\n\n系数: 14%物伤点数 14%暴击点数 14%攻速点数 17%法伤点数\n\n伤害加深: 〔数值〕%", // [79] r15 ZhuFu
		"攻击敌人〔数值〕秒后提高10~19%(+系数)攻击速度.\n攻击速度增加持续〔数值〕秒, 每次攻击延长〔数值〕秒最多延长〔数值〕秒.\n\n系数: 20%物伤点数 20%暴击点数 20%冷却点数 24%法伤点数\n\n攻击速度: 〔数值〕%\n\n冷却时间: 〔数值〕秒(15~9)", // [80] r16 DouZheng
		"攻击距离常驻增加0.5.\n\n短时间内普通攻击一个敌人3次时额外进行一次快速的普通攻击,额外攻击的伤害为75~100%.\n\n伤害系数: 〔数值〕%\n\n冷却时间: 〔数值〕秒(30~15)\n\n※远程英雄和四季映姬无法选择该天赋", // [81] r16 ZaiNan
		"按V使用,使用时向指定方向快速冲刺,前进时对前方释放魔法之拳引发冲击波,造成20~115(+系数)转化伤害(物理或魔法).\n\n系数:  70%物伤点数 70%暴击点数 70%攻速点数 140%法伤点数\n\n伤害:  〔数值〕\n\n冷却时间:  〔数值〕秒", // [82] r16 MoFa  <<< NEW from mod; 魔法之拳
		"生命值降低到30%以下时进入愤怒状态.\n\n愤怒状态持续6秒并生成一个护盾.\n\n如果自身生命值加点上限大于87则降低周围敌人45%移动速度.\n\n获得95~190+((生命值加点 * 0.5) + (物理抗性加点 * 5.9) + (法术抗性加点 * 5.9))点护盾.\n\n输出英雄护盾值只有60%.", // [83] r16 RongYu  <<< NEW from mod; 雷神之怒
		"按V键使用,使用时5~9秒内获得神之保佑,提高8%移动速度且每1秒消耗固定魔法治疗友方单位.\n\n魔法消耗: 4~27(+0.5%魔法值上限)\n生命恢复: 4~20(+1%基础魔法值上限)\n\n魔法值低于1%时该效果立刻解除.\n\n恢复: 〔数值〕\n\n持续时间: 〔数值〕秒\n\n冷却时间: 〔数值〕秒 - (60~34)", // [84] r16 ZhuFu
		"对敌人进行攻击时恢复8~46(+系数)生命值并在0.8~1.3(+系数)秒内提高〔数值〕%移动速度.\n拥有此技能时白鸟的呼唤额外提高4%移动速度.\n\n系数: 56%物伤点数 56%暴击点数 56%攻速点数56%冷却点数 66%法伤点数\n\n恢复: 〔数值〕\n\n持续时间: 〔数值〕秒\n\n冷却时间: 〔数值〕秒(16~8)", // [85] r17 DouZheng
		"每回合首次受到伤害时，获得相当于该次伤害75%的护盾，持续到回合结束。\n\n每回合首次受到致命伤害时，保留1点生命并免疫伤害〔数值〕秒（0.74~1.2）；非刺客角色无敌结束后死亡。", // [86] r17 ZaiNan
		"短时间内造成3次独立伤害后，移动速度提高1.5点，持续〔数值〕秒（2~2.92），并获得控制免疫，持续〔数值〕秒（1~1.46），并清除大部分控制效果。\n\n冷却时间：〔数值〕秒。", // [87] r17 MoFa
		"每0.5秒对周围3.9码内的敌人造成魔法伤害，距离越远伤害越低，最远造成50%伤害。\n\n每秒伤害：〔数值〕\n基础伤害：5~28\n物抗与法抗加点系数：0.7\n生命加点系数：0.2", // [88] r17 RongYu
		"你造成物理或法术伤害时，若目标对应抗性高于另一项抗性，则按较低抗性补足穿透差额。", // [89] r17 ZhuFu
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
