#version 450

// One upsample level of the bloom pyramid. Waylith PostEffectPass (fullscreen triangle, set 0):
// binding 0 is the coarser, half-size level already accumulated, binding 1 this level's own
// downsampled image (same size as the output), binding 2 the per-group parameters.
layout(location = 0) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _Coarse;
layout(set = 0, binding = 1) uniform sampler2D _Detail;

layout(set = 0, binding = 2) uniform Params
{
    float scatter;
    float radius;
} p;

vec3 tap(vec2 uv) { return textureLod(_Coarse, uv, 0.0).rgb; }

void main()
{
    // 3x3 tent filter on the coarse level (weights 1-2-1 / 16), offsets scaled by radius in coarse
    // texels. Mixing it with the detail level by scatter keeps the energy constant: the halo of a
    // light is the same total brightness at any scatter, scatter only moves it outward.
    vec2 d = p.radius / vec2(textureSize(_Coarse, 0));
    vec3 c = tap(vUv) * 4.0;
    c += (tap(vUv + vec2(-d.x, 0.0)) + tap(vUv + vec2(d.x, 0.0))
        + tap(vUv + vec2(0.0, -d.y)) + tap(vUv + vec2(0.0, d.y))) * 2.0;
    c += tap(vUv + vec2(-d.x, -d.y)) + tap(vUv + vec2(d.x, -d.y))
       + tap(vUv + vec2(-d.x,  d.y)) + tap(vUv + vec2(d.x,  d.y));
    c *= 1.0 / 16.0;

    vec3 detail = textureLod(_Detail, vUv, 0.0).rgb;
    outColor = vec4(mix(detail, c, p.scatter), 1.0);
}
