#version 450

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    vec4 _Color;      // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _Gain;      // @default = 1.0
    float _Radius;    // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

void main()
{
    // Blurs and draws the halo image of a whole bloom group over the view. Additive blending; the
    // per-object fade is already part of the scene image, so _Color only tints the group.
    // Dual Kawase upsample kernel, evaluated here instead of in a pyramid pass: four axial taps at
    // two halo texels (weight 1) and four diagonal taps at one halo texel (weight 2), divided by 12.
    vec2 uv = vUv * u._MainTex_ST.zw + u._MainTex_ST.xy;
    uv.y = 1.0 - uv.y;
    vec2 t = u._Radius / vec2(textureSize(_MainTex, 0));
    vec3 sum = texture(_MainTex, uv + vec2(-2.0 * t.x, 0.0)).rgb;
    sum += texture(_MainTex, uv + vec2(-t.x,  t.y)).rgb * 2.0;
    sum += texture(_MainTex, uv + vec2(0.0,  2.0 * t.y)).rgb;
    sum += texture(_MainTex, uv + vec2( t.x,  t.y)).rgb * 2.0;
    sum += texture(_MainTex, uv + vec2( 2.0 * t.x, 0.0)).rgb;
    sum += texture(_MainTex, uv + vec2( t.x, -t.y)).rgb * 2.0;
    sum += texture(_MainTex, uv + vec2(0.0, -2.0 * t.y)).rgb;
    sum += texture(_MainTex, uv + vec2(-t.x, -t.y)).rgb * 2.0;
    vec3 halo = (sum / 12.0) * u._Gain * u._Color.rgb * u._Color.a;
    outColor = vec4(halo, 1.0);
}