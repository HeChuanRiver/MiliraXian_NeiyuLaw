# MiliraXian VFX 制作指南

这份文档面向需要在 Mod 里制作或接入特效的开发者，涵盖 XML 配置、C# 发射器、实例化优化和配套 Shader 的写法。关于框架扩展和底层约束放在文末附录，日常做特效只需要看前面几节。

---

## 一、先看懂四个概念

这套 VFX 框架把一个特效拆成了「**发射器（Emitter）+ 若干 Pass**」：

| 概念 | 对应类型 | 作用 |
|---|---|---|
| **特效定义** | `VfxDef`（XML） | 声明特效的生命周期、粒子上限，以及要画哪些 Pass |
| **运行时模板** | `VfxEffect` | 由 `VfxDef` 编译并缓存一份，把 XML 里的名字解析成材质、通道 ID 和 Shader 属性 ID |
| **播放实例** | `VfxEmitter` | 每次 `Spawn` 创建一个，持有本次播放的锚点位置、寿命、通道数据和粒子数组 |
| **地图宿主** | `MapComponent_VfxEmitterHost` | 每张地图一个，负责创建/回收发射器、每帧更新与合批提交 |

一个特效可以包含任意多个 **Pass**（`<passes>` 里的每一项）。每个 Pass 独立指定自己的**渲染器、材质、渲染层、泛光配置**，以及可选的**粒子分组（`particleGroup`）**。

以荷雨为例：雨丝是一个 `Streak` Pass，雨幕是一个 `Field` Pass，落点水花是一个 `Sprite` Pass。它们共享同一个发射器的生命周期和状态，但各自独立绘制，以后要再加地面水痕或高光，往 `<passes>` 末尾追加一项即可，不用改已有代码。

### 我的特效需要写多少代码？

| 需求类型 | 需要写什么 |
|---|---|
| 单个场地/光环/光幕，只需要传半径、强度等参数 | **纯 XML**（`Field`）+ 业务代码里调用 `Spawn` / `SetChannel` |
| 需要大量粒子（雨丝、花瓣、火花）或每帧程序化排布 | XML + 继承 `VfxEmitter` 写粒子生成/更新逻辑 |
| 需要全新视觉图案 | 额外写一对 GLSL Shader（`vert` + `frag`）并注册 `ShaderDef` |

---

## 二、快速上手

### 场景 1：纯 XML 的持续场地特效

先跑通最简单的一条链路：给目标挂一个跟随移动的场地特效。下面先借用现有的春泉着色器 `MX_QH_SpringFlowInstanced` 占位（屏幕里会显示一圈 3×3 边界光幕），跑通后再按第五节换成你自己的 Shader。

**1. 添加 `VfxDef`**（放在 `1.7/Defs/MiscDefs/` 任意 XML 文件中）：

```xml
<MiliraXian.Characters.Common.Vfx.Emitter.VfxDef>
  <defName>MX_Vfx_FootGlow</defName>
  <fadeOutSeconds>0.4</fadeOutSeconds>
  <!-- 纯 Field 特效不需要粒子数组，capacity 保持默认 0 即可 -->

  <passes>
    <li>
      <name>glow</name>
      <renderer>Field</renderer>
      <texture>White</texture>
      <shaderDef>MX_QH_SpringFlowInstanced</shaderDef>
      <layer>0</layer>
      <!-- 借用的春泉 shader 是实例化版本，因此需要写这两行 -->
      <instanceMesh>quad</instanceMesh>
      <instanceVectors>2</instanceVectors>
      <field>
        <quadSize>(3, 3)</quadSize>
        <useAltitude>true</useAltitude>
        <altitude>MoteLow</altitude>
        <radiusChannel>radius</radiusChannel>
        <strengthChannel>strength</strengthChannel>
        <bindings>
          <li><uniform>_Glow</uniform><value>(1, 0.9, 0.7, 1)</value></li>
          <!-- (光幕高度, 预留, 线宽 sigma, 光幕增益) -->
          <li><uniform>_Params</uniform><value>(0.35, 1, 0.02, 3)</value></li>
        </bindings>
      </field>
    </li>
  </passes>
</MiliraXian.Characters.Common.Vfx.Emitter.VfxDef>
```

**2. 在 `VfxDefOf` 里声明字段**：

