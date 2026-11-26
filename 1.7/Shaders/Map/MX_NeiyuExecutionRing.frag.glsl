#version 450

// Neiyu multi-target execution floor ring: reconstructs the ring-relative ground offset from vUv
// and _FieldSize, evaluates polar coordinates, and maps the open or closed arc onto the curved
// blue-to-pink brush stroke inside BladeLight_Neiyu_E.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    // xy: quad extents (cells), z: north extra height (cells), set by FieldRenderer.
    vec4 _FieldSize; // @default = vec4(2.0, 2.0, 0.0, 0.0)
    // x: startYaw (rad, clockwise from +Z), y: sweep (rad), z: outer radius (cells), w: opacity.
    vec4 _RingArc;   // @default = vec4(0.0, 0.0, 1.0, 0.0)
    // xy: ring centre offset from emitter position (cells), z: 1 for closed floor wave else 0.
    vec4 _RingMeta;  // @default = vec4(0.0, 0.0, 0.0, 0.0)
} uMaterial;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const float TAU = 6.28318530718;

// Pixel pairs just inside the hole and outside the rim of BladeLight_Neiyu_E (512x512).
const vec2 INNER_PX[7] = vec2[7](
    vec2(172.0, 272.0),
    vec2(155.0, 212.0),
    vec2(158.0, 155.0),
    vec2(218.0, 137.0),
    vec2(255.0, 155.0),
    vec2(277.0, 174.0),
    vec2(294.0, 183.0)
);

const vec2 OUTER_PX[7] = vec2[7](
    vec2(132.0, 304.0),
    vec2(81.0, 220.0),
    vec2(67.0, 100.0),
    vec2(172.0, 41.0),
    vec2(272.0, 82.0),
    vec2(330.0, 129.0),
    vec2(354.0, 155.0)
);

void main()
{
    float startYaw = uMaterial._RingArc.x;
    float sweep = uMaterial._RingArc.y;
    float radius = uMaterial._RingArc.z;
    float opacity = uMaterial._RingArc.w;
    if (sweep <= 0.008 || radius <= 0.01 || opacity <= 0.001)
    {
        discard;
    }

    vec2 m = vec2(
        (vUv.x - 0.5) * uMaterial._FieldSize.x,
        vUv.y * uMaterial._FieldSize.y - 0.5 * (uMaterial._FieldSize.y - uMaterial._FieldSize.z));
    vec2 p = m - uMaterial._RingMeta.xy;
    float r = length(p);
    if (r > radius)
    {
        discard;
    }

    bool closed = uMaterial._RingMeta.z > 0.5;
    // Matches Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward: 0 = +Z, +PI/2 = +X.
    float angle = atan(p.x, p.y);
    float rel = mod(angle - startYaw, TAU);
    if (!closed && rel > sweep)
    {
        discard;
    }

    float u = closed ? (rel / TAU) : clamp(rel / max(sweep, 1e-4), 0.0, 1.0);
    float width = closed ? 1.35 : mix(0.15, 1.55, sqrt(u));
    float innerR = radius - width;
    if (r < innerR)
    {
        discard;
    }

    float v = clamp((r - innerR) / max(width, 1e-4), 0.0, 1.0);
    float arcAlpha = closed ? 1.0 : smoothstep(0.0, 1.0, u) * (1.0 - 0.18 * pow(u, 32.0));

    // Fold the brush back on itself for the closed floor wave so its join has matching UVs.
    float brush = closed ? (0.5 - 0.5 * cos(u * TAU)) : u;
    float samplePos = brush * 6.0;
    int section = clamp(int(floor(samplePos)), 0, 5);
    float frac = samplePos - float(section);
    vec2 px = mix(
        mix(INNER_PX[section], INNER_PX[section + 1], frac),
        mix(OUTER_PX[section], OUTER_PX[section + 1], frac),
        v);
    vec2 uv = vec2(px.x / 512.0, 1.0 - px.y / 512.0);
    vec4 stroke = texture(_MainTex, uv * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy);
    float luma = dot(stroke.rgb, vec3(0.2126, 0.7152, 0.0722));
    vec3 ink = clamp(mix(vec3(luma), stroke.rgb, 1.65) * 0.94, 0.0, 1.0);
    float coverage = 1.0 - pow(1.0 - stroke.a, 2.7);
    outColor = vec4(ink, coverage * arcAlpha * opacity) * vColor * uMaterial._Color;
}
