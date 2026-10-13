using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Profile;

namespace MiliraXian.Characters.Mingyuan
{
    public sealed class GameComponent_MingyuanWingUnityAnimation : GameComponent
    {
        public GameComponent_MingyuanWingUnityAnimation(Game game) { }
        public override void StartedNewGame() => MingyuanWingUnityAnimationRuntime.Reset();
        public override void LoadedGame() => MingyuanWingUnityAnimationRuntime.Reset();
        public override void GameComponentUpdate() => MingyuanWingUnityAnimationRuntime.Update();
    }

    [StaticConstructorOnStartup]
    internal static class MingyuanWingUnityAnimationRuntime
    {
        static MingyuanWingUnityAnimationRuntime()
        {
            CharacterLib.CharacterRaceParts.FlyAnimationOverride = TryGetOverride;
        }

        private static AnimationDef TryGetOverride(Pawn pawn, AnimationDef raceAnimation) =>
            TryGetFlyAnimation(pawn, pawn.Rotation, out AnimationDef animationDef) ? animationDef : null;

        private const string MingyuanPawnKindDefName = "MiliraXian_Mingyuan";
        private const string RaceFrameTextureRoot = "Milira/Pawn/Wings/Milira_Fly";
        private const string NorthAnimationDefName = "Milira_FlyNorth_Mingyuan_Unity";
        private const string EastAnimationDefName = "Milira_FlyEast_Mingyuan_Unity";
        private const string SouthAnimationDefName = "Milira_FlySouth_Mingyuan_Unity";
        private const string WestAnimationDefName = "Milira_FlyWest_Mingyuan_Unity";
        private const float FrameDrawSize = 3.75f;
        private const int AnimationDurationTicks = 36, FrameCount = 8;
        private const int NorthFrameSetIndex = 0, EastFrameSetIndex = 1, SouthFrameSetIndex = 2;
        private static readonly int[] FrameTicks = { 0, 3, 9, 12, 15, 21, 25, 30 };
        private static readonly Dictionary<int, DriverState> Drivers = new();
        private static readonly List<int> RemovalBuffer = new();
        private static readonly Material[][] FrameMaterials =
        {
            new Material[FrameCount], new Material[FrameCount], new Material[FrameCount]
        };
        private static bool runtimeFailed, frameMaterialsLoaded, frameMaterialsFailed, animationDefsResolved;
        private static AnimationDef northAnimation, eastAnimation, southAnimation, westAnimation;

        private sealed class DriverState
        {
            public Pawn Pawn;
            public Rot4 Facing;
            public float Phase;
            public int LastTick;
        }

        internal static bool TryGetFlyAnimation(Pawn pawn, Rot4 facing, out AnimationDef animationDef)
        {
            animationDef = null;
            if (runtimeFailed || pawn?.kindDef?.defName != MingyuanPawnKindDefName
                || !pawn.Spawned || pawn.Map != Find.CurrentMap
                || !TryResolveAnimationDefs() || !TryLoadFrameMaterials(pawn)) return false;
            if (!Drivers.TryGetValue(pawn.thingIDNumber, out DriverState state))
            {
                state = new DriverState
                {
                    Pawn = pawn, Phase = Mathf.Repeat(-Rand.RangeSeeded(0, AnimationDurationTicks, pawn.thingIDNumber), AnimationDurationTicks) / AnimationDurationTicks,
                    LastTick = Find.TickManager.TicksGame
                };
                Drivers.Add(pawn.thingIDNumber, state);
            }
            state.Facing = facing;
            animationDef = facing == Rot4.North ? northAnimation : facing == Rot4.South ? southAnimation
                : facing == Rot4.West ? westAnimation : eastAnimation;
            return true;
        }

