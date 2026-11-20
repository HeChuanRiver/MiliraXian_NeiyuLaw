# 制作 VFX：框架使用指南

## 这是什么

MiliraXian 的特效由「发射器 + 若干绘制项」组成。

**发射器**（emitter）决定这次播放有哪些粒子、每颗的寿命与属性，它可以每帧读游戏状态（墙的朝向、pawn 的位置）来决定粒子怎么排。
**绘制项**（pass）决定这些粒子怎么变成屏幕上的图形：每一项指定一个渲染器、一份材质、一个渲染层，并可以选择只画某一组粒子。

一个特效因此可以拆成几个互不相干的部分。以荷雨为例：雨丝是一组粒子由「线条渲染器」画成细线，落点水花是另一组粒子由「贴图渲染器」画成小面片，地面水痕和折射是两片场地由「场渲染器」逐像素绘制。它们同属一次播放，共享同一个寿命与淡出，但各自独立绘制，互不知道对方的存在。

之所以这样拆，是因为特效的复杂度几乎总在增长：一个新特效往往先是「一个贴图」，过一阵要加「一圈涟漪」，再要加「地面高光」。如果一开始所有东西都写在一个绘制函数里，每次增加都要改动公共代码，而且上一段的改动很容易破坏下一段。拆成绘制项之后，增加内容就是往 `<passes>` 里再加一个 `<li>`。

---

## 第一个特效：跟随 pawn 的光晕

我们先做一个能立刻看见的东西：脚下的一圈柔光，跟着一个 pawn 走。

### 第一步：写 Def

在 `1.7/Defs/MiscDefs/` 任意 xml 文件里加：

```xml
<MiliraXian.Characters.Common.Vfx.Emitter.VfxDef>
  <defName>MX_Vfx_FootGlow</defName>
  <fadeOutSeconds>0.4</fadeOutSeconds>
  <capacity>1</capacity>

  <passes>
    <li>
      <name>glow</name>
      <renderer>Field</renderer>
      <texture>White</texture>
      <shaderDef>MX_QH_SpringFlowInstanced</shaderDef>
      <layer>0</layer>
      <!-- 借用的 shader 是实例化的，所以这两行必须写：它从 _InstanceData 取中心、尺寸、半径、强度 -->
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
          <!-- (光幕高度, 未用, 线宽σ, 光幕增益) -->
          <li><uniform>_Params</uniform><value>(0.35, 1, 0.02, 3)</value></li>
        </bindings>
      </field>
    </li>
  </passes>
</MiliraXian.Characters.Common.Vfx.Emitter.VfxDef>
```

这个例子借用了现成的春泉 shader 作占位，它画的是「一块 3×3 场地边界的光幕」，不是脚下的圆形光晕——形状并不符合我们要的效果，但它能完整跑通整条链路，便于先确认接入方式正确。要真正做出圆光，需要自己写一个 shader，见下面「写 shader」一节。

值得一提的是它已经用上了实例化：`instanceMesh` 与 `instanceVectors` 让它在同屏出现多个时只会产生一次提交。

### 第二步：注册 DefOf

```csharp
[DefOf]
public static class VfxDefOf
{
    public static VfxDef MX_Vfx_FootGlow;
}
```

### 第三步：在内容代码里生出来

```csharp
MapComponent_VfxEmitterHost host = map.GetComponent<MapComponent_VfxEmitterHost>();
VfxEmitterHandle glow = host.Spawn(VfxDefOf.MX_Vfx_FootGlow, VfxAnchor.Attached(thing));

host.SetChannel(glow, VfxChannels.Radius, 1.5f);
host.SetChannel(glow, VfxChannels.Strength, 1f);
```

### 第四步：维持它

发射器不会自己活着。**每 tick 调一次 `Maintain`**，表示「来源还在」：

```csharp
public override void CompTick()
{
    host.Maintain(glow);
}
```

停止调用之后，它会按 `fadeOutSeconds` 淡出并被回收，不需要手动销毁。要提前结束就调 `host.Destroy(glow)`。

