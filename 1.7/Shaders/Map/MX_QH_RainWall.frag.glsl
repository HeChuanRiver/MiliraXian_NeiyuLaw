#version 450

// 荷雨：雨幕（站立的水雾），逐像素解析 —— 一面墙一个 quad、一次 DrawMesh。
//
// 雨丝不在这里。雨丝是离散元素（一条一条有位置、有速度的线段），由 RainWallVfx 在 CPU 上
// 按预览的 _strands() 逐条模拟、AddStreak 合批画出；shader 只负责连续场。
// 之前的版本在这里按屏幕列反解 chordZ、每列哈希放水滴 —— 那是对预览的近似，列量化、增益
// 拟合（RainGain=0.16）、南北向退化都是它带来的。离散的东西就该用离散的画法。
//
// 投影与预览一致（.cache/rain_proto/rain_wall_preview.py）：
//   屏幕 x = 地图 x，屏幕向上 = 地图 z，高度 h 直接加在 z 上。
//   所以像素 (X, Y) 的视线是「地图上 x = X 这一列，地面 z = Y - h，h 从 0 到 H」。

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _BloomTint; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // x, y：沿墙方向（东、北）；z, w：跨墙方向（东、北）
    vec4 _Axis;      // @default = vec4(0.0, 1.0, 1.0, 0.0)
    // x：半长，y：半厚，z：秒，w：强度 0..1
    vec4 _Band;      // @default = vec4(7.5, 1.5, 0.0, 1.0)
    float _BloomIntensity; // @default = 1.0
} u;

// ---- 照抄预览的常量 ------------------------------------------------------------------------
const float CURTAIN_H = 2.20;        // 雨幕比地面高出多少（格）
const float CURTAIN_FADE_POW = 2.4;  // 亮度随高度衰减
const float CURTAIN_EDGE_SOFT = 0.60; // 两端的柔度（格）
const float MIST_GAIN = 0.055;       // 雨幕增益（预览的 CURTAIN_GAIN）
const vec3 MIST_COLOR = vec3(0.62, 0.86, 0.78); // 水色，不是一块板
const int MIST_STEPS = 14;           // 预览 curtain_layer 的高度采样步数

// |v| <= edge 时为 1，到 |v| = edge + soft 降到 0。
float softEdge(float v, float edge, float soft)
{
    float a = abs(v);
    if (soft <= 1e-6)
    {
        return step(a, edge);
    }
    return clamp((edge + soft - a) / soft, 0.0, 1.0);
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
    vec2 al = u._Axis.xy;
    vec2 ac = u._Axis.zw;

    float X = vUv.x;
    float Y = vUv.y;

    // ---------------------------------------------------------------- 雨幕
    // 视线沿高度走：高度 h 处看到的是地面点 (X, Y - h)。把它落在墙带里的部分按高度加权累加，
    // 就是预览 curtain_layer 的积分（14 步）。
    float mist = 0.0;
    for (int i = 0; i < MIST_STEPS; i++)
    {
        float h = CURTAIN_H * (float(i) + 0.5) / float(MIST_STEPS);
        vec2 p = vec2(X, Y - h);
        float m = softEdge(dot(p, al), hl, CURTAIN_EDGE_SOFT) * softEdge(dot(p, ac), ht, 0.0);
        mist += m * pow(1.0 - h / CURTAIN_H, CURTAIN_FADE_POW);
    }
    vec3 col = MIST_COLOR * (mist / float(MIST_STEPS) * MIST_GAIN);

    col *= strength;
    col *= u._Color.rgb * u._Color.a * vColor.rgb * vColor.a * u._BloomTint.rgb * u._BloomIntensity;
    outColor = vec4(max(col, vec3(0.0)), 1.0);
}
