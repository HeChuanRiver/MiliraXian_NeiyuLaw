#version 450

// 荷雨：地面层 —— 水痕压暗、冷色、落水的涟漪、场地边界线。
//
// 混合是预乘的（One / OneMinusSrcAlpha），这不是随便选的：输出的 alpha 就是「这格地面被压暗了
// 多少」，输出的 rgb 是加在被压暗地面上的光。于是
//     dst_out = rgb + dst_in * (1 - alpha)
// 一步就复现了预览的两步操作：
//     canvas *= (1 - STAIN_DARKEN * mask)；canvas += 各层光
// 加法混合永远做不到压暗 —— 之前地面是一片空白，原因就在这里。
//
// 投影与预览一致：像素的地面坐标就是 uv（相对墙心的格偏移，x 东、y 北）。
// 涟漪与边界都在带子自己的（along, across）坐标里算，所以任意瞄准角度都成立。

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _BloomTint; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // 边界线的颜色（夏季色）。
    vec4 _Glow;      // @color @default = vec4(0.60, 0.95, 0.50, 1.0)
    // x, y：沿墙方向（东、北）；z, w：跨墙方向（东、北）
    vec4 _Axis;      // @default = vec4(0.0, 1.0, 1.0, 0.0)
    // x：半长，y：半厚，z：秒，w：强度 0..1
    vec4 _Band;      // @default = vec4(7.5, 1.5, 0.0, 1.0)
    // 四朵荷花，每朵 (along, across, 半径, 开花起始秒)。
    vec4 _LotusA;    // @default = vec4(0.0, 0.0, 0.4, 999.0)
    vec4 _LotusB;    // @default = vec4(0.0, 0.0, 0.4, 999.0)
    vec4 _LotusC;    // @default = vec4(0.0, 0.0, 0.4, 999.0)
    vec4 _LotusD;    // @default = vec4(0.0, 0.0, 0.4, 999.0)
    float _BloomIntensity; // @default = 1.0
} u;

// ---- 照抄 .cache/rain_proto/rain_wall_preview.py -------------------------------------------
const float STAIN_DARKEN = 0.34;    // 湿地面比干地面暗这么多
const float STAIN_TINT = 0.10;      // ……而且偏冷一点
const float STAIN_EDGE = 0.30;      // 水痕边缘的柔度（格）
const float BOUNDARY_HALF = 0.040;  // 边界线每侧的半宽（格）：按格定宽，不随镜头缩放变细
const float BOUNDARY_GAIN = 0.22;   // 线是判定的边界，要读得出来；预览的 0.13 在亮地上太暗
const float LOTUS_OPEN = 1.1;        // 开花用多少秒
const float LOTUS_BLOOM = 0.55;      // 花周围多出来的一圈光
const vec3 TINT_COLOR = vec3(0.06, 0.13, 0.16);
const vec3 LOTUS_COLOR = vec3(0.70, 0.95, 0.80);

// 一朵花的光晕：柔和的大晕 + 一小圈紧的白心。光晕在地面层，因为花长在地面上 ——
// 敌人走到花上时，花和它的光应该一起被挡住，而不是光浮在敌人脸上。
vec3 lotusHalo(vec4 lw, vec2 p, vec2 al, vec2 ac, float t)
{
    float open = clamp((t - lw.w) / LOTUS_OPEN, 0.0, 1.0);
    if (open <= 0.0)
    {
        return vec3(0.0);
    }
    vec2 d = p - (al * lw.x + ac * lw.y);
    float q = dot(d, d);
    float rr = max(lw.z, 1e-3);
    float sig = rr * 0.85;
    float tight = rr * 0.32;
    float amp = LOTUS_BLOOM * open;
    return LOTUS_COLOR * (amp * exp(-q / (2.0 * sig * sig)))
        + vec3(1.0) * (amp * 0.5 * exp(-q / (2.0 * tight * tight)));
}

// |v| <= edge 时为 1，到 |v| = edge + soft 降到 0。
float softEdge(float v, float edge, float soft)
{
    float a = abs(v);
    if (soft <= 1e-6)
    {
        return a <= edge ? 1.0 : 0.0;
    }
    return clamp((edge + soft - a) / soft, 0.0, 1.0);
}

void main()
{
    float s = u._Band.w;
    if (s <= 0.0)
    {
        discard;
    }

    float hl = u._Band.x;
    float ht = u._Band.y;
    float t = u._Band.z;
    vec2 al = u._Axis.xy;
    vec2 ac = u._Axis.zw;

    // 带内坐标：沿墙 a0、跨墙 b0。
    float a0 = dot(vUv, al);
    float b0 = dot(vUv, ac);
    float pxw = max(fwidth(vUv.x), 1e-5);

    // ---------------------------------------------------------------- 水痕
    float mask = softEdge(a0, hl, STAIN_EDGE) * softEdge(b0, ht, STAIN_EDGE) * s;
    float darken = STAIN_DARKEN * mask;
    vec3 light = TINT_COLOR * (mask * STAIN_TINT);

    // ---------------------------------------------------------------- 场地边界
    // 真正的 15x3 判定的边。出现时从瞄准点向外张开（预览的 BAND_GROW = 0.35 秒），之后常驻，
    // 和雨一起淡出 —— 张开的过程与淡出是两件事，不能让边界在淡出时又缩回去。
    //
    // 粗细用文件顶部那个按「格」定宽的 BOUNDARY_HALF，不能在这里再声明一个同名的局部常量 ——
    // 那会遮蔽全局值，改上面那行等于没改（上一次就是这样：全局已经调成 0.040，实机仍是细的）。
    // 只保留一个像素下限，免得放大到一格几十像素时线细到亚像素闪烁。
    float grow = max(BOUNDARY_HALF, 1.5 * pxw);
    float edge = 1.0 - smoothstep(0.0, grow, abs(max(abs(a0) - hl, abs(b0) - ht)));
    // 张开期间，超出的那段还没亮起来；张开结束后就是完整的十五格，两端不能再削。
    float g = clamp(t / 0.35, 0.0, 1.0);
    if (g < 1.0)
    {
        edge *= clamp((hl * g - abs(a0)) / 0.75, 0.0, 1.0);
    }
    light += u._Glow.rgb * (edge * s * BOUNDARY_GAIN);

    // ---------------------------------------------------------------- 荷花光晕
    // 和花本身同一层、同一位置：花是被这圈光从内部照亮的。
    light += lotusHalo(u._LotusA, vUv, al, ac, t);
    light += lotusHalo(u._LotusB, vUv, al, ac, t);
    light += lotusHalo(u._LotusC, vUv, al, ac, t);
    light += lotusHalo(u._LotusD, vUv, al, ac, t);

    // 预乘：(rgb, alpha)。alpha 压暗，rgb 加光。
    vec3 col = light * u._Color.rgb * u._Color.a * vColor.rgb * vColor.a;
    outColor = vec4(max(col, vec3(0.0)), clamp(darken, 0.0, 0.95));
}
