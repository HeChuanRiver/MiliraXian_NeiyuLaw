#version 450

// Flower Bell note: the shock wave's refraction. The same dome as MX_QH_BellWave is
// used as a lens: the scene behind is shifted along the falling slope of the sight line's length
// through the dome below the curtain height (thickest in the middle, steepest at the outline), so
// it swells outwards and bends hardest along the dome's edge. vUv is the map offset from the
// centre in cells.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;        // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // x: radius (cells), y: k, z: c, w: offset at saturation (cells, already faded)
    vec4 _Wave;         // @default = vec4(1.0, 0.5, 0.6, 0.0)
    // xy: screen UV per cell (y signed for the grab image's orientation), z: gain, w: difference step (cells)
    vec4 _DistortScale; // @default = vec4(0.0, 0.0, 1.5, 0.025)
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _GrabDistortionBackground;

// Length (in z) of the sight line through the dome below the curtain height.
float chord(vec2 p)
{
    float r = u._Wave.x;
    float k = max(u._Wave.y, 0.05);
    float hc = u._Wave.z * k * r;
    float k2 = k * k;
    float A = 1.0 + 1.0 / k2;
    float B = -2.0 * p.y / k2;
    float C = p.x * p.x + p.y * p.y / k2 - r * r;
    float D = B * B - 4.0 * A * C;
    if (r <= 0.0 || D <= 0.0)
    {
        return 0.0;
    }
    float sq = sqrt(D);
    float z1 = (-B - sq) / (2.0 * A);
    float z2 = min((-B + sq) / (2.0 * A), p.y);
    if (z1 >= p.y)
    {
        return 0.0;
    }
    return max(z2 - max(z1, p.y - hc), 0.0);
}

void main()
{
    float s = max(u._DistortScale.w, 1e-4);
    float lx0 = chord(vUv - vec2(s, 0.0));
    float lx1 = chord(vUv + vec2(s, 0.0));
    float ly0 = chord(vUv - vec2(0.0, s));
    float ly1 = chord(vUv + vec2(0.0, s));
    if (lx0 + lx1 + ly0 + ly1 <= 0.0)
    {
        discard;
    }
    vec2 dir = -vec2(lx1 - lx0, ly1 - ly0) / (2.0 * s);
    float mag = length(dir) + 1e-6;
    // Saturating: at most the full offset.
    vec2 off = dir * (min(mag * u._DistortScale.z, 1.0) / mag) * u._Wave.w;
    vec2 grabUv = gl_FragCoord.xy / vec2(textureSize(_GrabDistortionBackground, 0));
    // The picture at a point is taken from nearer the centre, so the inside swells outwards.
    vec3 bg = texture(_GrabDistortionBackground, grabUv - off * u._DistortScale.xy).rgb;
    outColor = vec4(bg, u._Color.a);
}
