using System.Collections.Generic;
using RimWorld;
using MiliraXian.Characters.QingHe.Hediffs;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class PawnFlyer_FlowerDivinationSlash : PawnFlyer
    {
        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            Pawn flyingPawn = FlyingPawn;
            if (flyingPawn != null)
            {
                HediffComp_FlowerDivination divination = FlowerCourtUtility.GetFlowerDivination(flyingPawn);
                divination?.TickFlowerDivinationSlashAfterimage(Map, DrawPos, flyingPawn.Rotation);
            }

            if (Map != null && Rand.Chance(0.35f))
            {
                FleckMaker.ThrowAirPuffUp(DrawPos, Map);
            }
        }
    }

    public class MapComponent_FlowerDivinationVisuals : MapComponent
    {
        private const string DefaultArcTexPathFirst = "MiliraXianQinghe/Effect/flower_divination_slash_1";
        private const string DefaultArcTexPathSecond = "MiliraXianQinghe/Effect/flower_divination_slash_2";
        private const int SecondArcDelayTicks = 5;
        private const float FirstArcAngleOffset = -9f;
        private const float SecondArcAngleOffset = 9f;
        private const int MaxVisuals = 24;
        private const int MaxAfterimages = 96;
        private const int MaxLightningBolts = 24;
        private const int DefaultLightningBoltDurationTicks = 18;
        private const int AfterimageTextureSize = 512;
        private const float AfterimageCameraZoom = 0.5f;
        private const float AfterimageMeshScale = 1f / AfterimageCameraZoom;
        private const int GhostAlphaSteps = 32;
        private static readonly Color GhostTint = new Color(1f, 0.94f, 0.97f, 1f);
        private readonly List<FlowerDivinationSlashArcVisual> arcVisuals = new List<FlowerDivinationSlashArcVisual>();
        private readonly List<FlowerDivinationAfterimage> afterimages = new List<FlowerDivinationAfterimage>();
        private readonly List<FlowerDivinationLightningBolt> lightningBolts = new List<FlowerDivinationLightningBolt>();
        private readonly List<FlowerDivinationSlashDelayedImpact> delayedImpacts = new List<FlowerDivinationSlashDelayedImpact>();
        private Mesh afterimageMesh;

        private static readonly Dictionary<string, Material> arcMaterials = new Dictionary<string, Material>();
        private static readonly HashSet<string> triedLoadArcMaterials = new HashSet<string>();
        private static Material lightningMaterial;
        private static bool triedLoadLightningMaterial;

        public MapComponent_FlowerDivinationVisuals(Map map) : base(map)
        {
        }

        public void AddArc(IntVec3 origin, Vector3 forward, float radius, float angleDegrees, int durationTicks, string texPath = null)
        {
            if (map == null || !origin.IsValid || forward.sqrMagnitude < 0.001f)
            {
                return;
            }

            forward.y = 0f;
            forward.Normalize();

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (!texPath.NullOrEmpty())
            {
                AddArcVisual(origin.ToVector3Shifted(), forward, radius, angleDegrees, 0f, now, durationTicks, texPath);
                return;
            }

            AddArcVisual(origin.ToVector3Shifted(), forward, radius, angleDegrees, FirstArcAngleOffset, now, durationTicks, DefaultArcTexPathFirst);
            AddArcVisual(origin.ToVector3Shifted(), forward, radius, angleDegrees, SecondArcAngleOffset, now + SecondArcDelayTicks, durationTicks, DefaultArcTexPathSecond);
        }

        public void AddLightningBolt(IntVec3 strikeCell, int durationTicks = DefaultLightningBoltDurationTicks)
        {
            if (map == null || !strikeCell.IsValid || !strikeCell.InBounds(map))
            {
                return;
            }

            Mesh boltMesh = LightningBoltMeshPool.RandomBoltMesh;
            if (boltMesh == null)
            {
                return;
            }

            if (lightningBolts.Count >= MaxLightningBolts)
            {
                lightningBolts.RemoveAt(0);
            }

            lightningBolts.Add(new FlowerDivinationLightningBolt
            {
                strikeCell = strikeCell,
                boltMesh = boltMesh,
                startTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0,
                durationTicks = Mathf.Max(1, durationTicks)
            });
        }

        public void AddDelayedImpact(Pawn caster, IntVec3 landing, IntVec3 directionCell, int delayTicks, CompProperties_AbilityFlowerDivinationSlash props)
        {
            if (map == null || caster == null || props == null || !landing.IsValid || !directionCell.IsValid)
            {
                return;
            }

            delayedImpacts.Add(new FlowerDivinationSlashDelayedImpact
            {
                caster = caster,
                landing = landing,
                directionCell = directionCell,
                triggerTick = (Find.TickManager != null ? Find.TickManager.TicksGame : 0) + Mathf.Max(0, delayTicks),
                props = props
            });
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

            afterimages.Add(new FlowerDivinationAfterimage
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

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            DrawLightningBolts(now);
            DrawAfterimages(now);
            DrawArcs(now);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (delayedImpacts.Count == 0)
            {
                return;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            for (int i = delayedImpacts.Count - 1; i >= 0; i--)
            {
                FlowerDivinationSlashDelayedImpact impact = delayedImpacts[i];
                if (now < impact.triggerTick)
                {
                    continue;
                }

                delayedImpacts.RemoveAt(i);
                CompAbilityEffect_FlowerDivinationSlash.ResolveDelayedConeImpact(impact.caster, map, impact.landing, impact.directionCell, impact.props);
            }
        }

        private void DrawLightningBolts(int now)
        {
            if (lightningBolts.Count == 0)
            {
                return;
            }

            Material material = ResolveLightningMaterial();
            if (material == null)
            {
                return;
            }

            for (int i = lightningBolts.Count - 1; i >= 0; i--)
            {
                FlowerDivinationLightningBolt bolt = lightningBolts[i];
                int age = now - bolt.startTick;
                if (age < 0 || age > bolt.durationTicks || bolt.boltMesh == null)
                {
                    lightningBolts.RemoveAt(i);
                    continue;
                }

                float brightness = LightningBrightness(age, bolt.durationTicks);
                if (brightness <= 0.01f)
                {
                    continue;
                }

                Graphics.DrawMesh(
                    bolt.boltMesh,
                    bolt.strikeCell.ToVector3ShiftedWithAltitude(AltitudeLayer.Weather),
                    Quaternion.identity,
                    FadedMaterialPool.FadedVersionOf(material, brightness),
                    0);
            }
        }

        private void DrawAfterimages(int now)
        {
            if (afterimages.Count == 0)
            {
                return;
            }

            for (int i = afterimages.Count - 1; i >= 0; i--)
            {
                FlowerDivinationAfterimage afterimage = afterimages[i];
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

        private void DrawArcs(int now)
        {
            if (arcVisuals.Count == 0)
            {
                return;
            }

            for (int i = arcVisuals.Count - 1; i >= 0; i--)
            {
                FlowerDivinationSlashArcVisual visual = arcVisuals[i];
                int age = now - visual.startTick;
                if (age < 0)
                {
                    continue;
                }

                if (age > visual.durationTicks)
                {
                    arcVisuals.RemoveAt(i);
                    continue;
                }

                DrawArc(visual, age / (float)visual.durationTicks);
            }
        }

        private static RenderTexture CapturePawnGhostTexture(Pawn pawn, Rot4 facing)
        {
            if (pawn?.Drawer?.renderer == null || Find.PawnCacheRenderer == null)
            {
                return null;
            }

            RenderTexture texture = new RenderTexture(AfterimageTextureSize, AfterimageTextureSize, 24, RenderTextureFormat.ARGB32);
            texture.name = "MX_QH_FlowerDivinationAfterimage";
            Find.PawnCacheRenderer.RenderPawn(pawn, texture, Vector3.zero, AfterimageCameraZoom, 0f, facing, renderHead: true, renderHeadgear: true, renderClothes: true);
            return texture;
        }

        private void DrawPawnGhostSnapshot(FlowerDivinationAfterimage afterimage, float alpha)
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

        private static void ReleaseAfterimage(FlowerDivinationAfterimage afterimage)
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

        private static void DrawArc(FlowerDivinationSlashArcVisual visual, float progress)
        {
            Material material = ResolveArcMaterial(visual.texPath);
            if (material == null)
            {
                return;
            }

            float clampedProgress = Mathf.Clamp01(progress);
            float easedMove = Mathf.SmoothStep(0f, 1f, clampedProgress);
            float easedScale = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(clampedProgress / 0.92f));
            float distance = Mathf.Lerp(-visual.radius * 0.12f, visual.radius * 0.56f, easedMove);
            float drawSize = Mathf.Lerp(visual.radius * 0.08f, visual.radius * 1.45f, easedScale);
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(clampedProgress / 0.12f))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((clampedProgress - 0.45f) / 0.55f)));
            alpha *= 0.72f;
            if (alpha <= 0.01f)
            {
                return;
            }

            Vector3 center = visual.origin + visual.forward * distance + Altitudes.AltIncVect * 4f;
            float angle = Mathf.Atan2(visual.forward.x, visual.forward.z) * 57.29578f + 180f + visual.angleOffsetDegrees;

            DrawArcMesh(center, angle, drawSize, alpha, material);
        }

        private static Material ResolveArcMaterial(string texPath)
        {
            if (texPath.NullOrEmpty())
            {
                return null;
            }

            Material material;
            if (arcMaterials.TryGetValue(texPath, out material))
            {
                return material;
            }

            if (triedLoadArcMaterials.Contains(texPath))
            {
                return null;
            }

            triedLoadArcMaterials.Add(texPath);
            Texture2D texture = ContentFinder<Texture2D>.Get(texPath, reportFailure: false);
            if (texture == null)
            {
                return null;
            }

            material = MaterialPool.MatFrom(texture, ShaderDatabase.MoteGlow, new Color(1f, 0.96f, 0.98f, 1f));
            arcMaterials[texPath] = material;
            return material;
        }

        private static Material ResolveLightningMaterial()
        {
            if (lightningMaterial != null)
            {
                return lightningMaterial;
            }

            if (triedLoadLightningMaterial)
            {
                return null;
            }

            triedLoadLightningMaterial = true;
            lightningMaterial = MatLoader.LoadMat("Weather/LightningBolt", -1);
            return lightningMaterial;
        }

        private static float LightningBrightness(int age, int durationTicks)
        {
            if (age <= 3)
            {
                return Mathf.Clamp01(age / 3f);
            }

            return Mathf.Clamp01(1f - age / (float)Mathf.Max(1, durationTicks));
        }

        private static float EaseOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - Mathf.Pow(1f - value, 3f);
        }

        private void AddArcVisual(Vector3 origin, Vector3 forward, float radius, float angleDegrees, float angleOffsetDegrees, int startTick, int durationTicks, string texPath)
        {
            while (arcVisuals.Count >= MaxVisuals)
            {
                arcVisuals.RemoveAt(0);
            }

            arcVisuals.Add(new FlowerDivinationSlashArcVisual
            {
                origin = origin,
                forward = forward,
                radius = Mathf.Max(0.5f, radius),
                angleDegrees = Mathf.Clamp(angleDegrees, 15f, 160f),
                angleOffsetDegrees = angleOffsetDegrees,
                startTick = startTick,
                durationTicks = Mathf.Max(1, durationTicks),
                texPath = texPath
            });
        }

        private static void DrawArcMesh(Vector3 center, float angle, float drawSize, float alpha, Material baseMaterial)
        {
            Material faded = FadedMaterialPool.FadedVersionOf(baseMaterial, Mathf.Clamp01(alpha));
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            float size = Mathf.Max(0.01f, drawSize);
            Matrix4x4 matrix = Matrix4x4.TRS(center, rotation, new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, faded, 0);
        }

        private struct FlowerDivinationSlashArcVisual
        {
            public Vector3 origin;
            public Vector3 forward;
            public float radius;
            public float angleDegrees;
            public float angleOffsetDegrees;
            public int startTick;
            public int durationTicks;
            public string texPath;
        }

        private struct FlowerDivinationAfterimage
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

        private struct FlowerDivinationLightningBolt
        {
            public IntVec3 strikeCell;
            public Mesh boltMesh;
            public int startTick;
            public int durationTicks;
        }

        private struct FlowerDivinationSlashDelayedImpact
        {
            public Pawn caster;
            public IntVec3 landing;
            public IntVec3 directionCell;
            public int triggerTick;
            public CompProperties_AbilityFlowerDivinationSlash props;
        }
    }
}
