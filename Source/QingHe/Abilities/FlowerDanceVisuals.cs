using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class MapComponent_QingheFlowerDanceVisuals : MapComponent
    {
        private const int MaxAfterimages = 96;
        private const int AfterimageTextureSize = 512;
        private const float AfterimageCameraZoom = 0.5f;
        private const float AfterimageMeshScale = 1f / AfterimageCameraZoom;
        private const int GhostAlphaSteps = 32;
        private static readonly Color GhostTint = new Color(1f, 0.94f, 0.97f, 1f);

        private readonly List<FlowerDanceAfterimage> afterimages = new List<FlowerDanceAfterimage>();
        private Mesh afterimageMesh;

        public MapComponent_QingheFlowerDanceVisuals(Map map) : base(map)
        {
        }

        public void AddAfterimage(Pawn pawn, Vector3 drawPos, int durationTicks, float startAlpha)
        {
            AddAfterimage(pawn, drawPos, pawn?.Rotation ?? Rot4.South, durationTicks, startAlpha);
        }

        public void AddAfterimage(Pawn pawn, Vector3 drawPos, Rot4 facing, int durationTicks, float startAlpha)
        {
            if (map == null || pawn == null || pawn.Destroyed || pawn.IsHiddenFromPlayer() || pawn.MapHeld != map)
            {
                return;
            }

            RenderTexture texture = CapturePawnGhostTexture(pawn, facing);
            if (texture == null)
            {
                return;
            }

            Material material = new Material(ShaderDatabase.Transparent)
            {
                mainTexture = texture,
                color = Color.white
            };

            if (afterimages.Count >= MaxAfterimages)
            {
                ReleaseAfterimage(afterimages[0]);
                afterimages.RemoveAt(0);
            }

            afterimages.Add(new FlowerDanceAfterimage
            {
                pawn = pawn,
                drawPos = drawPos,
                facing = facing,
                startTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0,
                durationTicks = Mathf.Max(1, durationTicks),
                startAlpha = Mathf.Clamp01(startAlpha),
                texture = texture,
                material = material
            });
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            ReleaseAllAfterimages();
            if (afterimageMesh != null)
            {
                Object.Destroy(afterimageMesh);
                afterimageMesh = null;
            }
        }

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();
            DrawAfterimages(Find.TickManager != null ? Find.TickManager.TicksGame : 0);
        }

        private void DrawAfterimages(int now)
        {
            if (afterimages.Count == 0)
            {
                return;
            }

            for (int i = afterimages.Count - 1; i >= 0; i--)
            {
                FlowerDanceAfterimage afterimage = afterimages[i];
                int age = now - afterimage.startTick;
                if (age < 0 || age > afterimage.durationTicks || afterimage.pawn == null || afterimage.pawn.Destroyed)
                {
                    ReleaseAfterimage(afterimage);
                    afterimages.RemoveAt(i);
                    continue;
                }

                float progress = Mathf.Clamp01(age / (float)afterimage.durationTicks);
                float alpha = afterimage.startAlpha * (1f - progress);
                if (alpha <= 0.01f || afterimage.texture == null)
                {
                    ReleaseAfterimage(afterimage);
                    afterimages.RemoveAt(i);
                    continue;
                }

                DrawPawnGhostSnapshot(afterimage, alpha);
            }
        }

        private static RenderTexture CapturePawnGhostTexture(Pawn pawn, Rot4 facing)
        {
            if (pawn?.Drawer?.renderer == null || Find.PawnCacheRenderer == null)
            {
                return null;
            }

            RenderTexture texture = new RenderTexture(AfterimageTextureSize, AfterimageTextureSize, 24, RenderTextureFormat.ARGB32);
            texture.name = "MX_QH_FlowerDanceAfterimage";
            Find.PawnCacheRenderer.RenderPawn(pawn, texture, Vector3.zero, AfterimageCameraZoom, 0f, facing, renderHead: true, renderHeadgear: true, renderClothes: true);
            return texture;
        }

        private void DrawPawnGhostSnapshot(FlowerDanceAfterimage afterimage, float alpha)
        {
            Material material = afterimage.material;
            if (material == null || material == BaseContent.ClearMat)
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

            MaterialPropertyBlock block = MX_QHRenderStatics.SharedPropertyBlock;
            Color tint = GhostTint;
            tint.a = QuantizeAlpha(alpha);
            block.SetColor(ShaderPropertyIDs.Color, tint);
            Graphics.DrawMesh(afterimageMesh, matrix, material, 0, null, 0, block);
            block.Clear();
        }

        private static float QuantizeAlpha(float alpha)
        {
            return Mathf.Clamp01(Mathf.Ceil(Mathf.Clamp01(alpha) * GhostAlphaSteps) / GhostAlphaSteps);
        }

        private void ReleaseAllAfterimages()
        {
            for (int i = 0; i < afterimages.Count; i++)
            {
                ReleaseAfterimage(afterimages[i]);
            }

            afterimages.Clear();
        }

        private static void ReleaseAfterimage(FlowerDanceAfterimage afterimage)
        {
            if (afterimage.texture != null)
            {
                afterimage.texture.Release();
                Object.Destroy(afterimage.texture);
            }

            if (afterimage.material != null)
            {
                Object.Destroy(afterimage.material);
            }
        }

        private struct FlowerDanceAfterimage
        {
            public Pawn pawn;
            public Vector3 drawPos;
            public Rot4 facing;
            public int startTick;
            public int durationTicks;
            public float startAlpha;
            public RenderTexture texture;
            public Material material;
        }
    }
}
