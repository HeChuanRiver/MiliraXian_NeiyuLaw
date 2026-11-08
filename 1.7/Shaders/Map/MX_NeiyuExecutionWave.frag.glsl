#version 450

#ifdef INSTANCED
#include "InstanceTransforms.glsl"
#endif

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;

void main()
{
    vec4 stroke = texture(_MainTex, vUv * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy);
    float luma = dot(stroke.rgb, vec3(0.2126, 0.7152, 0.0722));
    vec3 ink = clamp(mix(vec3(luma), stroke.rgb, 1.65) * 0.94, 0.0, 1.0);
    // Boost the brush itself so the scripted reveal and the tail still fade independently.
    float coverage = 1.0 - pow(1.0 - stroke.a, 2.7);
    outColor = vec4(ink, coverage) * vColor * uMaterial._Color;
}