到这里，一个只有一个绘制项、只有一颗粒子、完全由通道驱动参数的特效就跑通了。**这类需求不需要写任何 C# 绘制代码**——它们是绝大多数。

---

## 屏幕上的东西是怎么产生的

理解这条链路，后面的所有选择都会变得理所当然。

```
  ①  内容代码 Spawn 一个发射器
        ↓
  ②  发射器每帧 Advance：读游戏状态，决定有哪些粒子、每颗的属性
        ↓
  ③  每个绘制项调用一次渲染器：读粒子，产生顶点或实例数据
        ↓
  ④  宿主把这一帧所有绘制项的数据汇总，逐个提交
        ↓
  ⑤  Graphics.DrawMesh 只是入队，帧末才真正绘制
```

第 ⑤ 步是很多 bug 的来源：**提交那一刻屏幕上还没有任何东西**。命令里存的是网格、材质、矩阵和一份属性快照。因此同一帧内不能有两块几何共用一块网格（后一次上传会覆盖前一次），网格也不能在提交的同一帧销毁。框架里的网格池按「帧分代」回收，正是为了守住这条。

---

## 概念：谁负责什么

| 角色 | 类型 | 数量 | 职责 |
|---|---|---|---|
| 模板 | `VfxDef`（XML） | 每特效一个 | 声明由哪些绘制项组成 |
| 本体 | `VfxEffect` | 每特效一个，随 Def 缓存 | 把 XML 里的名字解析成材质与句柄 |
| 播放 | `VfxEmitter` | 每次播放一个 | 粒子、寿命、通道、锚点 |
| 宿主 | `MapComponent_VfxEmitterHost` | 每张地图一个 | 存发射器、汇总提交、清理内存 |

`VfxEffect` 是**不可变**的：它只存「这个特效长什么样」，被这个特效的所有播放共享。粒子与年龄在 `VfxEmitter` 上，所以同一个特效同时放两次互不干扰。

绘制项的定义可以写死成这样一句：**一个渲染器 + 一份材质 + 一个渲染层 + 一组粒子**。四者都写在同一层 —— 因此地面压暗不进泛光、光幕要进泛光这种差异，是在绘制项上表达，而不是在特效上。

---

## 粒子从哪来

### 默认行为

不写 `emitterClass` 时，发射器在 `Reset` 里放**一颗**粒子，位置是锚点，之后每帧跟随锚点。适合「一次播放就是一个贴图/一片场地」的特效。

### 需要自己算的时候

以下三种情况要写子类：

- 粒子数量由外部状态决定（雨丝 300 根、花瓣 40 片）；
- 每颗粒子的位置每帧都要重算（程序化排布）；
- 需要分组成几批由不同绘制项画。

```csharp
internal sealed class MyEmitter : VfxEmitter
{
    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position);   // 必须先调：数组与通道在这里分配

        for (int i = 0; i < 20; i++)
        {
            Emit(position + Offset(i), 0.6f, 0.25f, Color.white, 0);
        }
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        if (!base.Advance(deltaSeconds, nowTick, map))
        {
            return false;   // 让宿主回收
        }

        // 读游戏状态、重算粒子、持续发射、写通道
        return true;
    }
}
```

两个覆盖点都要**先调 `base`**：`base.Reset` 分配数组与通道并放好锚点粒子，`base.Advance` 推进年龄、淡出与锚点跟随。忘了调，年龄就不走、淡出就不生效，而且不会报错。

常用 API：

| 方法 | 用途 |
|---|---|
| `Emit(pos, lifetime, size, color, group = 0)` | 追加一颗粒子，返回下标；满了返回 -1 并只警告一次 |
| `Particles[i]`、`Count` | 直接读写粒子，程序化排布时逐帧覆盖 |
| `SetChannel(i, channel, value)`、`Channel(i, channel)` | 逐粒子浮点数据 |
| `SetVectorChannel(channel, vec4)`、`VectorChannel(channel)` | 发射器级 vec4 数据 |
| `FirstParticleOfGroup(group)` | 单粒子绘制项取「自己那颗粒子」 |
| `Alpha` | 当前整体淡出系数，需要逐帧淡出的渲染自己乘上 |

