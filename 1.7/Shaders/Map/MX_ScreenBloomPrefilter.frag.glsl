#version 450

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _Threshold; // @default = 0.02
    float _Knee;      // @default = 0.02
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

void main()
{
    // Reduces the scene image 4x in one pass. The target pixel centre is the centre of a 4x4 block of
    // source texels; four bilinear taps one texel off-centre each average a 2x2 block, so together
    // they cover the whole 4x4 block with equal weights. The soft-knee threshold then drops the
    // faintest values.
    vec2 uv = vUv * u._MainTex_ST.zw + u._MainTex_ST.xy;
    uv.y = 1.0 - uv.y;
    vec2 t = 1.0 / vec2(textureSize(_MainTex, 0));
    vec3 c = texture(_MainTex, uv + vec2(-t.x, -t.y)).rgb
           + texture(_MainTex, uv + vec2( t.x, -t.y)).rgb
           + texture(_MainTex, uv + vec2(-t.x,  t.y)).rgb
           + texture(_MainTex, uv + vec2( t.x,  t.y)).rgb;
    c *= 0.25;

    float knee = max(u._Knee, 1e-3);
    float l = dot(c, vec3(0.2126, 0.7152, 0.0722));
    float soft = clamp(l - u._Threshold + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee + 1e-4);
    float contrib = max(soft, l - u._Threshold) / max(l, 1e-4);
    outColor = vec4(c * contrib, 1.0);
}