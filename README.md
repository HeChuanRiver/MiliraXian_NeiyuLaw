# MiliraXian_Characters

`MiliraXian_Characters` 是一个面向 RimWorld 1.6 的角色扩展工程。  
从当前仓库实际内容来看，它已经不再是早期只围绕“霓羽”维护的单角色项目，而是以同一套主工程同时承载：

- `Neiyu / 霓羽`
- `QingHe / 清荷`
- `Zhaoli / 昭离`
- `Common` 通用战斗与资源底层

需要注意的是，仓库名已经体现出“多角色工程”的方向，但主程序集、解决方案和部分历史文件仍保留了早期命名，例如 `MiliraXian_NeiyuLaw.sln`、`MiliraXian_NeiyuLaw.csproj`。这是当前工程状态的一部分，并不代表仓库内容仍然只有霓羽。

## 工程状态速览

| 模块 | 当前状态 | 备注 |
| --- | --- | --- |
| `Neiyu / 霓羽` | 完成度最高 | 已具备独立玩法入口、较完整资源和兼容模块 |
| `QingHe / 清荷` | 玩法逻辑已成型 | C#、Def、武器形态和核心机制已接入，但美术、图标、VFX、语言仍大量占位 |
| `Zhaoli / 昭离` | 核心机制已落地 | 主要技能与被动系统已写入工程，但人物外观、专属资源和部分 Def 细节尚未收口 |
| `Common` | 已投入使用 | 提供专属资源、追踪弹道、持续拖尾、装备保护等通用能力 |
| `1.6/Defs` | 三角色均已接入 | 霓羽额外拥有剧本、任务和事件入口 |
| `1.6/Languages` | 部分完成 | 霓羽覆盖较完整，清荷与昭离仍主要依赖 Def 内中文文本 |
| `Content/Textures` | 资源层偏早期 | 当前仓库内正式贴图仍以 `MiliraXianNeiyu` 为主 |
| `About/About.xml` | 未同步到当前范围 | 仍按“霓羽单角色模组”描述，与现有仓库内容不完全一致 |
| 构建链路 | 可维护但不够可移植 | 依赖本地 RimWorld、Workshop 和第三方模组 DLL 路径 |

## 当前角色状态

### 霓羽 `Neiyu`

霓羽是当前仓库中最成熟、最接近完整交付形态的角色模块，代码、Def、语言和贴图都比较齐全。结合现有源码、Def 和既有 README，可以确认目前已经具备：

- 三形态武器切换
- 七个主动技能
- 三阶段计数护盾
- 翅膀与飞行动画
- 独立剧本
- 招募事件与招募任务
- 特殊角色管理器接入
- `Facial Animation` 条件兼容
- `Melee Animation` 条件兼容

霓羽目前仍是整个工程里“玩法入口最完整、资源最完整、对外说明最接近真实状态”的部分。

### 清荷 `QingHe`

清荷已经不是“概念稿”阶段，而是进入了“玩法逻辑基本成型、表现层待补齐”的阶段。当前仓库中已存在完整目录 `Source/QingHe`，并已纳入主项目编译。

已经落地的内容包括：

- 双资源循环：`激流 / Tempest` 与 `雅乐 / Elegance`
- 保命机制：`长息`
- 护盾机制：`花神护体`、`水镜`
- 主要主动技能：
  - `涌泉`
  - `水镜`
  - `扼流`
  - `横指`
  - `断魂`
  - `阳春`
- 三形态武器：
  - `琵琶`
  - `竹笛`
  - `琴`
- 持续场系统、状态面板、投射物、曲线追踪与拖尾表现
- 调试技能：
  - `Max Tempest`
  - `Max Elegance`

但清荷当前仍有明显缺口：

- 大量技能图标仍复用霓羽资源
- 多个技能的施法前摇、脉冲和收束 VFX 仍不足
- `Thing_SpawnedField.cs` 中仍保留周期图形效果的 TODO
- PawnKind 仍复用霓羽服装、头像、头型等占位资源
- `Content/Textures` 下尚未看到独立的清荷正式贴图目录
- 现有文档与 `ISSUES.md` 显示，`水镜` 的文本描述与当前实际实现存在偏差

整体上，清荷已经具备“可继续打磨”的主干，不再是从零开始的角色。

### 昭离 `Zhaoli`