死亡粒子的槽位会自动复用，不需要自己维护空闲队列；0 号槽是跟随锚点的那颗粒子，不参与复用。

### 一个发射器，两组粒子

粒子的 `ParticleGroup` 是一个标签，绘制项用 `particleGroup` 声明自己画哪一组（`-1` 表示全画）。它只在发射时确定、终生不变。

这样「雨丝的落点正好是水花的出生点、落地那一刻正好是水花闪亮的那一刻」就可以在**同一个循环里**表达：

```csharp
float phase = NextRand(ref state);
float speed = Mathf.Lerp(speedLo, speedHi, NextRand(ref state));

float u = (seconds * speed / SheetHeight + phase) % 1f;   // 0 = 顶端, →1 = 落地
float head = (1f - u) * SheetHeight;

Emit(strandHead(head), lifetime, length, strandColor, 0);   // 组 0：雨丝
Emit(landingPoint, lifetime, 1f, splashColor(phase, speed), 1);   // 组 1：水花
```

耦合不是靠两个绘制函数「保持一致」，而是靠它们本来就是同一次循环里的同一组变量。

---

## 三个渲染器

每一项只能指定一个渲染器；需要两种画法就写两个绘制项。

| 渲染器 | 产生的几何 | 适用 |
|---|---|---|
| `Field` | **一片**四边形覆盖整块场地，shader 逐像素算图案 | 地面水痕、光幕、护盾、折射 |
| `Sprite` | **每颗粒子一个**四边形，全部合批进一块网格 | 花瓣、火花、水花、光斑 |
| `Streak` | **每颗粒子一条**细长四边形，头亮尾淡 | 雨丝、刀光细线、拖尾 |

### Field

| 字段 | 含义 |
|---|---|
| `quadSize` | 四边形边长（格），`sizeFromRadius` 开启时不使用 |
| `sizeFromRadius` | 边长跟随 `radius` 通道：`ceil(R) * 2 + radiusPadding` |
| `radiusPadding` | 上式里的额外余量 |
| `extraHeight` | 向北额外溢出的高度（光幕上升的那一截） |
| `useAltitude`、`altitude` | 抬到某一高度层 |
| `centerUniform`、`sizeUniform` | 让 shader 知道画布中心与边长的 uniform（非实例化时用） |
| `radiusChannel`、`strengthChannel` | 实例化时直接读通道，不经过 uniform |

Field 的关键在于**它的 shader 要自己知道「场地有多大、边界在哪」**。当一片四边形覆盖整块场地时，顶点只告诉你四个角，中间每个像素的图案要靠 shader 用 uv 或坐标算出来。多数 Field shader 因此需要 `centerUniform` / `sizeUniform`（或改用实例化数据），否则它无从判断自己在场地里的位置。

### Sprite

| 字段 | 含义 |
|---|---|
| `plane` | `Ground`（躺地）、`Vertical`（竖立）、`Billboard`（面向镜头） |
| `quadSize` | 基础尺寸，`useParticleSize` 开启时再乘粒子的 `Size` |
| `useParticleSize` | 尺寸是否跟随粒子 |
| `useParticleRotation` | 面内旋转是否跟随粒子 |
| `useParticleColor` | 颜色是否跟随粒子（关闭则用 `tint`） |
| `tint` | 颜色乘数 |
| `atlasColumns`、`atlasRows`、`frameChannel` | 图集切分，帧号来自通道 |
| `useUvRect`、`uvMin`、`uvMax` | 不用图集，直接把 uv 交给 shader |
| `useAltitude`、`altitude` | 抬到某一高度层 |

