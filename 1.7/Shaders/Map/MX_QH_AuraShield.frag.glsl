#version 450

layout(location = 0) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;              // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _AdditiveGlowColor;  // @color @default = vec4(1.0, 0.45, 0.43, 1.0)
    vec4 _DissolveEdgeColor;  // @color @default = vec4(1.0, 0.75, 0.9, 1.0)
    float _BaseLayerAlpha;    // @default = 1.0
    float _AdditiveGlowAlpha; // @default = 1.0
    float _AdditiveGlowBreathAmplitude; // @default = 0.9
    float _AdditiveGlowBreathSpeed;     // @default = 0.21
    float _AdditiveGlowMinMask; // @default = 0.0
    float _Dissolve;          // @default = 0.0
    float _DissolveEdgeWidth; // @default = 0.08
    float _DissolveEdgeIntensity; // @default = 2.0
    float _DissolveNoiseScale;    // @default = 1.0
    float _EffectTime;        // @default = 0.0
    float _GameSeconds;       // global, set by TickManager
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
// Tileable grayscale fBm noise, histogram-equalized so dissolved area grows linearly with _Dissolve.
// White default: an unbound slot reads 1.0 everywhere and nothing dissolves.
layout(set = 2, binding = 2) uniform sampler2D _DissolveNoise; // @default = white

// Bone transforms read by the vertex shader for GPU skinning. Waylith parses the material
// layout from the fragment shader only, so the slot must be declared here to be registered.
layout(set = 2, binding = 3) readonly buffer _BoneTransforms
{
    vec4 data[];
} uBones;

void main()
{
    vec4 tex = texture(_MainTex, vUv);
    float alpha = tex.a * u._Color.a * u._BaseLayerAlpha;

    float breathPhase = 0.5 - 0.5 * cos(u._GameSeconds * u._AdditiveGlowBreathSpeed * 6.2831853);
    float breath = mix(1.0 - u._AdditiveGlowBreathAmplitude, 1.0 + u._AdditiveGlowBreathAmplitude, breathPhase);
    vec3 col = tex.rgb
        + u._AdditiveGlowColor.rgb * (u._AdditiveGlowAlpha * breath * max(tex.a - u._AdditiveGlowMinMask, 0.0));

    if (u._Dissolve > 0.0001)
    {
        float noise = texture(_DissolveNoise, vUv * u._DissolveNoiseScale).r;
        float width = max(u._DissolveEdgeWidth, 0.0001);
        // Threshold sweeps from -width to 1: at _Dissolve = 0 nothing is cut and no edge
        // shows; at _Dissolve = 1 every texel is below the threshold and discarded.
        float threshold = mix(-width, 1.001, u._Dissolve);
        if (noise < threshold)
        {
            discard;
        }
        // Glowing rim on the surviving side of the burn line, limited to the petal body.
        float edge = 1.0 - smoothstep(threshold, threshold + width, noise);
        col += u._DissolveEdgeColor.rgb * (edge * u._DissolveEdgeIntensity * tex.a);
    }

    outColor = vec4(col * u._Color.rgb, alpha);
}
