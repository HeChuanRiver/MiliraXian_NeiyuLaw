#version 450

// 荷雨：水面扭曲。不贴图，纯程序生成。
//
// 风纹：几片低频、缓慢弯曲的波包（不是正弦交叉，是"阵风"），只做折射，不做反射。
// 落点的水花不在这里 —— 水花是离散元素（300 个，各有位置和节拍），uniform 装不下，
// 由 RainWallVfx 合批成网格、走 MX_QH_RainWallSplash 画在本层之上。
//
// 和铃音冲击波扭曲同构：沿用原版的 DistortionBackground 抓屏点（queue 3151）。
// 荷花画在 3152，在抓屏之后，不会被水下扭曲扭到。

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;        // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _BloomTint;    // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // x, y：沿墙方向（东、北）；z, w：跨墙方向（东、北）
    vec4 _Axis;         // @default = vec4(0.0, 1.0, 1.0, 0.0)
    // x：半长，y：半厚，z：秒，w：强度 0..1
    vec4 _Band;         // @default = vec4(7.5, 1.5, 0.0, 1.0)
    // 8 个风纹波包，每个 (along, across, 波长, 振幅)
    vec4 _WindA;        // @default = vec4(0.0, 0.0, 1.0, 0.0)
    vec4 _WindB;        // @default = vec4(0.0, 0.0, 1.0, 0.0)
    vec4 _WindC;        // @default = vec4(0.0, 0.0, 1.0, 0.0)
    vec4 _WindD;        // @default = vec4(0.0, 0.0, 1.0, 0.0)
    vec4 _WindE;        // @default = vec4(0.0, 0.0, 1.0, 0.0)
    // xy：屏幕 UV 每格（x 正、y 负），z：扭曲增益，w：差分步长
    vec4 _DistortScale; // @default = vec4(0.0, 0.0, 1.5, 0.05)
    float _BloomIntensity; // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white
layout(set = 2, binding = 2) uniform sampler2D _GrabDistortionBackground;

// ---- 水面常量（照抄预览） ----------------------------------------------------------------------
const float WIND_DECAY = 1.5;      // 波包衰减宽度（格）
const float TINT_STRENGTH = 0.06;  // 冷色吸收
const vec3 TINT_COLOR = vec3(0.06, 0.13, 0.16);
const float EDGE_GLOW = 0.03;      // 水面边缘的微小反光（调暗）

// 带内坐标：沿墙 a、跨墙 b。
vec2 bandCoord(vec2 p, vec2 al, vec2 ac)
{
    return vec2(dot(p, al), dot(p, ac));
}

// |v| <= edge 时为 1，到 |v| = edge + soft 降到 0（平滑 S 曲线）。
float softEdge(float v, float edge, float soft)
{
    float a = abs(v);
    if (soft <= 1e-6)
    {
        return step(a, edge);
    }
    float t = clamp((a - edge) / soft, 0.0, 1.0);
    return 1.0 - t * t * (3.0 - 2.0 * t);
}

// 单个风纹波包：局部、缓慢弯曲的 crest。
vec2 windNormal(vec4 wp, vec2 p, vec2 al, vec2 ac, float t)
{
    vec2 c = al * wp.x + ac * wp.y;
    float lam = wp.z;
    float amp = wp.w;
    // 波包中心缓慢抖动
    float jx = c.x + 0.5 * sin(t * 0.7 + c.x * 3.1);
    float jz = c.y + 0.3 * cos(t * 0.5 + c.y * 7.3);
    vec2 d = p - vec2(jx, jz);
    // 沿波包方向的距离
    float dist = dot(d, vec2(0.8, 0.6));
    // 垂直方向：波峰弯曲
    float perp = dot(d, vec2(-0.6, 0.8));
    float bend = 1.0 * sin(perp * 1.8 + t * 0.8 + c.x) + 0.5 * sin(perp * 3.2 - t * 0.5 + c.y);
    dist += bend;
    // 高斯包络
    float env = exp(-(dist * dist) / (2.0 * WIND_DECAY * WIND_DECAY));
    // 缓慢变化的振幅
    float wobble = 0.6 + 0.4 * sin(t * 1.2 + c.x * 5.7);
    float wave = sin(6.2831 * dist / lam - t * 0.8) * env * wobble;
    // 法线方向：沿波包方向
    return vec2(0.8, 0.6) * wave * amp;
}

void main()
{
    float strength = u._Band.w;
    if (strength <= 0.0)
    {
        discard;
    }

    float hl = u._Band.x;
    float ht = u._Band.y;
    float t = u._Band.z;
    vec2 al = u._Axis.xy;
    vec2 ac = u._Axis.zw;

    vec2 p = bandCoord(vUv, al, ac);
    float a0 = p.x;
    float b0 = p.y;

    // 水面掩码：带内，边缘柔化。它同时是输出的 alpha —— Alpha 混合下，边缘那 0.30 格的
    // 衰减必须真的让像素淡出去，否则整条边是一刀切。quad 的 Pad 把这段衰减罩在 quad 内。
    float mask = softEdge(a0, hl, 0.30) * softEdge(b0, ht, 0.30) * strength;
    if (mask < 0.002)
    {
        discard;
    }

    // ---- 风纹：低频、缓慢弯曲的波包 ----------------------------------------------------------
    vec2 wind = vec2(0.0);
    wind += windNormal(u._WindA, p, al, ac, t);
    wind += windNormal(u._WindB, p, al, ac, t);
    wind += windNormal(u._WindC, p, al, ac, t);
    wind += windNormal(u._WindD, p, al, ac, t);
    wind += windNormal(u._WindE, p, al, ac, t);

    // 风纹弱；落点水花由 MX_QH_RainWallSplash 的合批网格画在本层之上
    vec2 normal = wind * 0.3 * mask;

    // ---- 折射 -----------------------------------------------------------------------------------
    vec2 grabUv = gl_FragCoord.xy / vec2(textureSize(_GrabDistortionBackground, 0));
    vec2 offset = normal * u._DistortScale.xy * u._DistortScale.z;
    vec3 bg = texture(_GrabDistortionBackground, grabUv - offset).rgb;

    // ---- 染色：轻微冷色 + 边缘微光 ---------------------------------------------------------------
    float absorb = TINT_STRENGTH * mask;
    bg *= (1.0 - absorb);
    bg += TINT_COLOR * (TINT_STRENGTH * mask * 0.5);

    // 水面边缘的一点点反光
    float edge = 1.0 - smoothstep(0.0, 0.30, abs(max(abs(a0) - hl, abs(b0) - ht)));
    bg += vec3(0.55, 0.80, 0.92) * (edge * EDGE_GLOW * mask);

    outColor = vec4(bg, u._Color.a * mask);
}
