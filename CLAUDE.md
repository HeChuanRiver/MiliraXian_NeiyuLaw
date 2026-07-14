# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a RimWorld 1.6 mod that adds three playable character factions ("Xian" themed):
- **Neiyu (霓羽)** — most mature, has full gameplay loop with scenario, quests, recruit events
- **QingHe (清荷)** — gameplay logic solid, VFX/icons still largely placeholders
- **Zhaoli (昭离)** — core systems implemented, assets and packaging layer trailing

All three characters share a single assembly `MiliraXian_NeiyuLaw.dll` and a `Common` combat/resource layer.

## Build Commands

Main assembly:
```powershell
dotnet msbuild .\MiliraXian_NeiyuLaw.sln /p:Configuration=Debug
```
Output goes to `1.6/Assemblies/`.

Melee Animation compat assembly (separate project, hard-coded local paths):
```powershell
dotnet msbuild .\MiliraXIanMA.csproj /p:Configuration=Debug
```
Output goes to `1.6/Mods/co.uk.epicguru.meleeanimation/Assemblies/`.

## Architecture

### Single-assembly Multi-character Layout
All C# compiles into one DLL (`MiliraXian_NeiyuLaw`). Source is split under:
- `Source/Common/` — reusable systems
- `Source/Neiyu/`, `Source/QingHe/`, `Source/Zhaoli/` — per-character logic
- `Source/Compat/MeleeAnimation/` — optional Melee Animation compat

Def files live under `1.6/Defs/` per category (not per character). Patches in `1.6/Patches/`.

### Character Initialization Pattern
Each character hooks `Pawn.SpawnSetup` via Harmony to attach:
- Special-resource Hediffs (see `PawnSpecialResourceUtility`)
- Passive state Hediffs (e.g. `MX_QH_LongBreath`, `MX_QH_SpringRegen`)
- Weapon-synced abilities (QingHe swaps Elegance abilities based on equipped instrument)

QingHe example: `MX_QHPatches.Patch_Pawn_SpawnSetup_Postfix` → `EnsureSpecialResourceComp`, `EnsureLongBreathState`, `SyncEleganceAbilityByCurrentWeapon`.

### Hediff-as-Resource Pattern
Character-specific resources (Tempest/Elegance for QingHe, Karma for Zhaoli) are stored inside `HediffWithComps` rather than separate comps. The common base is:
- `HediffCompProperties_PawnSpecialResource` / `HediffComp_PawnSpecialResource`
- Access via `PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, hediffDef)`

This lets resources appear on the health tab with custom gizmos and lifecycle tied to pawn health state.

### Damage Interception Chain
QingHe uses a two-stage damage interception:
1. `Pawn.PreApplyDamage` **Prefix** — `MX_QH_LongBreathDamageImmunity` can zero-out damage entirely (sets `absorbed = true`, returns `false` to skip original).
2. `Pawn.PreApplyDamage` **Postfix** — `HediffComp_LongBreathWard` processes damage that was *not* absorbed (e.g. by LotusShield), applying its own mitigation logic.

Priority is explicit: prefix at `Priority.First`, postfix at `Priority.Last`.

### Ability Job Drivers
Skills that require sustained/channeling behavior use custom `JobDriver` + `CompAbilityEffect` pairs:
- `CompAbilityEffect_*` handles validation, resource cost, and spawns the field/Thing
- `JobDriver_MX_*` runs the actual sustained behavior (e.g. `JobDriver_MX_YangChun`)

Examples: `SpringFlow`, `TempestDrain`, `HengZhi`, `DuanHun`, `YangChun`.

## Colored Text in RimWorld

RimWorld 基于 Unity IMGUI，支持多种彩色文字机制，项目中的使用示例如下：

### 1. Unity Rich Text 标签
XML 本地化字符串和 C# 中均可直接使用 Unity 富文本标签，例如：
- `&lt;color=#F05A5A&gt;红色文字&lt;/color&gt;`（XML中需转义尖括号）
- `&lt;color=red&gt;红色文字&lt;/color&gt;`
- `&lt;b&gt;粗体&lt;/b&gt;`、`&lt;i&gt;斜体&lt;/i&gt;`、`&lt;size=16&gt;字号&lt;/size&gt;`