`useUvRect` 是给「把 uv 当坐标用」的 shader 准备的。荷雨的水花 shader 里，uv 不是纹理坐标，而是**到落点的格偏移**（±0.45），所以它必须关闭图集、直接给 uv。

### Streak

| 字段 | 含义 |
|---|---|
| `width` | 横截面宽度（格） |
| `tailAlpha` | 尾端 alpha 占头端的比例 |
| `useAltitude`、`altitude` | 抬到某一高度层 |

Streak 的几何完全由粒子决定：**位置是头端，`Velocity` 是方向，`Size` 是长度，`Color` 是头端颜色**。尾端颜色由 `tailAlpha` 推出。所以发射器只要把方向写进 `Velocity`，线条就会朝着那个方向拖长。

---

## 数据的四种去向

同样一个「半径」，放在哪里决定了能不能合批。这是框架里最需要想清楚的一件事。

| 去向 | 谁提供 | 变化粒度 | 能合批吗 |
|---|---|---|---|
| 顶点 | 渲染器写进网格 | 每个顶点 | 可以 |
| 实例数据 | 发射器每帧写一行 | 每个实例 | 可以（一次 draw 画 N 个） |
| 帧级 uniform | 引擎按相机算 | 全屏一份，每帧变 | 同帧内只能一个值 |
| 绘制项常量 | XML 里写死 | 全特效一份 | 可以 |
| 通道 → uniform | 发射器每帧写 | **每个发射器一份** | 不可以 |

道理很简单：一次 draw 调用只带一份 uniform。字段相同就分不开，也就合不成批。如果两个发射器的半径不同而半径走 uniform，它们必然两次提交。

想让它们合成一次提交，就必须把半径下沉到**实例数据**里。

### 通道

通道是发射器上的命名数据，分浮点与 vec4 两种，容量各自 16 个。XML 用名字声明来源：

```csharp
// 浮点：每个发射器一份
host.SetChannel(handle, VfxChannels.Radius, 4.5f);

// vec4：也是每个发射器一份，但一次传四个分量
host.SetVectorChannel(handle, RainWallChannels.Band, new Vector4(7.5f, 1.5f, seconds, strength));
```

通道名**必须在使用它的 Def 被校验之前注册**。Def 校验发生在加载期，懒注册会来不及，所以写在启动类里：

```csharp
[StaticConstructorOnStartup]
internal static class RainWallChannels
{
    internal static readonly int Axis = VfxChannels.RegisterVector("rainWallAxis");
    internal static readonly int Band = VfxChannels.RegisterVector("rainWallBand");
}
```

注册时不写名字，XML 里 `<vecChannel>rainWallBand</vecChannel>` 就会找不到，Def 校验会直接报错——这是好事，比运行期静默不动要好。

---

## 实例化：一个形状，多个参数

### 何时需要

当「几何完全一样，只有参数不同」且同屏可能出现多个时。春泉属于这一类：一次播放就是一片四边形，两个春泉的差别只有中心、半径、强度。

不开实例化时，每个发射器一块网格、一次提交。开了之后，**同一绘制项的所有发射器合成一次 draw call**。

### 怎么开

```xml
<instanceMesh>quad</instanceMesh>     <!-- 共享几何：quad 或 plane（1×1 平面） -->
<instanceVectors>2</instanceVectors>  <!-- shader 每实例读几个 vec4（1..4） -->
```

### 布局契约

缓冲的步长固定是 **4 个 vec4**，与 `instanceVectors` 无关。即使只写了 2，shader 也必须按 4 跳，否则会读到上一行的残值：

```glsl
const int INSTANCE_STRIDE = 4;
int base = gl_InstanceIndex * INSTANCE_STRIDE;
```

春泉用的布局：

| 行 | 内容 |
|---|---|
| `row0` | `(center.x, center.z, size, extraHeight)` |
| `row1` | `(radius, strength, altitude, 0)` |

### shader 要写的三处

