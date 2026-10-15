#version 450
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _ArcaneClock; // @default = vec4(0.0, 0.0, 0.0, 1.0)
    vec4 _Flow; // @default = vec4(0.28, 0.48, 0.0, 0.0)
    float _Reveal; // @default = 1.0
} uMaterial;
void main()
{
    float r = length(vUv - 0.5) * 2.0;
    float t = uMaterial._ArcaneClock.x;
    float pulse = uMaterial._ArcaneClock.z;
    float base = exp(-r * r * 3.8) * (1.0 - smoothstep(0.70, 1.0, r));
    float edge = exp(-pow((r - 0.86) * 55.0, 2.0));
    float wake = exp(-pow((r - 0.79) * 14.0, 2.0));
    float mask = mix(base * (0.94 + 0.06 * sin(t * 1.3)), edge * 0.8 + wake * 0.24, pulse);
    vec4 color = uMaterial._Color * vColor;
    float alpha = clamp(mask * color.a * mix(0.8, uMaterial._Flow.y, pulse), 0.0, 0.32);
    if (alpha <= 0.001) discard;
    outColor = vec4(color.rgb * alpha, alpha * 0.55);
}
