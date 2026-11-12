#version 450
layout(location=0) in vec3 inPosition; // @vertex Position required
layout(location=3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0.0,0.0,0.0,0.0)
layout(location=4) in vec4 inUv1; // @vertex TexCoord1 optional @default = vec4(0.0,0.0,0.0,0.0)
layout(push_constant) uniform Push { mat4 modelMatrix; } push;
layout(set=0,binding=0) uniform VertexUniforms { mat4 ViewProj; } camera;
layout(set=2,binding=0) uniform MaterialUniforms {
    vec4 _CombustionFrame;
    vec4 _CombustionShape;
    vec4 _BloomTint;
    float _BloomIntensity;
    float _CombustionCapture;
} u;
layout(location=0) out vec2 vUv;
layout(location=1) out float vLife;
float hash(float x) { return fract(sin(x*127.1)*43758.5453); }
void main() {
    float id=inUv1.x,seed=u._CombustionFrame.z;
    float rnd=hash(id+seed),mode=u._CombustionShape.x;
    float life=mode==1.0?u._CombustionFrame.y/(0.35+rnd*0.65):
        fract(u._CombustionFrame.x*(0.6+rnd*0.7)+rnd);
    float angle=id*2.399963+seed;
    vec2 dir=vec2(cos(angle),sin(angle));
    vec2 position;
    if(mode==0.0) position=dir*(1.2-life)*0.8;
    else if(mode==1.0) position=dir*(1.0-exp(-life*3.5))*(0.3+rnd*0.6)
        +vec2(0,life*0.65-life*life*0.55);
    else position=vec2(dir.x*life*0.4,life*0.8);
    float width=(0.014+rnd*0.023)*(1.0-clamp(life,0.0,1.0));
    vec2 quad=vec2(inPosition.x*width,inPosition.z*width*(2.0+rnd*4.0));
    vec2 rotated=mat2(dir.x,dir.y,-dir.y,dir.x)*quad;
    vUv=inUv0.xy;
    vLife=life;
    gl_Position=camera.ViewProj*push.modelMatrix*vec4(position.x+rotated.x,0,position.y+rotated.y,1);
}
