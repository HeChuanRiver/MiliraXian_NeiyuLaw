#version 450
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _ArcaneClock; // @default = vec4(0.0, 0.0, 0.0, 1.0)
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p)
{
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1,0)), f.x),
               mix(hash(i + vec2(0,1)), hash(i + vec2(1,1)), f.x), f.y);
}
void main()
{
    float t = uMaterial._ArcaneClock.x;
    float seed = uMaterial._ArcaneClock.y;
    vec2 p = vUv - 0.5;
    float turn = seed * 6.2831853 + sin(t * 0.17 + seed) * 0.12;
    p = mat2(cos(turn), -sin(turn), sin(turn), cos(turn)) * p;
    float n = noise(p * 8.0 + vec2(seed * 17.0, -t * 0.08));
    float fine = uMaterial._ArcaneClock.w > 0.7 ? noise(p * 19.0 + vec2(t * 0.05, seed * 9.0)) : n;
    vec2 uv = clamp(p + 0.5 + vec2(n - 0.5, fine - 0.5) * 0.025, 0.0, 1.0);
    vec4 tex = texture(_MainTex, uv);
    float edge = 1.0 - smoothstep(0.26, 0.52, length(p) + (n - 0.5) * 0.10);
    vec4 color = uMaterial._Color * vColor;
    float alpha = clamp(tex.a * edge * (0.68 + n * 0.32) * color.a, 0.0, 0.69);
    vec3 rgb = color.rgb * mix(vec3(0.72, 0.77, 0.85), vec3(1.05, 1.0, 0.94), n);
    rgb *= (0.87 + 0.13 * fine) * (0.72 + 0.28 * tex.r);
    if (alpha <= 0.001) discard;
    outColor = vec4(rgb * alpha, alpha);
}
