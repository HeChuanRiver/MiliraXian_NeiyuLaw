#version 450
layout(location = 0) in vec2 vUv;
layout(location = 1) in vec2 vWorld;
layout(location = 2) flat in vec2 vCell;
layout(location = 3) in float vParticle;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _FireParams;    // @default = vec4(0.0, 0.0, 0.0, 1.0)
    vec4 _FireShape;     // @default = vec4(0.0, 0.0, 0.0, 0.0)
    vec4 _BloomTint;     // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _BloomIntensity; // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

const float TAU = 6.2831853;
const vec3 Ember = vec3(0.47, 0.10, 0.045);
const vec3 Red = vec3(0.86, 0.16, 0.065);
const vec3 Orange = vec3(1.0, 0.42, 0.14);
const vec3 Gold = vec3(1.0, 0.75, 0.37);
const vec3 Core = vec3(1.0, 0.96, 0.75);
const vec3 Violet = vec3(0.42, 0.10, 0.80);
const vec3 Lilac = vec3(0.90, 0.69, 1.0);

float band(float distance, float width)
{
    return 1.0 - smoothstep(width * 0.20, width, abs(distance));
}

float segment(vec2 p, vec2 a, vec2 b)
{
    vec2 ab = b - a;
    return length(p - a - ab * clamp(dot(p - a, ab) / dot(ab, ab), 0.0, 1.0));
}

float flecks(vec2 p)
{
    vec2 tile = floor(p);
    vec2 seed = fract(sin(vec2(dot(tile, vec2(32.83, 9.17)), dot(tile, vec2(57.37, 91.19)))) * 43758.54);
    return exp(-34.0 * length(fract(p) - mix(vec2(0.2), vec2(0.8), seed)));
}

