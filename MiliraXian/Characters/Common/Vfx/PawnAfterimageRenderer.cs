using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx
{
    /// <summary>
    /// The afterimages of one map: a list of short-lived ghosts, each pointing at a pawn snapshot
    /// owned by <see cref="GameComponent_MxVfxCaptures"/>.
    ///
    /// The textures belong to the capture service (game-scoped) and the material to the material
    /// pool, so neither is released here. The one thing this component does own is its quad mesh,
    /// which is destroyed when the map goes away: objects are not collected when they become
    /// unreachable in this engine, and <see cref="UnityEngine.Resources.UnloadUnusedAssets"/> is a
    /// no-op, so an undisposed mesh would hold its GPU buffers for the rest of the process.
    ///
    /// Ageing runs in <see cref="MapComponentUpdate"/>, which every map executes, so ghosts
    /// expired even when their map is not the one being drawn.
    /// </summary>
    public class MapComponent_PawnAfterimages : MapComponent, System.IDisposable
    {
        private const int MaxAfterimages = 96;
        private const float AfterimageCameraZoom = 0.5f;
        private const float AfterimageMeshScale = 1f / AfterimageCameraZoom;
        private const int AlphaSteps = 32;
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        private readonly List<PawnAfterimage> afterimages = new();
        private Mesh afterimageMesh;

        public MapComponent_PawnAfterimages(Map map) : base(map)
        {
        }

        public void AddAfterimage(Pawn pawn, Vector3 drawPos, int durationTicks, float startAlpha)
        {
            AddAfterimage(pawn, drawPos, pawn?.Rotation ?? Rot4.South, durationTicks, startAlpha, Color.white);
        }

        public void AddAfterimage(Pawn pawn, Vector3 drawPos, Rot4 facing, int durationTicks, float startAlpha)
        {
            AddAfterimage(pawn, drawPos, facing, durationTicks, startAlpha, Color.white);
        }

        public void AddAfterimage(Pawn pawn, Vector3 drawPos, Rot4 facing, int durationTicks, float startAlpha, Color tint)
            => AddAfterimage(pawn, drawPos, facing, durationTicks, startAlpha, tint, null);

        internal void AddAfterimage(Pawn pawn, Vector3 drawPos, Rot4 facing, int durationTicks, float startAlpha, Color tint,
            object owner, int? startTick = null)
        {
            if (map == null || pawn == null || pawn.Destroyed || pawn.IsHiddenFromPlayer() || pawn.MapHeld != map)
            {
                return;
            }

            if (Find.CameraDriver != null
                && !Find.CameraDriver.CurrentViewRect.ExpandedBy(2).Contains(drawPos.ToIntVec3()))
            {
                return;
            }

            // Make room first: the capture service keys snapshots by (pawn, facing, tick), so a
            // request issued for a ghost we are about to drop would be wasted work.
            while (afterimages.Count >= MaxAfterimages)
            {
                ReleaseAfterimage(afterimages[0]);
                afterimages.RemoveAt(0);
            }

            // The service may share this snapshot with other ghosts of the same moment; it is
            // reference counted, so every ghost owns exactly one reference.
            int captureId = GameComponent_MxVfxCaptures.Request(pawn, facing);
            if (captureId <= 0)
            {
                return;
            }

            afterimages.Add(new PawnAfterimage
            {
                pawn = pawn,
                drawPos = drawPos,
                startTick = startTick ?? Find.TickManager?.TicksGame ?? 0,
                durationTicks = Mathf.Max(1, durationTicks),
                startAlpha = Mathf.Clamp01(startAlpha),
                tint = tint,
                owner = owner,
                captureId = captureId
            });
        }

        internal void RemoveOwnedBy(object owner)
        {
            for (int i = afterimages.Count - 1; i >= 0; i--)
            {
                if (afterimages[i].owner != owner)
                {
                    continue;
                }
                ReleaseAfterimage(afterimages[i]);
                afterimages.RemoveAt(i);
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            ReleaseAllAfterimages();
            ReleaseMesh();
        }

        /// <summary>
        /// Quitting to the main menu and loading another save go through <see cref="Verse.Map.Dispose"/>,
        /// which never calls <see cref="Verse.MapComponent.MapRemoved"/>, so the mesh needs a second
        /// hook. Both are idempotent.
        /// </summary>
        public void Dispose()
        {
            ReleaseAllAfterimages();
            ReleaseMesh();
        }

        private void ReleaseMesh()
        {
            if (afterimageMesh != null)
            {
                Object.Destroy(afterimageMesh);
                afterimageMesh = null;
            }
        }

        /// <summary>
        /// Ageing lives here, not in <see cref="MapComponentDraw"/>: the draw pass only runs for the
        /// current map, so ghosts left behind on another map would otherwise never expire and would
        /// keep their snapshots alive forever.
        /// </summary>
        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            for (int i = afterimages.Count - 1; i >= 0; i--)
            {
                PawnAfterimage afterimage = afterimages[i];
                int age = now - afterimage.startTick;
                if (age < 0 || age > afterimage.durationTicks
                    || afterimage.pawn == null || afterimage.pawn.Destroyed)
                {
                    ReleaseAfterimage(afterimage);
                    afterimages.RemoveAt(i);
                }
            }
        }

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();
            if (Find.TickManager == null || afterimages.Count == 0)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            for (int i = afterimages.Count - 1; i >= 0; i--)
            {
                PawnAfterimage afterimage = afterimages[i];
                int age = now - afterimage.startTick;
                if (age < 0 || age > afterimage.durationTicks)
                {
                    continue;
                }

                float progress = Mathf.Clamp01(age / (float)afterimage.durationTicks);
                float alpha = afterimage.startAlpha * (1f - progress);
                if (alpha <= 0.01f)
                {
                    continue;
                }

                // Captured in the frame the ghost was made, so this is ready on its first draw.
                if (!GameComponent_MxVfxCaptures.TryGetTexture(afterimage.captureId, out RenderTexture shot))
                {
                    continue;
                }

                DrawAfterimage(afterimage, shot, alpha);
            }
        }

        private void DrawAfterimage(PawnAfterimage afterimage, RenderTexture shot, float alpha)
        {
            Material material = AfterimageMaterial;
            if (material == null)
            {
                return;
            }

            Vector3 pos = afterimage.drawPos;
            pos.y = AltitudeLayer.Pawn.AltitudeFor() + 0.08f;
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
            if (afterimageMesh == null)
            {
                afterimageMesh = TextureAtlasHelper.CreateMeshForUV(new Rect(0f, 0f, 1f, 1f), AfterimageMeshScale);
            }

            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            Color tint = afterimage.tint;
            tint.a *= QuantizeAlpha(alpha);
            block.SetTexture(MainTex, shot);
            block.SetColor(ShaderPropertyIDs.Color, tint);
            Graphics.DrawMesh(afterimageMesh, matrix, material, 0, null, 0, block);
            block.Clear();
        }

        private static float QuantizeAlpha(float alpha)
        {
            return Mathf.Clamp01(Mathf.Ceil(Mathf.Clamp01(alpha) * AlphaSteps) / AlphaSteps);
        }

        /// <summary>Pooled: the material is keyed by texture/shader/colour, so every map shares one.</summary>
        private static Material AfterimageMaterial =>
            MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.Transparent, Color.white);

        private void ReleaseAllAfterimages()
        {
            for (int i = 0; i < afterimages.Count; i++)
            {
                ReleaseAfterimage(afterimages[i]);
            }
            afterimages.Clear();
        }

        private static void ReleaseAfterimage(PawnAfterimage afterimage)
        {
            if (afterimage.captureId > 0)
            {
                GameComponent_MxVfxCaptures.Release(afterimage.captureId);
            }
        }

        private struct PawnAfterimage
        {
            public Pawn pawn;
            public Vector3 drawPos;
            public int startTick;
            public int durationTicks;
            public float startAlpha;
            public Color tint;
            public object owner;
            public int captureId;
        }
    }
}
