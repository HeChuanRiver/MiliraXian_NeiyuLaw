#version 450

// Rigid 3D blade: the vanilla Cutout rule (alpha test, depth write) on a quad that may be seen
// from behind, so the ShaderDef turns face culling off. The mesh UV has v growing toward the top
// of the image; like vanilla Cutout, _MainTex_ST (flipped by the engine) maps it to texture rows.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    vec4 _BladeReveal; // @default = vec4(0.0, 0.0, 0.0, 0.0)
    // Share of the blade drawn (a skill making the weapon vanish): pixels whose noise is above it drop out.
    float _BladeDissolve; // @default = 1.0
} uMaterial;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

float bladeHash(vec2 p)
{
    p = fract(p * vec2(234.34, 435.345));
    p += dot(p, p + 34.23);
    return fract(p.x * p.y);
}

void main()
{
    vec4 color = texture(_MainTex, vUv * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy) * uMaterial._Color * vColor;
    if (color.a < 0.5)
        discard;
    if (uMaterial._BladeDissolve < 1.0)
    {
        // Coarse cells of the texture, so the blade breaks up into flecks rather than single pixels.
        float n = bladeHash(floor(vUv * 48.0));
        if (n > uMaterial._BladeDissolve)
            discard;
    }
    if (uMaterial._BladeReveal.w > 0.0)
    {
        float along = dot(vUv, uMaterial._BladeReveal.xy) + uMaterial._BladeReveal.z;
        float extent = uMaterial._BladeReveal.w;
        float mask = smoothstep(-0.01, 0.04, along) * (1.0 - smoothstep(extent, extent + 0.06, along));
        vec3 tint = mix(vec3(1.0, 0.72, 0.88), vec3(0.60, 0.87, 1.0), clamp(along, 0.0, 1.0));
        float front = exp(-abs(along - extent) * 36.0);
        color.rgb = mix(color.rgb, mix(tint, vec3(0.96, 0.99, 1.0), front), mask * 0.85);
    }
    outColor = color;
}
