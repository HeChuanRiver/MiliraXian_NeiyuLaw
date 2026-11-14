#version 450

// 春泉涌流：边界光幕。
//
// 边界不是从贴图里「检测」出来的，而是直接对「内格 / 外格之间的格边线段」算欧氏距离：
//   - 线段内部，等距线是直线  ⇒ 格子边界的直线段严格笔直
//   - 顶点处，等距线是以顶点为圆心的圆弧 ⇒ 拐角自然圆滑，无 90° 叠、无 270° 缺角
//   line = exp(-(d/σ)^2) 就是「零厚度边界 + 高斯模糊」的闭式解。
// 与 .cache/spring_proto/spring_flow_preview.py 同一套公式、同一组常量。

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
    // x, z: 场地中心（地图格坐标）
    vec4 _FieldCenter; // @default = vec4(0.0, 0.0, 0.0, 0.0)
    float _FieldSize;  // mask 边长（格）= 2R + 3
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const int CURTAIN_STEPS = 24;

// 缓存：以当前像素所在格为原点，列 -2..2、行 -3..3 的「是否在范围内」
float gCell[7][5];
int gSize;

float cellRaw(ivec2 c)
{
    if (c.x < 0 || c.y < 0 || c.x >= gSize || c.y >= gSize)
    {
        return 0.0;
    }
    return texelFetch(_MainTex, c, 0).a;
}

// 局部坐标 p（格内 [0,1)）到水平线段（z = zs，x ∈ [x0,x1]）的距离
float segH(vec2 p, float x0, float x1, float zs)
{
    float du = p.x - clamp(p.x, x0, x1);
    float dv = p.y - zs;
    return sqrt(du * du + dv * dv);
}

// 垂直线段（x = xs，z ∈ [z0,z1]）
float segV(vec2 p, float xs, float z0, float z1)
{
    float dv = p.y - clamp(p.y, z0, z1);
    float du = p.x - xs;
    return sqrt(du * du + dv * dv);
}

// rowShift：地面上为 0；被光幕下移一格时为 -1
float lineAt(int rowShift, vec2 p, float sigma)
{
    float d = 1.0e9;
    for (int dz = -1; dz <= 1; dz++)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            int r = dz + 3 + rowShift;
            int c = dx + 2;
            float here = gCell[r][c];
            if (here != gCell[r + 1][c])
            {
                d = min(d, segH(p, float(dx), float(dx) + 1.0, float(dz) + 1.0));
            }
            if (here != gCell[r - 1][c])
            {
                d = min(d, segH(p, float(dx), float(dx) + 1.0, float(dz)));
            }
            if (here != gCell[r][c + 1])
            {
                d = min(d, segV(p, float(dx) + 1.0, float(dz), float(dz) + 1.0));
            }
            if (here != gCell[r][c - 1])
            {
                d = min(d, segV(p, float(dx), float(dz), float(dz) + 1.0));
            }
        }
    }
    float t = d / max(sigma, 0.001);
    return exp(-t * t);
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

    gSize = int(u._FieldSize);
    float size = float(gSize);

    // quad：x ∈ [中心 - size/2, 中心 + size/2]，z ∈ [同上] + 向上 height
    vec2 origin = vec2(u._FieldCenter.x - size * 0.5, u._FieldCenter.y - size * 0.5);
    float worldX = origin.x + vUv.x * size;
    float worldZ = origin.y + vUv.y * (size + height);

    // 地面像素所在的格与格内局部坐标
    vec2 q = vec2(worldX, worldZ) - origin;
    ivec2 c0 = ivec2(floor(q));
    vec2 local = q - vec2(c0);

    // 填充缓存（一次），光幕各步都复用
    for (int r = 0; r < 7; r++)
    {
        for (int c = 0; c < 5; c++)
        {
            gCell[r][c] = cellRaw(ivec2(c0.x + c - 2, c0.y + r - 3));
        }
    }

    // 地面那条线
    float line = lineAt(0, local, sigma);

    // 光幕：把地面线沿 +z 堆叠（屏幕上就是往上）
    float curtain = 0.0;
    for (int i = 0; i < CURTAIN_STEPS; i++)
    {
        float h = height * (float(i) + 0.5) / float(CURTAIN_STEPS);
        float v = local.y - h;
        int rowShift = 0;
        if (v < 0.0)
        {
            v += 1.0;
            rowShift = -1;
        }
        curtain += lineAt(rowShift, vec2(local.x, v), sigma)
                  * pow(1.0 - h / height, 1.4);
    }
    curtain *= (height / float(CURTAIN_STEPS)) * gain;

    float boundary = line + curtain;

    vec3 col = u._Glow.rgb * boundary;
    col *= u._Color.rgb * u._Color.a * vColor.rgb * vColor.a * u._BloomTint.rgb * u._BloomIntensity * strength;
    outColor = vec4(max(col, vec3(0.0)), 1.0);
}
