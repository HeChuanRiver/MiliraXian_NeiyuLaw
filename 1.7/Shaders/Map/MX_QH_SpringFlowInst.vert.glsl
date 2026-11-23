#version 450

// 春泉边界（实例化版）：一块共享四边形，逐实例的数据从 _InstanceData 读。
//
// 实例布局：每实例 <instanceVectors> 个 vec4（本项为 2），缓冲步长就等于它。
//   row0 = (center.x, center.z, sizeX, extraHeight)
//   row1 = (radius, strength, altitude, sizeZ)
//
// 顶点只提供 uv（0..1）；世界坐标由 uv 与实例数据算出：
//   worldX = center.x - sizeX/2 + uv.x * sizeX
//   worldZ = center.z - sizeZ/2 + uv.y * (sizeZ + extraHeight)

layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0.0)

layout(push_constant) uniform Push { mat4 modelMatrix; } push;

layout(set = 0, binding = 0) uniform VertexUniforms
{
    mat4 ViewProj;
} uVertex;

// 每实例 2 个 vec4；与 MX_QH_SpringFlowInst.frag.glsl 的声明必须一致（材质布局从 frag 解析）。
layout(set = 2, binding = 3) readonly buffer _InstanceData
{
    vec4 data[];
} uInst;

layout(location = 0) out vec4 vColor;
layout(location = 1) out vec3 vField;   // (X, Z, strength) —— 相对场心的格坐标与强度
layout(location = 2) out float vRadius;

const int INSTANCE_STRIDE = 2;

void main()
{
    int base = gl_InstanceIndex * INSTANCE_STRIDE;
    vec4 row0 = uInst.data[base];
    vec4 row1 = uInst.data[base + 1];

    vec2 center = row0.xy;
    float sizeX = row0.z;
    float sizeZ = row1.w;
    float extra = row0.w;

    float worldX = center.x - sizeX * 0.5 + inUv0.x * sizeX;
    float worldZ = center.y - sizeZ * 0.5 + inUv0.y * (sizeZ + extra);

    gl_Position = uVertex.ViewProj * vec4(worldX, row1.z, worldZ, 1.0);

    vColor = vec4(1.0);
    vField = vec3(worldX - center.x, worldZ - center.y, row1.y);
    vRadius = row1.x;
}
