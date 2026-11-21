#version 450

// Mirror Flower, Water Moon: the water disc under Qinghe, computed per pixel (surface, shading,
// sparkle and shatter). vUv is the ground offset from the disc centre in cells (x east, y north); the
// mesh itself is squashed north-south on the map, so a circle here is the ellipse on screen.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;    // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _Pink;     // @default = vec4(1.0, 0.62, 0.8, 1.0)
    vec4 _White;    // @default = vec4(1.0, 0.95, 0.98, 1.0)
    // x: water time (s), y: strength 0..1, z: shatter progress 0..1 (negative = whole water), w: radius (cells)
    vec4 _Water;    // @default = vec4(0.0, 1.0, -1.0, 1.6)
    // x: overall brightness, y: moon brightness, z: share of the pink surface light drawn (the bloom
    // submission keeps only a little of it, so the halo comes from the moon and the glints).
    vec4 _Water2;   // @default = vec4(1.0, 1.0, 1.0, 0.0)
    // Set by ScreenBloom for its own submission only.
    vec4 _BloomTint; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _BloomIntensity; // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const float PI = 3.14159265;
const int SEEDS = 22;

// Smoothstep that also works with e0 > e1 (a falling edge).
float sm(float e0, float e1, float x)
{
    float t = clamp((x - e0) / (e1 - e0), 0.0, 1.0);
    return t * t * (3.0 - 2.0 * t);
}

float hash(vec2 p)
{
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

// Smooth value noise in about -0.25..0.25.
float vnoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 w = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return (mix(mix(a, b, w.x), mix(c, d, w.x), w.y) - 0.5) * 0.5;
}

// NOISE_A (features 2.4 cells, scroll 18, 7 px/s) and NOISE_B (1.55 cells, -11, 13 px/s).
float n1(vec2 g, float t) { return vnoise(g / 2.4 + vec2(0.104, -0.040) * t + vec2(3.1, 7.7)); }
float n2(vec2 g, float t) { return vnoise(g / 1.55 + vec2(-0.099, -0.117) * t + vec2(11.3, 2.9)); }

float wav(vec2 w, float t, float angDeg, float lam, float amp, float sp)
{
    float a = radians(angDeg);
    float k = 2.0 * PI / lam;
    return amp * sin(k * dot(w, vec2(cos(a), sin(a))) - k * sp * t + angDeg);
}

// surface(): eight wind wavelets on noise-bent coordinates, plus two sparse ripples from her feet.
float height(vec2 g, float t, float rings, float R)
{
    float a = n1(g, t);
    vec2 w = g + 0.35 * vec2(a, n2(g, t));
    float h = wav(w, t, 10.0, 0.42, 1.0, 0.30) + wav(w, t, 75.0, 0.31, 0.8, 0.26)
        + wav(w, t, 140.0, 0.37, 0.7, 0.28) + wav(w, t, 200.0, 0.23, 0.55, 0.22)
        + wav(w, t, 260.0, 0.27, 0.5, 0.24) + wav(w, t, 320.0, 0.19, 0.4, 0.20)
        + wav(w, t, 35.0, 0.15, 0.3, 0.17) + wav(w, t, 110.0, 0.13, 0.25, 0.15);
    h *= 0.012;
    if (rings > 0.5)
    {
        float d = length(g) + 0.10 * a;
        for (int i = 0; i < 2; i++)
        {
            float uu = fract(t * 0.28 + float(i) * 0.5);
            float r = 0.2 + uu * R;
            float x = (d - r) / 0.12;
            float env = exp(-x * x) * pow(max(sin(PI * uu), 0.0), 1.5);
            h += 0.016 * env * cos(2.0 * PI * (d - r) / 0.30);
        }
    }
    return h;
}

// The single crest pushed out by the shatter.
float rippleHeight(vec2 g, float r)
{
    float x = (length(g) + 0.08 * n1(g, 0.0) - r) / 0.10;
    return 0.03 * exp(-x * x);
}

// shade(): normal from the slope, a light from the upper left, a narrow highlight.
void lightFrom(float hx, float hz, out float ndl, out float spec, out vec2 nxz)
{
    vec3 n = normalize(vec3(-hx, 0.06, -hz));
    vec3 L = normalize(vec3(-0.45, 0.35, 0.82));
    vec3 H = normalize(L + vec3(0.0, 1.0, 0.0));
    ndl = dot(n, L);
    spec = pow(clamp(dot(n, H), 0.0, 1.0), 40.0);
    nxz = n.xz;
}

void shadeWater(vec2 g, float t, float rings, float R, out float ndl, out float spec, out vec2 nxz)
{
    const float e = 0.007;
    float hx = (height(g + vec2(e, 0.0), t, rings, R) - height(g - vec2(e, 0.0), t, rings, R)) / (2.0 * e);
    float hz = (height(g + vec2(0.0, e), t, rings, R) - height(g - vec2(0.0, e), t, rings, R)) / (2.0 * e);
    lightFrom(hx, hz, ndl, spec, nxz);
}

// sparkle(): two fine noise fields drifting apart, multiplied and sharpened.
float sparkle(vec2 g, float t)
{
    float a = vnoise(g / 0.54 + vec2(0.95, 0.54) * t + vec2(5.0, 1.0)) + 0.5;
    float b = vnoise(g / 0.39 + vec2(-1.03, 1.17) * t + vec2(2.0, 8.0)) + 0.5;
    return pow(max(a * b * 2.2 - 0.35, 0.0), 2.5);
}

