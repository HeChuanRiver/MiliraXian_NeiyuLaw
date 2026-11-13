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
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p),f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),
        mix(hash(i+vec2(0,1)),hash(i+vec2(1)),f.x),f.y);
}
void main() {
    vec2 p=vUv*2.0-1.0;
    float t=u._CombustionFrame.x,age=clamp(u._CombustionFrame.y,0.0,1.0);
    float seed=u._CombustionFrame.z,r=length(p);
    if(r>=1.0) discard;
    float angle=atan(p.y,abs(p.x)<0.00001?0.00001:p.x);
    float mode=u._CombustionShape.x;
    vec2 drift=vec2(noise(p*2.7+seed-t),noise(p*2.7-seed+t))-0.5;
    float n=noise(p*5.0+drift*1.5+seed-t*0.6);
    float alpha,hot;
    vec3 color;
    if(mode<0.5) {
        float layer=u._CombustionShape.y;
        float front=mix(0.10,0.92,1.0-pow(1.0-age,2.55-layer*0.43));
        float ripples=noise(vec2(cos(angle),sin(angle))*5.0+seed+t);
        float d=abs(r-front-(ripples-0.5)*mix(0.15,0.02,age));
        float width=mix(0.084,0.018,age)*(1.0-layer*0.13);
        float aa=max(fwidth(d),0.006);
        alpha=(1.0-smoothstep(width-aa,width+aa,d))*smoothstep(0.22,0.48,n);
        hot=1.0-smoothstep(0.0,width+aa,d);
        color=mix(vec3(0.87,0.24,0.145),vec3(1.0,0.82,0.42),hot);
        color=mix(color,vec3(0.65,0.25,0.12),clamp(u._CombustionShape.y*0.3,0.0,0.6));
    } else if(mode<1.5) {
        float cracks=1.0-smoothstep(0.025,0.105,abs(n-0.5));
        float edge=1.0-smoothstep(0.36,0.85,r+(n-0.5)*0.22);
        alpha=edge*(0.12+cracks*0.6)*(1.0-age);
        hot=(1.0-age)*cracks;
        color=mix(vec3(0.18,0.06,0.075),vec3(1.0,0.53,0.18),hot*hot);
    } else if(mode<2.5) {
        float rays=pow(max(0.0,cos(angle*9.0+seed)),18.0)*mix(0.55,1.0,n);
        alpha=exp(-r*r*17.0)+rays*exp(-r*4.8)*0.72;
        hot=exp(-r*r*5.0);
        color=mix(vec3(1.0,0.49,0.15),vec3(1.0,0.965,0.82),hot);
    } else if(mode<3.5) {
        alpha=exp(-r*r*6.0)*(0.45+n*0.4);
        hot=0.0;
        color=vec3(0.65,0.17,0.055);
    } else if(mode<4.5) {
        alpha=smoothstep(0.25,0.7,n)*(1.0-smoothstep(0.3,0.95,r));
        hot=0.0;
        color=mix(vec3(0.149,0.098,0.122),vec3(0.34,0.22,0.21),n);
    } else {
        // Local emissive halo. The dense flame is rendered independently on top.
        float glowRadius=r+(n-0.5)*0.11;
        float radialGlow=exp(-glowRadius*glowRadius*5.8);
        float rays=pow(max(0.0,cos(angle*11.0+seed-t*1.2)),12.0)*smoothstep(0.35,0.68,n);
        alpha=radialGlow*0.61+rays*exp(-r*3.7)*0.27;
        hot=radialGlow*0.55+rays*0.30;
        color=mix(vec3(0.79,0.14,0.028),vec3(1.0,0.70,0.27),radialGlow);
    }
    alpha*=u._CombustionFrame.w*(1.0-smoothstep(0.91,1.0,r));
    if(u._CombustionCapture>0.5) alpha*=hot;
    alpha=clamp(alpha,0.0,1.0);
    outColor=vec4(color*u._BloomTint.rgb*u._BloomIntensity*alpha,alpha);
}
