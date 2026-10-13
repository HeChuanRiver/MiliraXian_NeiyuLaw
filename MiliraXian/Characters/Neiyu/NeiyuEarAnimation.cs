using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AlienRace;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters.Common.Vfx;

namespace MiliraXian.Characters.Neiyu
{
    internal struct NeiyuEarPose
    {
        public static readonly NeiyuEarPose Neutral = new NeiyuEarPose(0f, 1f, 1f);

        public NeiyuEarPose(float angle, float scaleX, float scaleY)
        {
            Angle = angle;
            ScaleX = scaleX;
            ScaleY = scaleY;
        }

        public float Angle { get; }
        public float ScaleX { get; }
        public float ScaleY { get; }
    }

    public sealed class GameComponent_NeiyuEarAnimation : GameComponent
    {
        public GameComponent_NeiyuEarAnimation(Game game)
        {
        }

        public override void StartedNewGame()
        {
            NeiyuEarAnimationRuntime.Reset();
        }

        public override void LoadedGame()
        {
            NeiyuEarAnimationRuntime.Reset();
        }

        public override void GameComponentUpdate()
        {
            NeiyuEarAnimationRuntime.Update();
        }
    }

    [StaticConstructorOnStartup]
    internal static class NeiyuEarAnimationRuntime
    {
        private const float DriverScanInterval = 0.5f, InactiveDriverLifetime = 3f, AlertCooldown = 2.5f;
        private static readonly Dictionary<int, DriverState> Drivers = new();
        private static readonly ConcurrentDictionary<int, NeiyuEarPose> Poses = new();
        private static readonly List<int> RemovalBuffer = new();
        private static readonly CharacterVfxDef Idle = DefDatabase<CharacterVfxDef>.GetNamedSilentFail("MX_Vfx_neiyu_ear_idle");
        private static readonly CharacterVfxDef Twitch = DefDatabase<CharacterVfxDef>.GetNamedSilentFail("MX_Vfx_neiyu_ear_twitch");
        private static readonly CharacterVfxDef Alert = DefDatabase<CharacterVfxDef>.GetNamedSilentFail("MX_Vfx_neiyu_ear_alert");
        private static float nextDriverScanAt;
        private static int lastGameTick = -1;
        private static float visualTime;

        private sealed class DriverState
        {
            public Pawn Pawn;
            public CharacterVfxAnimation Animation;
            public CharacterVfxDef Clip;
            public Transform Pair;
            public NeiyuEarPose BlendFrom;
            public System.Random Random;
            public float LastHealth, NextTwitchAt, NextAlertAllowedAt, LastSeenAt, ClipStart;
        }

        public static bool TryGetPose(Pawn pawn, out NeiyuEarPose pose)
        {
            if (pawn != null && Poses.TryGetValue(pawn.thingIDNumber, out pose)) return true;
            pose = NeiyuEarPose.Neutral;
            return false;
        }

        public static void Reset()
        {
            foreach (DriverState state in Drivers.Values) state.Animation.Dispose();
            Drivers.Clear(); Poses.Clear(); RemovalBuffer.Clear(); nextDriverScanAt = 0f;
            lastGameTick = -1; visualTime = 0f;
        }

        public static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                if (Drivers.Count > 0) Reset();
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (lastGameTick >= 0)
                visualTime += Mathf.Max(0, tick - lastGameTick) / (60f * Mathf.Max(1f, Find.TickManager.TickRateMultiplier));
            lastGameTick = tick;
            // The authored ear clips and twitch intervals have always used unscaled seconds.
            float now = visualTime;
            if (now >= nextDriverScanAt)
            {
                ScanCurrentMap(now);
                nextDriverScanAt = now + DriverScanInterval;
            }
            foreach (DriverState state in Drivers.Values) UpdateDriver(state, now);
        }

