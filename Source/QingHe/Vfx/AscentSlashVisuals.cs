using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Abilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Vfx
{
    public class PawnFlyer_AscentSlash : PawnFlyer
    {
        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            Pawn flyingPawn = FlyingPawn;
            if (flyingPawn != null && flyingPawn.MapHeld != null)
            {
                flyingPawn.MapHeld.GetComponent<MapComponent_QingheFlowerDanceVisuals>()?.AddAfterimage(
                    flyingPawn,
                    DrawPos,
                    flyingPawn.Rotation,
                    60,
                    0.44f);
            }

            if (Map != null && Rand.Chance(0.35f))
            {
                FleckMaker.ThrowAirPuffUp(DrawPos, Map);
            }
        }
    }

    public class MapComponent_QingheAscentSlashVisuals : MapComponent
    {
        private const string DefaultArcTexPathFirst = "MiliraXianQinghe/Effect/flower_divination_slash_1";
        private const string DefaultArcTexPathSecond = "MiliraXianQinghe/Effect/flower_divination_slash_2";
        private const int SecondArcDelayTicks = 5;
        private const float FirstArcAngleOffset = -9f;
        private const float SecondArcAngleOffset = 9f;
        private const int MaxVisuals = 24;
        private const int MaxLightningBolts = 24;
        private const int DefaultLightningBoltDurationTicks = 18;

        private static readonly Dictionary<string, Material> arcMaterials = new Dictionary<string, Material>();
        private static readonly HashSet<string> triedLoadArcMaterials = new HashSet<string>();
        private static Material lightningMaterial;
        private static bool triedLoadLightningMaterial;

        private readonly List<AscentSlashArcVisual> arcVisuals = new List<AscentSlashArcVisual>();
        private readonly List<AscentSlashLightningBolt> lightningBolts = new List<AscentSlashLightningBolt>();
        private readonly List<AscentSlashDelayedImpact> delayedImpacts = new List<AscentSlashDelayedImpact>();

        public MapComponent_QingheAscentSlashVisuals(Map map) : base(map)
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

            lightningBolts.Add(new AscentSlashLightningBolt
            {
                strikeCell = strikeCell,
                boltMesh = boltMesh,
                startTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0,
                durationTicks = Mathf.Max(1, durationTicks)
            });
        }

        public void AddDelayedImpact(Pawn caster, IntVec3 landing, IntVec3 directionCell, int delayTicks, CompProperties_AbilityAscentSlash props)
        {
            if (map == null || caster == null || props == null || !landing.IsValid || !directionCell.IsValid)
            {
                return;
            }

            delayedImpacts.Add(new AscentSlashDelayedImpact
            {
                caster = caster,
                landing = landing,
                directionCell = directionCell,
                triggerTick = (Find.TickManager != null ? Find.TickManager.TicksGame : 0) + Mathf.Max(0, delayTicks),
                props = props
            });
        }

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            DrawLightningBolts(now);
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
                AscentSlashDelayedImpact impact = delayedImpacts[i];
                if (now < impact.triggerTick)
                {
                    continue;
                }

                delayedImpacts.RemoveAt(i);
                CompAbilityEffect_AscentSlash.ResolveDelayedConeImpact(impact.caster, map, impact.landing, impact.directionCell, impact.props);
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
                AscentSlashLightningBolt bolt = lightningBolts[i];
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

        private void DrawArcs(int now)
        {
            if (arcVisuals.Count == 0)
            {
                return;
            }

            for (int i = arcVisuals.Count - 1; i >= 0; i--)
            {
                AscentSlashArcVisual visual = arcVisuals[i];
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

        private static void DrawArc(AscentSlashArcVisual visual, float progress)
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

        private void AddArcVisual(Vector3 origin, Vector3 forward, float radius, float angleDegrees, float angleOffsetDegrees, int startTick, int durationTicks, string texPath)
        {
            while (arcVisuals.Count >= MaxVisuals)
            {
                arcVisuals.RemoveAt(0);
            }

            arcVisuals.Add(new AscentSlashArcVisual
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

        private static void DrawArcMesh(Vector3 center, float angle, float drawSize, float alpha, Material baseMaterial)
        {
            Material faded = FadedMaterialPool.FadedVersionOf(baseMaterial, Mathf.Clamp01(alpha));
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            float size = Mathf.Max(0.01f, drawSize);
            Matrix4x4 matrix = Matrix4x4.TRS(center, rotation, new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, faded, 0);
        }

        private struct AscentSlashArcVisual
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

        private struct AscentSlashLightningBolt
        {
            public IntVec3 strikeCell;
            public Mesh boltMesh;
            public int startTick;
            public int durationTicks;
        }

        private struct AscentSlashDelayedImpact
        {
            public Pawn caster;
            public IntVec3 landing;
            public IntVec3 directionCell;
            public int triggerTick;
            public CompProperties_AbilityAscentSlash props;
        }
    }
}
