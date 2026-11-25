#version 450
layout(location=0) in vec3 inPosition; // @vertex Position required
layout(location=3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0.0,0.0,0.0,0.0)
layout(location = 4) in vec4 inUv1; // @vertex TexCoord1 optional @default = vec4(0.0, 0.0, 0.0, 0.0)
layout(location = 5) in vec4 inUv2; // @vertex TexCoord2 optional @default = vec4(0.0, 0.0, 0.0, 0.0)
layout(location = 6) in vec4 inUv3; // @vertex TexCoord3 optional @default = vec4(0.0, 0.0, 0.0, 0.0)
layout(push_constant) uniform Push { mat4 modelMatrix; } push;
layout(set=0,binding=0) uniform VertexUniforms { mat4 ViewProj; } camera;
layout(set=2,binding=0) uniform MaterialUniforms {
    vec4 _CombustionFrame;
    vec4 _CombustionShape;
    vec4 _BloomTint;
    float _BloomIntensity;
    float _CombustionCapture;
    float _MxBatched; // @default = 0.0
} u;
layout(location=0) out vec2 vUv;
layout(location = 4) flat out vec4 vFrame;
layout(location = 5) flat out vec4 vShape;
layout(location = 6) flat out float vGlow;
void main() {
    vFrame = u._MxBatched > 0.5 ? inUv2 : u._CombustionFrame;
    vShape = u._MxBatched > 0.5 ? inUv3 : u._CombustionShape;
    vGlow = u._MxBatched > 0.5 ? inUv1.x : 1.0;
    vUv=inUv0.xy;
    vec3 p=inPosition;
    // Ribbon vertices bend coherently, with a pinned bright leading end.
    if (u._MxBatched < 0.5 && (u._CombustionShape.x==1.0 || u._CombustionShape.x==2.0)) {
        float along=inUv0.y;
        p.x+=sin(along*5.0-u._CombustionFrame.x*3.1+u._CombustionFrame.z)
            *sin(along*3.14159265)*u._CombustionShape.z;
    }
    gl_Position=camera.ViewProj*push.modelMatrix*vec4(p,1.0);
}
