#version 450

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // Halo-only colour, set by ScreenBloom for its offscreen submission. vec4 members first:
    // the engine lays uniforms out the std140 way.
    vec4 _BloomTint;      // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _Brightness;    // @default = 1.0
    float _BloomIntensity; // @default = 1.0
    // Vertex alpha is stored divided by this, so opacities above 1 survive the 8-bit vertex colour.
    float _AlphaScale;    // @default = 1.0
    // 1 for the additive layers: alpha above 1 then adds more light instead of being clipped.
    float _Additive;      // @default = 0.0
    // Upper limit of the final opacity (1 = clamp like the offline preview; large = no clamp).
    float _AlphaMax;      // @default = 1000000.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

void main()
{
    vec4 tex = texture(_MainTex, vUv);
    float a = min(tex.a * vColor.a * u._AlphaScale * u._Color.a, u._AlphaMax);
    vec3 col = tex.rgb * vColor.rgb * u._Color.rgb * u._Brightness * u._BloomTint.rgb * u._BloomIntensity;
    if (u._Additive > 0.5)
    {
        // Additive blend multiplies by source alpha: pre-multiply here and pass alpha 1.
        outColor = vec4(col * max(a, 0.0), 1.0);
    }
    else
    {
        // Premultiplied (blend One, OneMinusSrcAlpha): the colour target is 8-bit, so a straight
        // colour above 1 (brightness 1.67) would be clipped to 1 before blending; col * a keeps it.
        float aa = clamp(a, 0.0, 1.0);
        outColor = vec4(col * aa, aa);
    }
}
