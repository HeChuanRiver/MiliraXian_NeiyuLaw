using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>
    /// CPU petals of one cut. Emitted at equal path distance along the spinning emitter circle,
    /// they move in the ring frame with a decaying speed and a random turn every 0.12 s, then fall.
    /// All petals of a cut are one mesh, drawn once per frame from an 8 x 8 sprite atlas.
    /// </summary>
    public static class SakuraPetals
    {
        private const int AtlasColumns = 8;
        private const int AtlasFrames = 64;
        private const float TurnInterval = 0.12f;
        private const float FallAcceleration = 1.2f;

        public sealed class Petal
        {
            public float emitTime;
            public float life;
            public Vector3 pos;
            public Vector3 dir;
            public float speed0;
            public float speed1;
            public float turnDegrees;
            public float size;
            public float frame0;
            public float fall;
            public float nextTurn;
            public float age;
            public uint seed;
            public bool started;
        }

        private static readonly List<Vector3> Verts = new();
        private static readonly List<Vector2> Uvs = new();
        private static readonly List<Color> Colors = new();
        private static readonly List<int> Tris = new();

        public static void Emit(List<Petal> into, CompProperties_AbilitySakuraDance props, SakuraCutProps cut, SakuraRingFrame frame, int startTick)
        {
            into.Clear();
            if (cut.petals.NullOrEmpty())
            {
                return;
            }
            Rand.PushState(Gen.HashCombineInt(startTick, cut.GetHashCode()));
            try
            {
                float pathSpeed = cut.petalRadius * Mathf.Abs(cut.angularSpeed) * Mathf.Deg2Rad;
                // Local launch direction: mostly forward along the spin, a bit outward.
                const float launch = 157.5f * Mathf.Deg2Rad;
                Vector3 baseDir = new(Mathf.Sin(launch), 0f, Mathf.Cos(launch));
                foreach (SakuraPetalEmitter e in cut.petals)
                {
                    int n = Mathf.FloorToInt(pathSpeed * cut.spinSeconds / Mathf.Max(0.01f, e.spacing));
                    for (int k = 0; k < n; k++)
                    {
                        if (!Rand.Chance(props.petalKeepFraction))
                        {
                            continue;
                        }
                        float te = (k + Rand.Value) * e.spacing / pathSpeed;
                        float angle = (cut.petalStartAngle + cut.angularSpeed * te) * Mathf.Deg2Rad;
                        float c = Mathf.Cos(angle);
                        float s = Mathf.Sin(angle);
                        Vector3 local = new(cut.petalRadius * c, 0f, -cut.petalRadius * s);
                        Vector3 d = new(baseDir.x * c + baseDir.z * s, 0f, -baseDir.x * s + baseDir.z * c);
                        float spread = props.petalSpreadDegrees * Mathf.Deg2Rad;
                        d += new Vector3(Rand.Gaussian(0f, spread), Rand.Gaussian(0f, spread), Rand.Gaussian(0f, spread));
                        float mult = 1f + Rand.Range(-0.6f, 0.6f);
                        into.Add(new Petal
                        {
                            emitTime = te,
                            life = e.lifetime,
                            pos = frame.center + frame.LocalToWorld(local),
                            dir = frame.LocalToWorld(d).normalized,
                            speed0 = e.speed * mult * props.petalSpeedScale,
                            speed1 = e.speed * mult * props.petalSpeedScale * 0.25f,
                            turnDegrees = e.turnDegrees,
                            size = props.petalSize * (1f - 0.2f * Rand.Value),
                            frame0 = 16f + Rand.Range(-16f, 16f),
                            seed = (uint)Rand.Int,
                        });
                    }
                }
            }
            finally
            {
                Rand.PopState();
            }
        }

        internal static void StepAndDraw(MapComponent_SakuraDanceVisuals.CutVisual v, float t, float dt, Material mat)
        {
            if (v.petals.Count == 0 || mat == null)
            {
                return;
            }
            CompProperties_AbilitySakuraDance props = v.props;
            Verts.Clear();
            Uvs.Clear();
            Colors.Clear();
            Tris.Clear();
            // Brightness goes to the shader: the 8-bit vertex colour would clip a tint above 1.
            Color tint = props.petalColor;
            float cell = 1f / AtlasColumns;
            foreach (Petal p in v.petals)
            {
                float age = t - p.emitTime;
                if (age < 0f || age > p.life)
                {
                    continue;
                }
                if (!p.started)
                {
                    p.started = true;
                    p.nextTurn = TurnInterval;
                    Step(p, age);
                }
                else
                {
                    Step(p, dt);
                }

                float x = age / p.life;
                float size = p.size * (x < 0.6f ? Mathf.Lerp(1.5f, 1f, x / 0.6f) : Mathf.Lerp(1f, 0.8f, (x - 0.6f) / 0.4f));
                float fade = x < 0.8f ? 1f : 1f - (x - 0.8f) / 0.2f;
                int frame = Mathf.FloorToInt(p.frame0 + (AtlasFrames - 1) * x);
                frame = ((frame % AtlasFrames) + AtlasFrames) % AtlasFrames;
                float u0 = frame % AtlasColumns * cell;
                // Row 0 of the atlas is the top of the image; UV v grows upward.
                float v1 = 1f - frame / AtlasColumns * cell;
                float v0 = v1 - cell;

                Vector3 c = MapComponent_SakuraDanceVisuals.ToMap(v, p.pos, props.heightToScreen);
                c.y += 0.01f;
                float h = size * 0.5f;
                int b = Verts.Count;
                Verts.Add(new Vector3(c.x - h, c.y, c.z - h));
                Verts.Add(new Vector3(c.x - h, c.y, c.z + h));
                Verts.Add(new Vector3(c.x + h, c.y, c.z + h));
                Verts.Add(new Vector3(c.x + h, c.y, c.z - h));
                Uvs.Add(new Vector2(u0, v0));
                Uvs.Add(new Vector2(u0, v1));
                Uvs.Add(new Vector2(u0 + cell, v1));
                Uvs.Add(new Vector2(u0 + cell, v0));
                Color col = new(tint.r, tint.g, tint.b, fade);
                Colors.Add(col);
                Colors.Add(col);
                Colors.Add(col);
                Colors.Add(col);
                Tris.Add(b);
                Tris.Add(b + 1);
                Tris.Add(b + 2);
                Tris.Add(b);
                Tris.Add(b + 2);
                Tris.Add(b + 3);
            }
            if (Verts.Count == 0)
            {
                return;
            }
            Mesh mesh = v.petalMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            mesh.Clear();
            mesh.SetVertices(Verts);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, props.petalBrightness);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, block);
        }

        /// <summary>Advances one petal by <paramref name="seconds"/> in 1/60 s sub-steps.</summary>
        private static void Step(Petal p, float seconds)
        {
            const float sub = 1f / 60f;
            while (seconds > 1e-5f)
            {
                float dt = Mathf.Min(sub, seconds);
                seconds -= dt;
                p.fall += FallAcceleration * dt;
                p.age += dt;
                float x = Mathf.Clamp01(p.age / p.life);
                float sp = p.speed0 + (p.speed1 - p.speed0) * Mathf.Sin(x * Mathf.PI * 0.5f);
                p.pos += p.dir * (sp * dt) + Vector3.down * (p.fall * dt);
                p.nextTurn -= dt;
                if (p.nextTurn <= 0f)
                {
                    p.nextTurn += TurnInterval;
                    p.seed = p.seed * 1664525u + 1013904223u;
                    Rand.PushState((int)p.seed);
                    Vector3 axis = Rand.UnitVector3;
                    float ang = Rand.Range(0f, p.turnDegrees);
                    Rand.PopState();
                    p.dir = Quaternion.AngleAxis(ang, axis) * p.dir;
                }
            }
            if (p.pos.y < 0f)
            {
                p.pos.y = 0f;
            }
        }
    }
}
