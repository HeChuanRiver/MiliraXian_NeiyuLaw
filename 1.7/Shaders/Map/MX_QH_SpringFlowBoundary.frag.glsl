#version 450

// 春泉涌流：边界光幕。
//
// 边界不是从贴图里「检测」出来的，而是直接对「内格 / 外格之间的格边线段」算欧氏距离：
//   - 线段内部，等距线是直线  ⇒ 格子边界的直线段严格笔直
//   - 顶点处，等距线是以顶点为圆心的圆弧 ⇒ 拐角自然圆滑，无 90° 叠、无 270° 缺角
//   line = exp(-(d/σ)^2) 就是「零厚度边界 + 高斯模糊」的闭式解。
//
// 格集合由 _FieldRadius 给出：格子 (dx, dz) 在范围内 ⟺ dx² + dz² ≤ radius²，
// 与玩法（CompSpringFlowField 的距离判定、GenRadial.NumCellsInRadius 的截断）
// 是同一条判据。
//
// 形状的解析写法：场是按行的半宽 w(k) = floor(sqrt(R² - k²)) 铺开的，所以每一层
// （z = k + 0.5 的水平格边、x = k + 0.5 的垂直格边）都能先算成一个只跟 X 有关的
// 「层项」：水平层给出 (层高, 到该层 x 区间的平方距离)，垂直层给出 (到该层竖线的
// 平方距离, 该层的 z 区间)。逐层取最小即可——这正是原来 3×3 邻域穷举的结果
// （离线对拍在可见带内误差 1e-15）。
//
// 于是一个像素只做两件事：预处理 5+5 个层项；之后每帧的 24 步光幕，每步只是
// 5 次 (dz² + c) 和 5 次 clamp，不再每步重算隶属与线段距离。
// 光幕的取样点最多比本像素近 height，所以平方距离超过 (height + 4σ) 的像素直接丢弃，
// 只有贴着边界的一圈才跑那 24 步。距离比较全程用平方，只在最后做一次 exp。

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _Glow;      // @color @default = vec4(1.0, 0.62, 0.80, 1.0)  // 春粉
    // x: 光幕高度（格）, y: 强度 0..1, z: 线宽 σ（格）, w: 光幕增益
    vec4 _Params;    // @default = vec4(0.4, 1.0, 0.024, 4.23)
    vec4 _BloomTint; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _BloomIntensity; // @default = 1.0
    // x, y: 场地中心（地图格坐标，y 即世界 z；格子中心 +0.5）
    vec4 _FieldCenter; // @default = vec4(0.0, 0.0, 0.0, 0.0)
    float _FieldSize;  // 场地边长（格）= 2R + 3
    float _FieldRadius; // CompProperties_SpringFlowField.radius，格
} u;

const int CURTAIN_STEPS = 24;
const int LEVELS = 5;

// 预计算的层项（每像素一次）。索引 i 对应层 k = kz - 2 + i 或 kx - 2 + i。
float gHz[LEVELS];      // 水平层的 z
float gHdu[LEVELS];     // 该水平层到 X 的平方距离（无该层时为 1e9）
float gVdu[LEVELS];     // 该垂直层竖线到 X 的平方距离
vec2 gVspan[LEVELS];    // 该垂直层的 z 区间（无该层时为 (1e9, 1e9)）
float gR2;

// 第 k 行（列）的半宽，以格计；空行返回 -1。
float wOf(int k)
{
    float kk = float(k);
    float v = gR2 - kk * kk;
    return v < 0.0 ? -1.0 : floor(sqrt(v) + 1.0e-4);
}

