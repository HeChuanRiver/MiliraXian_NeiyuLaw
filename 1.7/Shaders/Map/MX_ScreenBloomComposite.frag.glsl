#version 450

// Waylith PostEffectPass drawn inline into the backbuffer with additive blending: binding 0 is the
// top of the bloom group's accumulated pyramid, binding 1 the per-group parameters.
layout(location = 0) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _MainTex;

layout(set = 0, binding = 1) uniform Params
{
    float gain;
    float radius;
} p;

vec3 tap(vec2 uv) { return textureLod(_MainTex, uv, 0.0).rgb; }

void main()
{
    // Final 3x3 tent upsample to screen resolution (weights 1-2-1 / 16), so the last magnification
    // is filtered as smoothly as every level of the pyramid.
    vec2 d = p.radius / vec2(textureSize(_MainTex, 0));
    vec3 c = tap(vUv) * 4.0;
    c += (tap(vUv + vec2(-d.x, 0.0)) + tap(vUv + vec2(d.x, 0.0))
        + tap(vUv + vec2(0.0, -d.y)) + tap(vUv + vec2(0.0, d.y))) * 2.0;
    c += tap(vUv + vec2(-d.x, -d.y)) + tap(vUv + vec2(d.x, -d.y))
       + tap(vUv + vec2(-d.x,  d.y)) + tap(vUv + vec2(d.x,  d.y));
    outColor = vec4(c * (p.gain / 16.0), 1.0);
}