```csharp
[DefOf]
public static class VfxDefOf
{
    public static VfxDef MX_Vfx_FootGlow;
}
```

**3. 在业务逻辑中生成并更新参数**：

```csharp
MapComponent_VfxEmitterHost host = map.GetComponent<MapComponent_VfxEmitterHost>();
VfxEmitterHandle handle = host.Spawn(VfxDefOf.MX_Vfx_FootGlow, VfxAnchor.Follow(pawn));

host.SetChannel(handle, VfxChannels.Radius, 1.5f);
host.SetChannel(handle, VfxChannels.Strength, 1f);
```

**4. 每 tick 保活（持续型特效）**：

默认情况下 `requireMaintain` 为 `true`，只要来源还在，就每 tick 调一次 `Maintain`：

```csharp
public override void CompTick()
{
    host.Maintain(handle);
}
```

停止调用 `Maintain` 后，发射器会按 `fadeOutSeconds` 平滑淡出并自动回收；如果要立刻消失（不淡出），调 `host.Destroy(handle)`。

> **做一次性瞬发特效（比如受击、爆炸、斩击）**：把 `<requireMaintain>false</requireMaintain>` 并填上 `<lifetimeSeconds>1.2</lifetimeSeconds>`。`Spawn` 之后不需要再调 `Maintain`，它会自动播满 `lifetimeSeconds` 后按 `fadeOutSeconds` 淡出并回收。

---

### 场景 2：自定义多粒子发射器（多 Pass 联动）

当特效需要几十上百颗粒子、或者需要每帧根据游戏状态重算粒子位置时，给 `VfxDef` 指定 `<emitterClass>` 并填好 `<capacity>`，再写一个继承 `VfxEmitter` 的类：

```csharp
internal sealed class MyBurstEmitter : VfxEmitter
{
    // 必须保留 public 无参构造函数（或像这样不手写构造函数，由编译器默认生成）

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position); // 必须先调，负责分配/清空数组

        for (int i = 0; i < 20; i++)
        {
            Emit(position + RandomOffset(i), lifetimeSeconds: 0.6f, size: 0.25f, Color.white, particleGroup: 0);
        }
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        if (!base.Advance(deltaSeconds, nowTick, map))
        {
            return false; // 已过期或锚点目标消失，交给宿主回收
        }

        // 在这里读取游戏状态、更新粒子、继续 Emit 或写入通道
        return true;
    }
}
```

**重写 `Reset` 和 `Advance` 时一定要先调用 `base`**：`base.Reset` 会初始化粒子数组与通道，`base.Advance` 会推进发射器寿命、淡出、锚点位置和所有粒子的速度积分。

#### 用 `particleGroup` 让多个 Pass 同步

每颗粒子在 `Emit` 时可以带一个整数分组标签 `particleGroup`（默认 `0`）；Pass 在 XML 里配置 `<particleGroup>` 决定自己只画哪一组（`-1` 表示全画）。

在同一个循环里往不同分组发射粒子，就能保证多个视觉层天然对齐。例如荷雨（`RainWallEmitter`）在同一次循环里同时算出雨丝和落点水花：

```csharp
float phase = NextRand(ref state);
float speed = Mathf.Lerp(speedLo, speedHi, NextRand(ref state));
float u = (seconds * speed / SheetHeight + phase) % 1f;
float head = (1f - u) * SheetHeight;

// 组 0 给 Streak Pass 画雨丝，组 1 给 Sprite Pass 画水花
Emit(strandHead(head), 0f, length, strandColor, particleGroup: 0);
Emit(landingPoint, 0f, 1f, splashColor(phase, speed), particleGroup: 1);
```

#### `VfxEmitter` 常用成员

