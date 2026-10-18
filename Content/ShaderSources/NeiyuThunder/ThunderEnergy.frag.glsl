#version 450
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _Kind; // @default = 0.0
} uMaterial;
void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float core, glow;
    if (uMaterial._Kind < 0.5)
    {
        float edge = 1.0 - smoothstep(0.70, 1.0, abs(p.x));
        float aa = max(fwidth(p.y), 0.02);
        core = (1.0 - smoothstep(0.045, 0.045 + aa, abs(p.y))) * edge;
        glow = exp(-p.y * p.y * 8.0) * edge * 0.38;
    }
    else
    {
        float r = length(p);
        core = exp(-r * r * 30.0);
        glow = exp(-r * r * 5.0) * (1.0 - smoothstep(0.65, 1.0, r)) * 0.60;
    }
    vec4 color = vColor * uMaterial._Color;
    float alpha = (core + glow) * color.a;
    if (alpha < 0.001) discard;
    outColor = vec4((mix(color.rgb, vec3(0.957, 0.98, 1.0), 0.66) * core
        + color.rgb * glow) * color.a, clamp(alpha * 0.5, 0.0, 0.65));
}
