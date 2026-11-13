using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common.Combat;
using MiliraXian.Characters.Common.Vfx;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Vfx
{
    /// <summary>
    /// One ranged-evade flash: snapshot at trigger, fade out to MinAlpha (FadeSeconds), hold torn
    /// (HoldSeconds), fade back in (ReformSeconds). The real body is hidden the whole time and leaves
    /// nothing behind. Not saved: on load the pawn simply reappears; the effect is too short to matter.
    /// </summary>
    internal sealed class FlowerBellEvadeInstance
    {
        public Pawn pawn;
        public int startTick;
        public Rot4 facing;
        public RenderTexture snapshot;
        public Mesh mesh;

        // ---- timing (seconds of game time = ticks / 60) ----
        // Fade out to MinAlpha, hold there, fade back in.
        public const float FadeSeconds = 0.2f;
        public const float HoldSeconds = 0.6f;
        public const float ReformSeconds = 0.2f;
        public const float TotalSeconds = FadeSeconds + HoldSeconds + ReformSeconds;
        /// <summary>Alpha and disturbance held during the hold, where the fade turns around.</summary>
        public const float MinAlpha = 0.4f;
        public const float HoldK = 0.6f;

        public float Seconds => Mathf.Max(0f, Common.Vfx.VisualClock.TicksSince(startTick)) / 60f;
        public bool Done => Seconds >= TotalSeconds;

        /// <summary>True while the snapshot stands in for the real body.</summary>
        public bool BodyHidden => !Done;

        /// <summary>Alpha of the snapshot: 1 → 0.4 over the fade-out, 0.4 held, 0.4 → 1 over the fade-in.</summary>
        public float SnapshotAlpha
        {
            get
            {
                float t = Seconds;
                if (t < FadeSeconds)
                {
                    return Mathf.Lerp(1f, MinAlpha, t / FadeSeconds);
                }
                float reformStart = FadeSeconds + HoldSeconds;
                if (t < reformStart)
                {
                    return MinAlpha;
                }
                return Mathf.Lerp(MinAlpha, 1f, Mathf.Clamp01((t - reformStart) / Mathf.Max(1e-4f, ReformSeconds)));
            }
        }

        /// <summary>
        /// Shader disturbance k:
        /// fade-out: 0 → 0.6 (picture tears apart as it fades),
        /// hold: 0.6 (held torn),
        /// fade-in: 0.6 → 0 (picture re-forms as it appears).
        /// </summary>
        public float DisturbanceK
        {
            get
            {
                float t = Seconds;
                if (t < FadeSeconds)
                {
                    return Mathf.Lerp(0f, HoldK, t / FadeSeconds);
                }
                float reformStart = FadeSeconds + HoldSeconds;
                if (t < reformStart)
                {
                    return HoldK;
                }
                return Mathf.Lerp(HoldK, 0f, Mathf.Clamp01((t - reformStart) / Mathf.Max(1e-4f, ReformSeconds)));
            }
        }

        public void Release()
        {
            if (snapshot != null)
            {
                RenderTexture.ReleaseTemporary(snapshot);
                snapshot = null;
            }
            if (mesh != null)
            {
                Object.Destroy(mesh);
                mesh = null;
            }
        }
    }

    /// <summary>
    /// Draws all in-flight ranged evades on the current map (registered in MapComponent_QingheVfx).
    /// Triggered by <see cref="FlowerBellRangedEvadeBlocker"/> on a successful evade.
    /// </summary>
    [StaticConstructorOnStartup]
    internal sealed class FlowerBellEvadeVfx : IQingheVfxRenderer
    {
        private const int SnapshotSize = 512;
        private const float SnapshotZoom = 0.5f;
        private const float SnapshotCells = 2f / SnapshotZoom;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private static Material spriteMat;
        private static Material SpriteMaterial
        {
            get
            {
                if (spriteMat == null)
                {
                    Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail("MX_QH_MirrorSprite")?.Shader;
                    if (shader == null)
                    {
                        return null;
                    }
                    spriteMat = MaterialPool.MatFrom(BaseContent.WhiteTex, shader, Color.white);
                }
                return spriteMat;
            }
        }

        static FlowerBellEvadeVfx()
        {
            CombatPosture.RegisterImmunity(HidesBody);
            ExecutionGuard.Register((victim, _) => HidesBody(victim));
        }

        public static FlowerBellEvadeVfx GetOrCreate(Map map)
        {
            if (map == null) return null;
            FlowerBellEvadeVfx existing = MapComponent_QingheVfx.Find<FlowerBellEvadeVfx>(map, _ => true);
            if (existing != null) return existing;
            FlowerBellEvadeVfx vfx = new(map);
            MapComponent_QingheVfx.Register(map, vfx);
            return vfx;
        }

        private readonly Map map;
        private readonly List<FlowerBellEvadeInstance> instances = new();

        bool IQingheVfxRenderer.Active => instances.Count > 0 || map == Find.CurrentMap;

        public FlowerBellEvadeVfx(Map map)
        {
            this.map = map;
        }

        /// <summary>Called by the blocker on successful evade.</summary>
        public void Notify(Pawn pawn)
        {
            // Drop any existing entry for this pawn (re-evade replaces the last).
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                if (instances[i].pawn == pawn)
                {
                    instances[i].Release();
                    instances.RemoveAt(i);
                }
            }

            if (pawn?.Drawer?.renderer == null || Find.PawnCacheRenderer == null)
            {
                return;
            }

            RenderTexture rt = RenderTexture.GetTemporary(SnapshotSize, SnapshotSize, 24, RenderTextureFormat.ARGB32);
            rt.name = "MX_QH_EvadeSnapshot";
            MapComponent_MirrorVisuals.snapping = true;
            try
            {
                Find.PawnCacheRenderer.RenderPawn(pawn, rt, Vector3.zero, SnapshotZoom, 0f, pawn.Rotation,
                    renderHead: true, renderHeadgear: true, renderClothes: true);
            }
            finally
            {
                MapComponent_MirrorVisuals.snapping = false;
            }

            var inst = new FlowerBellEvadeInstance
            {
                pawn = pawn,
                startTick = Find.TickManager.TicksGame,
                facing = pawn.Rotation,
                snapshot = rt,
            };
            instances.Add(inst);
        }

        public static bool HidesBody(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                return false;
            }
            FlowerBellEvadeVfx vfx = MapComponent_QingheVfx.Find<FlowerBellEvadeVfx>(pawn.Map, v => true);
            if (vfx == null)
            {
                return false;
            }
            foreach (FlowerBellEvadeInstance inst in vfx.instances)
            {
                if (inst.pawn == pawn && inst.BodyHidden)
                {
                    return true;
                }
            }
            return false;
        }

        void IQingheVfxRenderer.Update(Map map)
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                if (instances[i].Done)
                {
                    instances[i].Release();
                    instances.RemoveAt(i);
                }
            }
        }

        void IQingheVfxRenderer.Draw(Map map)
        {
            if (instances.Count == 0 || Find.CurrentMap != map)
            {
                return;
            }
            Material mat = SpriteMaterial;
            if (mat == null)
            {
                return;
            }
            foreach (FlowerBellEvadeInstance inst in instances)
            {
                if (inst.snapshot == null || inst.pawn == null || !inst.pawn.Spawned || inst.pawn.IsHiddenFromPlayer())
                {
                    continue;
                }
                float alpha = inst.SnapshotAlpha;
                if (alpha <= 0.002f)
                {
                    continue;
                }
                float k = inst.DisturbanceK;
                float t = VisualClock.TicksSince(inst.startTick) / 60f;
                inst.mesh ??= MapComponent_QingheVfx.NewMesh("MX_QH_EvadeBody");
                MapComponent_QingheVfx.ClearBuffers();
                Vector3 center = inst.pawn.DrawPos;
                float half = SnapshotCells * 0.5f;
                float y = AltitudeLayer.Pawn.AltitudeFor() + 0.02f;
                Color col = new Color(1f, 1f, 1f, alpha);
                MapComponent_QingheVfx.AddVertex(new Vector3(center.x - half, y, center.z - half), new Vector2(0f, 0f), col);
                MapComponent_QingheVfx.AddVertex(new Vector3(center.x - half, y, center.z + half), new Vector2(0f, 1f), col);
                MapComponent_QingheVfx.AddVertex(new Vector3(center.x + half, y, center.z + half), new Vector2(1f, 1f), col);
                MapComponent_QingheVfx.AddVertex(new Vector3(center.x + half, y, center.z - half), new Vector2(1f, 0f), col);
                MapComponent_QingheVfx.AddTriangle(0, 1, 2);
                MapComponent_QingheVfx.AddTriangle(0, 2, 3);
                MapComponent_QingheVfx.Upload(inst.mesh);
                MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
                block.Clear();
                block.SetTexture(MainTexId, inst.snapshot);
                block.SetColor(ShaderPropertyIDs.Color, col);
                // t = time for wave animation; k = disturbance (0→1 during fade-out tears the picture apart);
                // seed from pawn id for variety; w = 1 (body scale, same as DrawBody in MirrorVisuals).
                float seed = (inst.pawn.thingIDNumber * 7919) % 1000f;
                block.SetVector(Shader.PropertyToID("_Wave"), new Vector4(t, k, seed, 1f));
                // Light pink sheen, strengthens as k rises — mirrors the scatter tint from MirrorVisuals.DrawBody.
                block.SetVector(Shader.PropertyToID("_Sheen"), new Vector4(1f, 0.72f, 0.85f, k * 0.35f));
                Graphics.DrawMesh(inst.mesh, Matrix4x4.identity, mat, 0, null, 0, block);
                block.Clear();
            }
        }

        void IQingheVfxRenderer.Dispose()
        {
            foreach (FlowerBellEvadeInstance inst in instances)
            {
                inst.Release();
            }
            instances.Clear();
        }
    }

    /// <summary>Hides the pawn body during an evade flash (the snapshot stands in).</summary>
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    internal static class Patch_PawnRenderer_EvadeHide
    {
        private static readonly AccessTools.FieldRef<PawnRenderer, Pawn> PawnRef =
            AccessTools.FieldRefAccess<PawnRenderer, Pawn>("pawn");

        [HarmonyPriority(Priority.First - 1)]  // just after the mirror hide patch
        private static bool Prefix(PawnRenderer __instance)
        {
            return MapComponent_MirrorVisuals.snapping || !FlowerBellEvadeVfx.HidesBody(PawnRef(__instance));
        }
    }
}
