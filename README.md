# 米莉拉角色拓展

**Milira Character Expansion · RimWorld 1.6 · v1.1 开发分支**

![米莉拉角色拓展封面](About/Preview.png)

为米莉拉的世界加入有自己故事、装备与战斗方式的特殊角色。

本模组基于 **Milira Race** 与 **Ariandel Library**，目前包含 **霓羽、清荷、昭离、明渊** 四位角色，以及专属招募任务、武器与服装、技能、资源机制和动画特效。它侧重角色体验与高辨识度的战斗设计，原始档位的强度明显高于原版。

[Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3684504594) · [下载完整安装包（Releases）](https://github.com/HeChuanRiver/MiliraXian_NeiyuLaw/releases) · [问题反馈](https://github.com/HeChuanRiver/MiliraXian_NeiyuLaw/issues) · [提交记录](https://github.com/HeChuanRiver/MiliraXian_NeiyuLaw/commits/dev/)

> 本文介绍 GitHub `dev` 分支，不代表创意工坊已发布全部相同内容。开发版仍在调整功能、数值和兼容性；用于长期存档前，请先备份并在副本中测试。

## 内容导航

- [角色与玩法](#角色与玩法)
- [安装与依赖](#安装与依赖)
- [三档强度设置](#三档强度设置)
- [兼容性与存档](#兼容性与存档)
- [反馈问题](#反馈问题)
- [开发与构建](#开发与构建)
- [贡献与致谢](#贡献与致谢)

## 角色与玩法

| 角色 | 核心玩法 | 内容概览 |
| --- | --- | --- |
| **霓羽** | 多形态武器、机动与护盾 | 花、剑、弓形态切换，近远程技能与阶段护盾；配有专属开局和投影招募事件。 |
| **清荷** | 花庭、剑术与成长 | 专属武器、剑压与花令资源、护盾和技能树；通过花庭相关内容展开角色支线。 |
| **昭离** | 因果、死亡与归返 | 「离断」与断斩、告死流场、诡医、定数、冥火、泯神；因果链接、替死、死亡成长，以及包含敌对遭遇的招募流程。 |
| **明渊** | 生命燃烧、自燃与过燃 | 「尘烬」「长虹」及聚焦／辐射形态，护焰修复与重生；配有独立衣装、头部、眼睛、光环、翅膀素材及白焰任务。 |

开发分支还包含角色传记与解锁奖励框架、简体中文／繁体中文／英文文本，以及耳羽、翅膀、光环和部分技能的动画支持。各角色的系统并不完全相同。

### 如何遇到角色

| 角色 | 当前入口 |
| --- | --- |
| 霓羽 | 专属开局「界外羽痕」，或「异界羽影」招募任务。 |
| 清荷 | 专属开局「花庭初绽」，或从「来自百花的呼唤」开始的花庭支线。 |
| 昭离 | 「亡者说」与「归亡者」任务链，包含藏身处交涉与后续选择。 |
| 明渊 | 「白焰征兆」任务，包含征兆、防守重生火焰与后续招募选择。 |

这些角色不是启用模组后同时自动加入。任务有各自的进度条件；请阅读来信中的目标、期限与后果，尤其不要忽略昭离任务的限时交涉和袭击警告。

## 安装与依赖

### 运行环境

- **RimWorld 1.6**。
- **Royalty、Biotech**：当前 Milira Race 的前置要求。
- 本仓库的 Unity AssetBundle 目前仅提供 **Windows** 构建；Linux／macOS 的动画与特效完整性尚未验证。

### 必需模组

本模组直接依赖：

| 模组 | 用途 |
| --- | --- |
| [Milira Race](https://steamcommunity.com/sharedfiles/filedetails/?id=3256974620) | 米莉拉种族及相关基础定义。 |
| [Ariandel Library](https://steamcommunity.com/sharedfiles/filedetails/?id=3665997350) | 特殊角色管理和共用功能。 |

还需要安装这些前置自身要求的 [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)、[Humanoid Alien Races](https://steamcommunity.com/sharedfiles/filedetails/?id=839005762) 和 [Ancot Library](https://steamcommunity.com/sharedfiles/filedetails/?id=2988801276)。具体版本与完整依赖请以各前置页面及游戏的依赖提示为准。

### 安装开发版

1. 在上方 Releases 页面下载对应版本的 `MiliraXian_NeiyuLaw-版本号.zip` 完整安装包。开发版标记为预发布版（Pre-release）。不要下载页面自动生成的 `Source code`，也不要将分支 ZIP 直接当作安装包。
2. 将模组文件夹放入 `RimWorld/Mods/`。文件夹内应直接包含 `About/`、`1.6/`、`Content/` 和 `LoadFolders.xml`，不要多套一层解压目录。
3. 在游戏模组列表启用前置与「米莉拉角色拓展 v1.1dev」，处理缺失依赖和排序提示后重启游戏。
4. 进入模组设置，按需选择角色强度；第一次测试建议使用新存档或现有存档副本。

Release 安装包包含运行用 DLL、定义、贴图、语言文件与资源包，**只游玩不需要安装 .NET SDK 或 Unity，也不需要自行编译**。源码仓库不再跟踪 DLL／PDB；克隆仓库用于开发时，需要自行编译。更新安装包时先移走旧模组目录，再解压新版本，避免已经改名或移除的 DLL 留在目录中。

### 加载顺序

先加载游戏与 DLC，再按依赖关系加载基础库、种族模组，最后加载本模组。Harmony 应位于依赖它的模组之前。

本模组要求位于 **Milira Race**、**Ariandel Library** 之后；若同时使用米莉拉基因补丁、米莉拉面部动画补丁、Milira Imperium 或 `HeChuanRiver.MiliraXian`，也应将本模组放在它们之后。后面这些项目是条件排序对象，不是全部必装前置。

不要同时启用本模组的工坊版与本地开发版，以免同名 Def、角色和补丁重复加载。

## 三档强度设置

入口：**选项 → 模组设置 → 米莉拉角色拓展**。

目前 **霓羽、昭离、明渊可以分别选择档位**，默认均为第一档；**清荷尚未接入这套三档设置**。

| 档位 | 设计目标 | 主要变化 |
| --- | --- | --- |
| **第一档：原始强度** | 体验完整的原始设计 | 保持原有技能、装备与被动效果；这是默认档位。 |
| **第二档：适度强度** | 完整的角色技能与适中的战斗数值 | 可使用形态切换、处决、治疗、复活和资源循环。 |
| **第三档：观赏模式** | 以角色陪伴、外观和剧情为主 | 封禁专属主动技能与核心战斗被动，进一步降低装备强度；仍可进行普通攻击和正常生活。 |

第二档的三位角色均可使用完整的特色能力：霓羽拥有斩首、群体治疗与多阶段护盾；昭离拥有告死处决、断肢修复、复活和链接替死；明渊拥有燃命传播、时间燃烧、双形态弓、特殊抗性与护焰修复。具体效果见技能说明，属性和冷却以当前信息面板为准。

使用时请注意：

- 这是**模组级设置**，不是每个存档或每一只 Pawn 单独保存的难度选项；专属装备定义也会随对应角色档位调整。
- 切回第一档会恢复原始配置，但不会撤销已经发生的伤害、治疗、死亡、资源消耗等游戏事件。
- 降档会处理已有的强化、层数和持续效果；昭离、明渊在第三档不会继续触发新的自动复活，已经进入离场返回流程的角色保留必要的返回保护。
- 装备品质、敌人类型和其他模组会影响实战强度。欢迎附上具体配置和战斗情况反馈。

设置页还提供特殊角色管理器集成、更新来信和意识保底选项。管理器集成默认开启，用于角色注册与读档修复；意识保底还会受到对应角色强度档位的限制。

## 兼容性与存档

### 可选联动

- **Facial Animation**：启用后加载专用眼睛与眼睑定义。明渊高光随眼球绘制，闭眼时一同隐藏，避免重复叠加。
- **Melee Animation**：启用后加载对应兼容目录；兼容层源码位于 `Source/Compat/MeleeAnimation/`。
- **米莉拉相关拓展**：按 `About/About.xml` 中的排序声明加载；有排序声明不等于已经验证所有组合。

上述两项可选联动由 [LoadFolders.xml](LoadFolders.xml) 按模组是否启用决定是否加载。不使用它们时，不需要手动复制兼容目录。

本仓库没有提供 Combat Extended 专用兼容层，不应默认其武器与伤害系统已经适配。其他修改死亡、复活、伤害结算、人物渲染或特殊角色唯一性的模组，也需要组合测试。

### 更新与移除

- 更新前备份存档，并记录使用的 Git 提交或工坊版本；不要混用不同版本的 DLL 与 XML。
- 开发分支含自定义 Hediff、任务、人物状态和存档数据。已有角色或任务的存档，**不建议中途直接卸载**。
- 不要将“切回第一档”当作回滚存档。需要回退一次更新时，应同时恢复匹配的模组版本和更新前存档。
- 项目包含缓存清理、档位过渡和复活返回保护，但这不等于任意旧版本之间都可以无风险热切换。

## 反馈问题

请到 [GitHub Issues](https://github.com/HeChuanRiver/MiliraXian_NeiyuLaw/issues) 提交可复现的问题，尽量附上：

1. RimWorld 版本、启用的 DLC，以及本模组的 Git 提交号或工坊版本。
2. 完整模组列表与加载顺序，尤其是种族、前置库、战斗及动画类模组。
3. 涉及的角色、强度档位、装备和当前状态。
4. 从正常状态到报错的操作步骤，以及预期结果和实际结果。
5. 日志中**第一次出现的错误及完整堆栈**；必要时附截图或可复现的存档副本。

Windows 的日志通常位于：

```text
%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
```

提供日志或存档前，请检查其中是否包含不希望公开的用户名、路径等信息。反馈卡顿时，也请说明地图规模、参战人数、游戏速度，以及是否只有某个技能或特效出现时才发生。

## 开发与构建

### 目录结构

```text
About/                     模组信息、封面与工坊 ID
1.6/
  Assemblies/              主程序集
  AssetBundles/Windows/    动画与特效资源包
  Defs/                    角色、技能、装备、任务等定义
  Languages/               简体中文、繁体中文、英文
  Mods/                    按需加载的兼容内容
  Patches/                 XML 补丁
Content/Textures/          游戏贴图
Source/
  Common/                  共用资源、异常状态、传记、技能树与工具
  Neiyu/                   霓羽
  QingHe/                  清荷
  Zhaoli/                  昭离
  Mingyuan/                明渊
  Compat/                  外部模组兼容源码
Tools/
  Unity/                   耳羽与翅膀资源构建脚本
  UnityVfx/                角色特效资源构建脚本
  Validation/              档位配置与快照回归检查
LoadFolders.xml            版本与条件加载入口
```

### 本地化约定

Def 中的可翻译文本直接使用**简体中文**，并作为唯一原文，包括 `label`、`description`、标题、消息、列表文字和自定义可翻译字段。项目不再保留简体中文 `DefInjected`；简体中文直接显示 Def 原文。

繁体中文和 English 的 `DefInjected` 必须以简体中文 Def 为准。修改原文时同步更新对应译文，补齐缺失条目并删除失效键，保持占位符、富文本标签、语法规则前缀和含义一致。列表字段应先核对继承后的结构；有翻译句柄时优先使用句柄，避免组件或剧本部件的数字索引随继承、DLC 改变而错位。

`Keyed` 继续独立维护：以简体中文 Keyed 为原文，同步繁体中文和英文。仅调整 DefInjected 时，不改动 Keyed 文本。

### 编译主程序集

项目采用 **SDK 风格 C# 工程**，语言版本为 **C# 10**，目标框架为 **.NET Framework 4.8.1**。以下步骤以 Windows 为基准。

准备好支持 C# 10 的 .NET SDK、.NET Framework 4.8.1 Developer Pack、[NuGet CLI](https://learn.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-restore)，以及本机 RimWorld 与前置模组程序集。已经使用 .NET SDK 9.0.306 验证过主项目构建。

在仓库根目录创建 `Directory.Build.local.props`，填写自己的安装位置。这个文件已被 Git 忽略，请勿提交个人路径：

```xml
<Project>
  <PropertyGroup>
    <RimWorldInstallDir>C:\Games\Steam\steamapps\common\RimWorld</RimWorldInstallDir>
    <RimWorldWorkshopDir>C:\Games\Steam\steamapps\workshop\content\294100</RimWorldWorkshopDir>
  </PropertyGroup>
</Project>
```

如果使用本地版前置或不同的目录结构，可以在同一文件中覆盖 `RimWorldManagedDir`、`AriandelLibraryDll`、`AlienRaceDll`、`ZAnimationModDll`；默认解析规则见 [Directory.Build.props](Directory.Build.props)。

在仓库根目录运行：

```powershell
nuget restore .\packages.config -PackagesDirectory .\packages
dotnet restore .\MiliraXian_NeiyuLaw.csproj
dotnet build .\MiliraXian_NeiyuLaw.csproj --configuration Release --no-restore
```

Harmony 2.4.2 使用 `packages.config` 与本地 `HintPath` 引用，**仅执行 `dotnet restore` 不会替代第一步的 Harmony 包恢复**。其他游戏和前置程序集由本机安装提供，不由本仓库分发。

主项目输出到 `1.6/Assemblies/MiliraXian_NeiyuLaw.dll`；将 `Release` 改为 `Debug` 可构建调试版本。二者使用同一输出目录，不要在游戏运行时覆盖程序集。

Melee Animation 兼容层是单独的 `MiliraXian_MACompat.csproj`，需要安装对应模组并正确设置 `ZAnimationModDll`；它不属于主项目的编译范围。

### 手动制作与发布安装包

准备好上述本地构建依赖（包括 Melee Animation）及 PowerShell 7，将此次源码和资源更新提交后，在仓库根目录执行：

```powershell
pwsh -File .\Tools\Release\New-ReleasePackage.ps1
```

默认打包 `HEAD`，版本名自动包含时间与提交号。也可以明确指定已有提交和版本名：

```powershell
pwsh -File .\Tools\Release\New-ReleasePackage.ps1 -Revision HEAD -Version dev-20260926-01
```

脚本从指定提交创建独立源码副本，读取本机依赖路径，分别重新编译主程序集与近战动画兼容层，再打包同一提交的游戏资源。未提交或未跟踪的文件不会进入安装包；不会使用工作目录的旧 DLL，也不会覆盖本地游戏正在使用的程序集。Harmony 优先使用本地对应版本的包，缺失时从 NuGet 下载。

输出位于 Git 忽略的 `.release/`：完整 ZIP 及其 `.sha256` 校验文件。包内的 `build-info.json` 记录源码提交、构建时间和依赖校验值，不包含个人安装路径。缺少依赖、编译失败或 XML 解析失败时停止打包。脚本不自动上传、不创建标签，也不发布到创意工坊。

手动发布步骤：

1. 检查安装包的游戏加载及可选联动，并确认对应提交已推送到远程。
2. 打开 GitHub Releases，创建新 Release，将标签指向安装包 `build-info.json` 中的准确提交。可先在本地用 `git tag 标签名 提交号` 创建标签，再用 `git push origin 标签名` 推送。
3. 上传 `.release/` 中对应的 ZIP 和 `.sha256`，填写更新说明；开发版勾选 **Pre-release**，检查后发布。

每次更新手动制作、上传一份安装包；本仓库不设置自动构建或定时发布。GitHub 的 `Source code` 附件只有源码与资源，不能代替完整安装包。外部前置仍由玩家单独安装，其 DLL 不随本模组分发。

### 资源与验证

普通 C# 构建不会重新生成 Unity AssetBundle。相关编辑器脚本位于 `Tools/Unity/` 和 `Tools/UnityVfx/`，其中仍有本机构建路径；复用前需要调整路径并准备相应的 Unity 工程与输入资源。

现有验证入口：

| 文件 | 检查范围 |
| --- | --- |
| [Test-CharacterPower.ps1](Tools/Validation/Test-CharacterPower.ps1) | 解析 XML，检查昭离／明渊档位配置的 Def 引用、反射字段、技能封禁入口与三语言设置键覆盖。 |
| [CharacterPowerSnapshotTests.cs](Tools/Validation/CharacterPowerSnapshotTests.cs) | 对实际快照引擎执行 100 轮切档，检查数值与引用恢复、角色配置独立性、类型转换和非法值回退。 |

这些是开发辅助检查，不是完整的游戏自动化测试。PowerShell 检查需要提供 `Mono.Cecil.dll` 路径，现有测试还含安装目录假设；换机器或将仓库放在游戏目录之外时，请先核对脚本中的依赖路径。编译和静态检查通过之后，仍需在游戏中测试加载、读档、战斗、切档和角色返回流程。

## 贡献与致谢

项目作者：**HeChuanRiver**。

感谢 Milira Race、Ancot Library、Ariandel Library、Humanoid Alien Races、Harmony 及可选联动模组的作者，也感谢提供角色美术、封面、测试与反馈的贡献者。美术与社区致谢可参见 [创意工坊页面](https://steamcommunity.com/sharedfiles/filedetails/?id=3684504594)，代码贡献记录见 [Contributors](https://github.com/HeChuanRiver/MiliraXian_NeiyuLaw/graphs/contributors)。

提交改动时请以 `dev` 为目标分支，在说明中写清修改动机、影响角色、档位行为和验证方式。涉及玩家可见文本时，请同步检查三种语言；不要提交临时产物、游戏存档、个人安装路径或第三方游戏程序集。

仓库目前未附统一的 `LICENSE` 文件。复用代码、美术或资源包前，请向维护者确认相应内容的授权范围；本 README 不额外授予第三方素材的使用许可。
