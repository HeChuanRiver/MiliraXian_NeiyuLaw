# MiliraXian_NeiyuLaw 合并后全量性能复核与功能对账

审查日期：2026-07-14
目标版本：RimWorld 1.6.4871
工作分支：`dev`（HEAD `6aea0b5`，已包含清荷合并 `ecd4606`）
证据口径：静态可疑／源码确认／Dubs PA 实测确认

## 结论

本轮以合并后的新清荷实现为唯一功能基准，没有恢复旧 `CompLotusShield`、旧领域或旧 AquaMirror/Tempest/YangChun 代码。合并前已经完成的公共优化逐项对账，缺失的公共角色识别已恢复；清荷目录外不再引用 `MX_QHCharacterUtility` 或清荷命名空间，并已用“不编译 `Source/QingHe`”的 Release 重编译验证独立性。

本轮确认并修复了合并后新增的灵息曲线逐段绘制、春泉边界整图 BoolGrid、清荷残影 RenderTexture/Material 抖动、月镜/神佑动态材质、神佑与花令逐 Tick 资源计算、异常条逐帧材质/列表/Stat 扫描、未到期队列遍历和重复登记日志。Debug、Release 隔离全量重编译均为 0 警告、0 错误；工作树内用户原有 DLL 没有被覆盖。

没有新的同存档 Dubs Performance Analyzer 前后捕获，也没有包含清荷的固定压力存档。因此下表的最高证据等级仍是“源码确认”，TPS/FPS、自耗时下降 50% 和稳态 GC 不能作为已实测成果结案。

## 当前清单

| 内容 | 结果 | 说明 |
|---|---:|---|
| C# | 167 个 | 比合并基线多 1 个恢复的公共角色识别工具 |
| Skill 原始 XML 盘点 | 302/302 可解析 | 包含 6 个 `.idea` 工程 XML，不属于 Mod 运行时内容 |
| Mod 运行时 XML | 296/296 可解析 | `1.6`、`About`、`Content` 与 `LoadFolders.xml` |
| 具体 DefName 条目 | 466 | 按父 Def 类型统计 |
| 同类型重复 DefName | 0 | 三个重名项均跨 Def 类型，合法 |
| PatchOperation | 41 | XML 可解析；实际补丁命中仍需新启动日志 |
| 运行时贴图 | 360 | 压缩体积 19,297,528 bytes |
| 程序集 | 2 | 主程序集与条件兼容程序集 |

三个跨类型重名项仅作信息记录：

- `MX_QH_SpringFlow`：AbilityDef / HediffDef
- `MX_QH_LunarMirror`：AbilityDef / ThingDef
- `MX_QH_IllusoryReflection`：AbilityDef / JobDef

没有仅凭文件哈希、透明边界或关键词删除/缩放贴图。方向帧、动画帧和命名约定需要的同哈希资源全部保留。

## 旧优化对账

| 项目 | 合并后状态 | 本轮处理 |
|---|---|---|
| 全局 `TickManager.TicksAbs` getter 补丁 | 完整保留移除状态 | 全源码无 `TicksAbs` 补丁/调用 |
| 全局属性修正 | 完整保留 | 仍只有一个 `StatExtension.GetStatValue` Postfix |
| 涅羽/照黎护盾入口 | 完整保留 | 单一 `PawnRenderer.RenderPawnAt` 入口、精确 Hediff 与弱键缓存 |
| 连续弹道轨迹 | 完整保留 | 固定环形点缓冲和每弹体每帧一次动态 Mesh |
| 分裂箭候选复用 | 完整保留 | 候选收集仍位于子箭循环外，一次分裂一次扫描 |
| 装饰 Fleck 预算 | 完整保留 | 屏外跳过，32/64 阈值为 1/2、1/4 |
| 延迟队列索引 | 完整保留 | 明渊时间燃烧/重生与照黎业链索引仍在 |
| 明渊开发者伤害 | 完整保留 | 仅开发者伤害命令作用域绕过免伤；1.6.4871 共解析到 14 个命令目标 |
| 公共清荷角色识别 | 合并时丢失 | 恢复 `MXCharacterIdentityUtility` 并移除目录外清荷类型依赖 |
| 旧清荷玩法实现 | 已被新实现替换 | 未恢复，避免双实现和合并冲突 |

## S/A/B/C 问题表