| 成员 | 说明 |
|---|---|
| `Position` | 当前锚点解析出的世界坐标（每帧更新） |
| `Alpha` | 当前淡出系数（正常播放时为 `1`，进入淡出后从 `1` 线性降到 `0`） |
| `Particles[i]`、`Count` | 粒子数组及当前已分配上限；程序化排布时可以直接覆写 `Particles[i]` |
| `Emit(pos, lifetime, size, color, group = 0)` | 发射一颗粒子并返回下标；数组满了返回 `-1` 并警告一次。死粒子的槽位会自动复用 |
| `SetEmitterChannel(ch, val)` / `EmitterChannel(ch)` | 读写发射器级浮点通道（给 uniform 和 `Field` 用） |
| `SetVectorChannel(ch, vec4)` / `VectorChannel(ch)` | 读写发射器级 `vec4` 通道 |
| `SetTextureChannel(ch, tex)` / `TextureChannel(ch)` | 读写发射器级动态贴图通道（如外部抓拍的 `RenderTexture`） |
| `SetChannel(i, ch, val)` / `Channel(i, ch)` | 读写第 `i` 颗粒子的逐粒子浮点通道（如每颗粒子独立的图集帧号或条带 U 坐标） |

---

## 三、配置速查

### 1. 生命周期与锚点

`VfxDef` 顶层字段：

| 字段 | 默认值 | 说明 |
|---|---|---|
| `requireMaintain` | `true` | `true` 表示需每 tick 调 `Maintain` 保活；`false` 表示一次性特效，按 `lifetimeSeconds` 自动播完（此时 `lifetimeSeconds` 必须 `> 0`） |
| `lifetimeSeconds` | `0` | 最长持续秒数。`<= 0` 表示无时间上限（仅在 `requireMaintain = true` 时合法） |
| `fadeOutSeconds` | `0.35` | 停止 `Maintain` 或到达 `lifetimeSeconds` 后的淡出时长（秒）；`0` 表示立刻销毁 |
| `capacity` | `0` | 同时存活的最大粒子数。纯 `Field` 特效留 `0` 即可，不用浪费内存 |
| `emitterClass` | 空 | 自定义 `VfxEmitter` 子类的完整类名（需有 `public` 无参构造函数） |
| `vfxClass` | 空 | 自定义 `VfxEffect` 子类的完整类名（需有带 `VfxDef` 参数的 `public`/`internal` 构造函数） |

`VfxAnchor` 支持五种挂法：
- `VfxAnchor.At(Vector3 pos)` / `VfxAnchor.At(IntVec3 cell)`：固定在某个坐标或格子。
- `VfxAnchor.Follow(Pawn pawn)`：跟随 Pawn 的 `DrawPos`；Pawn 消失或销毁时发射器自动回收。
- `VfxAnchor.Attached(Thing thing)` / `VfxAnchor.Attached(ThingComp comp)`：跟随 Thing 的 `DrawPos`；Thing 消失或销毁时发射器自动回收。

---

### 2. 四个内置渲染器

> **关于坐标系**：RimWorld 地图镜头是固定俯视的正交投影，沿世界 Y 轴立起的竖直面在镜头里会被压成一条看不见的细线。因此所有四边形一律平铺在地面（XZ 平面）上；要做出“向上升起的光幕/雨幕”，做法是把四边形沿 +Z（北）方向拉长（例如 `Field` 的 `extraHeight`）。

#### `Field`（场地渲染器）
挂在发射器锚点上，生成一块覆盖整片区域的四边形，由 Shader 逐像素绘制图案（光幕、地面水痕、护盾、水面折射）。**不依赖粒子数组**。

| 字段 | 默认值 | 说明 |
|---|---|---|
| `quadSize` | `(1, 1)` | 固定长宽（格）；开启 `sizeChannel` 或 `sizeFromRadius` 时不使用 |
| `sizeChannel` | 空 | `vec4` 通道名，读取 `(halfX, halfZ, _, _)` 作为半长半宽（优先级最高，适合朝向会变的矩形场） |
| `sizeFromRadius` | `false` | 开启后边长按 `ceil(radius) * 2 + radiusPadding` 计算 |
| `radiusPadding` | `3` | `sizeFromRadius` 的额外余量（格） |
| `extraHeight` | `0` | 向北（+Z）额外延伸的高度（格），给上升光幕/雨幕留出绘制区域 |
| `useAltitude` / `altitude` / `altitudeOffset` | `true` / `MoteLow` / `0` | 高度层及微调偏移；关闭 `useAltitude` 时在锚点 `y` 上加 `altitudeOffset` |
| `centerUniform` / `sizeUniform` | 空 | 非实例化时自动写入的 uniform：`centerUniform` 写 `(center.x, center.z, 0, 0)`，`sizeUniform` 写 `(sizeX, sizeZ + extraHeight, 0, 0)` |
| `radiusChannel` / `strengthChannel` | 空 | 实例化路径读取的发射器级浮点通道（`strength` 会自动乘上 `emitter.Alpha`） |
| `bindings` | 空 | 传递给 Shader 的额外 uniform |

