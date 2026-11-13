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
    float r=length(p);
    float along=mode<1.5?1.0-vUv.y:vUv.y;
    float spin=t*(0.65+core*0.45)+r*(1.3+core*1.7);
    vec2 swirl=mat2(cos(spin),sin(spin),-sin(spin),cos(spin))*p;
    vec2 flow=mode<0.5?swirl:vec2(p.x,along*2.0-t*1.7);
    vec2 warp=vec2(noise(flow*2.3+seed+t*0.33),
        noise(flow*2.3-seed-t*0.29))-0.5;
    float fuel=fbm(flow*3.3+warp*1.5+vec2(seed,-t*0.8));
    float fine=noise(flow*7.0+warp+seed-t);
    float distance,heat;
    if(mode<0.5) {
        // Sample around a circle so the crown has no seam at the angle wrap.
        vec2 radial=p/max(r,0.001);
        float crown=noise(radial*(4.4+r*1.5)+warp*1.2+vec2(seed,t*1.6));
        float tongue=smoothstep(0.43,0.88,crown+0.12*fine);
        float boundary=min(0.92,0.61+(fuel-0.43)*0.31+tongue*(0.18+0.16*core));
        distance=r-boundary;
        float seams=1.0-smoothstep(0.05,0.17,abs(fuel-0.46));
        float surface=pow(max(0.0,1.0-r/boundary),0.62);
        heat=clamp(surface*0.64+fuel*0.44+fine*0.13+seams*0.11,0.0,1.0);
        heat=mix(heat,0.84+fuel*0.15,1.0-smoothstep(0.09,0.31,r));
    } else {
        // Two intertwined streams taper into an eroded tail.
        float width=mix(0.62+0.06*core,0.02,pow(along,0.78));
        float bend=(fuel-0.42)*0.45+sin(along*7.0-t*3.0+seed)*0.07;
        float stream=abs(p.x+bend);
        distance=stream-width-(fuel-0.46)*0.42;
        distance=max(distance,along-0.95+(1.0-fine)*0.13);
        float filaments=1.0-smoothstep(0.03,0.18,abs(stream-width*0.39+(fine-0.5)*0.11));
        heat=clamp(0.48-stream*0.4+fuel*0.60+filaments*0.17-along*0.36,0.0,1.0);
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
