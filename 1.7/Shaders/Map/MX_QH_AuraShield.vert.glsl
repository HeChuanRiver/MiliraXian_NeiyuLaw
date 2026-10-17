#version 450

layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0.0, 0.0, 0.0, 0.0)
layout(location = 4) in vec4 inUv1; // @vertex TexCoord1 optional @default = vec4(0.0, 0.0, 0.0, 0.0)
layout(location = 5) in vec4 inUv2; // @vertex TexCoord2 optional @default = vec4(0.0, 0.0, 0.0, 0.0)

layout(push_constant) uniform Push { mat4 modelMatrix; } push;

layout(set = 0, binding = 0) uniform VertexUniforms
{
    mat4 ViewProj;
} uVertex;

// Per-bone rest-to-posed 2D affine transforms, two vec4 per bone:
// [2b] = (colX.x, colX.y, colY.x, colY.y), [2b+1] = (origin.x, origin.y, 0, 0).
// Must match the declaration in MX_QH_AuraShield.frag.glsl (the material layout is parsed from the frag).
layout(set = 2, binding = 3) readonly buffer _BoneTransforms
{
    vec4 data[];
} uBones;

layout(location = 0) out vec2 vUv;

void main()
{
    // Linear blend skinning in the mesh plane (mesh local x/z). inUv1 = up to 4 bone indices,
    // inUv2 = matching weights. Vertices with no weights (or meshes without the channels,
    // e.g. the flat fallback quad) pass through unchanged.
    vec2 rest = inPosition.xz;
    vec2 p = rest;
    float weightSum = inUv2.x + inUv2.y + inUv2.z + inUv2.w;
    if (weightSum > 0.0001)
    {
        p = vec2(0.0);
        for (int k = 0; k < 4; k++)
        {
            float w = inUv2[k];
            if (w <= 0.0)
            {
                continue;
            }
            int b = int(inUv1[k] + 0.5) * 2;
            vec4 cols = uBones.data[b];
            vec4 origin = uBones.data[b + 1];
            p += (origin.xy + cols.xy * rest.x + cols.zw * rest.y) * w;
        }
    }

    gl_Position = uVertex.ViewProj * (push.modelMatrix * vec4(p.x, inPosition.y, p.y, 1.0));
    vUv = inUv0.xy;
}