#### `Sprite`（贴图/面片渲染器）
每颗存活粒子生成一个四边形，所有粒子合并成一次提交（适合花瓣、水花、火花、光斑）。

| 字段 | 默认值 | 说明 |
|---|---|---|
| `quadSize` | `(1, 1)` | 基础宽高（格） |
| `useParticleSize` | `true` | 是否乘上粒子的 `Size` |
| `useParticleRotation` | `true` | 是否使用粒子的 `RotationDegrees` |
| `useParticleColor` | `true` | 是否使用粒子的 `Color`（关闭时视作纯白） |
| `tint` | 白色 | 叠乘在粒子颜色上的色调 |
| `atlasColumns` / `atlasRows` | `1` / `1` | 图集行列数；`frameChannel` 指定读取帧号的逐粒子浮点通道 |
| `useUvRect` / `uvMin` / `uvMax` | `false` / `(0,0)` / `(1,1)` | 跳过图集切分，直接把指定 UV 范围交给 Shader（例如荷雨水花用 UV 传相对落点的格偏移 `±0.45`） |
| `flipU` / `flipV` | `false` / `false` | 水平或垂直翻转贴图 UV（非实例化生效，适合倒影或倒置贴图） |
| `rotationRate` | `0` | 实例化模式下的自转角速度（度/秒），交给 Shader 按时间计算 |
| `useAltitude` / `altitude` / `altitudeOffset` | `false` / `MoteLow` / `0` | 高度层及微调偏移；关闭 `useAltitude` 时在粒子 `Position.y` 上加 `altitudeOffset` |
| `bindings` | 空 | 传递给 Shader 的额外 uniform |

#### `Streak`（线条/拖尾渲染器）
每颗存活粒子生成一条细长四边形，由粒子属性直接决定几何：**`Position` 是头端，`Velocity` 是指向尾端的方向，`Size` 是长度，`Color` 是头端颜色**（尾端透明度按 `tailAlpha` 衰减）。适合雨丝、丝状拖尾。

| 字段 | 默认值 | 说明 |
|---|---|---|
| `width` | `0.02` | 线条横截面宽度（格） |
| `tailAlpha` | `0.25` | 尾端透明度占头端的比例（0..1） |
| `useAltitude` / `altitude` / `altitudeOffset` | `false` / `MoteOverhead` / `0` | 高度层及微调偏移；关闭 `useAltitude` 时在粒子 `Position.y` 上加 `altitudeOffset` |
| `bindings` | 空 | 传递给 Shader 的额外 uniform |

#### `Ribbon`（条带渲染器）
把同一分组里连续排列的存活粒子首尾相连成一条或多条平滑条带（适合刀光、拔刀拖尾、花铃轨迹、闪电折线）。遇到死粒子或 `ribbonIdChannel` 变化时自动断开起一条新带；如果粒子填了非零的 `Side` 向量就直接拿它当半宽侧向（适合 3D 倾斜刀光投影），留零则由渲染器自动按相邻点切线算垂直宽度。

| 字段 | 默认值 | 说明 |
|---|---|---|
| `width` | `0.2` | 基础宽度（格），`useParticleSize` 开启时再乘粒子 `Size`（粒子自带 `Side` 时优先用 `Side`） |
| `useParticleSize` / `useParticleColor` / `tint` | `true` / `true` / 白色 | 宽度、颜色与色调控制 |
| `vMin` / `vMax` | `0` / `1` | 跨宽度方向的 V 范围 |
| `vFrames` / `frameChannel` | `1` / 空 | 水平条带帧动画行数，大于 `1` 时按发射器级通道 `frameChannel` 切换 V 区间 |
| `normalizeU` / `uScale` / `uChannel` | `false` / `1` / 空 | 沿条带方向的 U 坐标：优先读逐粒子 `uChannel`；否则按累积世界长度（或开启 `normalizeU` 按 `0..1`）乘 `uScale` |
| `ribbonIdChannel` | 空 | 可选逐粒子通道，相邻粒子 ID 不同时自动断带 |
| `useAltitude` / `altitude` / `altitudeOffset` | `false` / `MoteOverhead` / `0` | 高度层及微调偏移 |
| `bindings` | 空 | 传递给 Shader 的额外 uniform |

