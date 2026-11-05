#version 450

// Distortion band of the Sakura Dance trail: refracts the scene behind it with a scrolling
// normal map and leaves a faint shadow (the band's dark colour at low opacity).
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // xy: screen UV per world metre (x, z) for the refraction offset.
    vec4 _DistortScale;   // @default = vec4(0.0, 0.0, 0.0, 0.0)
    float _Intensity;     // @default = 0.0
    float _Shade;         // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _NormalTex; // @default = black
layout(set = 2, binding = 3) uniform sampler2D _GrabDistortionBackground;

void main()
{
    float mask = texture(_MainTex, vUv).a * vColor.a;
    if (mask < 0.002)
    {
        discard;
    }
    vec2 n = texture(_NormalTex, vUv).xy * 2.0 - 1.0;
    vec2 grabUv = gl_FragCoord.xy / vec2(textureSize(_GrabDistortionBackground, 0));
    vec2 offset = n * u._DistortScale.xy * (u._Intensity * mask);
    vec3 bg = texture(_GrabDistortionBackground, grabUv + offset).rgb;
    // vColor.r is the band colour (0 = black): the darker it is, the stronger the shadow.
    float shade = clamp(u._Shade * mask * (1.0 - vColor.r), 0.0, 1.0);
    outColor = vec4(bg * (1.0 - shade), clamp(mask * 4.0, 0.0, 1.0) * u._Color.a);
}
