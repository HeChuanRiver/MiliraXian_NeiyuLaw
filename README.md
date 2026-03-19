# MiliraXian_NeiyuLaw

`MiliraXian_NeiyuLaw` 是一个基于 RimWorld 1.6 的角色扩展模组工程。

当前仓库已经从“纯游戏目录改文件”整理成了可直接维护的项目结构，核心方向是：

- 以 `Neiyu` 为现有完整角色内容
- 以 `Zhaoli` 为正在持续开发的新角色内容
- 以 `Common` 为后续多角色共用底层

## 当前内容概览

### 霓羽

霓羽是当前仓库里完成度最高的角色模块，已经具备：

- 单独的 `PawnKindDef`
- 开局剧本
- 招募事件与任务脚本
- 三形态专属武器
- 七个专属技能
- 护盾系统
- 翅膀与飞行动画
- 特殊角色管理器接入
- 模组设置页
- 面部动画与 `Melee Animation` 兼容入口

### 昭离

昭离目前处于持续开发阶段，已落地的核心底层有：

- 单独的 `PawnKindDef`
- 单独的 `Backstory`
- 因果资源条与 Gizmo
- 告死流场
- 诡医
- 因果溢出后的沉眠状态
- 单独的飞行动画与翅膀入口

目前昭离仍有大量占位或待补完内容，例如：

- 专属武器 `离断`
- 冥火
- 泯神
- 讳亡
- 归亡者
- 更多视觉表现、数值和平衡

### Common

`Source/Common` 目前承载的是多角色通用资源系统底座，已经开始被昭离的“因果”使用。后续如果继续扩展其他角色的怒气、灵力、充能等专属资源，建议优先复用这里的结构。

## 仓库结构

```text
MiliraXian_NeiyuLaw/
├─ 1.6/
│  ├─ Defs/                         # 主 Def
│  ├─ Languages/                    # 多语言文本
│  ├─ Mods/                         # 条件兼容子模组
│  └─ Patches/                      # XML Patch
├─ About/                           # About.xml / Preview / PublishedFileId
├─ Content/                         # 贴图与资源
├─ Source/
│  ├─ Common/                       # 通用资源底层
│  ├─ Neiyu/                        # 霓羽相关 C#
│  ├─ Zhaoli/                       # 昭离相关 C#
│  └─ Compat/
│     └─ MeleeAnimation/            # Melee Animation 兼容 DLL 工程源码
├─ LoadFolders.xml                  # RimWorld 版本与条件加载入口
├─ MiliraXian_NeiyuLaw.csproj       # 主项目
├─ MiliraXIanMA.csproj              # Melee Animation compat 子项目
└─ README.md
```

## Source 分层说明

### `Source/Common`

当前主要是专属资源的通用底座：

- `PawnSpecialResource.cs`
- `PawnSpecialResourceUtility.cs`

适合放：

- 通用 HediffComp 资源实现
- 通用 Gizmo 资源条
- 多角色复用的工具方法

### `Source/Neiyu`

霓羽模块当前已经比较完整，主要包含：

- 技能实现
- 三形态武器与 Gizmo
- 护盾逻辑
- 招募事件与剧本逻辑
- 特殊角色管理器接入
- 翅膀和飞行动画补丁
- 旧命名空间兼容类

主要文件包括：

- `NeiyuScenarioAndRecruit.cs`
- `Comp_ModeSwitchWeapon.cs`
- `CompAbilityEffect_NeiyuFlowerSwordSkills.cs`
- `CompAbilityEffect_NeiyuThunderAndArrow.cs`
- `CompAbilityEffect_NeiyuWarpFeather.cs`
- `Hediff_NeiyuCountShield.cs`
- `MiliraXianCharactersWings.cs`

### `Source/Zhaoli`

昭离模块当前已经独立成目录，后续开发都应继续放在这里。

当前已有：

- `ZhaoliKarmaResource.cs`
- `ZhaoliDeathField.cs`
- `ZhaoliGuiyi.cs`
- `ZhaoliDormancy.cs`