---

### 3. Uniform 绑定（`<bindings>`）与通道

每个渲染器块里都可以配 `<bindings>`，把数据传给 Shader 的 `MaterialUniforms`。共支持 7 种写法：

```xml
<bindings>
  <!-- 1. vec4 常量（默认） -->
  <li><uniform>_Glow</uniform><value>(1, 0.62, 0.80, 1)</value></li>

  <!-- 2. float 常量（必须用 floatValue） -->
  <li><uniform>_Brightness</uniform><floatValue>1</floatValue></li>

  <!-- 3. 发射器级 float 通道（不写 component 直接写 float uniform；写 0..3 则替换 value 的 x/y/z/w 分量作为 vec4 写入） -->
  <li><uniform>_FieldRadius</uniform><channel>radius</channel></li>
  <li><uniform>_Params</uniform><channel>strength</channel><component>3</component><value>(0.4, 1, 0.02, 1)</value></li>

  <!-- 4. 发射器级 vec4 通道 -->
  <li><uniform>_Band</uniform><vecChannel>rainWallBand</vecChannel></li>

  <!-- 5. 引擎帧级变量：time（VisualClock 游戏秒数）或 screenPerCell（屏幕 UV 换算系数，供折射用） -->
  <li><uniform>_Time</uniform><frame>time</frame></li>
  <li><uniform>_DistortScale</uniform><frame>screenPerCell</frame></li>

  <!-- 6. 静态额外贴图（如溶解噪声图、扭曲法线图） -->
  <li><uniform>_NoiseTex</uniform><texture>MiliraXianQinghe/Effect/lotus_shield_dissolve_noise</texture></li>

  <!-- 7. 动态贴图通道（如外部抓拍组件产出的 RenderTexture） -->
  <li><uniform>_MainTex</uniform><texChannel>snapshotTex</texChannel></li>
</bindings>
```

#### 通道注册

框架内置了 5 个常用的浮点通道（在 `VfxChannels` 中已注册，XML 直接写名字即可）：
- `"radius"`（`VfxChannels.Radius`）
- `"strength"`（`VfxChannels.Strength`）
- `"progress"`（`VfxChannels.Progress`）
- `"alpha"`（`VfxChannels.Alpha`）
- `"angle"`（`VfxChannels.Angle`）

自定义通道（浮点上限 16、`vec4` 上限 16、贴图上限 8）需要放在 `[StaticConstructorOnStartup]` 静态类里提前注册，确保在进入游戏编译/预热 `VfxEffect` 之前可用：

```csharp
[StaticConstructorOnStartup]
internal static class RainWallChannels
{
    internal static readonly int Axis = VfxChannels.RegisterVector("rainWallAxis");
    internal static readonly int Band = VfxChannels.RegisterVector("rainWallBand");
    internal static readonly int Quad = VfxChannels.RegisterVector("rainWallQuad");
}
```

如果 XML 里写了未注册的通道名，`VfxEffect` 在进入游戏预热时会打 `Log.ErrorOnce` 提示具体的 `defName/passName` 和通道名。

---

### 4. 材质、层与泛光

`VfxPassDef` 上的材质与渲染层字段：

| 字段 | 说明 |
|---|---|
| `<texture>` | 贴图路径（用 `/` 分隔），或写 `White` 使用引擎内置白贴图（纯 Shader 程序化图案用） |
| `<shaderDef>` | 使用指定 `ShaderDef` 与 `<texture>` 组合出材质（由 `MaterialPool` 缓存复用） |
| `<materialDef>` | 直接引用 XML `MaterialDef`，配置后会忽略 `shaderDef` 和 `texture` |
| `<layer>` | 渲染层，默认 `0` |
| `<bloomGroup>` | 泛光组 `ScreenBloomGroupDef` 名称；不填则不进泛光 |
| `<bloomIntensity>` | 泛光强度，默认 `1` |

#### 接入泛光

在 Pass 上加上 `<bloomGroup>` 和 `<bloomIntensity>` 即可开启泛光，框架会自动把主绘制和泛光副本一并提交（非实例化与实例化路径均已支持）：

