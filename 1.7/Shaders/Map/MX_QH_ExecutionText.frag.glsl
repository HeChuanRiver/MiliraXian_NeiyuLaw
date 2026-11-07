#version 450

// The word shown over an enemy killed by Qinghe's execution: a white glyph texture that dissolves
// through a noise texture, its dissolving edge glowing.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // Halo-only colour, set by ScreenBloom for its offscreen submission. vec4 members first:
    // the engine lays uniforms out the std140 way.
    vec4 _BloomTint;      // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _EdgeColor;      // @color @default = vec4(1.0, 0.6, 0.7, 1.0)
    float _Brightness;    // @default = 1.0
    float _BloomIntensity; // @default = 1.0
    // 0 = whole, 1 = gone: pixels whose noise is below it have dissolved.
    float _Dissolve;      // @default = 0.0
    float _NoiseScale;    // @default = 1.0
    // Noise range above the threshold that glows in the edge colour.
    float _EdgeWidth;     // @default = 0.08
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _NoiseTex; // @default = white

void main()
{
    vec4 tex = texture(_MainTex, vUv);
    float a = tex.a * vColor.a * u._Color.a;
    vec3 col = tex.rgb * vColor.rgb * u._Color.rgb;
    if (u._Dissolve > 0.0)
    {
        float n = texture(_NoiseTex, vUv * u._NoiseScale).r;
        if (n < u._Dissolve)
        {
            discard;
        }
        float edge = 1.0 - smoothstep(u._Dissolve, u._Dissolve + max(u._EdgeWidth, 1e-4), n);
        col = mix(col, u._EdgeColor.rgb * 2.0, edge);
    }
    col *= u._Brightness * u._BloomTint.rgb * u._BloomIntensity;
    // Premultiplied (blend One, OneMinusSrcAlpha), as the Sakura Dance alpha layers.
    float aa = clamp(a, 0.0, 1.0);
    outColor = vec4(col * aa, aa);
}
