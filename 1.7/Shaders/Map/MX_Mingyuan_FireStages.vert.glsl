#version 450
layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0.0, 0.0, 0.0, 0.0)
layout(location = 4) in vec4 inUv1; // @vertex TexCoord1 optional @default = vec4(0.0, 0.0, 0.0, 0.0)
layout(push_constant) uniform Push { mat4 modelMatrix; } push;
layout(set = 0, binding = 0) uniform VertexUniforms { mat4 ViewProj; } uVertex;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _FireParams;
    vec4 _FireShape;
    vec4 _BloomTint;
    float _BloomIntensity;
} u;
layout(location = 0) out vec2 vUv;
layout(location = 1) out vec2 vWorld;
layout(location = 2) flat out vec2 vCell;
layout(location = 3) out float vParticle;

const float TAU = 6.2831853;

void main()
{
    vec3 p = inPosition;
    vUv = inUv0.xy;
    vCell = inUv1.xy;
    vParticle = 1.0;
    int shape = int(u._FireParams.x);
    float time = u._FireParams.y;
    if (shape == 4)
    {
        float height = inUv0.y;
        float angle = inUv0.x * TAU + time * 0.76 + height * 2.4;
        float inner = u._FireShape.y;
        float radius = mix(0.20 + 0.58 * pow(height, 0.68), 0.11 + 0.22 * height, inner);
        radius *= 1.0 + 0.08 * sin(angle * 3.0 + height * 12.0 - time * 3.5);
        p = vec3(radius * sin(angle), (0.04 + 0.04 * cos(angle)) * height,
            radius * cos(angle) * 0.50 + height * u._FireShape.x);
    }
    else if (shape == 8)
    {
        float seed = inUv1.x;
        float style = u._FireParams.z;
        float jitter = fract(sin(seed * 39.871 + 5.7) * 8419.32);
        float life = fract(time * mix(0.45, 1.35, jitter) + seed * 0.618034);
        float angle = seed * 2.399963 + time * (style < 1.0 || style == 3.0 ? 3.2 : 0.35) + life * 2.8;
        float radius = mix(0.20, 0.89, jitter);
        vec2 orbit = vec2(sin(angle), cos(angle));
        vec2 position;
        if (style < 1.0) position = orbit * radius * (1.0 - life * 0.76) + vec2(0.0, life * 0.85);
        else if (style < 2.0) position = vec2(orbit.x * radius * (0.45 + life * 0.52), -life * 1.32);
        else if (style < 3.0) position = orbit * (0.90 + 0.16 * life);
        else if (style < 4.0) position = orbit * radius * (1.0 - life * 0.68) + vec2(0.0, life * 0.83);
        else if (style < 5.0) position = orbit * mix(0.12, 0.92, life);
        else position = orbit * radius * (1.0 - life * 0.24) + vec2(0.0, life * 0.48);
        float width = mix(0.009, 0.036, jitter) * (style >= 2.0 ? 0.65 : 1.0);
        if (style == 5.0 && mod(seed, 3.0) == 0.0) width *= 0.45;
        float spin = angle + life * 7.0;
        vec2 feather = vec2(p.x * width, p.z * width * (2.2 + jitter));
        feather = mat2(cos(spin), sin(spin), -sin(spin), cos(spin)) * feather;
        p = vec3(position.x + feather.x, 0.0, position.y + feather.y);
        vParticle = seed < u._FireShape.z ? sin(life * 3.14159265) * (0.36 + jitter * 0.64) : 0.0;
    }
    vec4 world = push.modelMatrix * vec4(p, 1.0);
    vWorld = world.xz;
    gl_Position = uVertex.ViewProj * world;
}