```xml
<bloomGroup>MX_ScreenBloom_Glow</bloomGroup>
<bloomIntensity>0.8</bloomIntensity>
```

只配 XML 就能看到泛光效果。如果希望单独控制光晕的强度或偏色，在 Shader 的 `MaterialUniforms` 中声明 `_BloomTint` 和 `_BloomIntensity` 并乘到输出颜色上（泛光那一遍绘制时引擎会自动覆写这两个值，主绘制时它们分别是纯白和 `1`，不影响本体）：

```glsl
vec4 _BloomTint;       // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
float _BloomIntensity; // @default = 1.0
```

> 泛光层是缩小模糊后以**加法叠加**回画面的，只能提亮不能压暗，不要把压暗地面的 Pass 送进泛光。

---

## 四、实例化与 Shader 动画

### 什么时候开实例化

当同屏会出现多个「几何形状相同、只有位置或参数不同」的特效（如多个春泉、大量同类粒子）时，在 Pass 上配置 `instanceMesh` 和 `instanceVectors`，就可以把同一个 Pass 的所有实例合并成 **1 次 Draw Call**：

```xml
<instanceMesh>quad</instanceMesh>     <!-- 支持 quad 或 plane（内置 1×1 平面） -->
<instanceVectors>2</instanceVectors>  <!-- 每实例占几个 vec4（1..16），决定缓冲步长 -->
```

**注意事项**：
- Shader 里的步长常量 `INSTANCE_STRIDE` 必须和本 Pass 的 `<instanceVectors>` 完全一致，否则会读错行。
- 整批实例共享同一份 uniform，**逐实例变化的参数必须走实例数据**，`<bindings>` 里只放整批共享的常量或帧级变量（如 `_Glow`、`_Time`）。
- 实例化绘制时 `push.modelMatrix` 是单位矩阵，顶点世界坐标由 Shader 根据实例数据和 `inUv0` 自行算出。

### 两个渲染器的实例数据布局

**`Field` 实例化布局**（`instanceVectors = 2`，每实例 32 字节）：

| 行 | 分量内容 |
|---|---|
| `row0` | `(center.x, center.z, sizeX, extraHeight)` |
| `row1` | `(radius, strength * emitter.Alpha, altitude, sizeZ)` |

**`Sprite` 实例化布局**（`instanceVectors = 2` 或 `3`）：

| 行 | 分量内容 |
|---|---|
| `row0` | `(center.x, center.z, halfWidth, spawnSeconds)` |
| `row1` | `(altitude 或 center.y, rotation0, rotationRate, atlasFrame)` |
| `row2` | 颜色 `(r, g, b, a)`（已乘 `tint` 和 `emitter.Alpha`，仅在 `instanceVectors >= 3` 时写入） |

> 如果实例化 `Sprite` 需要长方形（`quadSize.x != quadSize.y`），框架会自动向材质块写入 `_QuadSize = (quadSize.x, quadSize.y, quadSize.y / quadSize.x, 0)`；Shader 里用 `halfHeight = halfWidth * u._QuadSize.z` 即可，画正方形时无需声明该 uniform。

### 把周期动画交给 Shader

自转、闪烁、脉动、UV 滚动这类只依赖经过时间的动画，不需要在 C# 里每帧重算粒子：

1. 在 `<bindings>` 里绑定 `<uniform>_Time</uniform><frame>time</frame>`；
2. 该时钟和粒子发射时记录的 `SpawnSeconds` 统一走 `VisualClock`（游戏暂停即停、倍速同步加速）；
3. 在 Shader 里用 `float age = u._Time - spawnSeconds;` 算出每颗粒子各自的播放进度（如 `rot = rotation0 + rotationRate * age`），就能在零 CPU 更新的情况下让粒子错开相位各自运动。

---

## 五、编写配套 Shader

GLSL 源码放在 `1.7/Shaders/Map/`，在 `*Shaders.xml` 里声明 `ShaderDef` 即可。现成的完整参考可以直接看 `MX_QH_SpringFlowInst.vert.glsl` / `.frag.glsl`（实例化）和 `MX_QH_RainWall.frag.glsl`（非实例化）。

写 Shader 时需要遵守这几条引擎约定：

