using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.QingHe.Abilities;
using MiliraXian.Characters.QingHe.Abilities.SakuraDance;
using MiliraXian.Characters.QingHe.Jobs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Vfx
{
    /// <summary>
    /// Visual settings of Mirror Flower, Water Moon. Every number copies a constant of the offline
    /// preview (.cache/mirror_proto/mirror_preview.py); change the script first, then copy here.
    /// </summary>
    public class MirrorVisualProps
    {
        // Projection (the game's blade projection, as the preview).
        public float heightToScreen = 0.35f;
        public float heightOrigin = -0.63f;
        public float depthToScreen = 0.55f;

        public Color pink = new(1f, 0.62f, 0.80f, 1f);
        public Color white = new(1f, 0.95f, 0.98f, 1f);

        // Water (WATER_R; CENTER_Z = the soles, cells below the drawn body centre).
        public float waterRadius = 1.6f;
        public float soleOffset = -0.62f;
        public float waterBrightness = 1f;

        // Body (BODY_FADE) and reflection (alpha 0.32, squash 0.8). She does not move up or down (owner, 11-08).
        public float bodyFadeSeconds = 0.25f;
        public float reflectionAlpha = 0.32f;
        public float reflectionSquash = 0.8f;
        /// <summary>The reflection's own sway in the still water (0..1 of the shatter's disturbance).</summary>
        public float reflectionSway = 0.18f;

        /// <summary>Moon and moon halo brightness (the preview's 1).</summary>
        public float moonBrightness = 2.2f;
        /// <summary>Share of the dim pink surface light that joins the bloom (the moon, glints and shards always do).</summary>
        public float waterBloomPinkShare = 0.15f;
        /// <summary>Halo of the water (moon, glints, ripples, shards) against its own light; the water itself is unchanged.</summary>
        public float waterBloomScale = 0.5f;

        // Shatter: flash, droplets.
        public float flashSeconds = 0.03f;
        public float flashBrightness = 0.5f;
        public int dropCount = 30;
        public float dropSize = 0.06f;

        // Cut (CUT_R_IN, CUT_R_OUT, CUT_TILT, LIGHT_LIFE, DISTORT_LIFE).
        public float cutInnerRadius = 2.9f;
        public float cutOuterRadius = 4.5f;
        public float cutTilt = 0.9f;
        public float lightLife = 0.2f;
        public float distortLife = 1f;
        /// <summary>Even pale band across the ring (soft at both edges).</summary>
        public string bandTexPath = "MiliraXianQinghe/Effect/SakuraDance/DistortMask";
        /// <summary>Round soft spot: the droplets and the hit flash.</summary>
        public string coreTexPath = "MiliraXianQinghe/Effect/SakuraDance/Glow";
        /// <summary>Soft band across its width, even along it: the bright outer line of the trail.</summary>
        public string lineTexPath = "MiliraXianQinghe/Effect/SakuraDance/DistortMask";
        public string distortTexPath = "MiliraXianQinghe/Effect/SakuraDance/DistortMask";
        public string distortNormalTexPath = "MiliraXianQinghe/Effect/SakuraDance/DistortNormal";
        // The preview adds the band at about 0.32 to 0.7 (with its glow) and the line at 1.1; bloom comes on top in the game.
        public float bandBrightness = 0.5f;
        public float coreBrightness = 1.1f;
        public float coreWidth = 0.08f;
        public float distortionMetresPerUnit = 0.04f;
        public float distortionIntensity = 2.5f;
        /// <summary>One bloom group for the whole skill: water, body sheen, droplets, flash and trail.</summary>
        public ScreenBloomProperties bloom;
        /// <summary>Colour layers of the trail (offsets from the middle of the band); empty = the built-in set.</summary>
        public List<SakuraTrailLayer> trailLayers = new();
    }

    /// <summary>
    /// Draws Mirror Flower, Water Moon from the stance's phase and phase time. Not saved: every
    /// frame is computed from the job driver's saved state, so a load rebuilds it. Snapshots of the
    /// body (the afterimage capture) stand in for her while she shatters and re-forms; the
    /// reflection is a snapshot too. At most two render textures per stance, released at once.
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class MapComponent_MirrorVisuals : MapComponent
    {
        private const int SnapshotSize = 512;
        private const float SnapshotZoom = 0.5f;
        /// <summary>Map size of the snapshot (it covers 2 / zoom cells).</summary>
        private const float SnapshotCells = 2f / SnapshotZoom;
        private const int Strips = 96;
        private const int WaterSegments = 48;

        private static readonly int WaterId = Shader.PropertyToID("_Water");
        private static readonly int Water2Id = Shader.PropertyToID("_Water2");
        private static readonly int PinkId = Shader.PropertyToID("_Pink");
        private static readonly int WhiteId = Shader.PropertyToID("_White");
        private static readonly int SheenId = Shader.PropertyToID("_Sheen");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int WaveId = Shader.PropertyToID("_Wave");
        private static readonly int GlowOnlyId = Shader.PropertyToID("_GlowOnly");

        private static Material waterMat;
        private static Material spriteMat;
        private static bool warned;
        private static readonly MirrorVisualProps DefaultProps = new();

        private readonly List<Instance> instances = new();

        public MapComponent_MirrorVisuals(Map map) : base(map)
        {
        }

        /// <summary>Starts drawing a pawn's stance (called every tick of the stance; cheap when already tracked).</summary>
        public void Track(Pawn pawn)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i].pawn == pawn)
                {
                    return;
                }
            }
            instances.Add(new Instance { pawn = pawn, seed = pawn.thingIDNumber * 7919 });
        }

        /// <summary>The real body of <paramref name="pawn"/> is not drawn now (the snapshot is).</summary>
        public static bool HidesBody(Pawn pawn)
        {
            return pawn?.jobs?.curDriver is JobDriver_IllusoryReflectionStance { BodyHidden: true };
        }

        public override void MapComponentUpdate()
        {
            // Snapshots are taken outside the map drawing pass (the pawn cache camera renders on its own).
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                Instance inst = instances[i];
                if (!Valid(inst, out JobDriver_IllusoryReflectionStance driver))
                {
                    // The trail is drawn by the strip renderer and fades on its own.
                    inst.Release();
                    instances.RemoveAt(i);
                    continue;
                }
                try
                {
                    Capture(inst, driver);
                }
                catch (System.Exception ex)
                {
                    Warn(ex);
                }
            }
        }

        public override void MapComponentDraw()
        {
            if (instances.Count == 0 || Find.CurrentMap != map)
            {
                return;
            }
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                Instance inst = instances[i];
                try
                {
                    if (Valid(inst, out JobDriver_IllusoryReflectionStance driver) && !inst.pawn.IsHiddenFromPlayer())
                    {
                        Draw(inst, driver);
                    }
                }
                catch (System.Exception ex)
                {
                    Warn(ex);
                    inst.Release();
                    instances.RemoveAt(i);
                }
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            foreach (Instance inst in instances)
            {
                inst.Release();
            }
            instances.Clear();
        }

        private static bool Valid(Instance inst, out JobDriver_IllusoryReflectionStance driver)
        {
            driver = inst.pawn is { Spawned: true, Dead: false } p ? p.jobs?.curDriver as JobDriver_IllusoryReflectionStance : null;
            if (driver != null && inst.driver != driver)
            {
                // A new cast by the same pawn: start clean.
                inst.Release();
                inst.driver = driver;
            }
            return driver != null && driver.Phase != MirrorPhase.Done;
        }

        private static void Warn(System.Exception ex)
        {
            if (!warned)
            {
                warned = true;
                Log.Error("[MiliraXian] Illusory Reflection visuals failed: " + ex);
            }
        }

        private static MirrorVisualProps PropsOf(JobDriver_IllusoryReflectionStance driver)
        {
            return driver.Properties?.visuals ?? DefaultProps;
        }

        // ---------------------------------------------------------------- snapshots

        private static void Capture(Instance inst, JobDriver_IllusoryReflectionStance driver)
        {
            Pawn pawn = inst.pawn;
            Rot4 facing = driver.StanceFacing;
            switch (driver.Phase)
            {
                case MirrorPhase.Windup:
                case MirrorPhase.Stance:
                case MirrorPhase.Exit:
                    // One snapshot serves the reflection while she waits and, frozen, the body that
                    // shatters (the frame before the hit). She stands still: every few frames is enough.
                    if (inst.body == null || Time.frameCount - inst.bodyFrame >= 4)
                    {
                        Snap(ref inst.body, pawn, facing);
                        inst.bodyFrame = Time.frameCount;
                    }
                    inst.reformTaken = false;
                    break;
                case MirrorPhase.Recover:
                case MirrorPhase.ReformDelay:
                    if (!inst.reformTaken && driver.Phase == MirrorPhase.Recover)
                    {
                        // She takes shape again as she stands now (the weapon still hidden).
                        Snap(ref inst.body, pawn, facing);
                        inst.reformTaken = true;
                    }
                    break;
            }
        }

        private static void Snap(ref RenderTexture rt, Pawn pawn, Rot4 facing)
        {
            if (pawn?.Drawer?.renderer == null || Find.PawnCacheRenderer == null)
            {
                return;
            }
            if (rt == null)
            {
                rt = RenderTexture.GetTemporary(SnapshotSize, SnapshotSize, 24, RenderTextureFormat.ARGB32);
                rt.name = "MX_QH_MirrorSnapshot";
            }
            snapping = true;
            try
            {
                Find.PawnCacheRenderer.RenderPawn(pawn, rt, Vector3.zero, SnapshotZoom, 0f, facing,
                    renderHead: true, renderHeadgear: true, renderClothes: true);
            }
            finally
            {
                snapping = false;
            }
        }

        /// <summary>A snapshot is being taken: the body must be drawn whatever the phase.</summary>
        internal static bool snapping;

        // ---------------------------------------------------------------- drawing

        private void Draw(Instance inst, JobDriver_IllusoryReflectionStance driver)
        {
            MirrorVisualProps vp = PropsOf(driver);
            CompProperties_AbilityIllusoryReflection props = driver.Properties;
            Pawn pawn = inst.pawn;
            // The drawn body centre on the ground (no motion plays during the stance).
            Vector3 center = pawn.DrawPos;
            Vector3 feet = new(center.x, 0f, center.z + vp.soleOffset);
            float waterT = VisualClock.TicksSince(driver.StanceStartTick) / 60f;
            float s = driver.PhaseSeconds;
            float windup = (props?.windupTicks ?? 30) / 60f;
            float shatter = (props?.shatterTicks ?? 30) / 60f;
            float exit = (props?.exitTicks ?? 30) / 60f;
            float reform = driver.ReformSeconds;
            float alt = AltitudeLayer.Pawn.AltitudeFor();

            switch (driver.Phase)
            {
                case MirrorPhase.Windup:
                {
                    float grow = JobDriver_IllusoryReflectionStance.Smooth(s / Mathf.Max(1e-3f, windup));
                    DrawWater(vp, feet, waterT, grow, -1f, alt);
                    DrawReflection(inst, vp, feet, center, waterT, vp.reflectionAlpha * grow, 0f, alt);
                    break;
                }
                case MirrorPhase.Stance:
                    DrawWater(vp, feet, waterT, 1f, -1f, alt);
                    DrawReflection(inst, vp, feet, center, waterT, vp.reflectionAlpha, 0f, alt);
                    break;
                case MirrorPhase.Exit:
                {
                    float k = Mathf.Clamp01(s / Mathf.Max(1e-3f, exit));
                    DrawWater(vp, feet, waterT, 1f - k, -1f, alt);
                    DrawReflection(inst, vp, feet, center, waterT, vp.reflectionAlpha * (1f - k), 0f, alt);
                    break;
                }
                case MirrorPhase.Shatter:
                {
                    float p = Mathf.Clamp01(s / Mathf.Max(1e-3f, shatter));
                    float pb = Mathf.Clamp01(s / Mathf.Max(1e-3f, vp.bodyFadeSeconds));
                    DrawWater(vp, feet, waterT, 1f, p, alt);
                    DrawDrops(inst, vp, feet, Mathf.Min(1f, p * 2.5f), alt);
                    if (pb < 1f)
                    {
                        DrawReflection(inst, vp, feet, center, waterT, vp.reflectionAlpha * Mathf.Max(0f, 1f - pb * 1.6f), pb, alt);
                        DrawBody(inst, vp, center, waterT, Mathf.Pow(pb, 0.7f), Mathf.Max(0f, 1f - Mathf.Pow(pb, 1.5f)), 11, alt);
                    }
                    float f = s / Mathf.Max(1e-3f, vp.flashSeconds);
                    DrawFlash(vp, center + new Vector3(0f, 0f, 0.1f), vp.flashBrightness * Mathf.Exp(-f * f), alt);
                    break;
                }
                case MirrorPhase.Recover:
                {
                    float q = s / Mathf.Max(1e-3f, reform);
                    if (q < 1f)
                    {
                        float e = JobDriver_IllusoryReflectionStance.Smooth(q);
                        DrawBody(inst, vp, center, waterT, (1f - e) * 0.9f, e, 5, alt);
                    }
                    break;
                }
            }

            if (driver.CutStartTick >= 0 && !inst.trailSpawned)
            {
                // Handed to the Sakura Dance strip renderer, which draws it and drops it when it has faded
                // (also after the job has ended).
                inst.trailSpawned = true;
                MirrorTrailSource.Spawn(map, vp, props, driver);
            }
        }

        private static readonly List<Vector3> Verts = new();
        private static readonly List<Vector2> Uvs = new();
        private static readonly List<Color> Colors = new();
        private static readonly List<int> Tris = new();

        private static void Upload(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(Verts);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();
        }

        private static void ClearBuffers()
        {
            Verts.Clear();
            Uvs.Clear();
            Colors.Clear();
            Tris.Clear();
        }

        private static Mesh waterMesh;

        /// <summary>The water disc: a fan over the disc radius, UV = ground offset in cells.</summary>
        private static void DrawWater(MirrorVisualProps vp, Vector3 feet, float t, float strength, float shatterP, float alt)
        {
            if (strength <= 0.002f)
            {
                return;
            }
            Material mat = WaterMaterial;
            if (mat == null)
            {
                return;
            }
            if (waterMesh == null)
            {
                waterMesh = NewMesh("MX_QH_MirrorWater");
            }
            ClearBuffers();
            // A little past the radius: the shards drift out and the crest runs past the edge.
            float r = vp.waterRadius * 1.5f;
            float y = alt - 0.03f;
            Verts.Add(new Vector3(feet.x, y, feet.z));
            Uvs.Add(Vector2.zero);
            Colors.Add(Color.white);
            for (int i = 0; i <= WaterSegments; i++)
            {
                float a = i * Mathf.PI * 2f / WaterSegments;
                float gx = Mathf.Cos(a) * r;
                float gz = Mathf.Sin(a) * r;
                Verts.Add(new Vector3(feet.x + gx, y, feet.z + gz * vp.depthToScreen));
                Uvs.Add(new Vector2(gx, gz));
                Colors.Add(Color.white);
                if (i > 0)
                {
                    Tris.Add(0);
                    Tris.Add(i + 1);
                    Tris.Add(i);
                }
            }
            Upload(waterMesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetVector(WaterId, new Vector4(t, Mathf.Clamp01(strength), shatterP, vp.waterRadius));
            block.SetVector(PinkId, vp.pink);
            block.SetVector(WhiteId, vp.white);
            if (vp.bloom != null)
            {
                // The halo comes from the moon, the glints and the shards; the dim pink surface mostly stays out.
                block.SetVector(Water2Id, new Vector4(vp.waterBrightness * vp.waterBloomScale, vp.moonBrightness, vp.waterBloomPinkShare, 0f));
                ScreenBloom.Submit(vp.bloom, waterMesh, Matrix4x4.identity, mat, block);
            }
            block.SetVector(Water2Id, new Vector4(vp.waterBrightness, vp.moonBrightness, 1f, 0f));
            Graphics.DrawMesh(waterMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        /// <summary>
        /// The snapshot on one quad. The shader moves every pixel by a smooth flowing field (the
        /// preview's row waves, continuous, plus a soft tearing noise), scaled by <paramref name="k"/>,
        /// with the pink sheen; <paramref name="alpha"/> fades it.
        /// </summary>
        private static void DrawBody(Instance inst, MirrorVisualProps vp, Vector3 center, float t, float k, float alpha, int seed, float alt)
        {
            if (inst.body == null || alpha <= 0.002f)
            {
                return;
            }
            Material mat = SpriteMaterial;
            if (mat == null)
            {
                return;
            }
            inst.bodyMesh ??= NewMesh("MX_QH_MirrorBody");
            ClearBuffers();
            float half = SnapshotCells * 0.5f;
            AddStrip(center.x - half, center.x + half, center.z - half, center.z + half, alt + 0.02f, 0f, 1f, Color.white, Color.white);
            Upload(inst.bodyMesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetTexture(MainTexId, inst.body);
            block.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha));
            block.SetVector(SheenId, new Vector4(1f, 170f / 255f, 210f / 255f, k * 0.5f));
            block.SetVector(WaveId, new Vector4(t, k, seed, 1f));
            if (vp.bloom != null)
            {
                block.SetFloat(GlowOnlyId, 1f);
                ScreenBloom.Submit(vp.bloom, inst.bodyMesh, Matrix4x4.identity, mat, block);
                block.SetFloat(GlowOnlyId, 0f);
            }
            Graphics.DrawMesh(inst.bodyMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        /// <summary>
        /// Her mirror image: the snapshot flipped about her soles, squashed, each row swayed by the
        /// waves (more and dimmer further from her feet), half pink, only inside the water.
        /// </summary>
        private static void DrawReflection(Instance inst, MirrorVisualProps vp, Vector3 feet, Vector3 center, float t, float alpha,
            float scatter, float alt)
        {
            if (inst.body == null || alpha <= 0.002f)
            {
                return;
            }
            Material mat = SpriteMaterial;
            if (mat == null)
            {
                return;
            }
            inst.reflectionMesh ??= NewMesh("MX_QH_MirrorReflection");
            ClearBuffers();
            float half = SnapshotCells * 0.5f;
            float rowH = SnapshotCells / Strips;
            float soleZ = feet.z;
            // Snapshot v of her soles (the picture's centre is the drawn body centre).
            float soleV = 0.5f + (soleZ - center.z) / SnapshotCells;
            float y = alt - 0.02f;
            float rimZ = vp.waterRadius * vp.depthToScreen;
            for (int i = 0; i < Strips; i++)
            {
                // Mirror row i goes down from the soles; it shows the picture row above the soles.
                float d0 = i * rowH * vp.reflectionSquash;
                float d1 = (i + 1) * rowH * vp.reflectionSquash;
                float vTop = soleV + i * rowH / SnapshotCells;
                float vBottom = soleV + (i + 1) * rowH / SnapshotCells;
                if (vTop >= 1f)
                {
                    break;
                }
                // The rows only carry the flip and the fade; the sway is in the shader (smooth, no steps).
                // Dims away from her feet, and fades out at the water's rim.
                float fade0 = Fade(d0, rimZ, vp);
                float fade1 = Fade(d1, rimZ, vp);
                if (fade0 <= 0.001f && fade1 <= 0.001f)
                {
                    continue;
                }
                AddStrip(center.x - half, center.x + half, soleZ - d1, soleZ - d0, y,
                    Mathf.Min(vBottom, 1f), vTop, new Color(1f, 1f, 1f, fade1), new Color(1f, 1f, 1f, fade0));
            }
            if (Verts.Count == 0)
            {
                return;
            }
            Upload(inst.reflectionMesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetTexture(MainTexId, inst.body);
            block.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha));
            block.SetVector(SheenId, new Vector4(1f, 175f / 255f, 215f / 255f, 0.45f));
            // The water's own sway: always a little (slower, wider rows), more as it shatters.
            block.SetVector(WaveId, new Vector4(t * 0.2f, Mathf.Max(vp.reflectionSway, scatter), 23f, 2f));
            Graphics.DrawMesh(inst.reflectionMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        private static float Fade(float depthMap, float rimZ, MirrorVisualProps vp)
        {
            float away = 1f - JobDriver_IllusoryReflectionStance.Smooth(depthMap / 1.1f);
            // Ground distance of the row below the soles (map z / depth factor) against the disc's soft rim.
            float g = depthMap / Mathf.Max(1e-3f, vp.depthToScreen);
            float inside = 1f - Mathf.Clamp01((g - 0.6f * vp.waterRadius) / (0.4f * vp.waterRadius));
            inside = inside * inside * (3f - 2f * inside);
            return Mathf.Clamp01(away * inside);
        }

        /// <summary>One strip: x0..x1 on the map, z0 (bottom) .. z1 (top), v of its bottom and top edge.</summary>
        private static void AddStrip(float x0, float x1, float z0, float z1, float y, float vBottom, float vTop, Color bottom, Color top)
        {
            int b = Verts.Count;
            Verts.Add(new Vector3(x0, y, z0));
            Verts.Add(new Vector3(x0, y, z1));
            Verts.Add(new Vector3(x1, y, z1));
            Verts.Add(new Vector3(x1, y, z0));
            Uvs.Add(new Vector2(0f, vBottom));
            Uvs.Add(new Vector2(0f, vTop));
            Uvs.Add(new Vector2(1f, vTop));
            Uvs.Add(new Vector2(1f, vBottom));
            Colors.Add(bottom);
            Colors.Add(top);
            Colors.Add(top);
            Colors.Add(bottom);
            Tris.Add(b);
            Tris.Add(b + 1);
            Tris.Add(b + 2);
            Tris.Add(b);
            Tris.Add(b + 2);
            Tris.Add(b + 3);
        }

        /// <summary>Fixed random value in -1..1 per (seed, index).</summary>
        private static float Jitter(int seed, int index)
        {
            uint h = (uint)(seed * 73856093 ^ index * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xffff) / 32767.5f - 1f;
        }

        private static void DrawDrops(Instance inst, MirrorVisualProps vp, Vector3 feet, float p, float alt)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(vp.coreTexPath, "MX_QH_SakuraTrailAdd");
            if (mat == null || p >= 1f)
            {
                return;
            }
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            Color c = vp.white;
            c.a = Mathf.Clamp01(1f - p * p);
            for (int i = 0; i < vp.dropCount; i++)
            {
                float a = (Jitter(inst.seed + 1, i) * 0.5f + 0.5f) * Mathf.PI * 2f;
                float sp = Mathf.Lerp(0.4f, 1.3f, Jitter(inst.seed + 2, i) * 0.5f + 0.5f);
                float up = Mathf.Lerp(0.6f, 1.6f, Jitter(inst.seed + 3, i) * 0.5f + 0.5f);
                float x = Mathf.Cos(a) * sp * p;
                float z = Mathf.Sin(a) * sp * p;
                float h = up * p * (1f - p) * 4f * 0.4f;
                Vector3 at = new(feet.x + x, alt + 0.01f, feet.z + z * vp.depthToScreen + h * vp.heightToScreen * 2f);
                MapComponent_SakuraDanceVisuals.AddQuad(at, vp.dropSize, 0f, Vector2.zero, Vector2.one, c);
            }
            inst.dropMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(inst.dropMesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 1.6f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AlphaScaleId, 1f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AdditiveId, 1f);
            if (vp.bloom != null)
            {
                ScreenBloom.Submit(vp.bloom, inst.dropMesh, Matrix4x4.identity, mat, block);
            }
            Graphics.DrawMesh(inst.dropMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        private static Mesh flashMesh;

        private static void DrawFlash(MirrorVisualProps vp, Vector3 at, float brightness, float alt)
        {
            if (brightness <= 0.01f)
            {
                return;
            }
            Material mat = MapComponent_SakuraDanceVisuals.Material(vp.coreTexPath, "MX_QH_SakuraTrailAdd");
            if (mat == null)
            {
                return;
            }
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            Color c = vp.white;
            c.a = 1f;
            MapComponent_SakuraDanceVisuals.AddQuad(new Vector3(at.x, alt + 0.03f, at.z), 1.4f, 0f, Vector2.zero, Vector2.one, c);
            flashMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(flashMesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, brightness * 2f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AlphaScaleId, 1f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AdditiveId, 1f);
            if (vp.bloom != null)
            {
                ScreenBloom.Submit(vp.bloom, flashMesh, Matrix4x4.identity, mat, block);
            }
            Graphics.DrawMesh(flashMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        private static Mesh NewMesh(string name)
        {
            Mesh mesh = new() { name = name };
            mesh.MarkDynamic();
            return mesh;
        }

        private static Material WaterMaterial
        {
            get
            {
                if (waterMat == null)
                {
                    Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail("MX_QH_MirrorWater")?.Shader;
                    if (shader == null)
                    {
                        Log.ErrorOnce("[MiliraXian] Illusory Reflection: missing shader 'MX_QH_MirrorWater'.", 0x51c7c1);
                        return null;
                    }
                    waterMat = MaterialPool.MatFrom(BaseContent.WhiteTex, shader, Color.white);
                }
                return waterMat;
            }
        }

        private static Material SpriteMaterial
        {
            get
            {
                if (spriteMat == null)
                {
                    Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail("MX_QH_MirrorSprite")?.Shader;
                    if (shader == null)
                    {
                        Log.ErrorOnce("[MiliraXian] Illusory Reflection: missing shader 'MX_QH_MirrorSprite'.", 0x51c7c2);
                        return null;
                    }
                    // One material: the snapshot is set per draw.
                    spriteMat = MaterialPool.MatFrom(BaseContent.WhiteTex, shader, Color.white);
                }
                return spriteMat;
            }
        }

        private sealed class Instance
        {
            public Pawn pawn;
            public JobDriver_IllusoryReflectionStance driver;
            public int seed;
            /// <summary>The one snapshot: her reflection in the stance, the body that shatters, then the re-form.</summary>
            public RenderTexture body;
            public int bodyFrame;
            public bool reformTaken;
            public Mesh bodyMesh;
            public Mesh reflectionMesh;
            public Mesh dropMesh;
            public bool trailSpawned;

            public void Release()
            {
                if (body != null)
                {
                    RenderTexture.ReleaseTemporary(body);
                    body = null;
                }
                Destroy(ref bodyMesh);
                Destroy(ref reflectionMesh);
                Destroy(ref dropMesh);
                trailSpawned = false;
                reformTaken = false;
            }

            private static void Destroy(ref Mesh mesh)
            {
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                    mesh = null;
                }
            }
        }

        /// <summary>
        /// The cut's trail: the tilted ring band from the inner to the outer radius, swept on the
        /// cut's own curve (<see cref="MirrorCutGeometry"/>). Every part fades by its own age: bright
        /// for <see cref="MirrorVisualProps.lightLife"/>, distorting for <see cref="MirrorVisualProps.distortLife"/>.
        /// Brightest on the high front side. Drawn by the Sakura Dance strip renderer. Only the band is
        /// drawn; the damage reaches the whole disc inside it.
        /// </summary>
        internal sealed class MirrorTrailSource : MapComponent_SakuraDanceVisuals.StripSource
        {
            public MirrorVisualProps vp;
            public Vector3 aim;
            public Vector3 center;
            public float startYaw;
            public float cutSeconds;

            public static void Spawn(Map map, MirrorVisualProps vp, CompProperties_AbilityIllusoryReflection props, JobDriver_IllusoryReflectionStance driver)
            {
                float cutSeconds = (props?.cutTicks ?? 24) / 60f;
                int start = driver.CutStartTick;
                MirrorTrailSource s = new()
                {
                    vp = vp,
                    aim = driver.Aim,
                    startYaw = props?.cutStartYaw ?? -135f,
                    cutSeconds = cutSeconds,
                    startTick = start,
                    center = driver.pawn.Position.ToVector3Shifted(),
                    pawnAltitude = AltitudeLayer.Pawn.AltitudeFor(),
                    emitSeconds = cutSeconds,
                    noisePhase = (start * 0.6180339f) % 1f * 100f,
                };
                s.groundZ = s.center.z;
                s.Build();
                map.GetComponent<MapComponent_SakuraDanceVisuals>()?.AddSource(s);
            }

            public override float HeightToScreen => vp.heightToScreen;
            public override float DepthToScreen => vp.depthToScreen;
            // Ring heights are from the pelvis, as the preview's ring_point.
            public override float HeightOrigin => vp.heightOrigin;
            public override string DistortionNormalTexPath => vp.distortNormalTexPath;
            public override float DistortionMetresPerUnit => vp.distortionMetresPerUnit;
            public override float ShadeOpacity => 0f;
            public override ScreenBloomProperties BloomFor(int layerIndex) => vp.bloom;
            public override Color TintFor(int layerIndex, SakuraTrailLayer layer) => layer.color;

            public void Build()
            {
                float band = vp.cutOuterRadius - vp.cutInnerRadius;
                // The distortion over the whole band, then the colour layers (the props' list, else the built-in set).
                layers.Add(new SakuraTrailLayer
                {
                    texPath = vp.distortTexPath,
                    normalTexPath = vp.distortNormalTexPath,
                    distortion = true,
                    radialOffset = 0f,
                    width = band,
                    // A point of the trail is gone this long after the head passed it.
                    segmentDuration = vp.distortLife,
                    distortionIntensity = vp.distortionIntensity,
                    textureFraction = 0.02f,
                    attachedUV = true,
                    liveFade = true,
                    startFade = 0.1f,
                    endFade = 0.1f,
                });
                layers.AddRange(vp.trailLayers.NullOrEmpty() ? DefaultLayers(vp) : vp.trailLayers);
                baseLayerCount = layers.Count;
                float longest = vp.distortLife;
                foreach (SakuraTrailLayer l in layers)
                {
                    longest = Mathf.Max(longest, l.segmentDuration);
                }
                lifeSeconds = cutSeconds + longest + 0.05f;
            }

            /// <summary>
            /// Like a Sakura Dance cut, fainter: a pale band brighter toward the outer edge, three
            /// filaments at different radii that live longer the further out they are, and the
            /// bright outer line. Offsets are from the middle of the band.
            /// </summary>
            private static List<SakuraTrailLayer> DefaultLayers(MirrorVisualProps vp)
            {
                float band = vp.cutOuterRadius - vp.cutInnerRadius;
                float outer = band * 0.5f;
                bool bloom = vp.bloom != null;
                return new List<SakuraTrailLayer>
                {
                    new()
                    {
                        texPath = "MiliraXianQinghe/Effect/SakuraDance/SakuraBand",
                        additive = true, bloom = bloom, color = vp.pink,
                        radialOffset = 0f, width = band,
                        segmentDuration = 0.3f, brightness = vp.bandBrightness, alpha = 0.8f,
                        alphaNoise = 0.35f, widthNoise = 0.1f, taperAnchor = 1f, tailWidth = 0.7f,
                        textureFraction = 0.01f, attachedUV = true, speedU = 0.6f,
                        liveFade = true, startFade = 5f, endFade = 40f,
                    },
                    new()
                    {
                        texPath = "MiliraXianQinghe/Effect/SakuraDance/SakuraFilament",
                        additive = true, bloom = bloom, color = vp.white,
                        radialOffset = outer - 0.45f, width = 0.35f, tailWidth = 0.3f,
                        segmentDuration = 0.45f, brightness = 1.6f, alpha = 0.55f, alphaNoise = 0.4f,
                        textureFraction = 0.1f, speedU = 0.02f,
                        liveFade = true, startFade = 5f, endFade = 60f,
                    },
                    new()
                    {
                        texPath = "MiliraXianQinghe/Effect/SakuraDance/SakuraThread",
                        additive = true, bloom = bloom, color = vp.pink,
                        radialOffset = outer - 0.9f, width = 0.5f, tailWidth = 0.4f,
                        segmentDuration = 0.35f, brightness = 1.4f, alpha = 0.45f, alphaNoise = 0.5f,
                        textureFraction = 0.12f, speedU = -0.03f,
                        liveFade = true, startFade = 5f, endFade = 60f,
                    },
                    new()
                    {
                        texPath = "MiliraXianQinghe/Effect/SakuraDance/SakuraFilament",
                        additive = true, bloom = bloom, color = vp.pink,
                        radialOffset = -outer + 0.35f, width = 0.3f, tailWidth = 0.2f,
                        segmentDuration = 0.25f, brightness = 1.2f, alpha = 0.35f, alphaNoise = 0.5f,
                        textureFraction = 0.08f, speedU = 0.04f,
                        liveFade = true, startFade = 5f, endFade = 60f,
                    },
                    new()
                    {
                        texPath = vp.lineTexPath,
                        additive = true, bloom = bloom, color = vp.white,
                        radialOffset = outer - band * 0.06f, width = vp.coreWidth, tailWidth = 0.5f,
                        segmentDuration = vp.lightLife, brightness = vp.coreBrightness,
                        headBoost = 1.5f, headLength = 0.1f, widthNoise = 0.3f,
                        textureFraction = 0.01f, attachedUV = true,
                        liveFade = true, startFade = 2f, endFade = 50f,
                    },
                };
            }

            /// <summary>Aim-relative yaw the head has reached at trail second <paramref name="te"/>.</summary>
            private float YawAt(float te) => MirrorCutGeometry.YawAt(startYaw, te / Mathf.Max(1e-3f, cutSeconds));

            private Vector3 RingPoint(float yaw, float r)
            {
                float heading = MirrorCutGeometry.HeadingOf(aim, yaw) * Mathf.Deg2Rad;
                // Lowest at the start yaw (the back-left), highest opposite, as the preview's ring_point.
                float h = 0.35f - vp.cutTilt * Mathf.Cos((yaw - startYaw) * Mathf.Deg2Rad);
                return new Vector3(center.x + Mathf.Cos(heading) * r, h * r / vp.cutOuterRadius, center.z + Mathf.Sin(heading) * r);
            }

            public override void Sample(int layerIndex, float te, float radialOffset, out Vector3 centre, out Vector3 side)
            {
                float yaw = YawAt(te);
                float mid = (vp.cutOuterRadius + vp.cutInnerRadius) * 0.5f;
                float r = mid + radialOffset;
                centre = RingPoint(yaw, r);
                side = RingPoint(yaw, r + 1f) - centre;
            }

            /// <summary>Every point fades by its own age over its layer's life (the preview's per-part age fade).</summary>
            public override float SampleAlpha(int layerIndex, SakuraTrailLayer layer, float te, float t)
            {
                float age = t - te;
                if (layer.distortion)
                {
                    float d = Mathf.Max(0f, 1f - age / Mathf.Max(1e-3f, vp.distortLife));
                    return Mathf.Pow(d, 1.2f);
                }
                float light = Mathf.Pow(Mathf.Max(0f, 1f - age / Mathf.Max(1e-3f, layer.segmentDuration)), 1.5f);
                float yaw = YawAt(te);
                float high = 0.55f + 0.45f * (1f - Mathf.Cos((yaw - startYaw) * Mathf.Deg2Rad)) * 0.5f;
                return Mathf.Min(1f, high * light);
            }
        }
    }

    /// <summary>
    /// Mirror Flower, Water Moon: the real body (with its weapon and shadow) is not drawn while the
    /// snapshot stands in for it. Selection, label and bars are left alone.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    internal static class Patch_PawnRenderer_MirrorHide
    {
        private static readonly AccessTools.FieldRef<PawnRenderer, Pawn> PawnRef =
            AccessTools.FieldRefAccess<PawnRenderer, Pawn>("pawn");

        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PawnRenderer __instance)
        {
            return MapComponent_MirrorVisuals.snapping || !MapComponent_MirrorVisuals.HidesBody(PawnRef(__instance));
        }
    }
}