// water(): pink light of the surface and white light of the moon, glints and glow.
void water(vec2 g, float t, float s, float rings, float R, out float pinkI, out float whiteI)
{
    float edge = 1.0 - sm(0.6 * R, R, length(g) + 0.15 * n1(g, t));
    float ndl;
    float spec;
    vec2 nxz;
    shadeWater(g, t, rings, R, ndl, spec, nxz);
    float body = max(0.06 + 0.07 * ndl, 0.0) * edge;
    float glint = 0.9 * spec * sparkle(g, t) * edge;
    // The crescent under her feet, seen through the waves: the normal shifts where it is read.
    vec2 m = g + 0.06 * nxz;
    float m1 = length(m);
    float m2 = length(m - vec2(0.26, 0.16));
    float moon = sm(0.84, 0.74, m1) * sm(0.60, 0.70, m2);
    moon *= 0.85 + 0.3 * clamp(ndl, 0.0, 1.0);
    float q = m1 / 0.75;
    float glow = 0.18 * exp(-q * q);
    pinkI = s * (body + glint * 0.35);
    // The moon and its halo carry the moon brightness; the glints keep theirs.
    whiteI = s * ((0.38 * moon + glow) * u._Water2.y + glint * 0.65);
}

vec2 seedAt(int i, float R)
{
    return (vec2(hash(vec2(float(i), 1.7)), hash(vec2(float(i), 9.3))) * 2.0 - 1.0) * R;
}

vec2 warpG(vec2 g)
{
    return g + 0.12 * vec2(n1(g, 0.0), n2(g, 0.0));
}

// Nearest and second nearest Voronoi seed (only seeds inside the disc count).
int nearestSeed(vec2 gw, float R, out float first, out float second)
{
    first = 1e6;
    second = 1e6;
    int best = -1;
    for (int i = 0; i < SEEDS; i++)
    {
        vec2 s = seedAt(i, R);
        if (length(s) >= R)
        {
            continue;
        }
        float d = length(gw - s);
        if (d < first)
        {
            second = first;
            first = d;
            best = i;
        }
        else if (d < second)
        {
            second = d;
        }
    }
    return best;
}

// shatter_water(): pieces drifting out and fading, the cracks flashing, one crest running out.
// pinkShare scales only the dim surface light of the pieces, as the whole water does: the bloom
// submission passes a small share, so the moon, glints, cracks and crest keep their full halo and
// the glow carries on from the stance without a drop.
float shatter(vec2 g, float t, float p, float R, float pinkShare)
{
    float ease = 1.0 - (1.0 - p) * (1.0 - p);
    float fade = pow(1.0 - p, 1.3);
    float outI = 0.0;
    float f;
    float sc;
    for (int i = 0; i < SEEDS; i++)
    {
        vec2 s = seedAt(i, R);
        if (length(s) >= R)
        {
            continue;
        }
        float dist = (0.10 + 0.35 * float((i * 37) % 10) / 10.0) * ease;
        vec2 src = g - normalize(s + vec2(1e-5)) * dist;
        if (nearestSeed(warpG(src), R, f, sc) == i)
        {
            float pw;
            float ww;
            water(src, t, 1.0, 0.0, R, pw, ww);
            outI += (pw * 1.6 * pinkShare + ww * 1.2) * fade;
        }
    }
    nearestSeed(warpG(g), R, f, sc);
    float c = (sc - f) / 0.035;
    outI += exp(-c * c) * (1.0 - sm(0.6 * R, R, length(g))) * 0.9 * exp(-p * 4.0);

    float r = 0.2 + ease * R * 1.1;
    const float e = 0.007;
    float hx = (rippleHeight(g + vec2(e, 0.0), r) - rippleHeight(g - vec2(e, 0.0), r)) / (2.0 * e);
    float hz = (rippleHeight(g + vec2(0.0, e), r) - rippleHeight(g - vec2(0.0, e), r)) / (2.0 * e);
    float ndl;
    float spec;
    vec2 nxz;
    lightFrom(hx, hz, ndl, spec, nxz);
    outI += (1.0 - p) * (0.06 * clamp(ndl, 0.0, 1.0) + 0.5 * spec);
    return outI;
}

void main()
{
    vec2 g = vUv;
    float t = u._Water.x;
    float s = u._Water.y;
    float p = u._Water.z;
    float R = u._Water.w;
    vec3 col;
    if (p < 0.0)
    {
        float pinkI;
        float whiteI;
        water(g, t, s, 1.0, R, pinkI, whiteI);
        col = u._Pink.rgb * pinkI * u._Water2.z + u._White.rgb * whiteI;
    }
    else
    {
        col = u._Pink.rgb * shatter(g, t, p, R, u._Water2.z);
    }
    col *= u._Water2.x * u._Color.a * texture(_MainTex, vec2(0.5)).a * u._BloomTint.rgb * u._BloomIntensity;
    // Additive blend multiplies by source alpha: the colour is the light to add, alpha 1.
    outColor = vec4(max(col, vec3(0.0)), 1.0);
}
