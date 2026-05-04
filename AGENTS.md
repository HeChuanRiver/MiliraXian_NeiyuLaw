# AGENTS.md

本文件为 Codex Agent 在此仓库中协作开发时提供指导。请所有子代理在开始工作前务必阅读。

---

## 项目身份

这里是 **MiliraXian_Characters**，一个 RimWorld 1.6 模组，为游戏添加三位"仙"主题的可玩角色阵营。

## 项目概览

三位角色共享单一程序集 `MiliraXian_NeiyuLaw.dll`，源代码按角色分目录，公共系统放在 `Common/` 下：

- **霓羽 (Neiyu)** — 最成熟，拥有完整的游戏循环：剧本、任务、招募事件
- **清荷 (QingHe)** — 游戏逻辑完善，VFX/图标仍有大量占位
- **昭离 (Zhaoli)** — 核心系统已实现，资源与打包层待完善

## 构建命令

主程序集：

```powershell
dotnet msbuild .\MiliraXian_NeiyuLaw.sln /p:Configuration=Debug
```

输出到 `1.6/Assemblies/`。

近战动画兼容程序集（独立项目，含硬编码本地路径）：

```powershell
dotnet msbuild .\MiliraXIanMA.csproj /p:Configuration=Debug
```

输出到 `1.6/Mods/co.uk.epicguru.meleeanimation/Assemblies/`。