```glsl
// 1) 顶点属性：本引擎 Position 在 0、TexCoord0 在 3
layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 3) in vec4 inUv0;      // @vertex TexCoord0 optional

// 2) 实例数据：名字必须与框架绑定用的属性 id 一致（_InstanceData），
//    且 vert 与 frag 都要声明——材质布局从 frag 解析
layout(set = 2, binding = 3) readonly buffer _InstanceData { vec4 data[]; } uInst;

// 3) 位置自己算：实例化 draw 的 modelMatrix 是单位阵
float worldX = row0.x - row0.z * 0.5 + inUv0.x * row0.z;
```

第 3 条容易忘。非实例化时 shader 用 `push.modelMatrix` 把网格摆到世界；实例化时那个矩阵是单位阵，位置必须由实例数据加顶点算出，或者干脆放弃顶点位置、只用 uv。

### 实例化的 uniform 归谁

整批共享一份 uniform，所以：

- **逐实例的差异必须走实例数据**。走 uniform 会得到「所有实例都是第一个的值」这种不报错、只画错的结果。
- 整批常量（颜色、亮度、混合）写进 `context.Engine.BlockFor(pass)` 拿到的属性块。它在 append 阶段写入、帧末提交时才用，所以能活下来。
- 非实例化的绘制项用 `context.Engine.Block`，那里写与提交紧邻，安全。

---

## 写 shader

大多数新特效需要一个新 shader，因为现有 shader 都是为某个具体画面写的。

### 文件与声明

两个 glsl 放 `1.7/Shaders/Map/`，再在某个 `*Shaders.xml` 里声明：

```xml
<ShaderDef>
  <defName>MyFootGlowShader</defName>
  <shaderProgram>
    <vertexShaderFileName>MyFootGlow.vert.glsl</vertexShaderFileName>
    <fragmentShaderFileName>MyFootGlow.frag.glsl</fragmentShaderFileName>
    <blendMode>Additive</blendMode>
    <depthWrite>false</depthWrite>
    <cullMode>None</cullMode>
    <renderQueue>3191</renderQueue>
  </shaderProgram>
</ShaderDef>
```

`MX_QH_SpringFlowInst.vert.glsl` 与 `.frag.glsl` 是目前最新的一对，可以直接当模板抄。

### uniform 块

写在 **frag** 里，引擎从这里解析材质布局。binding 号自己分配：0 是材质块，之后是 sampler，storage buffer 习惯用 3。

```glsl
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _Glow;      // @color @default = vec4(0.4, 0.8, 1.0, 1.0)
    vec4 _BloomTint; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _BloomIntensity; // @default = 1.0
} u;
```

后面两个不是可选的：泛光会把**同一份材质**再画进泛光层，那张副本用 `_BloomTint` 与 `_BloomIntensity` 着色。不声明它们，泛光要么全白要么全黑。

### 顶点属性位置

本引擎的顶点布局固定：位置在 `location = 0`，颜色在 2，`TexCoord0` 在 3，`TexCoord1`、`TexCoord2` 依次往后。把 uv 声明成 0 会读到位置数据，画面会以一种很难看懂的方式错掉。

### 与 shader 的契约

有些 shader 会**自己再算一遍时间**，为的是逐像素精度。这时 C# 必须传它算时间所需的**常量**，而不是算好的结果。

荷雨的水花就是这样：

```glsl
float cycle = fract(t * speed / SHEET_H + phase);   // t 来自 _Band.z，phase 来自顶点色
float fade  = (1.0 - cycle) * (1.0 - cycle);        // cycle ≈ 0 时最亮
```

```csharp
splash.Color = new Color(phase, speedT, rings, 1f);   // 传 phase，不是当前的 u
```

两边用同一个式子算周期，雨丝的落地（`u` 绕回 0）与水花的闪亮（`cycle ≈ 0`）就自动对齐了。如果 C# 传的是「当前进度」，shader 会算成 `fract(t·speed/H + u)`，相当于周期翻倍并逐帧漂移。

迁移旧特效时务必读一遍 shader 注释：这类契约经常只写在 GLSL 里，C# 代码看不出端倪。

