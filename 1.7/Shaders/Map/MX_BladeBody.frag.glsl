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
} uMaterial;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

void main()
{
    vec4 color = texture(_MainTex, vUv * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy) * uMaterial._Color * vColor;
    if (color.a < 0.5)
        discard;
    outColor = color;
}
