using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Vfx
{
    public class MapComponent_PawnAfterimages : MapComponent
    {
        private const int MaxAfterimages = 96;
        private const int AfterimageTextureSize = 512;
        private const float AfterimageCameraZoom = 0.5f;
        private const float AfterimageMeshScale = 1f / AfterimageCameraZoom;
        private const int AlphaSteps = 32;
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private readonly List<PawnAfterimage> afterimages = new List<PawnAfterimage>();
        private Mesh afterimageMesh;
        private Material afterimageMaterial;

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
        {
            if (map == null || pawn == null || pawn.Destroyed || pawn.IsHiddenFromPlayer() || pawn.MapHeld != map)
            {
                return;
            }

            RenderTexture texture = CapturePawnTexture(pawn, facing);
            if (texture == null)
            {
                return;
            }

            if (afterimages.Count >= MaxAfterimages)
            {
                ReleaseAfterimage(afterimages[0]);
                afterimages.RemoveAt(0);
            }

            afterimages.Add(new PawnAfterimage
            {
                pawn = pawn,
                drawPos = drawPos,
                startTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0,
                durationTicks = Mathf.Max(1, durationTicks),
                startAlpha = Mathf.Clamp01(startAlpha),
                tint = tint,
                texture = texture
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
            if (afterimageMaterial != null)
            {
                Object.Destroy(afterimageMaterial);
                afterimageMaterial = null;
            }
        }

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();
            DrawAfterimages(Find.TickManager != null ? Find.TickManager.TicksGame : 0);
        }

        private void DrawAfterimages(int now)
        {
            for (int i = afterimages.Count - 1; i >= 0; i--)
            {
                PawnAfterimage afterimage = afterimages[i];
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

                DrawAfterimage(afterimage, alpha);
            }
        }

        private static RenderTexture CapturePawnTexture(Pawn pawn, Rot4 facing)
        {
            if (pawn?.Drawer?.renderer == null || Find.PawnCacheRenderer == null)
            {
                return null;
            }

            RenderTexture texture = RenderTexture.GetTemporary(
                AfterimageTextureSize,
                AfterimageTextureSize,
                24,
                RenderTextureFormat.ARGB32);
            texture.name = "MX_PawnAfterimage";
            Find.PawnCacheRenderer.RenderPawn(pawn, texture, Vector3.zero, AfterimageCameraZoom, 0f, facing, renderHead: true, renderHeadgear: true, renderClothes: true);
            return texture;
        }

        private void DrawAfterimage(PawnAfterimage afterimage, float alpha)
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
            block.SetTexture(MainTex, afterimage.texture);
            block.SetColor(ShaderPropertyIDs.Color, tint);
            Graphics.DrawMesh(afterimageMesh, matrix, material, 0, null, 0, block);
            block.Clear();
        }

        private static float QuantizeAlpha(float alpha)
        {
            return Mathf.Clamp01(Mathf.Ceil(Mathf.Clamp01(alpha) * AlphaSteps) / AlphaSteps);
        }

        private Material AfterimageMaterial
        {
            get
            {
                if (afterimageMaterial == null)
                {
                    afterimageMaterial = new Material(ShaderDatabase.Transparent)
                    {
                        mainTexture = BaseContent.WhiteTex,
                        color = Color.white
                    };
                }

                return afterimageMaterial;
            }
        }

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
            if (afterimage.texture != null)
            {
                RenderTexture.ReleaseTemporary(afterimage.texture);
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
            public RenderTexture texture;
        }
    }
}
