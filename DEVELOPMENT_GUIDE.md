# 开发指南 — 米莉拉角色拓展（MiliraXian_Characters）

本文是面向开发者的详细规范，承接 `QWEN.md` 的三块内容：角色库（CharacterLib）架构细节、测试与验证禁令、文本与本地化规则。代理工作上下文以 `QWEN.md` 为准，本文为其展开。

## 一、MiliraXian_CharacterLib（内置角色库）

定位是**替代 AriandelLibrary 的自研内置角色库**，与主程序集同样输出到 `1.6/Assemblies/`，根命名空间 `MiliraXian.CharacterLib`。

- **依赖方向单向**：主程序集通过 `ProjectReference`（`<Private>False</Private>`）依赖本库，本库**不引用主程序集**，不反向依赖角色代码。构建主工程会自动先编本库；两者同在 `1.6/Assemblies/`，因此不作私有依赖复制。
- **核心工程既不引用 AL 也不引用 AlienRace**。引用列表只有 Lib.Harmony、Assembly-CSharp、System、System.Core、System.Xml 与 UnityEngine 模块。AL 只被 `MXCL_ALCompat.csproj` 引用。若日后把飞行动画替换（`AnimationDef`，属 AlienRace）移入核心，需要**新增** AlienRace 引用。
- 隔离靠**独立程序集**而非代码守卫：AL 类型全部只出现在 `Compat/AriandelLibraryIntegration/`，该工程输出到 `1.6/Mods/Ariandel.AriandelLibrary/Assemblies/`，由 `LoadFolders.xml` 的 `IfModActive` 条件加载，AL 缺席时整个 DLL 不加载。这比 `ModsConfig.IsActive` 守卫更彻底。
- 若将来确实需要在**同一程序集**内条件性触及第三方类型：`<Private>False</Private>` 只表示不复制 DLL，**不能**让程序集在运行时摆脱依赖。CLR 按方法粒度延迟解析，JIT 某方法时只要其签名或方法体出现该类型就会去加载对应程序集，缺席时在那一刻抛 `TypeLoadException` / `FileNotFoundException`。此时守卫与第三方类型**不能写在同一个方法里**。
- 加载顺序：RimWorld 按文件名字母序加载 `Assemblies/`，`MiliraXian_CharacterLib` 排在 `MiliraXian_NeiyuLaw` 之前。两者都注册 Harmony patch 时需留意此顺序。

### 设计需求（用户定义的职责边界）

CL 是**只依赖 AlienRace 的特殊角色库**，职责限定为三件事：

1. 按 `PawnKindDef` 接管角色生成；
2. 替换该角色独有的美术资源；
3. 替换 race-specified part（种族渲染树里硬编码的附加部件，如翅膀）。

**CL 不负责**：角色唯一性（是否只能存在一个、是否已招募过、招募事件守卫等），以及 Pawn 生成之后的任何行为（战斗、死亡、复活、状态维护）。这些属于主程序集的玩法代码。

联动边界：

| 对象 | 允许 | 禁止 |
| --- | --- | --- |
| AL（SCM） | 仅让 CL 角色能被 SCM**发现**并注册 | SCM 接管生成、死亡接管，或任何其他功能 |
| FA | 按 `PawnKindDef` 替换脸部资源与特定 FA 动画 | 其他 |

### CL 的 DefModExtension 清单

| 扩展 | 所在程序集 | 职责 |
| --- | --- | --- |
| `CharacterIdentityExtension` | CharacterLib | 固定身份：姓名键、头型、体型、backstory、年龄、特质表、forbiddenHediffs |
| `CharacterRegistrationExtension` | CharacterLib | `characterId`，对接 SCM 注册 |
| `CharacterRacePartExtension` | CharacterLib | race part 贴图替换（`parts` 列表：`source` + `prefix` → 目标路径） |
| `CharacterProtectionExtension` | CharacterLib | 保护性生成（`childKind` 等） |
| `CharacterCarrierExtension` | MXCL_ALCompat | 标记 SCM 载体 kindDef（见下「SCM 对接：载体模式」） |
| `CharacterFaceExtension` | MXCL_FACompat | 按 `PawnKindDef` 绑定 FA 五官 |

