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
layout(location=2) flat out float vSpark;
float hash(float x) { return fract(sin(x*127.1)*43758.5453); }
void main() {
    float id=inUv1.x,seed=u._CombustionFrame.z;
    float rnd=hash(id+seed),mode=u._CombustionShape.x;
    bool previewOrbit=mode==4.0;
    float life=mode==1.0?clamp(u._CombustionFrame.y/(0.30+rnd*0.70),0.0,1.0):
        fract(u._CombustionFrame.x*(0.64+rnd*0.85)+rnd);
    float orbitSpeed=mode==3.0?1.9:0.28;
    if(previewOrbit) orbitSpeed=mix(-0.92,-0.64,rnd);
    float angle=id*2.399963+seed+u._CombustionFrame.x*orbitSpeed;
    vec2 dir=vec2(cos(angle),sin(angle));
    vec2 position;
    if(mode==0.0) position=dir*(1.2-life)*0.75;
    else if(mode==1.0) position=dir*(1.0-exp(-life*4.1))*(0.25+rnd*0.36)
        +vec2(0,life*0.56-life*life*0.43);
    else if(mode==2.0) position=vec2(dir.x*life*0.35,-life*(0.35+0.48*rnd));
    else if(previewOrbit) position=dir*(mix(0.45,0.495,rnd)+life*0.045);
    else position=dir*mix(0.36,0.67,rnd)*vec2(1.0,0.79)+dir*life*0.12;
    float width=mix(0.010,0.031,rnd)*(1.0-life);
    if(previewOrbit) width*=0.36;
    float streak=mix(2.2,5.2,rnd)*(1.0-0.6*life);
    vec2 quad=vec2(inPosition.x*width,inPosition.z*width*streak);
    vec2 motion=mode==2.0?vec2(0.0,-1.0):(mode==3.0?vec2(-dir.y,dir.x):dir);
    if(previewOrbit) motion=vec2(dir.y,-dir.x);
    vec2 rotated=mat2(motion.y,-motion.x,motion.x,motion.y)*quad;
    vUv=inUv0.xy;
    vLife=life;
    vSpark=rnd;
    gl_Position=camera.ViewProj*push.modelMatrix*vec4(position.x+rotated.x,0,position.y+rotated.y,1);
}