> **注意**：在 XML Def 文件（如 `Defs/*.xml`）中使用 Rich Text 时，`<` 和 `>` 必须转义为 `&lt;` 和 `&gt;`，否则会被 XML 解析器视为嵌套标签导致 Parse Error。C# 字符串中可直接使用原始尖括号。

### 2. TaggedString.Colorize
RimWorld 提供的扩展方法，用于在代码中给文字上色，配合 `ColoredText` 和 `ColorLibrary` 使用：
```csharp
string warning = "警告文本".Colorize(ColoredText.WarningColor);
string redText = "失败".Colorize(ColorLibrary.RedReadable);
```

### 3. MoteMaker.ThrowText（3D 浮动文字）
在世界空间中显示带颜色的浮动文字：
```csharp
MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, remainingHits.ToString(), new Color(0.94f, 0.26f, 0.26f), 1.1f);
```
示例见 `Source/Zhaoli/ZhaoliDeathField.cs:352`。

### 4. Messages.Message（消息栏）
通过 `MessageTypeDefOf` 控制消息颜色，无需手动处理富文本：
```csharp
Messages.Message("操作成功", pawn, MessageTypeDefOf.PositiveEvent);
Messages.Message("操作失败", pawn, MessageTypeDefOf.RejectInput);
Messages.Message("需要注意", pawn, MessageTypeDefOf.CautionInput);
Messages.Message("中性信息", pawn, MessageTypeDefOf.NeutralEvent);
```

### 5. GUI.color（IMGUI 绘制）
在使用 `Widgets.Label`、`GUI.Label` 等绘制文字前，修改 `GUI.color` 可改变接下来所有绘制的颜色：
```csharp
GUI.color = Color.red;
Widgets.Label(rect, "红色标签");
GUI.color = Color.white; // 绘制结束后恢复
```

### 6. GenDraw（范围/区域预览）
技能范围预览或区域高亮时直接传入 `Color`：
```csharp
GenDraw.DrawRadiusRing(target.Cell, Props.radius, new Color(0.48f, 0.08f, 0.1f));
GenDraw.DrawFieldEdges(cells, new Color(0.44f, 0.12f, 0.16f));
```
示例见 `Source/Zhaoli/ZhaoliDeathField.cs:63` 与 `Source/Zhaoli/ZhaoliMinshen.cs:179`。

## External Dependencies

| Dependency | Source | Note |
|---|---|---|
| `Assembly-CSharp` | RimWorld `Managed/` | Core game |
| `UnityEngine.*` | RimWorld `Managed/` | Rendering/IMGUI |
| `0Harmony` | NuGet (`packages/`) | Bundled 2.4.2 |
| `AriandelLibrary` | Workshop `3665997350` | Required for `NeiyuSpecialPawnIntegration`; if missing, build fails on that file |
| `zAnimationMod` | Workshop `2944488802` | Only for `MiliraXIanMA.csproj` |

The main `.csproj` uses `..\..\..\..\workshop\content\294100\...` relative paths for RimWorld and AriandelLibrary. `MiliraXIanMA.csproj` still contains hard-coded `E:\` paths and will not build on a different machine without editing.

## Conditional Loading

`LoadFolders.xml` loads optional sub-mod content when dependencies are active:
- `Nals.FacialAnimation` → eye/lid defs
- `co.uk.epicguru.meleeanimation` → MA patches + compat assembly

## Namespace Map

| Directory | Namespace |
|---|---|
| `Source/Common` | `MiliraXian.Characters` |
| `Source/Neiyu` | `MiliraXian.Characters.Neiyu` |
| `Source/QingHe` | `MiliraXian.Characters.QingHe` |
| `Source/Zhaoli` | `MiliraXian.Characters.Zhaoli` |
| `Source/Compat/MeleeAnimation` | `MiliraXian.Characters.Neiyu.MeleeAnimationCompat` |

## Important Files

- `Source/Common/PawnSpecialResource.cs` — resource bar base
- `Source/Common/PawnSpecialResourceUtility.cs` — helper to get/ensure resource comps
- `Source/*/MX_*DefOf.cs` (or equivalent) — static `DefOf` caches per character
- `Source/*/MX_*Patches.cs` (or equivalent) — Harmony bootstrap per character
- `1.6/Defs/HediffDefs/MiliraXian_Qinghe_PawnResource.xml` — QingHe Tempest/Elegance defs

## References

若存在 `References.md`，则文档在 `References.md` 所指定的目录中，需要时可以查阅。
