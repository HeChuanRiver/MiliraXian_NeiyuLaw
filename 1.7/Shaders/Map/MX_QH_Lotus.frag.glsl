#version 450

// Zantetsuken: Guren's lotus, computed per pixel. vUv is the map offset from the
// lotus centre in cells (x east, y north on screen); the ground is squashed north-south by the
// depth factor, so a circle on the ground is an ellipse on screen. The light bars (curtain) are
// copies of the rim stacked up the screen, fading with height. Additive blend.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // x: brightness, y: scale of the flower, z: curtain height (screen cells), w: radius (cells)
    vec4 _Lotus;     // @default = vec4(0.45, 1.0, 0.0, 3.0)
    // x: depth factor (north-south squash), y: fill strength, z: rim strength, w: white share
    vec4 _Lotus2;    // @default = vec4(0.55, 0.07, 0.9, 0.25)
    vec4 _Glow;      // @color @default = vec4(1.0, 0.62, 0.8, 1.0)
    vec4 _White;     // @color @default = vec4(1.0, 0.95, 0.98, 1.0)
    vec4 _Fill;      // @color @default = vec4(1.0, 0.36, 0.52, 1.0)
    // Set by ScreenBloom for its own submission only.
    vec4 _BloomTint; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _BloomIntensity; // @default = 1.0
    // Width of the petal rims, cells.
    float _RimWidth; // @default = 0.07
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const float PI = 3.14159265;
const int CURTAIN_STEPS = 24;

float sm(float e0, float e1, float x)
{
    float t = clamp((x - e0) / (e1 - e0), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

// One ring of n petals: rim line, fill inside, centre vein.
void petals(vec2 g, float n, float R, float phase, float wf, out float rim, out float fill, out float vein)
{
    float d = length(g);
    float th = atan(g.y, g.x);
    float span = 2.0 * PI / n;
    float a = mod(th - phase + span * 0.5, span) - span * 0.5;
    float lat = abs(a) * d;
    float uu = clamp(d / R, 0.0, 1.0);
    float w = R * wf * pow(max(sin(PI * uu), 0.0), 0.75);
    float inside = d < R ? 1.0 : 0.0;
    float x = (w - lat) / max(u._RimWidth, 0.005);
    rim = exp(-x * x) * inside;
    fill = sm(-0.02, 0.06, w - lat) * inside;
    float v = lat / max(u._RimWidth * 0.55, 0.004);
    vein = exp(-v * v) * inside * sm(0.15, 0.5, uu) * (1.0 - uu);
}

// 8 outer petals and 8 inner ones half a petal off, no seed pod.
void lotus(vec2 g, float R, out float rim, out float fill)
{
    float r1, f1, v1, r2, f2, v2;
    petals(g, 8.0, R, 0.0, 0.24, r1, f1, v1);
    petals(g, 8.0, R * 0.78, PI / 8.0, 0.30, r2, f2, v2);
    float outer = 1.0 - sm(R * 0.97, R * 1.03, length(g));
    rim = clamp(max(r1, r2) + 0.35 * (v1 + v2), 0.0, 1.0);
    fill = max(f1 * 0.6, f2) * outer;
}

float rimAt(vec2 m, float R, float depth)
{
    float r, f;
    lotus(vec2(m.x, m.y / depth), R, r, f);
    return r;
}

void main()
{
    float bright = u._Lotus.x;
    float R = u._Lotus.w * u._Lotus.y;
    float top = u._Lotus.z;
    float depth = max(u._Lotus2.x, 0.05);
    vec2 m = vUv;

    float rim, fill;
    lotus(vec2(m.x, m.y / depth), R, rim, fill);

    float curtain = 0.0;
    if (top > 0.02)
    {
        // Copies of the rim stacked up the screen: the point here shows the rim from s cells below.
        float sum = 0.0;
        for (int i = 1; i <= CURTAIN_STEPS; i++)
        {
            float s = top * (float(i) / float(CURTAIN_STEPS));
            float f = pow(1.0 - s / top, 1.4);
            sum += rimAt(m - vec2(0.0, s), R, depth) * f;
        }
        // Normalised to the step count.
        curtain = clamp(sum * 0.18 * (top * 24.0 / float(CURTAIN_STEPS)), 0.0, 1.0);
    }

    float glow = rim * bright * u._Lotus2.z + curtain * bright;
    float white = rim * bright * u._Lotus2.w;
    vec3 col = u._Glow.rgb * glow + u._White.rgb * white + u._Fill.rgb * fill * u._Lotus2.y * bright;
    col *= u._Color.rgb * u._Color.a * vColor.rgb * vColor.a * u._BloomTint.rgb * u._BloomIntensity;
    outColor = vec4(max(col, vec3(0.0)), 1.0);
}
