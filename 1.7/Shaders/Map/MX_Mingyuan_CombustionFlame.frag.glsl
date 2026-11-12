#version 450
layout(location=0) in vec2 vUv;
layout(location=0) out vec4 outColor;
layout(set=2,binding=0) uniform MaterialUniforms {
    vec4 _CombustionFrame; // @default = vec4(0.0,0.0,0.0,1.0)
    vec4 _CombustionShape; // @default = vec4(0.0,0.0,0.0,0.0)
    vec4 _BloomTint; // @color @default = vec4(1.0,1.0,1.0,1.0)
    float _BloomIntensity; // @default = 1.0
    float _CombustionCapture; // @default = 0.0
} u;
float hash(vec2 p) {
    vec3 q=fract(vec3(p.xyx)*0.1031);
    q+=dot(q,q.yzx+33.33);
    return fract((q.x+q.y)*q.z);
}
float noise(vec2 p) {
    vec2 i=floor(p),f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),
        mix(hash(i+vec2(0,1)),hash(i+vec2(1)),f.x),f.y);
}
float fbm(vec2 p) {
    float n=0.0,amp=0.57;
    for(int i=0;i<3;i++) {
        n+=amp*noise(p);
        p=mat2(1.6,1.2,-1.2,1.6)*p+vec2(5.1,2.7);
        amp*=0.46;
    }
    return n;
}
void main() {
    vec2 p=vUv*2.0-1.0;
    float t=u._CombustionFrame.x,seed=u._CombustionFrame.z;
    float mode=u._CombustionShape.x,core=u._CombustionShape.y;
    float angle=atan(p.y,p.x);
    float r=length(p);
    float spin=t*(0.65+core*0.45)+r*(1.3+core*1.7);
    vec2 swirl=mat2(cos(spin),sin(spin),-sin(spin),cos(spin))*p;
    vec2 flow=mode<0.5?swirl:vec2(p.x,p.y-t*1.7);
    vec2 warp=vec2(noise(flow*2.3+seed+t*0.33),
        noise(flow*2.3-seed-t*0.29))-0.5;
    float fuel=fbm(flow*3.3+warp*1.5+vec2(seed,-t*0.8));
    float fine=noise(flow*7.0+warp+seed-t);
    float distance,heat;
    if(mode<0.5) {
        // Polar flow breaks the solar crown while preserving a compact, textured core.
        float crown=noise(vec2(angle*3.2+seed,r*2.0-t*1.4));
        float boundary=0.65+(fuel-0.43)*0.32+(crown-0.5)*0.17*core;
        distance=r-boundary;
        heat=clamp((boundary-r)*1.45+fuel*0.65+fine*0.11,0.0,1.0);
        heat=mix(heat,0.81+fuel*0.15,1.0-smoothstep(0.08,0.24,r));
    } else {
        // Two intertwined streams taper into an eroded tail.
        float along=mode<1.5?1.0-vUv.y:vUv.y;
        float width=mix(0.56,0.02,pow(along,0.75));
        float bend=(fuel-0.42)*0.45+sin(along*7.0-t*3.0+seed)*0.07;
        distance=abs(p.x+bend)-width-(fuel-0.46)*0.32;
        distance=max(distance,along-0.95+(1.0-fine)*0.13);
        heat=clamp(0.65-abs(p.x+bend)*0.6+fuel*0.50-along*0.38,0.0,1.0);
    }
    float aa=max(fwidth(distance)*1.3,0.009);
    float alpha=(1.0-smoothstep(-aa,aa,distance))*u._CombustionFrame.w;
    alpha*=1.0-smoothstep(0.91,1.0,max(abs(p.x),abs(p.y)));
    vec3 color=mix(vec3(0.388,0.125,0.157),vec3(0.871,0.239,0.145),smoothstep(0.15,0.43,heat));
    color=mix(color,vec3(1.0,0.545,0.208),smoothstep(0.39,0.68,heat));
    color=mix(color,vec3(1.0,0.816,0.42),smoothstep(0.66,0.86,heat));
    color=mix(color,vec3(1.0,0.965,0.82),smoothstep(0.88,0.99,heat));
    if(u._CombustionCapture>0.5) alpha*=smoothstep(0.76,0.98,heat);
    alpha=clamp(alpha,0.0,1.0);
    outColor=vec4(color*u._BloomTint.rgb*u._BloomIntensity*alpha,alpha);
}
