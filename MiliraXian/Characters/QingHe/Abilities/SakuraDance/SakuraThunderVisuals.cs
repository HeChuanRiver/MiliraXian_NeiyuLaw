using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>
    /// Electricity of a dancing pawn that caught the thunder: the sky bolt that strikes the blade
    /// at the catch, sparks crawling on the charged blade, and the bolts the blade discharges while
    /// a charged cut swings. The sky bolt and the discharges stay where they were made: their
    /// geometry comes from the blade pose at their own emit time, recomputed every frame from
    /// hashed seeds, so nothing is simulated or saved.
    /// Every bolt is a thin near-white core (with bloom) over a wide, faint, coloured halo, so it
    /// reads as white-hot lightning with a yellow glow instead of a flat yellow ribbon.
    /// </summary>
    internal static class SakuraThunderVisuals
    {
        private const float SparkLife = 0.15f;
        private const int SparkColumns = 4;
        /// <summary>The sky bolt reaches at least this far above the blade, and always past the top of the screen.</summary>
        private const float MinBoltHeight = 30f;
        private const float AboveBody = 0.03f;

        private struct Bolt
        {
            public Vector3 from;
            public Vector3 to;
            public float width;
            public float alpha;
            public int seed;
            /// <summary>Map metres between zigzag points, and the largest sideways step.</summary>
            public float segment;
            public float jitter;
            public int branches;
        }

        private static readonly List<Bolt> Bolts = new();

        internal sealed class ChargedBlade
        {
            public Pawn pawn;
            public int seed;
            public Mesh sparkMesh;
            public Mesh coreMesh;
            public Mesh haloMesh;
            public Mesh glowMesh;

            public void Dispose()
            {
                foreach (Mesh m in new[] { sparkMesh, coreMesh, haloMesh, glowMesh })
                {
                    if (m != null)
                    {
                        Object.Destroy(m);
                    }
                }
            }
        }

        /// <summary>Draws one charged blade; false once the pawn no longer carries the thunder.</summary>
        public static bool Draw(ChargedBlade c, float ticks)
        {
            Pawn pawn = c.pawn;
            if (pawn == null || !pawn.Spawned || pawn.jobs?.curDriver is not JobDriver_SakuraDance driver
                || !driver.ThunderVisible(ticks, out CompProperties_AbilitySakuraDance props))
            {
                return false;
            }
            float length = pawn.equipment?.Primary?.def.GetModExtension<BladeGraphicExtension>()?.length ?? 1.1f;
            ScreenBloomProperties bloom = props.thunderBloom ?? props.bloom;
            // Motion time: the dance plays slowly for a moment after the catch.
            float danceSeconds = driver.MotionSeconds(ticks);
            Bolts.Clear();

            // Sky bolt: strikes where the blade tip was at the catch, and stays there.
            float sinceCatch = (ticks - driver.ThunderCatchTick) / 60f;
            Vector3? glowAt = null;
            float glowK = 0f;
            if (driver.ThunderCatchTick >= 0 && sinceCatch >= 0f && sinceCatch < props.thunderBoltSeconds
                && driver.TryGetBladeAt(driver.MotionSeconds(driver.ThunderCatchTick), out BladePose catchPose, out Vector3 catchBody))
            {
                catchBody.y += AboveBody;
                Vector3 tip = SakuraThunderUtility.BodyOffsetToMap(catchBody, catchPose.grip + catchPose.dir.normalized * length, catchPose.heightToScreen);
                float k = 1f - sinceCatch / Mathf.Max(0.01f, props.thunderBoltSeconds);
                // Lightning flickers: full for the first frames, then dims and flashes back.
                int frame = Mathf.FloorToInt(sinceCatch * 30f);
                float flicker = frame < 2 ? 1f : frame % 2 == 0 ? 0.85f : 0.45f;
                Bolts.Add(new Bolt
                {
                    from = new Vector3(tip.x, tip.y, Mathf.Max(tip.z + MinBoltHeight * props.heightToScreen, ScreenTopZ() + 4f)),
                    to = tip,
                    width = props.thunderBoltWidth,
                    alpha = k * flicker,
                    seed = c.seed,
                    segment = 0.7f,
                    jitter = 0.35f,
                    branches = 3,
                });
                glowAt = tip;
                glowK = k * flicker;
            }

            AddDischarges(driver, props, c.seed, length, danceSeconds);
            DrawBolts(c, props, bloom);
            if (glowAt.HasValue)
            {
                DrawGlow(ref c.glowMesh, props, bloom, glowAt.Value, props.thunderGlowSize * (0.7f + 0.3f * glowK), glowK);
            }

            if (driver.TryGetBladePose(out BladePose pose))
            {
                Vector3 body = pawn.DrawPos;
                body.y += AboveBody;
                DrawSparks(c, props, bloom, body, pose, pose.dir.normalized, length, ticks);
            }
            return true;
        }

        private static float ScreenTopZ()
        {
            Camera cam = Find.Camera;
            return cam != null ? cam.transform.position.z + cam.orthographicSize : 0f;
        }

        /// <summary>Bolts shot out of the blade by every charged cut that has a discharge, at their emit-time pose.</summary>
        private static void AddDischarges(JobDriver_SakuraDance driver, CompProperties_AbilitySakuraDance props, int seed, float length, float danceSeconds)
        {
            SakuraDanceMotion motion = driver.CurrentMotion;
            if (motion == null || driver.ThunderCatchTick < 0)
            {
                return;
            }
            float catchSeconds = driver.MotionSeconds(driver.ThunderCatchTick);
            for (int ci = 0; ci < props.cuts.Count; ci++)
            {
                SakuraCutProps cut = props.cuts[ci];
                SakuraDischargeProps d = cut.thunderDischarge;
                if (d == null)
                {
                    continue;
                }
                float start = Mathf.Round(motion.CutTime(cut, ci) * 60f) / 60f + d.delay;
                // Only cuts made with the thunder already on the blade.
                if (start < catchSeconds || danceSeconds < start || danceSeconds > start + d.duration + d.life)
                {
                    continue;
                }
                float interval = Mathf.Max(1f / 60f, d.interval);
                int buckets = Mathf.Max(1, Mathf.FloorToInt(d.duration / interval) + 1);
                for (int b = 0; b < buckets; b++)
                {
                    float emit = start + b * interval;
                    float age = danceSeconds - emit;
                    if (age < 0f || age > d.life)
                    {
                        continue;
                    }
                    // Strongest at the strike, then dying out: fewer, shorter, thinner bolts later on.
                    float strength = d.duration > 0f ? Mathf.Pow(Mathf.Clamp01(1f - (emit - start) / d.duration), d.falloff) : 1f;
                    int count = Mathf.CeilToInt(d.perBurst * strength - 1e-3f);
                    if (count <= 0)
                    {
                        continue;
                    }
                    if (!driver.TryGetBladeAt(emit, out BladePose pose, out Vector3 body)
                        || !driver.TryGetBladeAt(emit + 1f / 60f, out BladePose next, out _))
                    {
                        continue;
                    }
                    body.y += AboveBody;
                    Vector3 dir = pose.dir.normalized;
                    for (int k = 0; k < count; k++)
                    {
                        int s = Gen.HashCombineInt(Gen.HashCombineInt(seed, ci * 977 + b), k);
                        Rand.PushState(s);
                        float along = Rand.Range(0.3f, 1f);
                        Vector3 p0 = pose.grip + dir * (length * along);
                        // Swing direction of that blade point.
                        Vector3 swing = next.grip + next.dir.normalized * (length * along) - p0;
                        swing = swing.sqrMagnitude > 1e-6f ? swing.normalized : dir;
                        Vector3 outDir = Vector3.Lerp(swing, dir, d.outward).normalized;
                        Vector3 jitterDir = Rand.UnitVector3 * Mathf.Tan(d.spreadDegrees * 0.5f * Mathf.Deg2Rad);
                        outDir = (outDir + jitterDir).normalized;
                        float len = d.length.RandomInRange * Mathf.Lerp(0.35f, 1f, strength);
                        Rand.PopState();
                        float x = age / d.life;
                        int frame = Mathf.FloorToInt(age * 30f);
                        float flicker = frame == 0 ? 1f : frame % 2 == 0 ? 0.8f : 0.5f;
                        Bolts.Add(new Bolt
                        {
                            from = SakuraThunderUtility.BodyOffsetToMap(body, p0, pose.heightToScreen),
                            to = SakuraThunderUtility.BodyOffsetToMap(body, p0 + outDir * len, pose.heightToScreen),
                            width = d.width * Mathf.Lerp(0.5f, 1f, strength),
                            alpha = (1f - x * x) * flicker * Mathf.Lerp(0.5f, 1f, strength),
                            seed = s,
                            segment = Mathf.Max(0.08f, len / 7f),
                            jitter = d.jitter,
                            branches = strength > 0.5f ? d.branches : 0,
                        });
                    }
                }
            }
        }

        private static void DrawBolts(ChargedBlade c, CompProperties_AbilitySakuraDance props, ScreenBloomProperties bloom)
        {
            if (Bolts.Count == 0)
            {
                return;
            }
            Material mat = MapComponent_SakuraDanceVisuals.Material(props.thunderBoltTexPath, "MX_QH_SakuraTrailAdd");
            if (mat == null)
            {
                return;
            }
            // Halo first (wide, faint, coloured, no bloom), then the bright core on top with bloom.
            BuildBolts(props.thunderHaloWidth);
            if (!MapComponent_SakuraDanceVisuals.BuffersEmpty)
            {
                c.haloMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
                MapComponent_SakuraDanceVisuals.Upload(c.haloMesh);
                Submit(c.haloMesh, mat, props.thunderColor, props.thunderHaloBrightness, null);
            }
            BuildBolts(1f);
            if (!MapComponent_SakuraDanceVisuals.BuffersEmpty)
            {
                c.coreMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
                MapComponent_SakuraDanceVisuals.Upload(c.coreMesh);
                Submit(c.coreMesh, mat, props.thunderCoreColor, props.thunderCoreBrightness, bloom);
            }
        }

        private static void BuildBolts(float widthScale)
        {
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            foreach (Bolt b in Bolts)
            {
                Rand.PushState(b.seed);
                try
                {
                    AppendJagged(b.from, b.to, b.width * widthScale, b.alpha, b.segment, b.jitter, 1f);
                    for (int i = 0; i < b.branches; i++)
                    {
                        // Branch from a point on the main path, shorter and thinner, bent off to one side.
                        float s = Rand.Range(0.2f, 0.8f);
                        Vector3 root = Vector3.Lerp(b.from, b.to, s);
                        Vector3 d = b.to - b.from;
                        d.y = 0f;
                        float len = d.magnitude * Rand.Range(0.15f, 0.35f);
                        if (len < 0.05f)
                        {
                            continue;
                        }
                        float ang = Rand.Range(25f, 55f) * (Rand.Bool ? 1f : -1f);
                        Vector3 bd = Quaternion.AngleAxis(ang, Vector3.up) * d.normalized;
                        AppendJagged(root, root + bd * len, b.width * widthScale * 0.6f, b.alpha * 0.8f, b.segment * 0.7f, b.jitter, 0.4f);
                    }
                }
                finally
                {
                    Rand.PopState();
                }
            }
        }

        /// <summary>One zigzag ribbon in map space; sideways steps are a bounded random walk in map metres.</summary>
        private static void AppendJagged(Vector3 from, Vector3 to, float width, float alpha, float segment, float jitter, float endTaper)
        {
            Vector3 d = to - from;
            d.y = 0f;
            float len = d.magnitude;
            if (len < 1e-3f || alpha <= 0.01f)
            {
                return;
            }
            Vector3 along = d / len;
            Vector3 perp = new(-along.z, 0f, along.x);
            int segments = Mathf.Clamp(Mathf.CeilToInt(len / Mathf.Max(0.02f, segment)), 2, 240);
            float step = len / segments;
            float maxSide = Mathf.Max(step * 2f, jitter * Mathf.Min(len, 6f) * 0.3f);
            float lateral = 0f;
            Color col = new(1f, 1f, 1f, Mathf.Clamp01(alpha));
            for (int i = 0; i <= segments; i++)
            {
                float s = (float)i / segments;
                if (i > 0 && i < segments)
                {
                    lateral = Mathf.Clamp(lateral + Rand.Range(-1f, 1f) * step * jitter * 4f, -maxSide, maxSide);
                }
                else if (i == segments)
                {
                    lateral *= 0.5f;
                }
                Vector3 p = Vector3.Lerp(from, to, s) + perp * lateral;
                float w = width * Mathf.Lerp(1f, endTaper, s);
                Vector3 half = perp * (w * 0.5f);
                MapComponent_SakuraDanceVisuals.AddStripPair(p - half, p + half, s * len * 0.5f, 0f, 1f, col, i > 0);
            }
        }

        private static void DrawGlow(ref Mesh mesh, CompProperties_AbilitySakuraDance props, ScreenBloomProperties bloom, Vector3 at, float size, float alpha)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(props.thunderGlowTexPath, "MX_QH_SakuraTrailAdd");
            if (mat == null || alpha <= 0f)
            {
                return;
            }
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            MapComponent_SakuraDanceVisuals.AddQuad(at, size, 0f, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            mesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(mesh);
            Submit(mesh, mat, props.thunderColor, props.thunderGlowBrightness, null);
        }

        private static void DrawSparks(ChargedBlade c, CompProperties_AbilitySakuraDance props, ScreenBloomProperties bloom,
            Vector3 body, in BladePose pose, Vector3 dir, float length, float ticks)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(props.thunderSparkTexPath, "MX_QH_SakuraTrailAdd");
            float interval = Mathf.Max(0.01f, props.thunderSparkInterval);
            if (mat == null || props.thunderSparksPerBurst <= 0)
            {
                return;
            }
            float t = ticks / 60f;
            int now = Mathf.FloorToInt(t / interval);
            int back = Mathf.CeilToInt(SparkLife / interval);
            float cell = 1f / SparkColumns;
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            for (int b = now - back; b <= now; b++)
            {
                float age = t - b * interval;
                if (age < 0f || age > SparkLife)
                {
                    continue;
                }
                float fade = 1f - age / SparkLife;
                for (int k = 0; k < props.thunderSparksPerBurst; k++)
                {
                    Rand.PushState(Gen.HashCombineInt(Gen.HashCombineInt(c.seed, b), k));
                    float along = Rand.Range(0.15f, 1f);
                    float size = props.thunderSparkSize * Rand.Range(0.6f, 1.2f);
                    float rot = Rand.Range(0f, 360f);
                    int frame = Rand.Range(0, SparkColumns * SparkColumns);
                    Rand.PopState();
                    Vector3 at = SakuraThunderUtility.BodyOffsetToMap(body, pose.grip + dir * (length * along), pose.heightToScreen);
                    float u0 = frame % SparkColumns * cell;
                    float v1 = 1f - frame / SparkColumns * cell;
                    MapComponent_SakuraDanceVisuals.AddQuad(at, size * (0.7f + 0.3f * fade), rot,
                        new Vector2(u0, v1 - cell), new Vector2(u0 + cell, v1), new Color(1f, 1f, 1f, fade));
                }
            }
            if (MapComponent_SakuraDanceVisuals.BuffersEmpty)
            {
                return;
            }
            c.sparkMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(c.sparkMesh);
            Submit(c.sparkMesh, mat, props.thunderCoreColor, props.thunderCoreBrightness, bloom);
        }

        private static void Submit(Mesh mesh, Material mat, Color color, float brightness, ScreenBloomProperties bloom)
        {
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetColor(MapComponent_SakuraDanceVisuals.ColorId, color);
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, brightness);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AlphaScaleId, 1f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AdditiveId, 1f);
            Matrix4x4 m = Matrix4x4.identity;
            if (bloom != null)
            {
                ScreenBloom.Submit(bloom, mesh, m, mat, block);
            }
            Graphics.DrawMesh(mesh, m, mat, 0, null, 0, block);
            block.Clear();
        }
    }
}