void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float t = u._FireParams.y;
    float f = u._FireParams.z;
    float r = length(p);
    float angle = atan(p.x, p.y + 0.00001);
    float cycle = fract(angle / TAU + 1.0);
    int shape = int(u._FireParams.x);
    vec3 color = Orange;
    float alpha = 0.0;

    if (shape == 0)
    {
        float life = clamp((vCell.y - u._FireShape.x) / max(1.0, f), 0.0, 1.0);
        int edge = int(vCell.x + 0.5);
        vec2 pos = vWorld;
        float weave = sin(pos.x * 5.7 + pos.y * 3.1 + t * 3.8 + sin(pos.y * 5.3 - t * 1.8));
        float tongues = pow(0.5 + 0.5 * sin(pos.y * 7.0 - t * 5.7 + weave * 1.7), 3.0);
        float outer = 0.0;
        if ((edge & 1) != 0) outer = max(outer, band(vUv.x - 0.036, 0.09 + tongues * 0.08));
        if ((edge & 2) != 0) outer = max(outer, band(1.0 - vUv.x - 0.036, 0.09 + tongues * 0.08));
        if ((edge & 4) != 0) outer = max(outer, band(vUv.y - 0.036, 0.09 + tongues * 0.08));
        if ((edge & 8) != 0) outer = max(outer, band(1.0 - vUv.y - 0.036, 0.09 + tongues * 0.08));
        float sparks = flecks(pos * 2.1 + vec2(0.0, -t * 0.65));
        float heat = smoothstep(0.0, 0.34, life);
        color = mix(Ember, mix(Red, Gold, 0.38 * tongues + 0.56 * outer + 0.06 * sparks), heat);
        alpha = (0.42 + tongues * 0.20 + outer * 0.30 + sparks * 0.15) * smoothstep(0.0, 0.08, life);
    }
    else if (shape == 1)
    {
        float warning = u._FireShape.x;
        float reveal = f >= 0.999 ? 1.0 : (1.0 - smoothstep(f - 0.008, f, cycle)) * step(0.002, f);
        float outer = band(r - 0.971, 0.011) + 0.54 * band(r - 0.925, 0.006);
        float inner = 0.76 * band(r - 0.786, 0.008) + 0.36 * band(r - 0.745, 0.005);
        float runeSector = fract(cycle * 36.0 + 0.5) - 0.5;
        vec2 rune = vec2(runeSector * 0.19, r - 0.85);
        float runes = band(abs(rune.x) * 1.4 + abs(rune.y) - 0.029, 0.008);
        runes += 0.7 * band(rune.y - rune.x * 1.6 + 0.027, 0.006) * step(abs(rune.x), 0.032);
        float spokes = band(fract(cycle * 12.0 + 0.5) - 0.5, 0.055)
            * smoothstep(0.58, 0.67, r) * (1.0 - smoothstep(0.72, 0.75, r));
        float star = 0.0;
        for (int i = 0; i < 6; i++)
        {
            float a = float(i) * TAU / 6.0;
            float b = a + TAU / 3.0;
            star = max(star, band(segment(p, vec2(sin(a), cos(a)) * 0.34, vec2(sin(b), cos(b)) * 0.34), 0.009));
        }
        float center = star + 0.6 * band(r - 0.365, 0.012) + 0.54 * band(abs(p.x) + abs(p.y) - 0.094, 0.014);
        float drawn = reveal * (outer + smoothstep(0.05, 0.45, f) * (inner + runes + spokes))
            + center * smoothstep(0.02, 0.7, f);
        float head = exp(-160.0 * abs(cycle - f)) * band(r - 0.967, 0.036) * step(f, 0.993);
        color = mix(Orange, Gold, clamp(0.45 + 0.22 * sin(cycle * TAU * 6.0 - t * 2.8) + head * 0.35, 0.0, 1.0));
        alpha = mix(clamp(drawn * 0.79 + head, 0.0, 1.0),
            (band(r - 0.92, 0.033) + 0.68 * band(abs(p.x) + abs(p.y) - 0.41, 0.04)) * (0.68 + 0.22 * sin(t * 5.0)), warning);
    }
    else if (shape == 2)
    {
        // The quad's forward edge is v=1, while Waylith's PNG texture starts at the top row.
        vec4 phoenix = texture(_MainTex, vec2(vUv.x, 1.0 - vUv.y));
        color = phoenix.rgb;
        alpha = phoenix.a * (0.88 + 0.08 * sin(t * 18.0));
    }
    else if (shape == 3)
    {
        float spiral = sin(angle * 5.0 + r * 19.5 + t * 5.2 + sin(angle * 3.0 - t * 2.4));
        float currents = smoothstep(0.0, 0.95, spiral) * (1.0 - smoothstep(0.52, 1.0, r));
        float rings = band(r - 0.89, 0.032) * (0.46 + 0.54 * sin(angle * 8.0 + t * 4.0));
        color = mix(Ember, Gold, clamp(currents * 0.88 + rings * 0.24, 0.0, 1.0));
        alpha = clamp(currents * 0.70 + rings * 0.38 + exp(-5.0 * r) * 0.35, 0.0, 1.0);
    }
    else if (shape == 4)
    {
        float h = vUv.y;
        float a = vUv.x * TAU;
        float inner = u._FireShape.y;
        float curl = sin(a * 4.0 - h * 23.0 + t * 4.2 + sin(a * 6.0 + h * 8.0 - t * 3.2));
        float wisp = sin(a * 7.0 - h * 38.0 + t * 6.8);
        float sheet = smoothstep(-0.08, 0.93, curl * 0.78 + wisp * 0.22);
        float rim = smoothstep(0.0, 0.14, h) * (1.0 - smoothstep(0.68, 1.0, h));
        color = mix(Red, mix(Gold, Core, inner * 0.68), sheet);
        alpha = rim * sheet * (0.40 + 0.33 * f);
    }
    else if (shape == 5)
    {
        float spin = 0.58 + 0.40 * abs(cos(t * 4.0));
        vec2 q = p / vec2(spin, 1.0);
        float diamond = abs(q.x) * 1.13 + abs(q.y) * 1.04;
        float crystal = 1.0 - smoothstep(0.87, 0.91, diamond);
        float facet = clamp(0.46 + 0.34 * sign(q.x + 0.12 * q.y) * sin(t * 3.6) + 0.20 * q.y, 0.0, 1.0);
        float rim = band(diamond - 0.88, 0.064);
        float facetRidge = band(q.x + 0.12 * q.y, 0.055) + 0.58 * band(q.y - abs(q.x) * 0.28, 0.044);
        color = mix(Red, Gold, facet);
        color = mix(color, Core, clamp(rim + 0.54 * facetRidge * crystal, 0.0, 1.0));
        alpha = (crystal * 0.86 + rim * 0.20 + exp(-4.9 * diamond) * 0.54) * mix(0.45, 1.0, f);
    }
    else if (shape == 6)
    {
        float pulse = band(r - mix(0.06, 1.0, f), 0.085 - 0.051 * f);
        float core = exp(-r * (6.0 + f * 14.0)) * (1.0 - f);
        float rays = pow(max(0.0, cos(angle * 8.0 + t)), 16.0) * exp(-r * 4.0) * (1.0 - f);
        color = mix(Orange, Core, clamp(0.42 * pulse + core * 1.35 + rays * 0.55, 0.0, 1.0));
        alpha = clamp(pulse * 0.60 + core + rays * 0.75, 0.0, 1.0);
    }
    else if (shape == 7)
    {
        float longitudinal = (p.y + 1.0) * 0.5;
        float width = mix(0.063, 0.73, longitudinal * longitudinal);
        float drift = sin(t * 19.0 - p.y * 17.0) * width * 0.22;
        float x = abs(p.x - drift);
        float flame = exp(-x * x / max(0.0001, width * width) * 4.8);
        float air = band(x - width * 0.76, 0.085) * (0.48 + 0.32 * sin(p.y * 29.0 - t * 24.0));
        float light = exp(-x * x / max(0.0001, width * width) * 28.0);
        color = mix(Red, Core, light * 0.92);
        alpha = clamp(flame + air * 0.54, 0.0, 1.0) * smoothstep(0.0, 0.52, longitudinal)
            * (1.0 - smoothstep(0.87, 1.0, longitudinal));
    }
    else if (shape == 9)
    {
        float drift = sin(angle * 9.0 - t * 2.4 + sin(angle * 5.0 + t * 1.3));
        float tongue = pow(0.5 + 0.5 * sin(angle * 21.0 + r * 21.0 - t * 5.0 + drift), 2.0);
        float edge = band(r - 0.96 + 0.065 * drift, 0.055 + 0.067 * tongue);
        float heat = pow(0.5 + 0.5 * sin(angle * 4.0 + r * 19.0 - t * 2.6 + drift), 3.0);
        float disk = (1.0 - smoothstep(0.89, 1.0, r)) * smoothstep(0.10, 0.48, r);
        color = mix(Red, Gold, clamp(heat * 0.63 + tongue * 0.43, 0.0, 1.0));
        alpha = disk * (0.075 + heat * 0.15) + edge * (0.12 + tongue * 0.32);
        alpha *= 1.0 - smoothstep(0.975, 1.0, r);
    }
    else if (shape == 10)
    {
        float neck = abs(p.x) - (0.09 + abs(p.y) * 0.70);
        float ends = band(abs(p.y) - 0.76, 0.085) * (1.0 - smoothstep(0.60, 0.70, abs(p.x)));
        float rails = band(neck, 0.085) * (1.0 - smoothstep(0.73, 0.81, abs(p.y)));
        float inside = (1.0 - smoothstep(-0.08, 0.0, neck)) * (1.0 - smoothstep(0.65, 0.76, abs(p.y)));
        float sand = flecks(vec2(p.x * 3.0, p.y * 5.0 + t * 3.0)) * inside;
        float flow = band(p.x, 0.095) * band(fract(p.y * 4.0 + t * 2.0) - 0.5, 0.21) * inside;
        color = mix(Violet, Lilac, clamp(ends + rails + sand + flow, 0.0, 1.0));
        alpha = ends + rails + inside * 0.24 + sand * 0.70 + flow * 0.70;
    }
    else if (shape == 11)
    {
        vec2 uv = vec2(vUv.x, 1.0 - vUv.y);
        vec4 sword = texture(_MainTex, uv);
        float charge = u._FireShape.x;
        float rise = 1.0 - smoothstep(f, f + 0.10, (vUv.y - 0.19) / 0.78);
        rise *= smoothstep(0.0, 0.12, f) * smoothstep(0.18, 0.24, vUv.y);
        float drift = sin(p.y * 41.0 - t * (16.0 + 14.0 * charge));
        float reach = 0.013 + 0.010 * drift + charge * 0.012;
        float spread = max(texture(_MainTex, uv + vec2(reach, 0.0)).a,
                           texture(_MainTex, uv - vec2(reach, 0.0)).a);
        float edge = max(0.0, spread - sword.a) * rise;
        float pulse = band(fract(vUv.y * 2.7 - t * (1.5 + charge * 0.9)) - 0.5, 0.13);
        float spark = flecks(vec2(p.x * 24.0, p.y * 13.0 - t * 7.0)) * rise;
        spark *= band(abs(p.x) - 0.09 - 0.028 * drift, 0.11);
        float tip = band(p.y - 0.94, 0.09) * band(p.x, 0.065) * rise * charge;
        color = mix(sword.rgb, mix(Violet, Lilac, clamp(pulse * 0.72 + spark + tip, 0.0, 1.0)),
                    clamp(rise * sword.a * (0.61 + 0.22 * drift) + edge + spark + tip, 0.0, 1.0));
        alpha = max(sword.a, edge * (0.62 + charge * 0.36) + spark * 0.64 + tip * 0.85);
    }
    else if (shape == 12)
    {
        float d = p.y * u._FireShape.y - u._FireShape.z + u._FireShape.x * p.x * p.x;
        float taper = 1.0 - smoothstep(0.65, 1.0, abs(p.x));
        float core = band(d, 0.067 * (0.4 + 0.6 * taper));
        float flow = sin(p.x * 31.0 - t * 23.0 + sin(p.x * 13.0 + t * 9.0));
        float rim = band(d + 0.06 + flow * 0.019, 0.125) * (0.63 + 0.34 * flow);
        float ghost = 0.35 * band(d + 0.24, 0.075) + 0.15 * band(d + 0.41, 0.071);
        float sand = flecks(vec2(p.x * 15.0 - t * 5.0, d * 22.0 + t * 3.0)) * band(d + 0.07, 0.19);
        color = mix(Violet, Lilac, clamp(core + sand * 0.82, 0.0, 1.0));
        alpha = taper * (core + rim * 0.67 + ghost * (1.0 - 0.55 * f) + sand * 0.58);
    }
    else
    {
        float feather = abs(p.x) * (1.1 + 0.48 * p.y) + abs(p.y) * 0.81;
        float vein = band(p.x + p.y * 0.16, 0.21);
        color = mix(Orange, Core, vein);
        alpha = (1.0 - smoothstep(0.31, 1.0, feather)) * vParticle;
        if (f == 5.0 && mod(vCell.x, 3.0) == 0.0)
        {
            color = mix(Gold, Core, band(r, 0.55));
            alpha = band(r, 0.82) * vParticle;
        }
    }

    alpha = clamp(alpha * u._FireParams.w, 0.0, 1.0);
    outColor = vec4(color * u._BloomTint.rgb * u._BloomIntensity * alpha, alpha);
}
