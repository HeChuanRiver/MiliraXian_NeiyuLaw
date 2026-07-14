# MiliraXian_NeiyuLaw 全量性能审查与修复报告

审查日期：2026-07-14  
目标版本：RimWorld 1.6  
工作分支：`character-Mingyuan`  
审查范围：C#、Harmony、XML/Def、贴图、程序集、渲染与存档生命周期

## 结论

本轮完成了计划内的源码级性能修复，并通过 Debug、Release 隔离全量编译和 XML/Def 静态验证。所有数值逻辑、公开 Def/XML/翻译/Scribe 标识保持兼容；装饰性小箭 Fleck 是唯一启用自适应降载的内容，不参与伤害、命中、目标、状态或资源判定。

没有可用的固定开发者存档和修复前 Dubs PA 捕获，因此本报告没有把源码重构写成实测性能成果。所有 TPS/FPS、自耗时下降 50%、稳态 GC 和场景等价验收仍标为“待同存档实测”。

## 口径与基线

### 审查起点

计划记录的审查起点为：227 个可解析 XML、361 个 Def 条目、294 张主运行时贴图，增量 Debug 构建 0 警告、0 错误。本机检测到 Dubs Performance Analyzer（Workshop `2038874626` 与 `3525634104`）。

工作树在审查开始前已经包含明渊角色、弓、XML、翻译、贴图迁移、程序集等未提交修改。本轮没有重置、覆盖或删除这些内容，也没有覆盖原本已经变更的 `1.6/Assemblies/MiliraXian_NeiyuLaw.dll`。

### 当前工作树清单

| 内容 | 当前结果 | 说明 |
|---|---:|---|
| C# | 102 个文件 | 包含当前分支已有的未跟踪明渊弓源码和本轮新增公共性能补丁 |
| XML | 233 个，233 个可解析 | 比计划起点多出的文件来自当前分支已有内容 |
| 非抽象 DefName | 360 个，全部唯一 | 精化后的脚本排除抽象模板；与原“361 个 Def 条目”不是同一统计口径 |
| PatchOperation | 38 个 | 静态解析通过；实际目标命中仍需启动日志确认 |
| 运行时贴图 | 295 张 | `Content/Textures` 294 张，条件兼容目录 1 张 |
| 其他图像素材 | 4 张 | About/临时工作图，不计入主运行时贴图基线 |
| 运行时程序集 | 2 个 | 主程序集及条件兼容程序集 |

## 证据等级

- **静态可疑**：关键词、结构或资产形态提示风险，尚未证明是热点。
- **源码确认**：调用链、频率、实例数量或分配/绘制扇出可由源码确定。
- **Dubs PA 实测确认**：相同存档、镜头、倍速与采样时长的捕获确认热点或收益。

本轮没有新的 Dubs PA 捕获，因此下表的已修复项最高证据等级为“源码确认”。

## S/A/B/C 问题与修复

