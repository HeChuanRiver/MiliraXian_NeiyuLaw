#version 450

// One downsample level of the bloom pyramid. Waylith PostEffectPass (fullscreen triangle, set 0):
// binding 0 is the previous, twice as large level.
layout(location = 0) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 0) uniform sampler2D _MainTex;

vec3 tap(vec2 uv) { return textureLod(_MainTex, uv, 0.0).rgb; }

void main()
{
    // 13-tap downsample (Jimenez, Call of Duty: Advanced Warfare): five overlapping 2x2 boxes, the
    // centre one weighted 0.5 and the four corner ones 0.125 each. Offsets are in source texels.
    vec2 t = 1.0 / vec2(textureSize(_MainTex, 0));
    vec3 A = tap(vUv + t * vec2(-2.0, -2.0));
    vec3 B = tap(vUv + t * vec2( 0.0, -2.0));
    vec3 C = tap(vUv + t * vec2( 2.0, -2.0));
    vec3 D = tap(vUv + t * vec2(-1.0, -1.0));
    vec3 E = tap(vUv + t * vec2( 1.0, -1.0));
    vec3 F = tap(vUv + t * vec2(-2.0,  0.0));
    vec3 G = tap(vUv);
    vec3 H = tap(vUv + t * vec2( 2.0,  0.0));
    vec3 I = tap(vUv + t * vec2(-1.0,  1.0));
    vec3 J = tap(vUv + t * vec2( 1.0,  1.0));
    vec3 K = tap(vUv + t * vec2(-2.0,  2.0));
    vec3 L = tap(vUv + t * vec2( 0.0,  2.0));
    vec3 M = tap(vUv + t * vec2( 2.0,  2.0));

    vec3 c = (D + E + I + J) * (0.5 / 4.0)
           + (A + B + F + G) * (0.125 / 4.0)
           + (B + C + G + H) * (0.125 / 4.0)
           + (F + G + K + L) * (0.125 / 4.0)
           + (G + H + L + M) * (0.125 / 4.0);
    outColor = vec4(c, 1.0);
}