| ID | 等级 | 证据 | 合并后问题 | 已实施修复 | 验证状态 |
|---|---|---|---|---|---|
| MERGE-001 | A | 源码确认 | 清荷目录外 3 处重新直接引用 `MX_QHCharacterUtility`/清荷命名空间，整包覆盖清荷会使其他角色编译失败 | 恢复 Common 身份工具；涅羽、照黎、明渊只按稳定 PawnKind defName 识别 | 排除 `Source/QingHe` 的 Release 编译通过 |
| RENDER-001 | S | 源码确认 | 灵息曲线最多约 7×96 条线段并附带双侧扭曲，约 736 次 `DrawMesh`/轨迹/帧 | 每个残影层一个固定动态 Mesh，当前层扭曲一个 Mesh；固定列表容量，离屏跳过，DeSpawn/Destroy 释放 | 设计上限约 8 次提交/轨迹/帧；Dubs/Frame Debugger 待测 |
| RENDER-002 | A | 源码确认 | 春泉领域每帧生成半径格并 `ClearAndResizeTo(map)` 整张 BoolGrid | 按径向格数量缓存相对边界拓扑；绘制时只平移并裁剪地图边缘 | 春泉默认路径不再清整图 BoolGrid；画面对照待测 |
| RENDER-003 | A | 源码确认 | 每个清荷残影创建 512×512 RenderTexture 和 Material，满 96 时 `RemoveAt(0)` | 96 个固定环形槽；RenderTexture/Material 槽内复用；过期仅停用，换图释放池 | 无头删和持续资源创建；池预热后 GC 待测 |
| RENDER-004 | A | 源码确认 | 月镜按 path/color/alpha 字符串扩张材质；神佑每帧按色取材质并修改共享材质 `_EffectTime` | 固定白色基础材质；颜色、透明度和 EffectTime 统一由 MPB 提交 | 编译通过；月镜/神佑颜色、呼吸和受击光晕待画面对照 |
| RENDER-005 | A | 源码确认 | 清荷登龙 DrawPos 状态由静态 Pawn ID 字典持有 | Harmony `___pawn` 字段注入；无活动状态 O(1) 返回；状态改为 `ConditionalWeakTable`，死亡、DeSpawn、回菜单清理 | 1.6 `Pawn_DrawTracker.pawn` 已反射核验 |
| TICK-001 | A | 源码确认 | 春泉可能重复范围枚举和重复计算施法倍率 | 合并代码已是单次 `AllPawnsSpawned` + 平方距离；本轮确认保留，不重复改写 | 10 Tick 脉冲、阵营与异常数值未改 |
| TICK-002 | A | 源码确认 | 神佑护盾虽有部分累计回复，破盾/受击延迟仍逐 Tick 递减且界面重复查 Stat | 回复固定 15 Tick 结算；伤害/恢复/保存前强制冲刷；破盾与回复延迟改绝对到期 Tick；Max/regen 缓存 | 旧四个 Scribe 键原名保留；交错伤害时序待游戏内测 |
| TICK-003 | A | 源码确认 | 神佑充能、警告和隐身结束每 Tick 做边界/Stat/Hediff 检查 | 充能/警告改绝对期限；技能上限与回复速度每 60 Tick 刷新；隐身只在预计结束点附近检查 | 旧 Scribe 键原名保留；旧档回读待测 |
| TICK-004 | A | 源码确认 | 花令每 Tick重复求 MaxValue/回复 Stat 并逐 Tick加值 | 15 Tick 累计；消费、主动增加与保存前强制结算；UI 最多 15 Tick 阶梯刷新 | 战斗消费前结算；旧 `highlightTicksLeft` 键保留 |
| UI-001 | A | 源码确认 | 每个异常条每帧拼材质字符串、扫描全部 Hediff 求堆叠位置，并重复求积累上限 Stat | 材质按 Def 扩展实例缓存；堆叠序号由 Hediff 每 60 Tick维护；积累上限缓存，应用/触发检查仍取精确值 | 异常阈值、伤害、衰减公式未改；百 Pawn 待 Dubs |
| QUEUE-001 | B | 源码确认 | 照黎附着动画和旧独立重生组件每 Tick扫描未到期列表 | 增加 `nextDueTick`/`nextRebirthTick`，入队、处理和读档后重算 | 未到期时 O(1) 返回 |
| LIFE-001 | B | 源码确认 | 多个静态 MPB/Texture 初始化类触发本 Mod `StaticConstructorOnStartup` 日志警告 | 为 5 个既有持有者及新增 Shader ID 持有者补启动标记 | 源码与编译已修；必须重新启动游戏确认日志消失 |
| LOG-001 | B | 源码确认 | 特殊角色静态 ID 冲突在 600 Tick恢复审计中重复刷相同 Pawn 警告 | 按 Pawn ID 每局只记录一次，回主菜单清空 | 不改变登记/冲突处理语义 |
| ASSET-001 | C | 静态可疑 | 同哈希方向帧、动画帧与透明边界可能看似重复 | 不改资源；等待实际显示尺寸、纹理内存与加载峰值证据 | 360 张贴图保持不变 |