建议继续沿这个目录扩展：

- 主动技能
- 被动技能
- 专武逻辑
- 专属 Hediff
- 角色状态控制


## Def 与资源结构

### `1.6/Defs`

当前已经按用途拆分：

- `AbilityDefs`
- `AnimaitionDefs`
- `ApparelDefs`
- `BackStoryDef`
- `Effects`
- `GraphicStateDefs`
- `HeadDefs`
- `HediffDefs`
- `PawnkindDef`
- `QuestScriptDefs`
- `ScenarioDefs`
- `Storyteller`
- `ThingDefs`
- `TraitDef`

其中和当前两个角色最相关的文件有：

- `Defs/AbilityDefs/MiliraXian_Ability_Neiyu.xml`
- `Defs/AbilityDefs/MiliraXian_Ability_Zhaoli.xml`
- `Defs/PawnkindDef/MiliraXian_PawnkindDef_Neiyu.xml`
- `Defs/PawnkindDef/MiliraXian_PawnkindDef_Zhaoli.xml`
- `Defs/HediffDefs/MiliraXian_Zhaoli_Karma.xml`
- `Defs/HediffDefs/MiliraXian_Zhaoli_DeathField.xml`
- `Defs/HediffDefs/MiliraXian_Zhaoli_Dormancy.xml`

### `1.6/Languages`

当前保留三套语言目录：

- `ChineseSimplified (简体中文)`
- `ChineseTraditional (繁體中文)`
- `English`

开发时目前常用流程是：

- 先在主 Def 里直接写中文 `label/description`
- 功能做完后再统一整理语言文件

### `Content`

当前资源主要集中在 `Content/Textures/MiliraXianNeiyu/` 下。

这部分已经包含：

- 霓羽服装贴图
- 武器贴图
- 特效贴图
- UI 图标
- 面部与光环资源
- 翅膀与飞行动画帧

昭离目前部分贴图和表现仍在复用霓羽资源，后续会逐步拆分。

## 条件兼容模块

### Facial Animation

通过 [LoadFolders.xml](/D:/RimWorldModForMe/MiliraXian_NeiyuLaw/LoadFolders.xml) 条件加载：

- `1.6/Mods/Nals.FacialAnimation`

当前目录内容：

- `Defs/UniqueEye.xml`
- `Defs/UniqueLid.xml`

## 当前依赖

`About/About.xml` 当前声明依赖：

- `Ancot.MiliraRace`
- `Ariandel.AriandelLibrary`

并设置了 `loadAfter`：

- `Ariandel.AriandelLibrary`
- `Ancot.MiliraRace`
- `Ancot.MiliraRaceGenePatch`
- `Ancot.MiliraRaceFacialAnimation`
- `Ariandel.MiliraImperium`
- `HeChuanRiver.MiliraXian`

## 目前已实现的主要玩法入口

### 霓羽

- 开局剧本：`界外羽痕`
- 招募事件：`异界羽影`
- 招募任务：`MXNL_NeiyuProjectionRecruitQuest`
- 主要技能：
  - 折跃引羽
  - 雷锁印域
  - 流羽裂界
  - 花阵·祈生
  - 花阵·瘴庭
  - 剑势·天坠
  - 剑势·断首

### 昭离

当前已经接入的技能：

- 告死流场
- 诡医

当前已经接入的状态：

- 因果
- 诸事无常·沉眠

## 构建与调试

### 主工程构建

```powershell
dotnet build D:\RimWorldModForMe\MiliraXian_NeiyuLaw\MiliraXian_NeiyuLaw.csproj -c Release
```

### `Melee Animation` compat 工程构建

```powershell
dotnet build D:\RimWorldModForMe\MiliraXian_NeiyuLaw\MiliraXIanMA.csproj -c Release
```

### 调试建议

- 构建前尽量关闭 RimWorld，避免 DLL 被占用
- XML 改动完成后，注意同步项目目录与游戏目录
- 新功能优先先在主 Def 中写中文 `label/description`
- 功能稳定后再统一整理语言文件