        internal static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                Drivers.Clear();
                return;
            }
            int now = Find.TickManager.TicksGame;
            RemovalBuffer.Clear();
            foreach (KeyValuePair<int, DriverState> pair in Drivers)
            {
                DriverState state = pair.Value; Pawn pawn = state.Pawn;
                AnimationDef animation = pawn?.Drawer?.renderer?.CurAnimation;
                if (pawn == null || pawn.Destroyed || pawn.Dead || !pawn.Spawned || pawn.Map != Find.CurrentMap
                    || !pawn.Flying && animation != northAnimation && animation != eastAnimation && animation != southAnimation && animation != westAnimation)
                {
                    RemovalBuffer.Add(pair.Key);
                    continue;
                }
                float tickRate = Mathf.Max(1f, Find.TickManager.TickRateMultiplier);
                float visualTicks = Mathf.Max(0, now - state.LastTick) * Mathf.Min(3f, tickRate) / tickRate;
                state.Phase = Mathf.Repeat(state.Phase + visualTicks / AnimationDurationTicks, 1f);
                state.LastTick = now;
                if (Find.CameraDriver?.CurrentViewRect.ExpandedBy(1).Contains(pawn.Position) != true) continue;
                try { SampleAndDraw(state); }
                catch (Exception exception)
                {
                    runtimeFailed = true;
                    Log.ErrorOnce("[MiliraXian Mingyuan] Wing frames could not be drawn; using the race animation.\n" + exception, 178643201);
                    foreach (DriverState driver in Drivers.Values)
                    {
                        Pawn owner = driver.Pawn;
                        // Let the flight tracker restore its declared animation and keep its landing bookkeeping.
                        owner?.Drawer?.renderer?.SetAnimation(null);
                    }
                    break;
                }
            }
            if (runtimeFailed) Drivers.Clear();
            else foreach (int id in RemovalBuffer) Drivers.Remove(id);
        }

        internal static void Reset()
        {
            Drivers.Clear(); RemovalBuffer.Clear(); runtimeFailed = false;
        }

        private static bool TryResolveAnimationDefs()
        {
            if (animationDefsResolved)
            {
                return true;
            }

            northAnimation = DefDatabase<AnimationDef>.GetNamedSilentFail(NorthAnimationDefName);
            eastAnimation = DefDatabase<AnimationDef>.GetNamedSilentFail(EastAnimationDefName);
            southAnimation = DefDatabase<AnimationDef>.GetNamedSilentFail(SouthAnimationDefName);
            westAnimation = DefDatabase<AnimationDef>.GetNamedSilentFail(WestAnimationDefName);
            animationDefsResolved = northAnimation != null
                && eastAnimation != null
                && southAnimation != null
                && westAnimation != null;

            if (!animationDefsResolved)
            {
                Log.ErrorOnce(
                    "[MiliraXian Mingyuan] One or more Unity wing AnimationDefs are missing; using the legacy eight-frame animation.",
                    178643202);
            }

            return animationDefsResolved;
        }

        private static bool TryLoadFrameMaterials(Pawn pawn)
        {
            if (frameMaterialsLoaded)
            {
                return true;
            }

            if (frameMaterialsFailed)
            {
                return false;
            }

            try
            {
                LoadFrameSet(pawn, NorthFrameSetIndex, "North");
                LoadFrameSet(pawn, EastFrameSetIndex, "East");
                LoadFrameSet(pawn, SouthFrameSetIndex, "South");
                frameMaterialsLoaded = true;
                return true;
            }
            catch (Exception exception)
            {
                frameMaterialsFailed = true;
                Log.ErrorOnce(
                    "[MiliraXian Mingyuan] One or more full flight frames could not be loaded; using the legacy eight-frame animation.\n"
                    + exception,
                    178643203);
                return false;
            }
        }

        private static void LoadFrameSet(Pawn pawn, int frameSetIndex, string direction)
        {
            Material[] materials = FrameMaterials[frameSetIndex];
            for (int frameIndex = 0; frameIndex < FrameCount; frameIndex++)
            {
                // Resolved through the character's declared part swaps rather than a second
                // hardcoded root, so the destination is written in one place only.
                string raceTexturePath = RaceFrameTextureRoot + direction + "_" + (frameIndex + 1);
                string texturePath = CharacterLib.CharacterRaceParts.TexPathFor(pawn, raceTexturePath);
                if (texturePath == null)
                {
                    throw new InvalidOperationException(
                        "No part swap declares a replacement for " + raceTexturePath + ".");
                }

                Texture2D texture = ContentFinder<Texture2D>.Get(texturePath, reportFailure: false);
                if (texture == null)
                {
                    throw new FileNotFoundException("Missing Mingyuan flight frame texture: " + texturePath);
                }

                // Match Graphic_Single's default flight-frame shader. Transparent alpha
                // crossfades make displaced feather silhouettes pulse at frame boundaries.
                Material material = MaterialPool.MatFrom(texture, ShaderDatabase.Cutout, Color.white);
                if (material == null)
                {
                    throw new InvalidOperationException("Could not create the shared material for " + texturePath);
                }

                materials[frameIndex] = material;
            }
        }


        private static void SampleAndDraw(DriverState state)
        {
            int frameSet = state.Facing == Rot4.North ? NorthFrameSetIndex
                : state.Facing == Rot4.South ? SouthFrameSetIndex : EastFrameSetIndex;
            float tick = state.Phase * AnimationDurationTicks;
            int frame = 0;
            while (frame + 1 < FrameCount && tick >= FrameTicks[frame + 1]) frame++;
            float layer = state.Facing == Rot4.South ? -10f : 60f;
            float bodyAngle = state.Facing == Rot4.East ? 8f : state.Facing == Rot4.West ? -8f : 0f;
            Vector3 position = state.Pawn.DrawPos;
            position.y += PawnRenderUtility.AltitudeForLayer(layer);
            position.z += BodyBobAt(state.Phase);
            Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.AngleAxis(bodyAngle, Vector3.up), new Vector3(FrameDrawSize, 1f, FrameDrawSize));
            Graphics.DrawMesh(state.Facing == Rot4.West ? MeshPool.plane10Flip : MeshPool.plane10,
                matrix, FrameMaterials[frameSet][frame], 0);
        }

        private static float BodyBobAt(float phase)
        {
            float tick = phase * AnimationDurationTicks;
            if (tick <= 10f)
            {
                return Mathf.Lerp(-0.0125f, 0.01f, tick / 10f);
            }

            if (tick <= 15f)
            {
                return Mathf.Lerp(0.01f, 0.0125f, (tick - 10f) / 5f);
            }

            return Mathf.Lerp(0.0125f, -0.0125f, (tick - 15f) / 21f);
        }

    }

    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
    internal static class Patch_MingyuanWingUnityAnimation_ClearAllMapsAndWorld
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            MingyuanWingUnityAnimationRuntime.Reset();
        }
    }
}