> **注意**：`MiliraXIanMA.csproj` 含有硬编码的 `E:\` 路径，在别的机器上需要手动修改才能编译。

## 目录结构

```
MiliraXian_Characters/
├── 1.6/                    # Mod 运行时根目录
│   ├── Assemblies/         # 编译产物
│   ├── Defs/               # XML Def（按类别而非角色划分）
│   └── Patches/            # XML Patch
├── Source/                 # C# 源码
│   ├── Common/             # 可复用系统（命名空间 MiliraXian.Characters）
│   ├── Neiyu/              # 霓羽逻辑（MiliraXian.Characters.Neiyu）
│   ├── QingHe/             # 清荷逻辑（MiliraXian.Characters.QingHe）
│   ├── Zhaoli/             # 昭离逻辑（MiliraXian.Characters.Zhaoli）
│   └── Compat/MeleeAnimation/  # 近战动画兼容（MiliraXian.Characters.Neiyu.MeleeAnimationCompat）
├── About/                  # Mod 元数据
├── Content/                # 纹理、音频等资源
├── packages/               # NuGet 包（含 0Harmony 2.4.2）
└── References.md           # 参考资料索引 ← 重要！查阅外部文档前先看这个
```

## 命名空间映射

| 目录 | 命名空间 |
|---|---|
| `Source/Common` | `MiliraXian.Characters` |
| `Source/Neiyu` | `MiliraXian.Characters.Neiyu` |
| `Source/QingHe` | `MiliraXian.Characters.QingHe` |
| `Source/Zhaoli` | `MiliraXian.Characters.Zhaoli` |
| `Source/Compat/MeleeAnimation` | `MiliraXian.Characters.Neiyu.MeleeAnimationCompat` |

## 代码约定

### C# 风格

遵循 `RW_Codex/AGENTS.md` 中定义的风格（4 空格缩进、Allman 大括号、PascalCase 命名）。关键规则：

- 新类型优先放在已有的 `MX_*` 命名约定文件内，除非逻辑足够独立
- Harmony Patch 类统一命名为 `MX_*Patches`，内部方法命名 `Patch_<TargetMethod>_<Prefix|Postfix|Transpiler>`
- `DefOf` 静态缓存类统一命名为 `MX_*DefOf`
- 始终通过 `PawnSpecialResourceUtility` 访问角色特殊资源，不要直接操作 HediffComp

### XML 约定

- Def 文件按类别放在 `1.6/Defs/` 对应子目录下，**不按角色划分**
- XML 中使用 Unity Rich Text 标签时，`<` 和 `>` 必须转义为 `&lt;` 和 `&gt;`
- 补丁放在 `1.6/Patches/` 下

### 关键文件

| 文件 | 用途 |
|---|---|
| `Source/Common/PawnSpecialResource.cs` | 资源条基类 |
| `Source/Common/PawnSpecialResourceUtility.cs` | 获取/确保资源 Comp 的辅助方法 |
| `Source/*/MX_*DefOf.cs` | 各角色静态 DefOf 缓存 |
| `Source/*/MX_*Patches.cs` | 各角色 Harmony 启动入口 |
| `1.6/Defs/HediffDefs/MiliraXian_Qinghe_PawnResource.xml` | 清荷 Tempest/Elegance 资源 Def |

## 核心设计模式

### Hediff 即资源 (Hediff-as-Resource)

角色特殊资源（清荷的激流/雅乐、昭离的因果）存储于 `HediffWithComps` 内部，而非独立 Comp。公共基类：

- `HediffCompProperties_PawnSpecialResource` / `HediffComp_PawnSpecialResource`
- 通过 `PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, hediffDef)` 访问

这使得资源可以显示在健康页签上，带有自定义 Gizmo，且生命周期与 Pawn 健康状态绑定。

### 角色初始化

每个角色通过 Harmony Hook `Pawn.SpawnSetup` 来附加：
- 特殊资源 Hediff（见 `PawnSpecialResourceUtility`）
- 被动状态 Hediff（如 `MX_QH_LongBreath`、`MX_QH_SpringRegen`）
- 武器同步技能（清荷根据装备乐器切换雅乐技能）

示例：`MX_QHPatches.Patch_Pawn_SpawnSetup_Postfix` → `EnsureSpecialResourceComp`、`EnsureLongBreathState`、`SyncEleganceAbilityByCurrentWeapon`。

### 伤害拦截链

清荷使用两阶段伤害拦截：

1. `Pawn.PreApplyDamage` **Prefix**（`Priority.First`）— `MX_QH_LongBreathDamageImmunity` 可完全归零伤害（设 `absorbed = true`，返回 `false` 跳过原始逻辑）
2. `Pawn.PreApplyDamage` **Postfix**（`Priority.Last`）— `HediffComp_LongBreathWard` 处理未被吸收的伤害，施加自身减免逻辑

### 技能 JobDriver

需要持续/引导行为的技能使用自定义 `JobDriver` + `CompAbilityEffect` 对：

- `CompAbilityEffect_*` 负责验证、资源消耗、生成场/Thing
- `JobDriver_MX_*` 运行实际持续行为

示例：`SpringFlow`、`TempestDrain`、`HengZhi`、`DuanHun`、`YangChun`。

## 外部依赖

| 依赖 | 来源 | 说明 |
|---|---|---|
| `Assembly-CSharp` | RimWorld `Managed/` | 核心游戏 |
| `UnityEngine.*` | RimWorld `Managed/` | 渲染/IMGUI |
| `0Harmony` | NuGet (`packages/`) | 内置 2.4.2 |
| `AriandelLibrary` | Workshop `3665997350` | 霓羽特殊 Pawn 集成必需；缺失会导致该文件编译失败 |
| `zAnimationMod` | Workshop `2944488802` | 仅 `MiliraXIanMA.csproj` 需要 |

主 `.csproj` 使用 `..\..\..\..\workshop\content\294100\...` 相对路径引用 RimWorld 和 AriandelLibrary。

## 条件加载

`LoadFolders.xml` 在依赖激活时加载可选子模组内容：
- `Nals.FacialAnimation` → 眼睛/眼睑 Def
- `co.uk.epicguru.meleeanimation` → MA 补丁 + 兼容程序集

## 参考资料

> **重要**：所有外部参考资料的完整索引在 `References.md` 中。在查阅任何外部文档前，请先阅读该文件。

核心参考路径：

| 路径 | 内容 |
|---|---|
| `D:\RimworldSourceCode` | RimWorld 1.6 反编译源码（`Core/`、`Mods/`） |
| `D:\RimworldSourceCode\RW_Codex` | 本 Mod 参考实现、设计文档与示例代码库 |
| `D:\RimworldSourceCode\RW_Codex\CodeExamples` | 自包含垂直切片示例（一个文件夹 = 一个 mechanic） |
| `D:\RimworldSourceCode\RW_Codex\AGENTS.md` | 代码风格、提交格式等协作约定 |

## RimWorld 彩色文字速查

在 RimWorld 中显示彩色文字有以下途径：

1. **Unity Rich Text 标签** — `<color=#F05A5A>文字</color>`（XML 中需转义尖括号）
2. **TaggedString.Colorize** — `"警告".Colorize(ColoredText.WarningColor)`
3. **MoteMaker.ThrowText** — 世界空间浮动文字，见 `Source/Zhaoli/ZhaoliDeathField.cs:352`
4. **Messages.Message** — 消息栏，通过 `MessageTypeDefOf` 控制颜色
5. **GUI.color** — IMGUI 绘制前修改，绘制后恢复 `Color.white`
6. **GenDraw** — 范围/区域预览直接传 `Color`，见 `Source/Zhaoli/ZhaoliDeathField.cs:63`

## Agent 协作规则

1. **查阅参考优先**：遇到不确定的 RimWorld API 或现有模式时，先用 `rimsearcher` MCP 工具检索，或查阅 `References.md` 指向的文档
2. **遵循现有模式**：新增功能应参照 `CodeExamples/` 中的垂直切片和现有角色的实现方式
3. **保持程序集统一**：所有 C# 代码编译到同一个 DLL，注意命名空间划分，避免循环依赖
4. **XML 与 C# 协同**：修改 Def 时同步检查对应的 C# 引用（DefOf、Patch 目标等）
5. **编译验证**：每次修改完成后运行构建命令确认无编译错误
6. **保守修改**：不要擅自重构或"改进"现有代码风格，除非任务明确要求
