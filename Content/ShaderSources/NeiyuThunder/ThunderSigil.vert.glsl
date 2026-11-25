#version 450
#ifdef INSTANCED
#include "InstanceTransforms.glsl"
layout(set = 2, binding = 3) readonly buffer _InstanceData { vec4 data[]; } plates;
#endif

layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 2) in vec4 inColor; // @vertex Color optional @default = vec4(1.0, 1.0, 1.0, 1.0)
layout(location = 3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0.0, 0.0, 0.0, 0.0)
layout(push_constant) uniform Push { mat4 modelMatrix; } push;
layout(set = 0, binding = 0) uniform VertexUniforms { mat4 ViewProj; } uVertex;
layout(location = 0) out vec4 vColor;
layout(location = 1) out vec2 vUv;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(0.45, 0.875, 1.0, 1.0)
    vec4 _SecondaryColor; // @color @default = vec4(0.735, 0.635, 1.0, 1.0)
    // seconds, mode, seed, quality
    vec4 _Clock; // @default = vec4(0.0, 0.0, 0.0, 1.0)
    // charge, reveal, intensity, final
    vec4 _State; // @default = vec4(0.0, 1.0, 1.0, 0.0)
} uMaterial;
layout(location = 2) flat out vec4 vClock;
layout(location = 3) flat out vec4 vState;

void main()
{
#ifdef INSTANCED
    mat4 model = instanceTransforms[gl_InstanceIndex];
    int row = gl_InstanceIndex * 2;
    vClock = plates.data[row];
    vState = plates.data[row + 1];
#else
    mat4 model = push.modelMatrix;
    vClock = uMaterial._Clock;
    vState = uMaterial._State;
#endif
    gl_Position = uVertex.ViewProj * model * vec4(inPosition, 1.0);
    vColor = inColor;
    vUv = inUv0.xy;
}
