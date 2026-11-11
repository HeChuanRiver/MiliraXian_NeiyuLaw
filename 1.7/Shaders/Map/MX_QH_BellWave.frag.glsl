#version 450

// Flower Bell note: the shock wave, computed per pixel.
// vUv is the map offset from the wave centre in cells (x east, y north on screen). Height above
// the ground is drawn as a plain shift up the screen, so a pixel's line of sight is the column
// x = X with z + h = Y. The curtain lies on a flattened hemisphere x^2 + z^2 + (h/k)^2 = r^2 and
// is drawn from the ground up to c*k*r, open above. Its light is the length of the sight line
// inside a thin shell (faint face-on, bright where it grazes), fading with height; the far wall
// is dimmer. The ground ring (the damage reach) is a bright line on top. Additive blend.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // Set by ScreenBloom for its own submission only.
    vec4 _BloomTint; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // Season colour of the curtain and of the ring's halo.
    vec4 _Glow;      // @color @default = vec4(1.0, 0.62, 0.8, 1.0)
    // Colour of the ring's core (whiter).
    vec4 _Core;      // @color @default = vec4(1.0, 0.9, 0.95, 1.0)
    // x: radius (cells), y: k (dome height / radius), z: c (share of the dome height drawn), w: brightness
    vec4 _Wave;      // @default = vec4(1.0, 0.5, 0.6, 0.0)
    // x: shell thickness (cells), y: cap of the grazing brightening, z: far wall share, w: face-on gain
    vec4 _Shell;     // @default = vec4(0.05, 4.0, 0.5, 0.35)
    // x: ring core sigma (cells), y: core peak, z: halo sigma (cells), w: halo peak
    vec4 _Foot;      // @default = vec4(0.02, 0.55, 0.06, 0.165)
    // Power of the fade with height (1 = linear to the curtain top).
    float _FadePow;  // @default = 1.0
    float _BloomIntensity; // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

// Sight line x = X, z + h = Y against the dome of radius rho. z1: where it enters (nearest the
// viewer, highest), z2u: where it leaves, z2: the same cut at the ground (h = 0 <=> z = Y).
bool chords(float X, float Y, float rho, float k, out float z1, out float z2u, out float z2)
{
    float k2 = k * k;
    float A = 1.0 + 1.0 / k2;
    float B = -2.0 * Y / k2;
    float C = X * X + Y * Y / k2 - rho * rho;
    float D = B * B - 4.0 * A * C;
    float sq = sqrt(max(D, 0.0));
    z1 = (-B - sq) / (2.0 * A);
    z2u = (-B + sq) / (2.0 * A);
    z2 = min(z2u, Y);
    return rho > 0.0 && D > 0.0 && z1 < Y;
}

// Length (in z) of one wall crossing, and its fade with height (taken at the middle of the crossing).
float wall(float z0, float z1, float Y, float hc, out float w)
{
    float zEnd = min(z1, Y);
    float h = Y - 0.5 * (z0 + zEnd);
    w = pow(clamp(1.0 - h / hc, 0.0, 1.0), u._FadePow);
    return max(zEnd - z0, 0.0);
}

void main()
{
    float r = u._Wave.x;
    float k = max(u._Wave.y, 0.05);
    float hc = max(u._Wave.z * k * r, 1e-4);
    float bright = u._Wave.w;
    float T = max(u._Shell.x, 1e-4);
    float X = vUv.x;
    float Y = vUv.y;

    vec3 col = vec3(0.0);
    float a1, a2u, a2, b1, b2u, b2;
    bool vo = chords(X, Y, r, k, a1, a2u, a2);
    if (vo)
    {
        bool vi = chords(X, Y, max(r - T, 0.0), k, b1, b2u, b2);
        // near wall: entering the outer surface to entering the inner one (or leaving)
        float f1 = vi ? b1 : a2;
        // far wall: leaving the inner surface to leaving the outer one
        float k0 = vi ? b2u : a2;
        float fw, bw;
        float fl = wall(a1, f1, Y, hc, fw);
        float bl = wall(k0, a2, Y, hc, bw);
        // The sight line runs sqrt(2) per unit of z; brightness is relative to a face-on crossing.
        float s2 = 1.41421356 / T;
        float em = min(fl * s2, u._Shell.y) * fw + u._Shell.z * min(bl * s2, u._Shell.y) * bw;
        col += u._Glow.rgb * em * u._Shell.w * bright;
    }

    // the ground ring: a bright core and a season halo
    float d = abs(length(vUv) - r);
    float core = exp(-0.5 * (d * d) / max(u._Foot.x * u._Foot.x, 1e-8)) * u._Foot.y;
    float halo = exp(-0.5 * (d * d) / max(u._Foot.z * u._Foot.z, 1e-8)) * u._Foot.w;
    col += (u._Core.rgb * core + u._Glow.rgb * halo) * bright;

    col *= u._Color.rgb * u._Color.a * vColor.rgb * vColor.a * u._BloomTint.rgb * u._BloomIntensity;
    outColor = vec4(max(col, vec3(0.0)), 1.0);
}