        private static void ScanCurrentMap(float now)
        {
            Map map = Find.CurrentMap;
            if (map != null && Idle != null && Twitch != null && Alert != null)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || pawn.Dead) continue;
                    DriverState state = EnsureDriver(pawn, now);
                    if (state != null) state.LastSeenAt = now;
                }
            }
            RemovalBuffer.Clear();
            foreach (KeyValuePair<int, DriverState> pair in Drivers)
            {
                Pawn pawn = pair.Value.Pawn;
                if (pawn == null || pawn.Destroyed || pawn.Dead || !pawn.Spawned || pawn.Map != map
                    || now - pair.Value.LastSeenAt > InactiveDriverLifetime) RemovalBuffer.Add(pair.Key);
            }
            foreach (int id in RemovalBuffer)
            {
                Drivers[id].Animation.Dispose();
                Drivers.Remove(id); Poses.TryRemove(id, out _);
            }
        }

        private static DriverState EnsureDriver(Pawn pawn, float now)
        {
            if (Drivers.TryGetValue(pawn.thingIDNumber, out DriverState state)) return state;
            CharacterVfxAnimation animation = Idle.Instantiate();
            if (animation == null) return null;
            Transform pair = animation.Find("EarPair_Motion");
            if (pair == null) { animation.Dispose(); return null; }
            state = new DriverState
            {
                Pawn = pawn, Animation = animation, Pair = pair, Clip = Idle, LastHealth = CurrentHealth(pawn),
                Random = new System.Random(unchecked(pawn.thingIDNumber * 397 ^ 0x4E495955)),
                LastSeenAt = now, NextAlertAllowedAt = now
            };
            state.NextTwitchAt = now + NextInterval(state, false);
            Drivers.Add(pawn.thingIDNumber, state); Poses[pawn.thingIDNumber] = NeiyuEarPose.Neutral;
            return state;
        }

        private static void UpdateDriver(DriverState state, float now)
        {
            Pawn pawn = state.Pawn;
            if (!Find.TickManager.Paused && !pawn.Dead && !pawn.Downed && pawn.Awake())
            {
                float health = CurrentHealth(pawn);
                if (health < state.LastHealth - 0.001f && now >= state.NextAlertAllowedAt)
                {
                    FireTrigger(state, Alert, now);
                    state.NextAlertAllowedAt = now + AlertCooldown;
                    state.NextTwitchAt = now + NextInterval(state, IsAlerted(pawn));
                }
                state.LastHealth = health;
                if (now >= state.NextTwitchAt)
                {
                    FireTrigger(state, Twitch, now);
                    state.NextTwitchAt = now + NextInterval(state, IsAlerted(pawn));
                }
            }
            float progress = (now - state.ClipStart) / Mathf.Max(0.001f, state.Clip.duration);
            if (progress >= 1f) { state.Clip = Idle; progress = 0f; }
            state.Animation.Sample(Mathf.Clamp01(progress), state.Clip);
            NeiyuEarPose pose = ReadPose(state.Pair);
            if (state.Clip.blendInDuration > 0f && now - state.ClipStart < state.Clip.blendInDuration)
            {
                float blend = Mathf.Clamp01((now - state.ClipStart) / state.Clip.blendInDuration);
                pose = new NeiyuEarPose(Mathf.Lerp(state.BlendFrom.Angle, pose.Angle, blend),
                    Mathf.Lerp(state.BlendFrom.ScaleX, pose.ScaleX, blend), Mathf.Lerp(state.BlendFrom.ScaleY, pose.ScaleY, blend));
            }
            Poses[state.Pawn.thingIDNumber] = pose;
        }

        private static void FireTrigger(DriverState state, CharacterVfxDef clip, float now)
        {
            state.BlendFrom = Poses.TryGetValue(state.Pawn.thingIDNumber, out NeiyuEarPose pose) ? pose : NeiyuEarPose.Neutral;
            state.Clip = clip; state.ClipStart = now;
        }

        private static float NextInterval(DriverState state, bool alerted)
        {
            float minimum = alerted ? 6f : 10f;
            float maximum = alerted ? 12f : 22f;
            return Mathf.Lerp(minimum, maximum, (float)state.Random.NextDouble());
        }

        private static bool IsAlerted(Pawn pawn)
        {
            return pawn.Drafted || pawn.InMentalState;
        }

        private static float CurrentHealth(Pawn pawn)
        {
            if (pawn?.health?.summaryHealth == null)
            {
                return 1f;
            }

            return pawn.health.summaryHealth.SummaryHealthPercent;
        }

        private static NeiyuEarPose ReadPose(Transform pair)
        {
            float angle = Mathf.Clamp(Mathf.DeltaAngle(0f, pair.localEulerAngles.z), -5f, 5f);
            Vector3 scale = pair.localScale;
            return new NeiyuEarPose(
                angle,
                Mathf.Clamp(scale.x, 0.94f, 1.06f),
                Mathf.Clamp(scale.y, 0.94f, 1.06f));
        }

    }

    internal static class NeiyuEarRenderUtility
    {
        private const string EarAddonName = "milira ear feather Neiyu";

        public static bool TryGetEarPose(PawnRenderNode node, PawnDrawParms parms, out NeiyuEarPose pose)
        {
            pose = NeiyuEarPose.Neutral;
            if (parms.pawn == null
                || parms.Portrait
                || parms.Statue
                || parms.flags.FlagSet(PawnRenderFlags.Cache)
                || !NeiyuEquipmentUtility.IsNeiyu(parms.pawn)
                || !(node is AlienPawnRenderNode_BodyAddon))
            {
                return false;
            }

            AlienPartGenerator.BodyAddon addon = AlienPawnRenderNodeWorker_BodyAddon.AddonFromNode(node);
            return addon != null
                && addon.Name == EarAddonName
                && NeiyuEarAnimationRuntime.TryGetPose(parms.pawn, out pose);
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    internal static class Patch_NeiyuEarAnimation_DisablePawnCache
    {
        [HarmonyPrefix]
        private static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (NeiyuEquipmentUtility.IsNeiyu(___pawn))
            {
                disableCache = true;
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.RotationFor))]
    internal static class Patch_NeiyuEarAnimation_Rotation
    {
        [HarmonyPostfix]
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Quaternion __result)
        {
            if (!NeiyuEarRenderUtility.TryGetEarPose(node, parms, out NeiyuEarPose pose))
            {
                return;
            }

            float angle = parms.facing == Rot4.West ? -pose.Angle : pose.Angle;
            __result *= Quaternion.AngleAxis(angle, Vector3.up);
        }
    }

    [HarmonyPatch(typeof(AlienPawnRenderNodeWorker_BodyAddon), nameof(AlienPawnRenderNodeWorker_BodyAddon.ScaleFor))]
    internal static class Patch_NeiyuEarAnimation_Scale
    {
        [HarmonyPostfix]
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (!NeiyuEarRenderUtility.TryGetEarPose(node, parms, out NeiyuEarPose pose))
            {
                return;
            }

            __result.x *= pose.ScaleX;
            __result.z *= pose.ScaleY;
        }
    }
}
