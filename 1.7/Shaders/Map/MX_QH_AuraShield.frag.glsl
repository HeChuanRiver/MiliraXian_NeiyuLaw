#version 450

layout(location = 0) in vec2 vUv;
layout(location = 1) in vec2 vRest;
layout(location = 2) in vec2 vPos;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;              // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _AdditiveGlowColor;  // @color @default = vec4(1.0, 0.45, 0.43, 1.0)
    vec4 _DissolveEdgeColor;  // @color @default = vec4(1.0, 0.75, 0.9, 1.0)
    // Iridescence: cosine palette a + b * cos(2pi * (c * phase + d)), rgb used.
    vec4 _IridColorA;         // @color @default = vec4(0.62, 0.50, 0.62, 1.0)
    vec4 _IridColorB;         // @color @default = vec4(0.45, 0.45, 0.45, 1.0)
    vec4 _IridColorC;         // @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _IridColorD;         // @default = vec4(0.0, 0.25, 0.5, 1.0)
    vec4 _BandColor;          // @color @default = vec4(1.0, 0.92, 0.95, 1.0)
    vec4 _RimColor;           // @color @default = vec4(1.0, 0.85, 0.95, 1.0)
    // Radiation center of the petals in UV space (xy); flow and hue start here.
    vec4 _EffectOrigin;       // @default = vec4(0.5, 0.647, 0.0, 0.0)
    // Halo-only colour: ScreenBloom sets these for its offscreen submission and restores them
    // before the body is drawn, so the shield itself is unaffected. Keep vec4 members above the
    // scalars: the engine derives uniform offsets the std140 way, while GLSL blocks default to
    // the shared layout, and every other shader here follows that order.
    vec4 _BloomTint;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _BaseLayerAlpha;    // @default = 1.0
    float _AdditiveGlowAlpha; // @default = 1.0
    float _AdditiveGlowBreathAmplitude; // @default = 0.9
    float _AdditiveGlowBreathSpeed;     // @default = 0.21
    float _AdditiveGlowMinMask; // @default = 0.0
    float _IridIntensity;     // @default = 0.0
    float _IridLumaMask;      // @default = 0.5
    float _IridLateralFreq;   // @default = 4.0
    float _IridRadialFreq;    // @default = 3.0
    float _IridFlowSpeed;     // @default = 0.35
    float _BandIntensity;     // @default = 0.0
    float _BandRadialFreq;    // @default = 2.5
    float _BandSpeed;         // @default = 0.35
    float _BandWidth;         // @default = 0.12
    float _RimIntensity;      // @default = 0.0
    float _RimWidth;          // @default = 2.0
    float _StrainGain;        // @default = 0.0
    float _StrainThin;        // @default = 0.0
    float _BreakProgress;     // @default = 0.0
    float _BreakGlow;         // @default = 0.0
    float _BreakPhaseKick;    // @default = 0.0
    float _Dissolve;          // @default = 0.0
    float _DissolveEdgeWidth; // @default = 0.08
    float _DissolveEdgeIntensity; // @default = 2.0
    float _DissolveNoiseScale;    // @default = 1.0
    float _EffectTime;        // @default = 0.0
    float _GameSeconds;       // global, set by TickManager
    float _BloomIntensity;    // @default = 1.0
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

const float TWO_PI = 6.2831853;
const float TEXTURE_SIZE = 512.0;

void main()
{
    vec4 tex = texture(_MainTex, vUv);
    float alpha = tex.a * u._Color.a * u._BaseLayerAlpha;
    float t = u._GameSeconds;

    float breathPhase = 0.5 - 0.5 * cos(t * u._AdditiveGlowBreathSpeed * TWO_PI);
    float breath = mix(1.0 - u._AdditiveGlowBreathAmplitude, 1.0 + u._AdditiveGlowBreathAmplitude, breathPhase);
    vec3 col = tex.rgb
        + u._AdditiveGlowColor.rgb * (u._AdditiveGlowAlpha * breath * max(tex.a - u._AdditiveGlowMinMask, 0.0));

    // Stretch of the skinned surface at this pixel, positive = stretched, negative = compressed.
    // One pixel covers a fixed area in skinned space but a smaller rest-space area where the
    // cloth is stretched, so log2(skinned area / rest area) is 0 in the rest pose at any zoom.
    // Derivatives are taken before any discard so the 2x2 quad stays valid.
    float strain = 0.0;
    if (u._StrainGain != 0.0 || u._StrainThin != 0.0)
    {
        vec2 posX = dFdx(vPos);
        vec2 posY = dFdy(vPos);
        vec2 restX = dFdx(vRest);
        vec2 restY = dFdy(vRest);
        float skinnedArea = abs(posX.x * posY.y - posX.y * posY.x);
        float restArea = abs(restX.x * restY.y - restX.y * restY.x);
        strain = clamp(log2(max(skinnedArea, 1e-12) / max(restArea, 1e-12)), -1.0, 1.0);
    }

    // Breaking: lights swell around mid-break (peak at 0.5) and the phase is kicked forward.
    float breakBoost = 1.0 + u._BreakGlow * 4.0 * u._BreakProgress * (1.0 - u._BreakProgress);
    float breakKick = u._BreakPhaseKick * u._BreakProgress;

    vec2 fromOrigin = vUv - u._EffectOrigin.xy;
    float radius = length(fromOrigin);

    if (u._IridIntensity > 0.0001)
    {
        float phase = fromOrigin.x * u._IridLateralFreq + radius * u._IridRadialFreq
            - t * u._IridFlowSpeed + breakKick;
        vec3 palette = u._IridColorA.rgb
            + u._IridColorB.rgb * cos(TWO_PI * (u._IridColorC.rgb * phase + u._IridColorD.rgb));
        float luma = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
        col += palette * (u._IridIntensity * breakBoost * tex.a * mix(1.0, luma, u._IridLumaMask));
    }

    if (u._BandIntensity > 0.0001)
    {
        // A band sweeping outward from the origin: rising edge over [0, w], falling over [w, 2w].
        float w = clamp(u._BandWidth, 0.01, 0.5);
        float s = fract(radius * u._BandRadialFreq - t * u._BandSpeed - breakKick);
        float band = smoothstep(0.0, w, s) * (1.0 - smoothstep(w, 2.0 * w, s));
        col += u._BandColor.rgb * (band * u._BandIntensity * breakBoost * tex.a);
    }

    if (u._RimIntensity > 0.0001)
    {
        // Inside-edge mask from four UV-space taps (zoom independent, follows the petal when it deforms).
        float d = u._RimWidth / TEXTURE_SIZE;
        float neighborMin = min(
            min(texture(_MainTex, vUv + vec2(d, 0.0)).a, texture(_MainTex, vUv - vec2(d, 0.0)).a),
            min(texture(_MainTex, vUv + vec2(0.0, d)).a, texture(_MainTex, vUv - vec2(0.0, d)).a));
        float rim = clamp(tex.a - neighborMin, 0.0, 1.0);
        col += u._RimColor.rgb * (rim * u._RimIntensity * breakBoost * tex.a);
    }

    // Stretched cloth reads brighter and thinner, compressed cloth darker and denser.
    col *= max(1.0 + u._StrainGain * strain, 0.0);
    alpha *= clamp(1.0 - u._StrainThin * strain, 0.0, 1.5);

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

    outColor = vec4(col * u._Color.rgb * u._BloomTint.rgb * u._BloomIntensity, alpha);
}
