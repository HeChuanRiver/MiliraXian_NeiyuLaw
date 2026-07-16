using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MiliraXian.Characters.Vfx;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Vfx
{
    internal enum AscentSlashVisualStage
    {
        Dash,
        Ascending,
        Hover,
        Descending
    }

    internal struct AscentSlashVisualState
    {
        public AscentSlashVisualStage stage;
        public int stageStartTick;
        public int stageEndTick;
        public Vector3 dashStartPos;
        public Vector3 dashEndPos;
        public float maxAltitudeLayers;
        public float maxForwardOffset;
        public float easingPower;
    }

    internal static class AscentSlashVisualTracker
    {
        private sealed class StateHolder
        {
            public AscentSlashVisualState state;
        }

        private static ConditionalWeakTable<Pawn, StateHolder> states = new();
        private static int activeStateCount;

        public static void BeginDash(Pawn pawn, int startTick, int endTick, Vector3 startPos, Vector3 endPos)
        {
            SetState(pawn, new AscentSlashVisualState
            {
                stage = AscentSlashVisualStage.Dash,
                stageStartTick = startTick,
                stageEndTick = Mathf.Max(startTick + 1, endTick),
                dashStartPos = startPos,
                dashEndPos = endPos,
                easingPower = 2.6f
            });
        }

        public static void BeginAscent(Pawn pawn, int startTick, int endTick, float maxAltitudeLayers, float maxForwardOffset, float easingPower)
        {
            SetHeightStage(pawn, AscentSlashVisualStage.Ascending, startTick, endTick, maxAltitudeLayers, maxForwardOffset, easingPower);
        }

        public static void BeginHover(Pawn pawn, int startTick, int endTick, float maxAltitudeLayers, float maxForwardOffset)
        {
            SetHeightStage(pawn, AscentSlashVisualStage.Hover, startTick, endTick, maxAltitudeLayers, maxForwardOffset, 1f);
        }

        public static void BeginDescent(Pawn pawn, int startTick, int endTick, float maxAltitudeLayers, float maxForwardOffset, float easingPower)
        {
            SetHeightStage(pawn, AscentSlashVisualStage.Descending, startTick, endTick, maxAltitudeLayers, maxForwardOffset, easingPower);
        }

        public static void Clear(Pawn pawn)
        {
            if (pawn != null)
            {
                if (states.Remove(pawn))
                {
                    activeStateCount = Mathf.Max(0, activeStateCount - 1);
                }
            }
        }

        public static void ClearAll()
        {
            states = new ConditionalWeakTable<Pawn, StateHolder>();
            activeStateCount = 0;
        }

        public static bool TryApply(Pawn pawn, ref Vector3 drawPos)
        {
            if (activeStateCount == 0 || pawn == null || !states.TryGetValue(pawn, out StateHolder holder))
            {
                return false;
            }

            AscentSlashVisualState state = holder.state;

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : state.stageEndTick;
            float progress = state.stageEndTick > state.stageStartTick
                ? Mathf.Clamp01((now - state.stageStartTick) / (float)(state.stageEndTick - state.stageStartTick))
                : 1f;

            if (state.stage == AscentSlashVisualStage.Dash)
            {
                int sampledTick = Mathf.Min(now + 1, state.stageEndTick);
                progress = state.stageEndTick > state.stageStartTick
                    ? Mathf.Clamp01((sampledTick - state.stageStartTick) / (float)(state.stageEndTick - state.stageStartTick))
                    : 1f;
                float easedProgress = 1f - Mathf.Pow(1f - progress, state.easingPower);
                Vector3 absolutePos = Vector3.Lerp(state.dashStartPos, state.dashEndPos, easedProgress);
                absolutePos.y = state.dashStartPos.y;
                drawPos = absolutePos;
                return true;
            }

            float height;
            switch (state.stage)
            {
                case AscentSlashVisualStage.Ascending:
                    height = 1f - Mathf.Pow(1f - progress, state.easingPower);
                    break;
                case AscentSlashVisualStage.Hover:
                    height = 1f;
                    break;
                case AscentSlashVisualStage.Descending:
                    height = 1f - Mathf.Pow(progress, state.easingPower);
                    break;
                default:
                    return false;
            }

            drawPos += Altitudes.AltIncVect * (state.maxAltitudeLayers * height)
                + Vector3.forward * (state.maxForwardOffset * height);
            return true;
        }

        private static void SetHeightStage(
            Pawn pawn,
            AscentSlashVisualStage stage,
            int startTick,
            int endTick,
            float maxAltitudeLayers,
            float maxForwardOffset,
            float easingPower)
        {
            SetState(pawn, new AscentSlashVisualState
            {
                stage = stage,
                stageStartTick = startTick,
                stageEndTick = Mathf.Max(startTick + 1, endTick),
                maxAltitudeLayers = Mathf.Max(0f, maxAltitudeLayers),
                maxForwardOffset = Mathf.Max(0f, maxForwardOffset),
                easingPower = Mathf.Max(1f, easingPower)
            });
        }

        private static void SetState(Pawn pawn, AscentSlashVisualState state)
        {
            if (pawn == null)
            {
                return;
            }

            if (states.TryGetValue(pawn, out StateHolder holder))
            {
                holder.state = state;
                return;
            }

            states.Add(pawn, new StateHolder { state = state });
            activeStateCount++;
        }
    }

    [HarmonyPatch(typeof(Verse.Profile.MemoryUtility), nameof(Verse.Profile.MemoryUtility.ClearAllMapsAndWorld))]
    internal static class MX_QHAscentSlashClearAllMapsPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            AscentSlashVisualTracker.ClearAll();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class MX_QHAscentSlashPawnKillPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn __instance)
        {
            AscentSlashVisualTracker.Clear(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn), typeof(DestroyMode))]
    internal static class MX_QHAscentSlashPawnDeSpawnPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn __instance)
        {
            AscentSlashVisualTracker.Clear(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn_DrawTracker), "DrawPos", MethodType.Getter)]
    public static class MX_QHAscentSlashDrawPosPatches
    {
        [HarmonyPostfix]
        public static void Patch_DrawPos_Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            Pawn pawn = ___pawn;
            if (pawn == null || pawn.Destroyed || !pawn.Spawned)
            {
                return;
            }

            AscentSlashVisualTracker.TryApply(pawn, ref __result);
        }
    }

    public class PawnFlyerWorker_AscentSlashDive : PawnFlyerWorker
    {
        public PawnFlyerWorker_AscentSlashDive(PawnFlyerProperties properties) : base(properties)
        {
        }

        public override float GetHeight(float t)
        {
            if (t < 0.42f)
            {
                return Mathf.Pow(Mathf.Clamp01(t / 0.42f), 0.22f);
            }

            return 1f - Mathf.Pow(Mathf.Clamp01((t - 0.42f) / 0.58f), 1.2f);
        }
    }

    public class PawnFlyer_AscentSlash : PawnFlyer
    {
        private const float AscentStagePortion = 0.32f;
        private const float HoverStagePortion = 0.08f;
        private const float MaxAltitudeLayers = 96f;

        public override Vector3 DrawPos
        {
            get
            {
                return ComputeTwoStageDrawPos(out _, out _);
            }
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            Vector3 drawPos = ComputeTwoStageDrawPos(out _, out _);
            if (FlyingPawn != null)
            {
                FlyingPawn.DynamicDrawPhaseAt(phase, drawPos);
                return;
            }

            FlyingThing?.DynamicDrawPhaseAt(phase, drawPos);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Vector3 drawPos = ComputeTwoStageDrawPos(out Vector3 groundPos, out float height);
            DrawShadow(groundPos, height);
            if (CarriedThing != null && FlyingPawn != null)
            {
                PawnRenderUtility.DrawCarriedThing(FlyingPawn, drawPos, CarriedThing);
            }
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            Pawn flyingPawn = FlyingPawn;
            if (flyingPawn != null && flyingPawn.MapHeld != null)
            {
                flyingPawn.MapHeld.GetComponent<MapComponent_PawnAfterimages>()?.AddAfterimage(
                    flyingPawn,
                    DrawPos,
                    flyingPawn.Rotation,
                    60,
                    0.44f,
                    MX_QHRenderStatics.AfterimageTint);
            }

            if (Map != null && Rand.Chance(0.35f))
            {
                FleckMaker.ThrowAirPuffUp(DrawPos, Map);
            }
        }

        private Vector3 ComputeTwoStageDrawPos(out Vector3 groundPos, out float height)
        {
            float progress = ticksFlightTime > 0 ? Mathf.Clamp01(ticksFlying / (float)ticksFlightTime) : 1f;
            Vector3 destination = DestinationPos;
            float screenRise = Mathf.Max(0f, def?.pawnFlyer?.heightFactor ?? 0f);
            Vector3 liftOffset = Altitudes.AltIncVect * MaxAltitudeLayers + Vector3.forward * screenRise;

            if (progress < AscentStagePortion)
            {
                float ascentProgress = Mathf.Clamp01(progress / AscentStagePortion);
                float easedAscent = 1f - Mathf.Pow(1f - ascentProgress, 2.2f);
                height = easedAscent;
                groundPos = startVec;
                Position = groundPos.ToIntVec3();
                return startVec + liftOffset * easedAscent;
            }

            float diveStart = AscentStagePortion + HoverStagePortion;
            if (progress < diveStart)
            {
                height = 1f;
                groundPos = startVec;
                Position = groundPos.ToIntVec3();
                return startVec + liftOffset;
            }

            float diveProgress = Mathf.Clamp01((progress - diveStart) / (1f - diveStart));
            float easedDive = diveProgress * diveProgress;
            Vector3 peak = startVec + liftOffset;
            Vector3 drawPos = Vector3.Lerp(peak, destination, easedDive);
            groundPos = Vector3.Lerp(startVec, destination, easedDive);
            height = 1f - easedDive;
            Position = groundPos.ToIntVec3();
            return drawPos;
        }

        private void DrawShadow(Vector3 drawLoc, float height)
        {
            Material shadowMaterial = def?.pawnFlyer?.ShadowMaterial;
            if (shadowMaterial == null)
            {
                return;
            }

            float shadowScale = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(height));
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(drawLoc, Quaternion.identity, new Vector3(shadowScale, 1f, shadowScale));
            Graphics.DrawMesh(MeshPool.plane10, matrix, shadowMaterial, 0);
        }
    }

    public class MapComponent_QingheAscentSlashVisuals : MapComponent
    {
        private const int MaxLightningBolts = 24;
        private const int DefaultLightningBoltDurationTicks = 18;

        private static Material lightningMaterial;
        private static bool triedLoadLightningMaterial;

        private readonly List<AscentSlashLightningBolt> lightningBolts = new();

        public MapComponent_QingheAscentSlashVisuals(Map map) : base(map)
        {
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

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            DrawLightningBolts(now);
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

        private struct AscentSlashLightningBolt
        {
            public IntVec3 strikeCell;
            public Mesh boltMesh;
            public int startTick;
            public int durationTicks;
        }

    }
}