void buildLevels(float X, float Z)
{
    int kz = int(floor(Z));
    int kx = int(floor(X));
    for (int i = 0; i < LEVELS; i++)
    {
        int k = kz - 2 + i;
        float lo = wOf(k);
        float hi = wOf(k + 1);
        gHz[i] = float(k) + 0.5;
        if ((lo < 0.0 && hi < 0.0) || lo == hi)
        {
            gHdu[i] = 1.0e9;
        }
        else
        {
            float a = min(lo, hi) + 0.5;
            float b = max(lo, hi) + 0.5;
            float du = max(0.0, max(a - abs(X), abs(X) - b));
            gHdu[i] = du * du;
        }

        int k2 = kx - 2 + i;
        float lo2 = wOf(k2);
        float hi2 = wOf(k2 + 1);
        float du2 = X - (float(k2) + 0.5);
        if ((lo2 < 0.0 && hi2 < 0.0) || lo2 == hi2)
        {
            gVdu[i] = 1.0e9;
            gVspan[i] = vec2(1.0e9, 1.0e9);
        }
        else
        {
            gVdu[i] = du2 * du2;
            gVspan[i] = vec2(min(lo2, hi2) + 0.5, max(lo2, hi2) + 0.5);
        }
    }
}

// 到最近格边的平方距离（Z 是当前的取样高度，光幕每一步都在变）
float dist2At(float Z)
{
    float best = 1.0e9;
    float az = abs(Z);
    for (int i = 0; i < LEVELS; i++)
    {
        float dz = Z - gHz[i];
        best = min(best, gHdu[i] + dz * dz);

        float dv = max(0.0, max(gVspan[i].x - az, az - gVspan[i].y));
        best = min(best, gVdu[i] + dv * dv);
    }
    return best;
}

// exp(-(d/σ)^2)，输入是平方距离，免掉 sqrt
float lineFromSquared(float d2, float sigma)
{
    float s2 = max(sigma * sigma, 1.0e-6);
    return exp(-d2 / s2);
}

void main()
{
    float strength = u._Params.y;
    if (strength <= 0.0)
    {
        discard;
    }

    float height = u._Params.x;
    float sigma = u._Params.z;
    float gain = u._Params.w;

    float size = u._FieldSize;
    gR2 = u._FieldRadius * u._FieldRadius;

    // quad：x ∈ [中心 - size/2, 中心 + size/2]，
    //       z ∈ [同上] 再向北多出 height（光幕向上溢出的那一截）
    vec2 origin = vec2(u._FieldCenter.x - size * 0.5, u._FieldCenter.y - size * 0.5);
    float worldX = origin.x + vUv.x * size;
    float worldZ = origin.y + vUv.y * (size + height);

    // 以场地中心为原点的格坐标：格心在整数上，格 (i, j) 占 [i-0.5, i+0.5]
    float X = worldX - u._FieldCenter.x;
    float Z = worldZ - u._FieldCenter.y;

    buildLevels(X, Z);
    float ground2 = dist2At(Z);

    // 贴着边界的一圈之外什么都没有：光幕的取样点最多比本像素近 height，
    // 平方距离再小也不可能低于 (height + 4σ)，到此为止全部丢掉。
    float horizon = height + 4.0 * sigma;
    if (ground2 > horizon * horizon)
    {
        discard;
    }

    // 地面那条线
    float line = lineFromSquared(ground2, sigma);

    // 光幕：把地面线沿 +z 逐层堆叠（每一层都是「下移 h 之后的同一条边界」）
    float curtain = 0.0;
    for (int i = 0; i < CURTAIN_STEPS; i++)
    {
        float h = height * (float(i) + 0.5) / float(CURTAIN_STEPS);
        curtain += lineFromSquared(dist2At(Z - h), sigma) * pow(1.0 - h / height, 1.4);
    }
    curtain *= (height / float(CURTAIN_STEPS)) * gain;

    float boundary = line + curtain;

    vec3 col = u._Glow.rgb * boundary;
    col *= u._Color.rgb * u._Color.a * vColor.rgb * vColor.a * u._BloomTint.rgb * u._BloomIntensity * strength;
    outColor = vec4(max(col, vec3(0.0)), 1.0);
}