---

## 新增一个渲染器

三个内置渲染器覆盖了目前所有需求，但新的画面形式迟早会出现——比如沿路径铺开的条带（Ribbon），或者一段自定义网格（Mesh）。加一个渲染器不需要改宿主，也不需要改编译器：宿主按绘制项归并提交，它并不关心这一项由谁画。

改动集中在下面八处。

**一、参数类。** 新建 `VfxRibbonParams`（放在 `VfxFieldParams.cs` 或单独文件），只放这一项专属的数据。默认值在构造函数里给全，别依赖字段初始化器——那样容易被后来者漏改。

**二、XML 镜像。** 在 `VfxDef.cs` 里加 `VfxRibbonDef`，字段与参数类一一对应，并在 `VfxPassDef` 上加一个引用：

```csharp
public VfxRibbonDef ribbon;
```

**三、`VfxPass` 上的槽位。**

```csharp
internal VfxRibbonParams Ribbon;
```

构造函数里要置 `null`。目前 `VfxPass` 的构造函数会把每个参数槽位都置空，跟着照做即可。

**四、注册表。** 在 `VfxDefCompiler.cs` 的 `VfxRenderers` 里加常量与分支：

```csharp
internal const string Ribbon = "Ribbon";

case Ribbon:
    renderer = RibbonRenderer.Instance;
    return true;
```

**五、校验。** `VfxDef.ConfigErrors` 里加一条「这种渲染器必须带对应块」，并把它的 bindings 一起交给已有的 `ValidateBindings`：

```csharp
if (pass.renderer == VfxRenderers.Ribbon && pass.ribbon == null)
{
    yield return prefix + "the Ribbon renderer needs a <ribbon> block";
}

ValidateBindings(pass.ribbon?.bindings, prefix + "ribbon");
```

**六、编译。** `VfxEffect.BuildPass` 里映射一次：

```csharp
if (source.ribbon != null)
{
    pass.Ribbon = BuildRibbon(source.ribbon);
}
```

`BuildRibbon` 负责把 XML 里的名字解析成材质、通道 id、uniform id，和现有的 `BuildField` / `BuildSprite` 一样。

**七、渲染器类。** 新建文件，实现 `IVfxRenderer`：

```csharp
internal sealed class RibbonRenderer : IVfxRenderer
{
    internal static readonly RibbonRenderer Instance = new();

    private readonly VfxMeshWriter writer = new();   // 复用；每帧新建会持续分配

    public void Render(VfxEmitter emitter, VfxPass pass, int passIndex, in VfxRenderContext context)
    {
        VfxRibbonParams ribbon = pass?.Ribbon;
        if (ribbon == null || emitter.Count <= 0)
        {
            return;
        }

        Material material = context.Assets.Material(pass);
        if (material == null)
        {
            return;
        }

        MaterialPropertyBlock block = context.Engine.Block;
        block.Clear();
        for (int i = 0; i < ribbon.Bindings.Count; i++)
        {
            VfxUniformBinding.Apply(block, emitter, ribbon.Bindings[i], 0, context.Engine.Frame);
        }

        writer.Clear();
        float alpha = emitter.Alpha;
        for (int i = 0; i < emitter.Count; i++)
        {
            VfxParticle particle = emitter.Particles[i];
            if (!particle.Alive)
            {
                continue;
            }
            if (pass.ParticleGroup >= 0 && particle.ParticleGroup != pass.ParticleGroup)
            {
                continue;
            }
            // 把粒子变成顶点，写进 writer
        }

        if (writer.IsEmpty)
        {
            block.Clear();
            return;
        }

        Mesh mesh = emitter.MeshAt(passIndex);
        if (mesh == null)
        {
            block.Clear();
            return;
        }
        writer.Upload(mesh);

        ScreenBloomProperties bloom = context.Assets.Bloom(pass);
        if (bloom != null)
        {
            context.Engine.Bloom(bloom, mesh, material);
        }
        context.Engine.Draw(mesh, material, pass.Layer);
        block.Clear();
    }
}
```

