#version 450

// 荷雨：落点水花 —— 离散元素，走几何合批（和雨丝同构）。
//
// 每个落点一个小 quad（约 300 个，一墙一次 draw）：
//   vUv    = 相对落点中心的格偏移（东、北），quad 半边长 SPLASH_QUAD_HALF 格
//   vColor = (下落相位 0..1, 归一化落速 0..1, 出环标志 1/0, 1)
// 位置、相位、落速与 RainWallVfx.AddStrands 消费同一条随机流，所以这朵水花正好在那根雨丝
// 的脚下、以它的落体周期为节拍：cycle = 0 是落地瞬间，cycle = 1 时同一根的下一滴落地。
//
// 主人的比例：慢速的一半（speed < 中位数）只闪泡沫水花；快速的一半同时推开一圈涟漪。
// 即 100% 水花 + 50% 涟漪。
//
// 采样沿用原版抓屏点（queue 3151）的 DistortionBackground，画在基础水面之上、荷花之下；
// 混合是 Alpha：环/泡沫有多强，就把多少「被这朵水花扭曲的背景」盖上去。

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;        // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _BloomTint;    // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // x：半长，y：半厚（未用），z：秒，w：强度 0..1
    vec4 _Band;         // @default = vec4(7.5, 1.5, 0.0, 1.0)
    // xy：屏幕 UV 每格（x 正、y 负），z：扭曲增益，w：差分步长
    vec4 _DistortScale; // @default = vec4(0.0, 0.0, 1.5, 0.05)
    float _BloomIntensity; // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white
layout(set = 2, binding = 2) uniform sampler2D _GrabDistortionBackground;

// ---- 照抄预览（.cache/rain_proto/rain_wall_preview.py 的 _splash_normals / water_distort）-------
const float SHEET_H = 2.20;        // 雨帘高（格），与 RainWallVfx.SheetHeight 相同
const float SPEED_LO = 1.70;       // RAIN_SPEED
const float SPEED_HI = 2.60;
const float RIPPLE_R0 = 0.06;      // 涟漪起半径（格）
const float RIPPLE_R1 = 0.35;      // 涟漪终半径（格）：预览的 0.06 + 0.29·t
const float RIPPLE_BAND = 0.10;    // 环厚占半径比例
const float RIPPLE_AMP = 0.35;     // 涟漪法线强度（柔和）
const float FOAM_WINDOW = 0.20;    // 泡沫闪光只占落地周期的前 20%
const float FOAM_RADIUS = 0.04;    // 泡沫闪光的半径（格）
const float FOAM_AMP = 0.25;       // 泡沫闪光的强度
const vec3 SKY = vec3(0.90, 0.95, 1.00);

void main()
{
    float strength = u._Band.w;
    if (strength <= 0.0)
    {
        discard;
    }
    float t = u._Band.z;

    // 这朵水花对应的那根雨丝：相位、落速、是否出环
    float phase = vColor.r;
    float speed = mix(SPEED_LO, SPEED_HI, vColor.g);
    bool rings = vColor.b > 0.5;

    // 与雨丝同步的落地周期
    float cycle = fract(t * speed / SHEET_H + phase);
    float fade = (1.0 - cycle) * (1.0 - cycle);
    float r = mix(RIPPLE_R0, RIPPLE_R1, cycle);

    vec2 d = vUv;
    float dist = length(d);

    // 涟漪环：柔和的单环法线，只有快速的一半出环
    vec2 normal = vec2(0.0);
    float ring = 0.0;
    if (rings && dist > 1e-4)
    {
        float w = max(0.015, r * RIPPLE_BAND);
        ring = exp(-pow((dist - r) / w, 2.0)) * fade * RIPPLE_AMP;
        normal = (d / dist) * ring;
    }

    // 泡沫闪光：落地瞬间中心的一小团无方向亮色，随周期衰减
    float glowFade = max(0.0, 1.0 - cycle / FOAM_WINDOW);
    float foam = exp(-pow(dist / FOAM_RADIUS, 2.0)) * glowFade * fade * FOAM_AMP;

    // 覆盖度：环或泡沫有多强，就盖多少被扭曲的背景上去
    float cover = clamp(ring * 2.0 + foam * 2.0, 0.0, 1.0) * strength;
    if (cover < 0.004)
    {
        discard;
    }

    // 折射：按环的法线偏移抓屏采样
    vec2 grabUv = gl_FragCoord.xy / vec2(textureSize(_GrabDistortionBackground, 0));
    vec2 offset = normal * u._DistortScale.xy * u._DistortScale.z;
    vec3 bg = texture(_GrabDistortionBackground, grabUv - offset).rgb;

    // 泡沫混向天光
    bg = bg * (1.0 - foam * 0.5) + SKY * foam;

    outColor = vec4(bg * u._Color.rgb * vColor.a, cover * u._Color.a);
}
