#version 450

// 春泉边界（实例化版）：数学与 MX_QH_SpringFlowBoundary.frag.glsl 完全一致，
// 只是把逐实例参数（场心、边长、半径、强度）从 uniform 换成了顶点插值——因为它们本来就随实例不同。
//
// 这使得同一笔的 N 个春泉可以一次提交：材质只剩全实例共享的常量。

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec3 vField;   // (X, Z, strength)
layout(location = 2) in float vRadius;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _Glow;      // @color @default = vec4(1.0, 0.62, 0.80, 1.0)
    // x: 光幕高度（格）, y: 未用, z: 线宽 σ（格）, w: 光幕增益
    vec4 _Params;    // @default = vec4(0.4, 1.0, 0.024, 4.23)
    vec4 _BloomTint; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _BloomIntensity; // @default = 1.0
} u;

// 与 vert 的声明一致；材质布局从 frag 解析，所以这里必须写。
layout(set = 2, binding = 3) readonly buffer _InstanceData
{
    vec4 data[];
} uInst;

const int CURTAIN_STEPS = 24;
const int LEVELS = 5;

float gHz[LEVELS];
float gHdu[LEVELS];
float gVdu[LEVELS];
vec2 gVspan[LEVELS];
float gR2;

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

float lineFromSquared(float d2, float sigma)
{
    float s2 = max(sigma * sigma, 1.0e-6);
    return exp(-d2 / s2);
}

void main()
{
    float strength = vField.z;
    if (strength <= 0.0)
    {
        discard;
    }

    float height = u._Params.x;
    float sigma = u._Params.z;
    float gain = u._Params.w;

    float X = vField.x;
    float Z = vField.y;
    gR2 = vRadius * vRadius;

    buildLevels(X, Z);
    float ground2 = dist2At(Z);

    float horizon = height + 4.0 * sigma;
    if (ground2 > horizon * horizon)
    {
        discard;
    }

    float line = lineFromSquared(ground2, sigma);

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