**八、shader。** 如果现有 shader 的顶点输入能满足，就复用；否则按「写 shader」一节的约定写一对新的。渲染器写什么顶点属性，shader 就必须声明什么：位置在 `location = 0`、颜色在 2、`TexCoord0` 在 3。

### 渲染器必须遵守的六条

这些约束不写在注释里就会反复踩：

1. **它是单例，被所有发射器共用。** 任何「这次播放」的状态都不能存在字段里；只允许放临时缓冲，并在每次 `Render` 开头 `Clear`。
2. **不许持有 `Map`、`Pawn`、`Thing`。** 需要地图用 `context.Engine.Map`，需要锚点位置就现场解析——绘制发生在 tick 之前，缓存的坐标会晚一帧。
3. **不许每帧分配**，也不许每帧新建网格。
4. **不许销毁网格。** 要临时网格就用 `context.Meshes.Rent` / `Give`，框架会按帧回收。
5. **必须尊重 `particleGroup` 与 `emitter.Alpha`。** 前者决定这一项画哪些粒子，后者是整体淡出系数。
6. **属性块用前 `Clear`、用后 `Clear`。** 非实例化路径共用一份草稿块，写完就要立刻提交；实例化路径用 `BlockFor(pass)`，它的内容会活到帧末。

想走实例化，就把「遍历粒子产生顶点」换成「每颗粒子追加一行实例数据」：

```csharp
context.Engine.AddInstance(pass, matrix, row, 0);
```

此时不再需要 `emitter.MeshAt`，共享网格写在 `<instanceMesh>` 上，整项由宿主在帧末统一提交。

### 新渲染器不会带来的副作用

宿主的分桶键是绘制项本身，所以新渲染器的多项会自动按「同材质、同层」合并，不需要额外登记。它也不会影响已有的渲染器：注册表是唯一入口，`TryGet` 找不到名字才会报错。

---

## 材质、贴图、泛光、层

| XML 字段 | 说明 |
|---|---|
| `<texture>White</texture>` | 引擎白贴图，shader 自己算图案时用它 |
| `<texture>Path/To/Tex</texture>` | 普通贴图路径 |
| `<shaderDef>…</shaderDef>` | 用 ShaderDef 指定的 shader |
| `<materialDef>…</materialDef>` | 直接用一个 XML MaterialDef，优先于 shader + texture |
| `<layer>` | 渲染层，0 是普通层 |
| `<bloomGroup>`、`<bloomIntensity>` | 泛光组与强度；不写就不进泛光 |

材质由 `MaterialPool` 创建并全 mod 共享，同样的组合只建一次，框架从不销毁它们（它们属于池）。

写泛光前先想清楚这一项该不该进：地面的压暗图层是「让这里更暗」，把它送进只会加光的泛光层，结果会是亮得发白。春泉的边界光幕该进，荷雨的地面水痕不该进。

---

## 线程与资源

三条规则，违反了会得到奇怪的结果或者干脆什么都不显示。

**资源只能在主线程创建。** 材质、贴图、GPU 缓冲都算。Def 的 `ConfigErrors` 可能跑在后台线程，所以那里只校验纯数据（名字、取值范围），绝不能碰 `ContentFinder`。框架里曾经在那里检查贴图是否存在，日志里出现的就是「Tried to get a texture from a different thread」。

**不要在地图初始化回调里建资源。** 地图生成本身可能被异步长任务包在后台线程里执行，`FinalizeInit` 不保证在主线程。框架的预热因此挂在 `MapComponentUpdate` 的第一帧，那里可以确定是主线程。

**网格不能在提交的同一帧销毁。** 提交只是入队，帧末才真正绘制。网格池按帧分代回收。

### 预热

进入游戏时，框架会把所有 Def 编译成 `VfxEffect`（建材质、解析句柄），并给实例化缓冲预分配显存。这样首次施放不会因为懒创建而卡顿。

