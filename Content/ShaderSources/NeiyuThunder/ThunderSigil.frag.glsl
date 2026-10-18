#version 450
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(0.45, 0.875, 1.0, 1.0)
    vec4 _SecondaryColor; // @color @default = vec4(0.735, 0.635, 1.0, 1.0)
    // seconds, mode, seed, quality
    vec4 _Clock; // @default = vec4(0.0, 0.0, 0.0, 1.0)
    // charge, reveal, intensity, final
    vec4 _State; // @default = vec4(0.0, 1.0, 1.0, 0.0)
} uMaterial;
const float PI = 3.14159265359;
const float TAU = 6.28318530718;
float hash(float n) { return fract(sin(n * 127.1 + 311.7) * 43758.5453); }
float stroke(float d, float w)
{
    float aa = max(fwidth(d), 0.002);
    return 1.0 - smoothstep(w, w + aa, abs(d));
}
float ring(float r, float radius, float w) { return stroke(r - radius, w); }
float segment(vec2 p, vec2 a, vec2 b, float w)
{
    vec2 d = b - a;
    return stroke(length(p - a - d * clamp(dot(p - a, d) / dot(d, d), 0.0, 1.0)), w);
}
float runes(vec2 p, float r, float a, float rotation, float radius, float count)
{
    float angle = a + rotation;
    float cell = floor((angle / TAU + 0.5) * count);
    float local = fract((angle / TAU + 0.5) * count) - 0.5;
    float h = hash(cell + floor(uMaterial._Clock.z));
    // Each angular cell has its own unequal stem, crossbar and slanted branch.
    vec2 q = vec2(local * 0.26, r - radius);
    float glyph = segment(q, vec2(-0.027, -0.045), vec2(0.015, 0.045 + h * 0.025), 0.006);
    glyph += segment(q, vec2(-0.045, h * 0.04), vec2(0.04, 0.018), 0.005);
    glyph += segment(q, vec2(0.01, -0.03), vec2(0.04, -0.015 - h * 0.02), 0.004);
    return glyph * step(0.17, h);
}
void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float r = length(p), a = atan(p.y, p.x);
    float t = uMaterial._Clock.x, mode = uMaterial._Clock.y;
    float seed = uMaterial._Clock.z;
    float quality = uMaterial._Clock.w;
    float charge = uMaterial._State.x, reveal = uMaterial._State.y;
    float intensity = uMaterial._State.z, finalHit = uMaterial._State.w;
    float speed = 0.16 + charge * 0.9 + finalHit * charge * 0.55;
    float spin = t * speed + seed;
    float core = 0.0, glow = 0.0, accents = 0.0;
    if (mode < 1.5 || (mode > 2.5 && mode < 4.5))
    {
        float construction = mode > 0.5 && mode < 1.5 ? reveal : 1.0;
        float firstBuild = mode > 0.5 && mode < 1.5 ? clamp(construction * 3.0, 0.0, 1.0) : construction;
        float angularBuild = smoothstep(0.0, 0.06, firstBuild * 1.1 - fract(a / TAU + 0.5));
        float second = smoothstep(0.333, 0.75, construction);
        float ticks = pow(max(0.0, cos((a + spin) * 24.0)), 18.0);
        // The outer edge is exactly 0.88: C# scales it to the configured radius.
        core = ring(r, 0.88, 0.004) * angularBuild;
        core += ring(r, 0.74, 0.005) * second * (0.30 + 0.70 * step(-0.35, sin(a * 9.0 - spin)));
        core += ticks * ring(r, 0.835, 0.021) * angularBuild * 0.7;
        glow = exp(-pow((r - 0.88) * 32.0, 2.0)) * 0.22 * angularBuild;
        glow += exp(-r * r * 3.5) * 0.055 * construction;
        if (quality > 0.1)
        {
            accents += runes(p, r, a, spin, 0.64, 23.0) * second * 0.75;
            float hexagon = cos(mod(a + spin + PI / 6.0, PI / 3.0) - PI / 6.0) * r;
            core += stroke(hexagon - 0.44, 0.004) * second * 0.45;
            float flow = pow(max(0.0, sin(a * 6.0 + r * 21.0 + t * 2.0)), 18.0);
            glow += flow * (1.0 - smoothstep(0.2, 0.70, r)) * 0.12 * second;
            for (int i = 0; i < 6; i++)
            {
                float phase = float(i) * TAU / 6.0 + spin * (0.8 + hash(float(i)) * 0.3);
                vec2 node = vec2(cos(phase), sin(phase)) * (0.54 + 0.04 * sin(t + float(i)));
                float dotGlow = exp(-dot(p - node, p - node) * 1800.0);
                accents += dotGlow * second * (0.6 + charge * 0.5);
            }
        }
        if (mode > 2.5)
        {
            float bolt = segment(p, vec2(0.06, 0.34), vec2(-0.16, -0.015), 0.019);
            bolt += segment(p, vec2(-0.16, -0.015), vec2(0.12, 0.035), 0.016);
            bolt += segment(p, vec2(0.12, 0.035), vec2(-0.04, -0.33), 0.017);
            core += bolt;
            float nodes = pow(max(0.0, cos(a * 3.0 + spin * 1.3)), 52.0);
            accents += nodes * ring(r, 0.90, 0.04) * (0.8 + charge);
            glow += exp(-r * r * 5.0) * charge * 0.22;
        }
        else if (mode < 0.5)
        {
            // Delicate flowing field; no opaque area fill.
            glow += (0.5 + 0.5 * sin(r * 34.0 - t * 1.6 + sin(a * 5.0))) * 0.025
                * (1.0 - smoothstep(0.65, 0.86, r));
            core *= 0.6;
        }
        else
        {
            float pull = smoothstep(0.75, 1.0, construction);
            glow += exp(-r * r * mix(7.0, 35.0, pull)) * pull * 0.25;
            accents *= 0.85 + 0.15 * sin(t * 7.0);
        }
    }
    else if (mode < 2.5)
    {
        float life = clamp(reveal, 0.0, 1.0);
        float radius = mix(0.2, 0.82, smoothstep(0.0, 0.32, life))
            * (1.0 - smoothstep(0.36, 1.0, life));
        float disturbance = sin(a * 7.0 + t * 1.8 + seed) * 0.012 + sin(a * 11.0 - t) * 0.007;
        for (int i = 0; i < 3; i++)
        {
            float d = r + disturbance - max(0.03, radius - float(i) * 0.15);
            core += exp(-d * d * 6500.0) * (1.0 - float(i) * 0.22);
            glow += exp(-d * d * 420.0) * 0.26;
        }
        glow += exp(-r * r * 8.0) * 0.11;
        intensity *= sin(life * PI);
    }
    else if (mode < 5.5)
    {
        float radius = 0.08 + reveal * 0.79;
        float jagged = sin(a * 13.0 + seed) * sin(a * 5.0 - t * 3.0) * 0.012;
        core = ring(r + jagged, radius, 0.008) * (1.0 - reveal);
        glow = exp(-pow((r - radius) * 18.0, 2.0)) * 0.28 * (1.0 - reveal);
        glow += exp(-r * r * 22.0) * exp(-reveal * 19.0) * 1.4;
        float veins = pow(max(0.0, sin(a * 17.0 + sin(r * 34.0 + seed) * 1.5)), 32.0);
        accents = veins * (1.0 - smoothstep(radius * 0.2, radius + 0.15, r))
            * exp(-reveal * 4.0) * 0.7;
    }
    else
    {
        float expansion = 0.45 + reveal * 0.43;
        float breakup = step(reveal * 0.85, hash(floor((a + PI) * 11.0) + seed));
        core = ring(r, expansion, 0.008) * breakup * (1.0 - reveal);
        accents = runes(p, r, a, t * 0.5, expansion - 0.13, 17.0) * breakup * (1.0 - reveal);
        glow = exp(-pow((r - expansion) * 16.0, 2.0)) * (1.0 - reveal) * 0.24;
    }
    float edge = 1.0 - smoothstep(0.94, 1.0, r);
    vec3 primary = uMaterial._Color.rgb;
    vec3 secondary = uMaterial._SecondaryColor.rgb;
    vec3 rgb = mix(primary, vec3(0.957, 0.98, 1.0), 0.68) * core;
    rgb += primary * glow + mix(secondary, vec3(0.95, 0.72, 0.96), 0.14) * accents;
    rgb *= intensity * uMaterial._Color.a * edge * vColor.rgb * vColor.a;
    float alpha = clamp((core * 0.28 + glow * 0.6 + accents * 0.4) * intensity
        * uMaterial._Color.a * edge * vColor.a, 0.0, 0.65);
    if (max(max(rgb.r, rgb.g), rgb.b) < 0.001) discard;
    outColor = vec4(rgb, alpha);
}