昭离当前处于“核心系统已经落地，但角色资料和资源层尚未完全收口”的状态。她的源码目录、Def、Hediff 和武器文件都已进入主工程。

已经实现或已明确接入的内容包括：

- 因果资源系统
- 因果链接网络
- 因果溢出与沉眠
- 讳亡护盾
- 替死复苏与归亡者重生
- 主动技能：
  - `告死流场`
  - `诡医`
  - `冥火`
  - `泯神`
- 专武技能：
  - `断斩`

目前昭离的主要待完善点也很明确：

- `PawnKindDef` 中仍有大量 TODO
- 服装、头型、头像、特质等仍复用或占位自霓羽
- `SpecialPawnExtension` 中与角色管理器相关的头像与说明仍未正式启用
- 技能图标仍大量复用霓羽资源
- 飞行动画、光环、头发、FA 眼睛/眼皮等资源仍待替换
- 当前仓库内尚未看到独立的昭离正式贴图目录

也就是说，昭离的“玩法 C#”明显先于“角色资源和包装层”推进。

## 通用层与兼容模块

### `Source/Common`

`Common` 已经不只是一个预留目录，而是在主项目中实际承载多角色复用逻辑。当前可见的通用能力包括：

- 专属资源底座：`PawnSpecialResource`、`PawnSpecialResourceUtility`
- 不可破坏装备：`CompUnbreakableEquipment`
- 调试资源补满能力：`CompAbilityEffect_DebugMaxResource`
- 曲线追踪弹道：`CompProjectileHomingCurve`、`ProjectileHomingCurveBase`
- 连续拖尾：`CompProjectileContinuousTrail`
- 若干基础工具类与通用组件

从现在的结构看，后续若再加入新角色，继续复用 `Common` 是比较自然的方向。

### 条件兼容

当前 `LoadFolders.xml` 已配置两个条件加载模块：

- `1.6/Mods/Nals.FacialAnimation`
- `1.6/Mods/co.uk.epicguru.meleeanimation`

其中：

- `Facial Animation` 主要通过额外 Def 文件接入
- `Melee Animation` 额外使用了独立兼容工程 `MiliraXIanMA.csproj`

## 数据与资源层状态

### `1.6/Defs`

当前 Def 层已经覆盖三名角色，主要目录包括：

- `AbilityDefs`
- `AnimaitionDefs`
- `ApparelDefs`
- `BackStoryDef`
- `DamageDefs`
- `Effects`
- `GraphicStateDefs`
- `HeadDefs`
- `HediffDefs`
- `JobDefs`
- `PawnkindDef`
- `QuestScriptDefs`
- `ScenarioDefs`
- `Storyteller`
- `ThingDefs`
- `TraitDef`

其中一个很明显的现状是：

- 霓羽拥有更完整的玩法入口 Def，包括 `ScenarioDefs`、`QuestScriptDefs`、`Storyteller`
- 清荷与昭离已经补齐了能力、PawnKind、Hediff、GraphicState、飞行动画和武器相关 Def
- 清荷与昭离的部分 Def 仍含 TODO、占位文本或占位资源路径

### `1.6/Languages`

当前保留三套语言目录：

- `ChineseSimplified (简体中文)`
- `ChineseTraditional (繁體中文)`
- `English`

但从仓库现状来看，真正比较完整的翻译覆盖仍主要集中在霓羽相关内容。清荷和昭离的大量文本目前仍直接写在 Def 内，尚未完全整理进语言文件。

### `Content/Textures`

当前仓库内正式可见的贴图目录仍主要是：

- `Content/Textures/MiliraXianNeiyu`

这说明目前的资源层状态更接近：

- 霓羽：正式资源较完整
- 清荷：逻辑已进仓，正式资源大多待补
- 昭离：逻辑已进仓，正式资源大多待补

这也和 `RW_Codex` 中关于清荷 VFX 缺口、清荷 GUI/图标待绘、昭离资源替换 TODO 等文档结论一致。

## 目前最明显的未同步点

结合仓库代码、现有 README 和 `D:\RimworldSourceCode\RW_Codex` 中的文档，可以把当前最明显的未同步点整理为以下几类：

1. **仓库真实范围已经扩大，但元数据仍停留在霓羽单角色时期**  
   `About/About.xml` 里的说明仍主要描述“独立角色 + 新剧本 + 招募事件”，没有体现清荷与昭离已经进入工程主线。

