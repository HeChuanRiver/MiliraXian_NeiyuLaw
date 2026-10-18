#version 450
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _ArcaneClock; // @default = vec4(0.0, 0.0, 0.0, 1.0)
    vec4 _Stroke; // @default = vec4(1.35, 0.58, 5.5, 0.32)
    vec4 _Flow; // @default = vec4(0.28, 0.48, 0.0, 0.0)
    vec4 _ChantFinish; // @default = vec4(0.0, 0.0, 0.0, 0.0)
    float _Reveal; // @default = 1.0
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
const float TAU = 6.28318530718;
void main()
{
    vec2 p = vUv - 0.5;
    float angle = fract(atan(p.x, p.y) / TAU + 1.0);
    float reveal = uMaterial._Reveal;
    float sweep = reveal >= 0.9999 ? 1.0 : smoothstep(0.0, 0.009, reveal - angle);
    // Alpha stores the signed Euclidean distance, +/-16 source texels.
    // A single fetch supplies all three stroke layers without duplicate geometry.
    float d = (texture(_MainTex, vUv).a - 0.5) * 32.0;
    float aa = max(0.65, fwidth(d) * 0.65);
    float width = uMaterial._Stroke.x;
    float ink = smoothstep(-aa, aa, d + width);
    float core = smoothstep(-aa, max(aa, 3.0 * uMaterial._Stroke.y), d + width * 0.25);
    float outside = max(0.0, -d - width);
    float rim = exp(-outside * outside / 3.8) * (1.0 - ink);
    float radius = uMaterial._Stroke.z;
    float halo = exp(-outside * outside / (radius * radius)) * (1.0 - ink * 0.65);
    float t = uMaterial._ArcaneClock.x;
    float seed = uMaterial._ArcaneClock.y;
    float radial = uMaterial._ArcaneClock.z;
    float path = mix(angle * 3.0, length(p) * 7.0, radial);
    float flow = pow(0.5 + 0.5 * sin(TAU * (path + t * 0.19 + seed)), 18.0);
    float grain = 0.96 + 0.04 * sin(angle * 163.0 + length(p) * 113.0 - t * 1.7);
    float breath = 0.97 + 0.03 * sin(t * 1.35 + seed * TAU);
    float tip = reveal >= 0.9999 ? 0.0 : exp(-pow((reveal - angle) * 120.0, 2.0));
    float seal = uMaterial._ChantFinish.x;
    float pull = uMaterial._ChantFinish.y;
    // The completion breath is shared by every rune. During convergence a
    // single highlight travels inward with the controller's tick-sampled phase.
    float inward = exp(-pow((length(p) * 2.0 - (1.0 - pull)) * 12.0, 2.0))
        * sin(pull * 3.14159265359);
    vec4 color = uMaterial._Color * vColor;
    float glow = uMaterial._Stroke.w * uMaterial._ArcaneClock.w * (1.0 + seal * 0.65);
    float alpha = clamp((ink * 0.88 + rim * 0.26 + halo * glow * 0.35) * color.a * sweep, 0.0, 0.94);
    vec3 hue = mix(color.rgb, vec3(0.91, 0.97, 1.0), core * (0.24 + seal * 0.20 + inward * 0.16));
    float light = grain * breath * (0.85 + flow * uMaterial._Flow.x + tip * 0.32 + seal * 0.24 + inward * 0.22);
    vec3 rgb = hue * (ink * light + rim * 0.30 + halo * glow * 0.23) * color.a * sweep;
    // A narrow cool contour protects fine runes against bright terrain.
    rgb += vec3(0.012, 0.020, 0.033) * rim * 0.2 * color.a * sweep;
    if (alpha <= 0.001) discard;
    outColor = vec4(rgb, alpha);
}