## 功能与兼容性对账

- 没有修改 packageId、既有 DefName、翻译键、XML 公共字段或第三方依赖。
- 既有 Scribe 键没有重命名。绝对期限是运行时索引；保存时仍写原剩余 Tick 键，旧存档可按旧值重建。
- 新清荷春泉、月镜、神佑、花令、异常、登龙、镜花水月、任务和技能树仍是唯一玩法来源。
- 神佑和花令 UI 最多以 15 Tick 阶梯刷新；任何伤害、恢复、消费、能力判定或保存前会先结算待处理量。
- 唯一允许的可见降载是装饰视觉：清荷残影离屏跳过，活动残影超过 32/64 时按 Pawn ID 与 Tick 的确定性采样降为 1/2、1/4。战斗命中、伤害、护盾和异常不读取该预算。
- 明渊开发者伤害旁路仍只在开发者伤害命令的 ThreadStatic 作用域内生效，普通战斗免伤逻辑未放宽。
- 工作树开始前已有的 `1.6/Assemblies/MiliraXian_NeiyuLaw.dll` 修改和 `CLAUDE.md` 删除状态均保留。

## 静态与构建验证

| 验证项 | 结果 |
|---|---|
| Debug 隔离全量重编译 | 通过，0 警告、0 错误 |
| Release 隔离全量重编译 | 通过，0 警告、0 错误 |
| 排除 `Source/QingHe` 的 Release 编译 | 通过 |
| 清荷目录外类型/命名空间依赖 | 0 |
| Mod 运行时 XML 解析 | 296/296 |
| 同具体 Def 类型重复项 | 0 |
| 自定义 XML 类型解析 | 159 个，未解析 0 个 |
| RimWorld 1.6 私有字段/生命周期 | `Pawn_DrawTracker.pawn`、`MemoryUtility.ClearAllMapsAndWorld`、`Thing.DeSpawn/Destroy`、`MapComponent.MapRemoved` 已反射核验 |
| 明渊开发者伤害目标 | 14 个实际方法已解析 |
| `git diff --check` | 通过 |

隔离构建位置：

- Debug：`C:/Users/mache/AppData/Local/Temp/MiliraXian_Audit/final-debug-summary/MiliraXian_NeiyuLaw.dll`
- Release：`C:/Users/mache/AppData/Local/Temp/MiliraXian_Audit/final-release-summary/MiliraXian_NeiyuLaw.dll`
- 清荷排除构建：`C:/Users/mache/AppData/Local/Temp/MiliraXian_Audit/no-qinghe-release-summary/MiliraXian_NeiyuLaw.dll`

当前脏发布 DLL 的 SHA-256 仍为 `DDDCB3E137EAAE02AE4AF7E2B1CF52719F7A8B8ADCCC1B87862FF7BC5BD32443`，本轮没有覆盖它。

## 尚未结案的运行时验证

以下项目必须由 RimWorld 实际运行或 Dubs PA 完成，当前保持“待测”：

- 重新启动游戏，确认本 Mod 不再出现 `StaticConstructorOnStartup` 警告、Harmony 目标错误或 PatchOperation 红字。
- `my.rws`：涅羽属性/护盾/领域/弓/分裂箭/近战/死亡重生，以及明渊普通伤害、开发者伤害、燃烧免疫、时间燃烧、彩虹弓和重生。
- `testforny.rws`：照黎属性、业链、护盾、AI、斩击、延迟动画与重生。
- 新建固定清荷压力存档：春泉、月镜、神佑、充能、花令、剑压、异常、登龙、镜花水月、任务和技能树。
- 旧存档读取、新档回读、连续返回主菜单再开局，检查弱键状态、RenderTexture 池和动态 Mesh 是否跨局残留。
- Dubs PA 对照：百 Pawn 异常、多个春泉、月镜弹幕、16 条灵息轨迹、96 残影、27 分裂箭、64 箭雨。
- 只有同存档、同镜头、同倍速和同采样时长证明 S/A 自耗时下降至少 50%、池预热后无持续 GC、整体 TPS/FPS 不低于基线，才升级为“Dubs PA 实测确认”。

## 建议的功能回归顺序

1. 先做新游戏与旧档加载、回菜单再开局，排除启动和静态生命周期问题。
2. 分角色跑基础属性、伤害/免伤、资源、死亡/重生。
3. 再跑清荷春泉、月镜、神佑、花令、异常和登龙的逐 Tick/逐帧对照。
4. 最后固定镜头与倍速采集 Dubs PA；先录合并基线 DLL，再录本轮隔离 Release DLL。
