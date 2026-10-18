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
void main()
{
    float t = uMaterial._ArcaneClock.x;
    float seed = uMaterial._ArcaneClock.y;
    float kind = uMaterial._ArcaneClock.z;
    vec2 p = vUv - 0.5;
    float mask = texture(_MainTex, vUv).a;
    float detail = 1.0;
    if (kind < 0.5) // Column and curved trail share the same longitudinal flow.
    {
        float edge = exp(-p.x * p.x * 15.0) * (1.0 - smoothstep(0.36, 0.5, abs(p.x)));
        float threads = pow(0.5 + 0.5 * sin(p.x * 79.0 + sin(vUv.y * 13.0 - t * 2.0)), 9.0);
        float flow = pow(0.5 + 0.5 * sin(vUv.y * 27.0 - t * 8.0 + p.x * 12.0), 5.0);
        mask *= edge;
        detail = 0.72 + threads * 0.20 + flow * 0.16;
    }
    else if (kind < 1.5) // Spherical annulus: X traverses its thickness.
    {
        mask = exp(-pow(p.x * 8.0, 2.0)) * (1.0 - smoothstep(0.4, 0.5, abs(p.x)));
        detail = 0.93 + 0.07 * sin(vUv.y * 81.0 - t * 2.0);
    }
    else if (kind < 2.5) // Ground shockwave: analytical front and broad soft wake.
    {
        float r = length(p) * 2.0;
        mask = exp(-pow((r - 0.86) * 44.0, 2.0))
            + exp(-pow((r - 0.78) * 15.0, 2.0)) * 0.25;
    }
    else if (kind > 3.5) // Controlled star glint, used sparingly in comet and impact.
    {
        float r = length(p);
        mask = exp(-r * r * 23.0) * 0.42;
        mask += exp(-abs(p.x) * 80.0 - abs(p.y) * 8.0) * 0.34;
        mask += exp(-abs(p.y) * 80.0 - abs(p.x) * 8.0) * 0.34;
        mask *= 1.0 - smoothstep(0.36, 0.5, r);
        detail = 0.95 + 0.05 * sin(t * 2.0 + seed);
    }
    vec4 color = uMaterial._Color * vColor;
    float alpha = clamp(mask * color.a, 0.0, 0.92);
    if (alpha <= 0.001) discard;
    outColor = vec4(min(color.rgb, vec3(1.25)) * alpha * detail, alpha);
}
