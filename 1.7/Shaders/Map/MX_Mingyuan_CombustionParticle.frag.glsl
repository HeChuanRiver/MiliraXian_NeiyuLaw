#version 450
layout(location=0) in vec2 vUv;
layout(location=1) in float vLife;
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
    float alpha=(1.0-smoothstep(0.3,1.0,abs(p.x)+abs(p.y)*0.45))
        *sin(age*3.14159265)*u._CombustionFrame.w;
    vec3 color=mix(vec3(1.0,0.82,0.42),vec3(0.388,0.125,0.157),age*age);
    outColor=vec4(color*alpha,alpha);
}
