#version 450
layout(location=0) in vec2 vUv;
layout(location=1) in float vLife;
layout(location=2) flat in float vSpark;
layout(location=0) out vec4 outColor;
layout(set=2,binding=0) uniform MaterialUniforms {
    vec4 _CombustionFrame; // @default = vec4(0.0,0.0,0.0,1.0)
    vec4 _CombustionShape; // @default = vec4(0.0,0.0,0.0,0.0)
    vec4 _BloomTint; // @color @default = vec4(1.0,1.0,1.0,1.0)
    float _BloomIntensity; // @default = 1.0
    float _CombustionCapture; // @default = 0.0
} u;
void main() {
    vec2 p=vUv*2.0-1.0;
    float age=clamp(vLife,0.0,1.0);
    float profile=abs(p.x)*(1.0+0.7*abs(p.y))+abs(p.y)*0.62;
    float alpha=(1.0-smoothstep(0.17,1.0,profile))
        *max(0.0,sin(age*3.14159265))*u._CombustionFrame.w;
    float hot=(1.0-smoothstep(0.21,0.82,age))*smoothstep(0.43,0.79,vSpark);
    vec3 color=mix(vec3(0.86,0.26,0.075),vec3(1.0,0.91,0.59),hot);
    color=mix(color,vec3(0.388,0.125,0.157),age*age);
    if(u._CombustionCapture>0.5) alpha*=hot;
    outColor=vec4(color*u._BloomTint.rgb*u._BloomIntensity*alpha,alpha);
}
