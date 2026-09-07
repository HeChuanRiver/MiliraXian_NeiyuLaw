# 告死免疫与 AL 兼容验证

## 修复范围

霓羽、清荷、昭离、明渊按 PawnKind 免疫告死，不依赖阵营、强度档位或 AL 注册状态。告死的积累、减速、割伤与处决均不生效，也不产生处决奖励。普通目标仍按原来的 9 点积累结算。

积累上限 StatPart 返回 0，使用异常系统已有的免疫入口。割伤回调和隐藏结算分别检查免疫；旧存档中的积累与结算状态可以移除，`PostRemoved` 不会因此处决特殊角色。

告死清理阶段检查真实死亡状态、是否已丢弃以及容器归属，避免清理被 AL 或其他保命机制救下的角色。销毁尸体前先在保留 `InnerPawn` 的情况下离开地图，再解除角色引用。

## 本地 AL 反编译核对

- 工坊项目：`3665997350`，`1.6/Assemblies/AriandelLibrary.dll`。
- 核对日期：2026-09-07；本地文件修改时间：2026-08-11；程序集版本：`1.0.0.0`。
- SHA-256：`24C7B28283055902AE6E566A245F5B6FA7C9AE3B10BE51D1073CED0A8160CCCB`。
- `AriandelLibrary_Pawn_Kill_Patch.Prefix` 对受保护且已注册的玩家角色执行治疗与虚境回收，并返回 `false`，因此调用 `Pawn.Kill` 不代表角色真的死亡。
- `SpecialPawnManager.RecoverDeadPawn` 经 `ProcessPawnRecovery` 使用 `VirtualPawnContainer` 保存角色；旧的 `AriandelLibrary_GameComponent_VoidPawnManager` 仍作为兼容入口和旧数据迁移层。
- AL 的治疗会移除负面 Hediff。原告死结果在 `PostRemoved` 中执行处决，会与治疗过程、原版健康状态遍历发生重入；直接清理 `Kill` 返回后的角色也缺少状态检查。

本次免疫在调用 `Kill` 之前生效，没有修改 AL 程序集、拦截 AL 的恢复流程或依赖其私有字段。其他原因造成的死亡仍交给原有保护机制。这里核对的是上述本地二进制，不保证未安装的后续版本行为。

## 自动验证

```powershell
dotnet build .\MiliraXian_NeiyuLaw.csproj --configuration Release --no-restore
.\Tools\Validation\Test-DeathSentence.ps1
.\Tools\Validation\Test-CharacterPower.ps1
.\Tools\Validation\Test-Presentation.ps1
```

`Test-DeathSentence.ps1` 检查 XML 连接关系，并在 .NET Framework 下执行正式 DLL 中的 StatPart、割伤回调、结算移除与清理方法。使用真实 RimWorld 和本地 AL 类型；无 Unity 的测试夹具仅构造必要状态，不加载玩家存档，也不模拟完整战斗。测试包含四位角色 × 三档强度、普通角色与其他 AL 角色不被误判为免疫、无初始化记录的旧结算状态、重复移除，以及 AL 容器归属保护。

## 游戏内复验项（未在本次无界面测试中执行）

1. 在敌我双方各放置四位特殊角色，以敌对昭离释放告死流场：不应新增告死、对应减速或割伤，不应处决、进入虚境或增加施法者处决奖励。
2. 加载含告死积累或隐藏结算状态的旧存档：推进游戏后应安全移除残留状态。AL 治疗清除这些状态时同样不应触发处决。
3. 对普通目标释放告死：仍在 9 点积累时结算，尸体正常消失。
4. 通过其他伤害触发特殊角色的保命、替死、复活或 AL 虚境恢复：应维持原行为。

已经处于 AL 虚境中的角色不会被自动召回，以免改变正常回收或玩家主动收纳的状态。