| ID | 级别 | 证据 | 位置与原行为 | 已实施修复 | 静态验证 | 运行时状态 |
|---|---|---|---|---|---|---|
| PERF-001 | S | 源码确认 | 全局 `TickManager.TicksAbs` getter 启动兼容 Postfix 会放大到全游戏所有绝对 Tick 读取；本 Mod 没有其他 `TicksAbs` 调用 | 完全移除该 Harmony 补丁，以新游戏/读档/场景启动验证替代 | 源码中无 `TicksAbs` 补丁或调用；Debug/Release 通过 | 启动红字与场景流程待测 |
| PERF-002 | S | 源码确认 | 连续轨迹使用头部 `RemoveAt(0)`，每个历史分段单独取材质并绘制，单枚弹体单帧可放大到约 84 次提交 | 固定容量环形缓冲；预分配顶点/UV/颜色/索引；每枚弹体一个动态 Mesh、每帧至多一次 `DrawMesh`；离屏跳过；销毁/读档清理 Mesh | 无轨迹 `RemoveAt(0)`；一次绘制入口；双配置编译通过 | 轨迹外观、稳态 GC、draw-call 数待 Dubs/Frame Debugger |
| PERF-003 | A | 源码确认 | 涅羽与照黎各自对全局 `StatExtension.GetStatValue` 做 Postfix，均可能先进入角色/Hediff逻辑 | 合并为一个 Postfix；先对受影响 `StatDef` 做引用/集合过滤，再访问精确 Hediff；保持最终值后修正顺序 | 全源码只剩一个 `GetStatValue` Harmony 入口 | 四角色属性值逐项对照待测 |
| PERF-004 | A | 源码确认 | 涅羽、照黎护盾各自对 `PawnRenderer.RenderPawnAt` 打补丁；逐帧按字符串颜色键扩张材质缓存并重复查 Hediff/Comp | 合并为一个渲染 Postfix；精确 HediffDef 与弱键组件缓存；共享基础材质和 `MaterialPropertyBlock`；移除、死亡、DeSpawn、全局地图/世界清理时失效 | 1.6 原始字段/方法签名已反射核验；只剩一个渲染入口；双配置编译通过 | 护盾旋转、透明度、死亡/重生和回菜单待画面对照 |
| PERF-005 | A | 源码确认 | `Pawn_DrawTracker.DrawPos` 与 `Need_Food.NeedInterval` 在调用时反射读取 Pawn | 使用 Harmony 字段注入，且无活动状态时立即返回 | 1.6 `pawn` 字段已从程序集核验；目标路径无逐次 `FieldInfo.GetValue` | 飞行/饥饿行为待游戏内验证 |
| PERF-006 | A | 源码确认 | 清和领域脉冲使用 `GenRadial` 迭代器、临时集合和重复 Hediff 查找；阳春保护每 Tick 通知 | 保持原脉冲间隔；改为 `AllPawnsSpawned` 无分配列表和平方距离过滤；缓存施法者组件；阳春在初始化和原脉冲边界刷新保护 | 三个领域源码与双配置编译通过 | 多清和领域的数值、时序和 GC 待测 |
| PERF-007 | A | 源码确认 | 特殊角色恢复逻辑重复扫描地图、商队和世界 Pawn；完成成员用 List 查询；延迟事件未到期也逐 Tick 遍历 | 创建/招募/Hediff 生命周期即时登记；每 600 Tick 一次合并恢复审计；运行时 HashSet；照黎业链、明渊重生使用 `nextDueTick` 快速跳过 | 单一恢复审计入口；队列空/未到期均 O(1) 返回；编译通过 | 招募、死亡/重生、世界 Pawn、旧事件读取待测 |
| PERF-008 | A | 源码确认 | 分裂箭为每个子箭重复构建目标候选，追踪重选重复扫描 | 每次分裂只构建一次池化候选 List/HashSet，所有子箭复用；按权重单遍选取；追踪重选复用同一收集器 | 子箭循环外只有一次候选收集；编译通过 | 27 分裂箭的目标、数量、伤害与扫描次数待测 |
| PERF-009 | A | 源码确认 | 连续小箭装饰 Fleck 按固定频率生成，不看屏幕或同类发射器数量 | 离屏不生成；同 Def 活动发射器超过 32/64 时按 ThingID 哈希确定性降为 1/2、1/4 频率 | 预算仅包围 Fleck 生成；战斗判定路径未引用预算 | 64 发箭雨的视觉与 FPS 对照待测 |
| PERF-010 | A | 源码确认 | 莲花护盾回复和部分警告/冷却状态逐 Tick 递减或重复查资源组件 | 护盾回复按 15 Tick 累计，受击前冲刷待结算回复；重置、长息警告与延迟队列改为绝对到期 Tick | 旧倒计时 Scribe 键继续读取；新增绝对 Tick 键为增量字段；编译通过 | 旧存档、新存档与伤害交错时序待测 |
| PERF-011 | B | 源码确认 | 涅羽近战残影以 Pawn ID 为静态字典键，生命周期不受 Verb 回收控制 | 改为 `ConditionalWeakTable<Verb_MeleeAttack, SlashState>` | 不再有永久 Pawn ID 状态表 | 长时间换武器/回菜单内存待测 |
| PERF-012 | B | 静态可疑 | `ShotReport` 目标与体型因子仍使用缓存的 `FieldInfo.GetValue` | 未改：按射击报告而非每 Tick/每帧调用，且涉及值类型装箱和兼容风险；先等 Dubs 证明 | 已定位，未伪装为性能成果 | 若在 Dubs 中占比明显，再换已核验 FieldRef/专用补丁 |
| PERF-013 | B | 静态可疑 | 若干技能命中、照黎斩击和公共半径工具仍使用 `GenRadial.RadialCellsAround`；照黎短视觉采样仍有小列表 `RemoveAt(0)` | 未批量替换：这些路径频率、半径和上限不同，关键词不足以证明热点 | 已逐处保留并列为观察项 | 用对应技能压力场景采样后再决定 |
| PERF-014 | C | 静态可疑 | 大量方向帧/动画帧同哈希，部分 1024 贴图透明边界很大 | 不删除、不默认缩放；先确认 RimWorld 命名约定、实际显示尺寸和视觉品质 | 见资产审查 | 纹理内存与加载峰值待游戏内测量 |