2. **玩法逻辑推进明显快于资源层**  
   清荷与昭离的代码、Def、Hediff、武器、飞行动画入口都已经建立，但图标、贴图、肖像、头型、面部动画资源仍大量占位或复用霓羽资源。

3. **语言整理没有追上功能扩展**  
   现有多语言目录仍以霓羽为主，清荷与昭离不少文本直接写在 Def 中，后续如果继续扩展对外发布，需要补做语言收口。

4. **构建依赖仍强绑定开发者本地环境**  
   主工程依赖本地 RimWorld 与 Workshop 中的 DLL 路径；兼容工程 `MiliraXIanMA.csproj` 仍保留硬编码盘符路径。

5. **部分说明文案与实际实现存在偏差**  
   例如 `RW_Codex/ISSUES.md` 中已记录清荷 `水镜` 的文本与当前实现不完全一致，这类内容后续应统一整理。

## 仓库结构

```text
MiliraXian_Characters/
├─ 1.6/
│  ├─ Assemblies/                      # 主工程输出
│  ├─ Defs/                            # 三角色主 Def
│  ├─ Languages/                       # 多语言文本
│  ├─ Mods/                            # 条件兼容子模组
│  └─ Patches/                         # XML Patch
├─ About/                              # About.xml / Preview / PublishedFileId
├─ Content/                            # 贴图与资源
├─ Source/
│  ├─ Common/                          # 通用资源与通用战斗底层
│  ├─ Neiyu/                           # 霓羽
│  ├─ QingHe/                          # 清荷
│  ├─ Zhaoli/                          # 昭离
│  └─ Compat/
│     └─ MeleeAnimation/               # Melee Animation 兼容源码
├─ LoadFolders.xml                     # 条件加载入口
├─ MiliraXian_NeiyuLaw.sln             # 主解决方案
├─ MiliraXian_NeiyuLaw.csproj          # 主工程
├─ MiliraXIanMA.csproj                 # Melee Animation 兼容工程
├─ packages.config                     # NuGet 包配置
└─ README.md
```

## 构建与调试

### 主工程

主工程输出目录为：

- `1.6/Assemblies`

常用构建命令：

```powershell
dotnet msbuild .\MiliraXian_NeiyuLaw.sln /p:Configuration=Debug
```

当前仓库已经自带 `Lib.Harmony 2.4.2` 包，但仍依赖以下本地环境：

- RimWorld `Managed` DLL
- Workshop 中的 `AriandelLibrary.dll`

在当前检查环境下，主工程构建失败的直接原因是：

- `AriandelLibrary.dll` 的引用路径不存在，导致 `NeiyuSpecialPawnIntegration.cs` 无法解析 `AriandelLibrary` 命名空间

### `Melee Animation` 兼容工程

兼容工程输出目录为：

- `1.6/Mods/co.uk.epicguru.meleeanimation/Assemblies`

但需要注意，`MiliraXIanMA.csproj` 目前仍使用硬编码的本地路径引用，例如：

- `E:\SteamLibrary\...`
- `E:\下载\HarmonyMod\...`

这说明该兼容工程目前更偏向作者本地开发环境，尚未整理成可直接迁移的通用构建配置。

## 建议的下一步整理方向

如果后续要继续把这个仓库整理成更适合长期维护和对外协作的形态，优先级比较高的事情大致有：

1. 更新 `About/About.xml` 与对外描述，使其与“三角色工程”的实际范围一致。
2. 为清荷、昭离补齐独立图标、贴图、肖像、头型和面部动画资源，减少对霓羽资源的占位复用。
3. 收口清荷、昭离的语言文件，把现在写在 Def 里的文本迁移到 `Languages`。
4. 清理 `PawnKindDef`、`GraphicStateDefs`、`AnimationDefs` 和 Patch 中遗留的 TODO。
5. 整理工程引用路径，让主工程与兼容工程都更容易在新环境中直接构建。

---

如果只用一句话概括当前状态，那么这个仓库现在更像是：

> 一个已经从“霓羽单角色模组”演进成“三角色共仓开发”的 RimWorld 角色工程，其中霓羽最成熟，清荷和昭离的玩法逻辑已经明显成型，但资源、语言、元数据与构建配置仍在追赶代码进度。
