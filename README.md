# SC-touhoubox · 东方月灵传助手

《东方月灵传》的 Windows 桌面辅助工具，C# / WPF / .NET 9。

## 下载

**[⬇ 下载最新版 SC-touhoubox.zip](https://github.com/RumiaTinge/SC-touhoubox/releases/latest)**

解压后直接双击 `SC-touhoubox.exe` 即可运行 —— 自带 .NET 运行时和全部图片资源，不用另外装东西。
（想自己编译见下面「编译与运行」。）

## 功能

| 功能 | 说明 |
| --- | --- |
| 房间悬浮窗 | 游戏内浮层显示 10 人房间：头像、昵称、MMR、排名、胜率、场次，自己那行标「我」；可折叠玩家列表和聊天记录，位置/透明度/字号可调 |
| 本局聊天 | 悬浮窗里显示**当前这局**的聊天记录（含表情），自己发的指令行（`-vk` 之类）默认过滤掉 |
| 防踢保活 | 三档：静默（只发游戏内指令）/ 切换程序 / 两者都用，可设间隔 |
| 开局警报 | 房间满人、对局开始时响铃 + 横幅提示，音调、时长、位置可调 |
| 对战数据 | 最近对局详情（双方 5v5，技能 / 召唤 / 符文 / 属性 / 加点图标）、英雄战绩、英雄加点、英雄名表编辑 |
| 本地存档 | 直接读游戏写的 `touhou.SC2Bank` 拿战绩和加点，**不联网**；站点读不到时自动顶上 |
| 天赋树 | 面板里的可视化加点器：五系天赋、依赖关系、三条加点规则强制、每个天赋的详细说明 |

托盘图标右键有菜单，热键：`Ctrl+Alt+Shift+A` 试响 / `+T` 显示隐藏悬浮窗 / `+K` 保活开关。

> 游戏显示模式要设成 **窗口化(全屏)**，独占全屏会盖住悬浮窗。

## 编译与运行

需要 **.NET 9 SDK**；运行需要 **.NET 9 桌面运行时**。

```powershell
# 在仓库根目录
powershell -ExecutionPolicy Bypass -File .\build.ps1          # 编译 + 打包到 dist\
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Run     # 再顺手启动
```

`dist\` 是自包含的一套（`SC-touhoubox.exe` + 依赖 dll + 图片资源 `art\`，约 5 MB / 408 个文件），拷到任意目录双击即用。

> `build.ps1` 里有中文，必须存成 **带 BOM 的 UTF-8**，否则 Windows PowerShell 5.1 会按 GBK 解析报语法错。

## 仓库结构

```
src\                 C# 源码
  Sc2Toolbox\        *.cs / *.xaml
  SC-touhoubox.csproj
art\                 图片资源 402 张（技能 / 英雄 / 符文 / 召唤 / 属性 / 区域）
build.ps1            编译 + 打包到 dist\
icon.ico             托盘图标
```

`art\` 通过 csproj 里的 `Content` 规则随构建拷贝到输出目录，所以**克隆下来直接编译就能跑**，不用另外下资源。

## 数据目录

设置、名表缓存、运行日志都在：

1. `C:\Users\<你>\AppData\Roaming\SC-touhoubox\`（首选）
2. 写不进去时回退到 exe 同级的 `data\`

里面有 `settings.json`（所有开关）、`heroes.tsv`（英雄名表）、`cache.json`（最近快照，离线也能看）、`toolbox.log`（运行日志，排查问题先看它）、`img\`（下载过的图标缓存）。

这些文件**不在仓库里**，是每台机器自己的运行数据。

## 说明

- 程序只允许跑一个实例，会在本机监听 `POST http://127.0.0.1:8030/lobbyPlayerList` 接收房间数据。
- 悬浮窗数据来自本机 `touhou_client` 的只读接口，程序**不改游戏**。
- `art\` 里是游戏本体的图标素材，随包是为了离线可用；仅供个人使用，版权归游戏作者所有。
- 程序申请的是 `asInvoker`（普通权限，不弹 UAC）。如果「防踢保活」在游戏里没反应，用**管理员身份运行**再试。

## 许可

MIT，见 LICENSE。