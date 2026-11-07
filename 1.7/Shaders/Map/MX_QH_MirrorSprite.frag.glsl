#version 450

// Mirror Flower, Water Moon: a snapshot of Qinghe (the afterimage capture) seen through disturbed
// water. The picture is displaced per pixel by a smooth flowing field (no hard strips): sideways
// waves along the rows, a little vertical ripple, and soft noise that tears it apart as _Wave.y
// grows. A pink sheen and a few bright crests ride on the waves. Vertex alpha fades the reflection
// away from her feet.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;    // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // Like vanilla shaders (and the blade body): mesh v grows toward the top of the picture and
    // the engine's flipped _MainTex_ST maps it to texture rows.
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    // rgb: sheen colour; a: how far the picture is mixed toward it.
    vec4 _Sheen;    // @default = vec4(1.0, 0.67, 0.82, 0.0)
    // x: time (s), y: disturbance 0..1, z: seed, w: wave scale (1 = body, larger = calmer reflection)
    vec4 _Wave;     // @default = vec4(0.0, 0.0, 0.0, 1.0)
    vec4 _BloomTint; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _BloomIntensity; // @default = 1.0
    // 1 for the bloom submission: only the bright crests glow, not the picture.
    float _GlowOnly; // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

float hash(vec2 p)
{
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float vnoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 w = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), w.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), w.x), w.y) * 2.0 - 1.0;
}

void main()
{
    float t = u._Wave.x;
    float k = u._Wave.y;
    float seed = u._Wave.z;
    // v from the bottom of the picture; rows in preview pixels (the picture is 4 cells, 72 px per cell).
    float rowPx = vUv.y * 288.0;
    // The preview's two row waves, now continuous in v, plus a slow noise instead of per-strip jitter.
    float wave = sin(rowPx * 0.11 / u._Wave.w + t * 40.0) * 9.0 + sin(rowPx * 0.037 / u._Wave.w - t * 25.0) * 14.0;
    float tear = vnoise(vec2(vUv.y * 22.0 + seed, t * 3.0 + seed * 0.37)) * 22.0;
    float dxPx = k * (wave + tear);
    // A little vertical ripple so the picture wobbles rather than slides.
    float dyPx = k * 4.0 * sin(vUv.x * 40.0 + t * 30.0 + seed);
    vec2 uv = vUv + vec2(dxPx, dyPx) / 288.0;
    vec4 tex = texture(_MainTex, uv * u._MainTex_ST.zw + u._MainTex_ST.xy);
    float a = clamp(tex.a * vColor.a * u._Color.a, 0.0, 1.0);
    if (a < 0.002)
    {
        discard;
    }
    // Bright crests where the waves bend the picture the most.
    float crest = pow(max(0.0, sin(rowPx * 0.11 / u._Wave.w + t * 40.0)), 12.0) * k;
    vec3 col = mix(tex.rgb, u._Sheen.rgb, u._Sheen.a) + u._Sheen.rgb * crest * 0.6;
    col *= u._Color.rgb;
    if (u._GlowOnly > 0.5)
    {
        // The halo takes the sheen and the crests only.
        col = u._Sheen.rgb * (u._Sheen.a * 0.5 + crest);
    }
    col *= u._BloomTint.rgb * u._BloomIntensity;
    // Premultiplied (blend One, OneMinusSrcAlpha).
    outColor = vec4(col * a, a);
}