四位角色的真实 PawnkindDef（`1.6/Defs/PawnkindDef/`）各挂前四个扩展，**无任何 AL 扩展**。

### 特殊角色的身份与限定式生成

身份有三层且互相独立：

1. **PawnKindDef 本身**——地基。`combatPower` 0 防止被当袭击单位刷出，`fixedGender`、`defaultFactionType`、`xenotypeSet` 锁定基本属性。
2. **CL 的 `CharacterIdentityExtension`**——生成后改名/换 backstory/定年龄/剔特质（取代原 AL `FixedIdentityExtension`；沿用其技能增益差值补偿逻辑：替换 backstory 后用旧集合 −1、新集合 +1 累加 `skillGains` 再夹到 [0,20]，否则技能数值错乱）。
3. **`MiliraXian/Characters/Neiyu/SpecialPawnCoreStateRepair.cs`**——主程序集自写的兜底修复，在 `Pawn.SpawnSetup` 前重建残缺 tracker 图，并硬编码修复视觉身份（霓羽补 `MiliraNeiyuHead`/Female/Bald/两个 backstory，明渊无条件纠正 `MiliraMingyuanHead`）。

**SCM 对接：载体模式**。`SpecialPawnExtension`（AL 类型）只挂在条件目录 `1.6/Mods/Ariandel.AriandelLibrary/Defs/SpecialPawnCarriers.xml` 的 4 个载体 kindDef 上（各配 `CharacterCarrierExtension`），AL 缺席时整个目录不加载；真实角色 PawnkindDef 不被 SCM 接管生成与死亡。

限定式生成有两条路径：

- **XML 声明式**：`apparelRequired` 强制装备、`apparelTags` + `apparelMoney` 99999 卡死随机池、`weaponTags` 限定武器、`gearHealthRange` 1-1 与 `itemQuality`/`min|maxApparelQuality` 全锁 Excellent、`backstoryFiltersOverride` 按 tag 限定、`disallowedTraits(WithDegree)` 排除冲突、`forcedHairColor` 固定发色。
- **代码命令式**：`NeiyuRecruitUtility.GenerateRecruitPawn` 手工构造 `PawnGenerationRequest`（`forceGenerateNewPawn`、`allowDead/allowDowned: false`、`mustBeCapableOfViolence`、`colonistRelationChanceFactor: 20f`），生成后接 `EnsureDefaultLoadout` + `MarkForLoadoutStabilization` + `EnsureRecruitPawnPersisted`。

**唯一性守卫** `CanOfferRecruitment()` 是四重检查（不在专属开局、事件未触发、招募任务不存在、`NeiyuExistsAnywhere()` 扫 `AllMapsWorldAndTemporary_AliveOrDead`），**纯按 `kindDef` 比较、零 AL 依赖**。

### 独有美术资源绑定

| 资源 | 绑定机制 |
| --- | --- |
| 头部贴图 | `HeadTypeDef`（`randomChosen: false`）+ `forcedHeadTypeDef` |
| 服装／武器 | `wornGraphicPath` + `apparelRequired` + `CompPawnKindRestrictedWeapon` / `CompPawnKindEquipmentDurability`（按 `allowedPawnKinds`/`protectedPawnKinds` 列 PawnKindDef） |
| 翅膀／race part | PawnkindDef 挂 `CharacterRacePartExtension`，由 CharacterLib 的 `CharacterRaceParts` / `CharacterRacePartPatches`（postfix 原版 `PawnRenderNode.GraphicFor`）按 `Props.texPath` 前缀匹配替换 Graphic |
| FA 五官 | `FacialAnimation.*TypeDef` + `probability` 0.0001 + `CharacterFaceExtension.pawnKind`，patch `DrawFaceGraphicsComp.CompRenderNodes`（internal，运行时经 `AccessTools.TypeByName` 解析）执行绑定；另 postfix `FacialAnimationModSettings.ShouldDrawRaceXenoType` 强制显示 |

