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
float line(float d,float w) {
    float aa=max(fwidth(d),0.0015);
    return 1.0-smoothstep(w-aa,w+aa,abs(d));
}
void main() {
    vec2 p=vUv*2.0-1.0;
    float r=length(p);
    if(r>=1.0) discard;
    float a=atan(p.y,abs(p.x)<0.00001?0.00001:p.x),t=u._CombustionFrame.x;
    float draw=u._CombustionFrame.y;
    float charge=u._CombustionShape.y;
    float outer=line(r-0.93,0.005)*smoothstep(0.0,0.035,draw-fract(a/6.2831853+0.5));
    float inner=line(r-0.76,0.004)*(0.55+0.45*smoothstep(0.2,0.7,cos(a*6.0+t*0.6)));
    float sector=mod(a-t*0.12+0.1309,0.2618)-0.1309;
    float glyph=line(sector,0.013)*step(0.79,r)*step(r,0.87);
    glyph=max(glyph,line(r-0.835+sector*0.22,0.005)*step(abs(sector),0.058));
    float triangle=line(r*cos(mod(a+t*0.08+1.0472,2.0944)-1.0472)-0.32,0.004);
    triangle*=1.0-smoothstep(0.68,0.72,r);
    float spark=pow(max(0.0,cos(a-t*1.4)),90.0)*line(r-0.93,0.013);
    float arcPhase=0.5+0.5*cos(a*5.0-t*3.1);
    float arc=line(r-mix(0.86,0.915,charge),0.006+charge*0.006)
        *smoothstep(0.55,0.96,arcPhase)*charge;
    float alpha=max(outer,max(inner,max(glyph,triangle*0.4)))*draw+(spark+arc)*draw;
    if(u._CombustionCapture>0.5) alpha*=max(outer*0.32,max(spark,arc));
    alpha=clamp(alpha*u._CombustionFrame.w,0.0,0.8);
    vec3 color=mix(vec3(0.87,0.24,0.145),vec3(1.0,0.82,0.42),0.55+0.45*spark);
    outColor=vec4(color*u._BloomTint.rgb*u._BloomIntensity*alpha,alpha);
}