1. **固定的顶点属性 `location`**：
   - `location = 0`：`inPosition`（`vec3`）
   - `location = 2`：`inColor`（`vec4`）
   - `location = 3`：`inUv0`（`vec4`），之后依次为 `TexCoord1`、`TexCoord2`
   - 千万不要把 UV 声明在 `location = 0`，否则读到的是顶点位置。
2. **`MaterialUniforms` 写在 frag 里**：引擎从片段着色器解析材质布局（`layout(set = 2, binding = 0)`）。
3. **实例化缓冲声明**：必须命名为 `_InstanceData`，并且 **vert 和 frag 两边都要声明**（否则材质布局里不包含该缓冲，Shader 读出来全是 0）：
   ```glsl
   layout(set = 2, binding = 3) readonly buffer _InstanceData
   {
       vec4 data[];
   } uInst;
   ```
4. **注意与 C# 的公式契约**：如果 Shader 自己按时间算周期（例如荷雨水花用 `fract(t * speed / H + phase)`），C# 必须传不变的初相 `phase`，不要传已经算好的当前进度 `u`，否则周期会翻倍并持续漂移。

---

## 六、常见问题排查

| 现象 | 常见原因 |
|---|---|
| `Spawn` 后完全看不见，或刚闪一下就没了 | 默认 `requireMaintain = true` 但没有每 tick 调 `Maintain`；一次性特效请设 `<requireMaintain>false</requireMaintain>` 并配 `<lifetimeSeconds>` |
| XML 里改了 `vec4` 常量没生效或数值不对 | 检查是否写错成了 `<floatValue>`；反之给 `float` uniform 赋值必须用 `<floatValue>` 而不是 `<value>` |
| 同一帧里多个特效只看得到最后一个 | 两块几何复用了同一块 `Mesh`，后一次上传覆盖了前一次 |
| 实例化后所有对象参数都一样 | 把逐实例变化的参数写进了 `<bindings>`（uniform）而不是实例数据 |
| 实例化读到乱序数据或全 0 | `INSTANCE_STRIDE` 与 `<instanceVectors>` 不一致；或 `_InstanceData` 没在 frag 里声明 |
| 贴图 UV 错乱成坐标拉伸 | `TexCoord0` 写成了 `location = 0`，应改为 `location = 3` |
| 自定义 `emitterClass` 创建报错 | `emitterClass` 缺少 `public` 无参构造函数（注意：`vfxClass` 才需要带 `VfxDef` 参数的构造函数） |
| 预热报 `unknown channel` / `unknown vec4 channel` | 通道名拼错，或没有在 `[StaticConstructorOnStartup]` 类里提前注册 |
| 调了 `bloomIntensity` 但光晕亮度没变化 | Shader 里没有声明并乘上 `_BloomTint` / `_BloomIntensity` |
| 日志报 `Tried to get a texture from a different thread` | 在后台线程（如 `ConfigErrors` 或地图生成期间）访问了 `ContentFinder` / 创建了材质 |

---

## 附录：框架维护与扩展

以下内容仅在**扩展框架本身**（新增渲染器类型、维护底层池化机制、迁移旧代码）时需要参考。

### A. 帧内顺序与资源规则

1. **帧内执行顺序**：RimWorld 一帧里先跑当前地图的 `MapComponentDraw`（提交绘制命令），再跑所有地图的 `MapComponentUpdate`（推进 `Advance` 并清理过期网格），帧末再由引擎统一消费 `Graphics.DrawMesh` 队列。
   - 因此渲染器需要锚点坐标时应现场解析 `Anchor.TryResolve`，不要只依赖上一帧缓存；
   - `Graphics.DrawMesh` 只是入队、不拷贝顶点，同一帧内不能销毁或覆写已经提交的网格。宿主通过 `VfxMeshPool` 按帧号延迟一帧销毁（`Retire` → 下一帧 `Sweep`）。
2. **主线程资源约束**：材质、贴图、GPU 缓冲只能在主线程创建。`VfxDef.ConfigErrors` 跑在 DevMode 的并行加载阶段且早于 `[StaticConstructorOnStartup]`，因此只做纯数据语法校验，绝不访问 `ContentFinder`，通道存在性检查统一留在主线程编译/预热 `VfxEffect` 时执行。
3. **预热时机**：地图生成期间的 `FinalizeInit` 可能跑在后台线程，所以预热挂在 `MapComponentUpdate` 的第一帧执行，提前编译所有 `VfxEffect` 并为实例化 Pass 预留 `ComputeBuffer`。

