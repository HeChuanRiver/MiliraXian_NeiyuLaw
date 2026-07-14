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
        private readonly PawnAfterimageSlot[] afterimages = new PawnAfterimageSlot[MaxAfterimages];
        private int activeAfterimageCount;
        private int nextSlotIndex;
        private Mesh afterimageMesh;

        public MapComponent_PawnAfterimages(Map map) : base(map)
        {
            for (int i = 0; i < afterimages.Length; i++)
            {
                afterimages[i] = new PawnAfterimageSlot();
            }
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
            if (map == null || pawn == null || pawn.Dead || pawn.Destroyed || pawn.IsHiddenFromPlayer() || pawn.MapHeld != map)
            {
                return;
            }

            if (Find.CameraDriver != null
                && !Find.CameraDriver.CurrentViewRect.ExpandedBy(2).Contains(drawPos.ToIntVec3()))
            {
                return;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            PruneExpired(now);
            int loadMultiplier = activeAfterimageCount > 64 ? 4 : activeAfterimageCount > 32 ? 2 : 1;
            if (loadMultiplier > 1)
            {
                int stableSample = Gen.HashCombineInt(pawn.thingIDNumber, now) & int.MaxValue;
                if (stableSample % loadMultiplier != 0)
                {
                    return;
                }
            }

            int slotIndex = FindReusableSlot();
            PawnAfterimageSlot slot = afterimages[slotIndex];
            if (!EnsureResources(slot) || !CapturePawnTexture(pawn, facing, slot.texture))
            {
                return;
            }

            if (!slot.active)
            {
                activeAfterimageCount++;
            }

            slot.active = true;
            slot.pawn = pawn;
            slot.drawPos = drawPos;
            slot.startTick = now;
            slot.durationTicks = Mathf.Max(1, durationTicks);
            slot.startAlpha = Mathf.Clamp01(startAlpha);
            slot.tint = tint;
            nextSlotIndex = (slotIndex + 1) % MaxAfterimages;
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
            for (int i = 0; i < afterimages.Length; i++)
            {
                PawnAfterimageSlot afterimage = afterimages[i];
                if (!afterimage.active)
                {
                    continue;
                }

                int age = now - afterimage.startTick;
                if (age < 0
                    || age > afterimage.durationTicks
                    || afterimage.pawn == null
                    || afterimage.pawn.Dead
                    || afterimage.pawn.Destroyed
                    || afterimage.pawn.MapHeld != map)
                {
                    Deactivate(afterimage);
                    continue;
                }

                float progress = Mathf.Clamp01(age / (float)afterimage.durationTicks);
                float alpha = afterimage.startAlpha * (1f - progress);
                if (alpha <= 0.01f || afterimage.texture == null)
                {
                    Deactivate(afterimage);
                    continue;
                }

                DrawAfterimage(afterimage, alpha);
            }
        }

        private static bool CapturePawnTexture(Pawn pawn, Rot4 facing, RenderTexture texture)
        {
            if (pawn?.Drawer?.renderer == null || Find.PawnCacheRenderer == null || texture == null)
            {
                return false;
            }

            Find.PawnCacheRenderer.RenderPawn(pawn, texture, Vector3.zero, AfterimageCameraZoom, 0f, facing, renderHead: true, renderHeadgear: true, renderClothes: true);
            return true;
        }

        private void DrawAfterimage(PawnAfterimageSlot afterimage, float alpha)
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

            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            Color tint = afterimage.tint;
            tint.a *= QuantizeAlpha(alpha);
            block.SetColor(ShaderPropertyIDs.Color, tint);
            Graphics.DrawMesh(afterimageMesh, matrix, material, 0, null, 0, block);
            block.Clear();
        }

        private static float QuantizeAlpha(float alpha)
        {
            return Mathf.Clamp01(Mathf.Ceil(Mathf.Clamp01(alpha) * AlphaSteps) / AlphaSteps);
        }

        private void ReleaseAllAfterimages()
        {
            for (int i = 0; i < afterimages.Length; i++)
            {
                ReleaseSlot(afterimages[i]);
            }

            activeAfterimageCount = 0;
            nextSlotIndex = 0;
        }

        private static void ReleaseSlot(PawnAfterimageSlot afterimage)
        {
            afterimage.active = false;
            afterimage.pawn = null;
            if (afterimage.texture != null)
            {
                afterimage.texture.Release();
                Object.Destroy(afterimage.texture);
                afterimage.texture = null;
            }

            if (afterimage.material != null)
            {
                Object.Destroy(afterimage.material);
                afterimage.material = null;
            }
        }

        private int FindReusableSlot()
        {
            for (int offset = 0; offset < MaxAfterimages; offset++)
            {
                int index = (nextSlotIndex + offset) % MaxAfterimages;
                if (!afterimages[index].active)
                {
                    return index;
                }
            }

            return nextSlotIndex;
        }

        private static bool EnsureResources(PawnAfterimageSlot slot)
        {
            if (slot.texture == null)
            {
                slot.texture = new RenderTexture(AfterimageTextureSize, AfterimageTextureSize, 24, RenderTextureFormat.ARGB32)
                {
                    name = "MX_PawnAfterimage"
                };
            }

            if (slot.material == null)
            {
                slot.material = new Material(ShaderDatabase.Transparent)
                {
                    mainTexture = slot.texture,
                    color = Color.white
                };
            }

            return slot.texture != null && slot.material != null;
        }

        private void PruneExpired(int now)
        {
            for (int i = 0; i < afterimages.Length; i++)
            {
                PawnAfterimageSlot slot = afterimages[i];
                if (slot.active
                    && (now - slot.startTick > slot.durationTicks
                        || slot.pawn == null
                        || slot.pawn.Dead
                        || slot.pawn.Destroyed
                        || slot.pawn.MapHeld != map))
                {
                    Deactivate(slot);
                }
            }
        }

        private void Deactivate(PawnAfterimageSlot slot)
        {
            if (!slot.active)
            {
                return;
            }

            slot.active = false;
            slot.pawn = null;
            activeAfterimageCount = Mathf.Max(0, activeAfterimageCount - 1);
        }

        private sealed class PawnAfterimageSlot
        {
            public bool active;
            public Pawn pawn;
            public Vector3 drawPos;
            public int startTick;
            public int durationTicks;
            public float startAlpha;
            public Color tint;
            public RenderTexture texture;
            public Material material;
        }
    }
}
