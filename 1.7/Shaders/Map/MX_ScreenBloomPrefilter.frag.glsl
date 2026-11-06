#version 450

// Base level of the bloom pyramid, at the scene image's own resolution so the final halo is as
// sharp as the scene image. Waylith PostEffectPass (fullscreen triangle, set 0): binding 0 is the
// bloom group's scene image, binding 1 the per-group threshold.
layout(location = 0) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _MainTex;

layout(set = 0, binding = 1) uniform Params
{
    float threshold;
    float knee;
} p;

float luma(vec3 c) { return dot(c, vec3(0.2126, 0.7152, 0.0722)); }

void main()
{
    // Four bilinear taps half a texel off-centre together form a 3x3 tent around the pixel. Each
    // tap is weighted by 1 / (1 + luma) (Karis average), so one very bright texel cannot dominate
    // the pyramid; without it a small glow flickers as it moves across texel boundaries.
    vec2 t = 0.5 / vec2(textureSize(_MainTex, 0));
    vec3 a = textureLod(_MainTex, vUv + vec2(-t.x, -t.y), 0.0).rgb;
    vec3 b = textureLod(_MainTex, vUv + vec2( t.x, -t.y), 0.0).rgb;
    vec3 c = textureLod(_MainTex, vUv + vec2(-t.x,  t.y), 0.0).rgb;
    vec3 d = textureLod(_MainTex, vUv + vec2( t.x,  t.y), 0.0).rgb;
    float wa = 1.0 / (1.0 + luma(a));
    float wb = 1.0 / (1.0 + luma(b));
    float wc = 1.0 / (1.0 + luma(c));
    float wd = 1.0 / (1.0 + luma(d));
    vec3 col = (a * wa + b * wb + c * wc + d * wd) / (wa + wb + wc + wd);

    // Soft-knee threshold drops the faintest values.
    float knee = max(p.knee, 1e-3);
    float l = luma(col);
    float soft = clamp(l - p.threshold + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee + 1e-4);
    float contrib = max(soft, l - p.threshold) / max(l, 1e-4);
    outColor = vec4(col * contrib, 1.0);
}