## 关键实现说明

### 全局补丁与护盾

- `GetStatValue` 合并入口先拒绝不受影响的 Stat，再按涅羽、照黎原有的最终结果修正规则执行。
- `RenderPawnAt` 合并入口使用 `PawnRenderer.pawn` 的预生成 FieldRef；护盾组件仅按精确 HediffDef 获取。
- 弱键缓存不会强持有 Pawn。Hediff 移除、Pawn 死亡、DeSpawn 和 `Verse.Profile.MemoryUtility.ClearAllMapsAndWorld` 都会主动失效。
- 静态材质表只包含固定贴图路径，不保存 Pawn、Thing、Map；每实例颜色/透明度通过共享 `MaterialPropertyBlock` 传入。

### 周期任务与存档

- 领域的伤害、资源、目标规则和脉冲边界保持不变；平方距离只替代候选枚举方式。
- 莲花护盾把连续浮点回复累积到 15 Tick 边界；任何伤害判定前先应用尚未结算的同等回复量。
- 原有剩余 Tick 字段仍在保存与读取；加载旧档时迁移为绝对到期 Tick。现有 Scribe 键没有重命名。
- 新增 `nextDueTick` 都是运行时索引，不形成新的公共 Mod API。

### 弹体与视觉预算

- 轨迹点数量、Catmull-Rom 形状、宽度、颜色渐变和弹体逻辑保持原配置；仅把多个分段提交合为一个 Mesh 提交。
- 分裂箭数量、目标资格、权重、伤害与飞行逻辑不变；候选集从“每个子箭一次”降为“每次分裂一次”。
- Fleck 预算只控制装饰生成。阈值为 32/64 个同 Def 活动发射器，ThingID 哈希保证同场景可复现。

## XML、程序集与贴图审查

### XML/Def

- 当前 233 个 XML 全部可以由 XML 解析器读取。
- 360 个非抽象 DefName 全部唯一，没有新增重复具体 DefName。
- 38 个 PatchOperation 均可解析；是否命中外部 Mod 的实际节点需要 RimWorld 启动日志验证。
- C# Debug/Release 都针对本机 RimWorld 1.6 `Assembly-CSharp.dll` 和 Harmony 2.4.2 编译通过。
- “无缺失 XML 类型”和“无启动红字”必须由游戏实际加载确认，本报告不以静态编译代替启动验证。

### 贴图

主 `Content/Textures` 结果：

| 指标 | 结果 |
|---|---:|
| 文件数 | 294 |
| 压缩文件体积 | 14,799,960 bytes（14.11 MiB） |
| 像素量 | 101,307,004（101.31 MP） |
| RGBA32 理论解码量 | 405,228,016 bytes（386.46 MiB） |
| 最大边 ≥1024 | 59 |
| 最大边 ≥2048 | 0 |
| 同哈希组 | 43 组 / 112 个文件 |
| 理论重复磁盘量 | 1,522,373 bytes（1.45 MiB） |
| alpha 包围盒外区域 ≥20% | 264 |