### B. 新增一种渲染器

当内置的 `Field` / `Sprite` / `Streak` 无法满足新几何形态（如沿路径铺开的 `Ribbon` 或自定义 `Mesh`）时，宿主与实例化批处理无需改动，只需在框架内补齐以下几处：

1. **参数类**（如 `VfxRibbonParams`）：存放该渲染器专属的运行时参数，在构造函数里初始化所有默认值。
2. **XML 定义**（`VfxDef.cs`）：新增对应的 `VfxRibbonDef`，并在 `VfxPassDef` 上加对应字段（如 `public VfxRibbonDef ribbon;`）。
3. **Pass 槽位**（`VfxPass.cs`）：在 `VfxPass` 上加对应参数字段并在构造函数里置 `null`。
4. **注册表**（`VfxDefCompiler.cs`）：在 `VfxRenderers` 里添加名称常量与 `TryGet` 分支。
5. **语法校验**（`VfxDef.cs`）：在 `ConfigErrors` 里检查必填参数块，并用 `foreach (string error in ValidateBindings(...)) yield return error;` 接入绑定校验。
6. **编译映射**（`VfxEffect.cs`）：在 `BuildPass` 中把 XML 参数解析成运行时参数类（通道解析用 `ChannelOf` / `VectorChannelOf`）。
7. **实现 `IVfxRenderer`**：编写单例渲染器类。

**编写渲染器必须遵守的 6 条约束**：
1. 渲染器是全局单例，禁止在字段里保存单次播放的状态（仅可保留复用的临时缓冲，且每次 `Render` 开头 `Clear`）。
2. 禁止持有 `Map`、`Pawn`、`Thing` 引用；需要地图从 `context.Engine.Map` 取。
3. 热路径禁止每帧分配新对象或新建 `Mesh`。
4. 禁止直接 `Object.Destroy` 网格；临时网格用 `emitter.MeshAt(passIndex)` 或 `context.Meshes.Rent` / `Give` / `Retire`。
5. 粒子类渲染器必须尊重 `pass.ParticleGroup` 与 `emitter.Alpha`。
6. 非实例化路径使用 `context.Engine.Block`，用前 `Clear`、提交后 `Clear`；实例化路径使用 `context.Engine.BlockFor(pass)`，内容会保留到帧末 `FlushInstances` 统一提交。

### C. 迁移旧特效的步骤

1. 盘点旧实现里的每个 `Graphics.DrawMesh`，通常一个绘制调用对应一个 Pass。
2. 按形态归类：逐像素区域 → `Field`；每颗粒子一个面片 → `Sprite`；细长线段 → `Streak`。
3. 梳理 uniform 来源：全特效不变的常量留 XML；随发射器变化的走发射器级通道；随镜头/时间变化的走 `<frame>`。
4. 若需要程序化粒子，把粒子生成搬进 `VfxEmitter` 子类的 `Advance`，保持随机种子和抽取顺序与旧实现一致。
5. 进游戏对比画面（特别是多 Pass 联动时序与淡入淡出），确认无误后删掉旧的材质、网格和绘制类。

### D. 当前能力与迁移进度

| 项目 | 当前状态 |
|---|---|
| 核心管线（`VfxDef` / `VfxEffect` / `VfxEmitter` / `Host` / 预热 / `VisualClock` 统一时钟） | 已实现 |
| 持续保活（`requireMaintain = true`）与一次性自动播完（`requireMaintain = false`） | 已实现 |
| 内置渲染器：`Field`、`Sprite`、`Streak`、`Ribbon` | 已实现 |
| 贴图绑定（静态额外贴图 + 动态贴图通道）、高度偏移与贴图翻转 | 已实现 |
| 实例化支持 | `Field`（支持长方形与自动淡出）、`Sprite`（支持 `altitude`、长方形与 Shader 动画）已实现；`Streak` / `Ribbon` 尚未 |
| 扩展渲染器（`Mesh`）、曲线参数（`SimpleCurve`） | 尚未 |
| 已迁移特效 | 春泉边界光幕；荷雨的雨丝、雨幕、落点水花 |
| 仍为旧实现 | 荷雨的地面水痕、水面折射、荷花 |