FA 的 XML 侧在 `1.6/Mods/Nals.FacialAnimation/Defs/` 的 Unique{Eye,Lid,Brow}.xml；动画级定制走同工程 `Patch_CreateAnimationDict.cs` + `FaceAnimationOverrideDef`（机制就绪，XML 目前零引用）。

### 米莉拉翅膀渲染（race part 替换的前提）

`renderTree` 挂在**种族 `ThingDef`** 上（`Race_Milira.xml`：`<renderTree>Milira_Milira</renderTree>`），不是挂在 PawnKindDef 上——**全米莉拉种族共用同一棵渲染树**，树里翅膀节点的贴图路径是硬编码常量（`Milira/Pawn/BodyAddon/LeftWingNew/LeftWingFront`）。因此「按角色换翅膀」在 XML 层无原生入口，只能在运行时覆盖。

节点结构：`AncotLibrary.PawnRenderNodeProperties_BodyPart` + `AncotLibrary.PawnRenderNode_BodyPart` + `Milira.PawnRenderNodeWorker_BodyPartFlight`，`debugLabel` / `bodyPartLabel` 为 `left wing` / `right wing`，各带一个 `* behind` 子节点。`PawnRenderNodeWorker_BodyPartFlight.CanDrawNow` 在 `pawn.Flying` 或当前动画 defName 以 `Milira_Fly` 开头时返回 false（交给飞行动画接管）。

现行做法是 postfix 原版 `PawnRenderNode.GraphicFor`（`virtual`，官方预留的覆盖点），按 `Props.texPath` **前缀**匹配替换 Graphic——同一声明可同时覆盖 front/behind 子节点与飞行动画 keyframe 引用的 GraphicStateDef 路径。**这条路子正确，不需要改。**

**CharacterLib 不需要引用 Milira.dll**：翅膀链路只用到原版 `Graphic_Multi` / `AnimationDef` / `PawnRenderNode` 加字符串 defName。主工程虽硬依赖 Milira Race，却刻意只用反射访问其类型（范式见 `MilianCompatibility`：`AccessTools.TypeByName("Milira.MilianUtility")`），新代码沿用即可。

## 二、测试禁令（绝对）

**不要编写任何测试脚本、测试程序或测试工程。** 覆盖单元测试、回归检查、断言程序、PowerShell 验证脚本、临时验证脚本与内联验证代码，无论是否计划保留。不要引入测试框架，也不要重建曾被删除的检查。

允许的验证只有三种：

1. 构建受影响的工程并修正编译错误。
2. 阅读代码与 Def 确认意图。
3. 向用户明确说明无法验证的部分，并描述需要在游戏内确认什么。

游戏行为由用户在 RimWorld 内实机验证。若确信需要测试，先征求明确许可。

## 三、文本与本地化

Def 中的可翻译文本**直接写简体中文**并作为唯一原文，包括 `label`、`description`、标题、消息、列表文字与自定义可翻译字段。不要创建或保留简体中文 `DefInjected`。

繁体中文与英文 `DefInjected` 以简体中文 Def 为准：改原文时同步更新译文、补齐缺失条目、删除失效键，保持占位符、富文本标签、语法规则前缀与含义一致。列表字段先解析 XML 继承再选翻译路径，优先用翻译句柄而非数字索引。

`Keyed` 独立维护：简体中文 Keyed 为原文，同步繁体与英文。纯 DefInjected 任务不要改动 Keyed。

**未发布角色**（清荷、明渊）：没有明确指示时不要读取或编辑其繁体中文与英文本地化文件；搜索与校验也限制在许可的语言范围内。