如果加了新 shader 之后首次施放仍然顿一下，那是引擎在首次实例化提交时建立管线状态，属于引擎侧行为，框架这边预热不到。

---

## 排查

| 症状 | 原因 |
|---|---|
| 同一帧里只看得见最后一个 | 两块几何共用了一块网格，被后一次上传覆盖 |
| 实例化后所有实例参数一样 | 逐实例的差异写进了 uniform，而不是实例数据 |
| 实例数据读成乱序 | shader 按 2 行步长读，而缓冲步长是 4 个 vec4 |
| uv 变成位置 | `TexCoord0` 声明在 `location = 0`，应是 3 |
| 实例化 shader 读到全 0 | `_InstanceData` 的名字与属性 id 不符，或 frag 里没声明 |
| 特效贴在地面 | 实例化路径丢了 altitude，顶点 y 不能写 0 |
| 某个量每帧漂移、周期翻倍 | 把「当前进度」当成「相位」传给 shader |
| 日志报 from a different thread | 在后台线程碰了材质或贴图 |
| Def 校验说通道未知 | 通道没有在启动类里注册 |
| 泛光全白或全黑 | shader 没声明 `_BloomTint` / `_BloomIntensity` |

---

## 提交前自查

- [ ] `defName`、`VfxDefOf` 字段、XML 三者一致
- [ ] 每个绘制项都有对应参数块：`Field` 用 `<field>`，`Sprite` 用 `<sprite>`，`Streak` 用 `<streak>`
- [ ] 用到的浮点通道与 vec4 通道都已在启动类注册
- [ ] uniform 名字与 shader 声明逐字符一致
- [ ] 实例化项写了 `instanceMesh` 与 `instanceVectors`，shader 按 4 行步长读
- [ ] 同形状多实例的绘制项考虑过实例化
- [ ] 需要泛光的 shader 声明了 `_BloomTint` 与 `_BloomIntensity`
- [ ] 贴图路径没有反斜杠；纯程序图案用 `White`
- [ ] `ConfigErrors` 里没有访问资源
- [ ] 覆盖 `Reset` / `Advance` 时都先调了 `base`
- [ ] 新的 `vfxClass` / `emitterClass` 构造函数能被反射找到（带 `VfxDef` 参数的 `internal` 即可）

---

## 迁移一个旧特效

1. 数一数它画了哪些内容：旧实现里每个 `Graphics.DrawMesh` 通常就是一个绘制项。
2. 归类：整片逐像素图案 → `Field`；每颗一个面元 → `Sprite`；每条细线 → `Streak`。
3. 把每一项的 uniform 分三类：整项常量留在 XML；逐发射器的走通道；相机导出的走帧级 uniform。
4. 把旧的绘制函数搬进 `emitterClass.Advance`，随机流与公式**逐字保持一致**（种子、抽取顺序都不能变）。
5. 对比画面，尤其检查「两个事件的配合」——落地与水花这类时序，不能只看单帧。
6. 删掉旧路径：材质字段、网格列表、绘制函数、只服务于它的常量。
7. 编译到 0 警告 0 错误，进游戏验收。

---

## 目前的能力边界

| 能力 | 状态 |
|---|---|
| `VfxDef` / `VfxEffect` / `VfxEmitter` / 宿主 / 预热 | 已实现 |
| 渲染器 `Field`、`Sprite`、`Streak` | 已实现 |
| 粒子分组、实例化、帧级 uniform、vec4 通道 | 已实现 |
| `Field` 实例化 | 已实现，春泉在用 |
| `Sprite`、`Streak` 实例化 | 尚未：它们仍按「每发射器一块网格」绘制 |
| `Ribbon`（沿路径的条带）、`Mesh`（自定义网格）渲染器 | 尚未 |
| 曲线参数（`SimpleCurve`） | 尚未 |
| 已迁移的特效 | 春泉、荷雨的雨丝与水花 |
| 仍是旧渲染器 | 荷雨的地面、水面、雨幕、荷花 |