另有 1 张条件兼容贴图，完整运行时纹理根合计 295 张。透明边界数字是 alpha 包围盒估计，不等同于可直接裁切；角色翅膀方向帧、动画帧和武器命名变体受 RimWorld 加载约定约束。没有仅凭哈希或透明边界删除、裁切或降采样任何资源。

## 验证矩阵

| 验证项 | 结果 | 证据/待办 |
|---|---|---|
| Debug 全量重编译 | 通过 | 隔离输出；0 错误，无警告输出 |
| Release 全量重编译 | 通过 | 隔离输出；0 错误，无警告输出 |
| XML 解析 | 通过 | 233/233 |
| 重复具体 DefName | 通过 | 0 |
| 全局 `TicksAbs` 补丁 | 通过 | 0 个 |
| 全局 `GetStatValue` 入口 | 通过 | 1 个 |
| 全局 `RenderPawnAt` 护盾入口 | 通过 | 1 个 |
| RimWorld 1.6 私有字段/生命周期符号 | 通过 | 本机程序集反射核验 |
| 涅羽：属性、护盾、领域、招募、死亡/重生、弓与近战动画 | 待游戏内 | 需要固定开发者存档 |
| 照黎：属性、业链、护盾、死亡/重生、AI/斩击 | 待游戏内 | 需要固定开发者存档 |
| 清和：领域、莲花/水镜护盾、资源与状态 | 待游戏内 | 需要固定开发者存档 |
| 明渊：重生、时间/生命燃烧、弹幕和弓 | 待游戏内 | 需要固定开发者存档 |
| 旧存档读取、新档回读、连续回菜单再开局 | 待游戏内 | 检查迁移键和缓存生命周期 |
| Dubs PA 百 Pawn、多个清和领域、27 分裂箭、64 箭雨、四护盾 | 待实测 | 需要同存档修复前/后捕获 |
| S/A 自耗时下降 ≥50%、整体 TPS/FPS 不下降 | 待实测 | 不能由静态检查推断 |

## 构建产物与工作树保护

- Debug 隔离产物：`C:/Users/mache/.codex/visualizations/2026/07/14/019f5ea8-08fb-7511-8ce7-bdc46620ddb3/build-debug/MiliraXian_NeiyuLaw.dll`
- Release 隔离产物：`C:/Users/mache/.codex/visualizations/2026/07/14/019f5ea8-08fb-7511-8ce7-bdc46620ddb3/build-release/MiliraXian_NeiyuLaw.dll`
- 工作树内已经修改的发布 DLL 未被覆盖，避免破坏审查前的用户产物。
- packageId、既有 DefName、翻译键、XML 对外字段和既有 Scribe 键均未重命名；未新增第三方依赖。

## Skill 交付

已创建并安装 `$rimworld-mod-performance-audit`：

- 安装目录：`C:/Users/mache/.codex/skills/rimworld-mod-performance-audit`
- `SKILL.md`：精简的端到端执行流程
- `references/audit-rules.md`：修订后的 S/A/B/C、证据、Harmony、渲染、存档与资产规则
- `references/rimworld-1.6-verification.md`：1.6 原始符号核验流程
- `references/report-template.md`：统一问题与验证报告格式
- `scripts/inventory.ps1`：只读仓库盘点脚本
- `agents/openai.yaml`：由初始化流程生成并补全界面元数据

`quick_validate.py` 对暂存版本和已安装版本均返回 `Skill is valid!`；盘点脚本已在本仓库执行端到端测试，且不会写入仓库。

## 下一轮实测要求

要把“源码确认”升级为“Dubs PA 实测确认”，应使用同一固定存档、Mod 列表、游戏版本、镜头、倍速、热身与采样时长分别采集修复前后数据。至少覆盖：常规百 Pawn、多个清和领域、27 分裂箭、64 发箭雨、四角色护盾/渲染压力、旧档回读和连续回菜单。记录自耗时、调用次数、GC 分配、TPS/FPS，并逐项核对数值和时序；没有前后捕获的项目继续保持“待测”。
