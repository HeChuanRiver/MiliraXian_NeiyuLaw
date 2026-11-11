using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    public class MXMeleeSlashGhostExtension : DefModExtension
    {
        public string effecterDefName;
        public string fallbackFleckDefName = "ExplosionFlash";
        public FloatRange scaleRange = new(4.6f, 6.2f);
        public int minIntervalTicks = 5;
        public bool playOnMiss = false;
        public float facingAngleOffset = 0f;
        public List<string> cycleFleckDefNames = new();
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_MXNL_MeleeSlashGhost_TryCastShot
    {
        private sealed class SlashState
        {
            public int LastFxTick;
            public bool HasFxTick;
            public int NextCycleIndex;
        }

        private readonly record struct Swing(Pawn Caster, Map Map, MXMeleeSlashGhostExtension Extension,
            IntVec3 SourceCell, IntVec3 TargetCell, Vector3 Position, float Rotation);

        // Cut and Stab are separate verbs on the same weapon and must advance one cycle.
        private static readonly ConditionalWeakTable<Pawn, SlashState> StateByPawn = new();

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(Verb_MeleeAttack __instance, out Swing __state)
        {
            __state = default;

            MXMeleeSlashGhostExtension ext = __instance.EquipmentSource?.def.GetModExtension<MXMeleeSlashGhostExtension>();
            if (ext == null || __instance is IBladeMotionVerb { MotionActive: true })
            {
                return;
            }

            // Match the native early exits and capture the aim before hit effects move the target.
            Pawn caster = __instance.CasterPawn;
            if (caster is not { Spawned: true } || caster.stances.FullBodyBusy
                || __instance.CurrentTarget.Thing is not { } target)
            {
                return;
            }

            Vector3 position = target.DrawPos;
            Vector3 direction = position - caster.DrawPos;
            float rotation = direction.x * direction.x + direction.z * direction.z < 0.0001f
                ? caster.Rotation.AsAngle
                : direction.AngleFlat();
            __state = new Swing(caster, caster.Map, ext, caster.Position, target.Position, position,
                rotation + ext.facingAngleOffset);
        }

        [HarmonyPostfix]
        private static void Postfix(Swing __state, bool __result, bool __runOriginal)
        {
            MXMeleeSlashGhostExtension ext = __state.Extension;
            if (!__runOriginal || ext == null || (!__result && !ext.playOnMiss))
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            SlashState state = StateByPawn.GetOrCreateValue(__state.Caster);
            if (state.HasFxTick && now - state.LastFxTick < ext.minIntervalTicks)
            {
                return;
            }

            state.LastFxTick = now;
            state.HasFxTick = true;

            Rand.PushState();
            try
            {
                PlaySlash(__state, state);
            }
            finally
            {
                Rand.PopState();
            }
        }

        private static void PlaySlash(Swing swing, SlashState state)
        {
            MXMeleeSlashGhostExtension ext = swing.Extension;
            float scale = ext.scaleRange.RandomInRange;

            if (ext.cycleFleckDefNames is { Count: > 0 } cycle)
            {
                int index = state.NextCycleIndex % cycle.Count;
                state.NextCycleIndex = (index + 1) % cycle.Count;
                FleckDef cycleFleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(cycle[index]);
                if (cycleFleckDef != null)
                {
                    SpawnDirectionalFleck(swing.Map, swing.Position, cycleFleckDef, scale, swing.Rotation);
                    return;
                }
            }

            EffecterDef fxDef = ext.effecterDefName.NullOrEmpty()
                ? null
                : DefDatabase<EffecterDef>.GetNamedSilentFail(ext.effecterDefName);

            if (fxDef != null)
            {
                TargetInfo source = new(swing.SourceCell, swing.Map);
                TargetInfo target = new(swing.TargetCell, swing.Map);
                fxDef.Spawn(source, target, scale)?.Cleanup();
                return;
            }

            FleckDef fallback = ext.fallbackFleckDefName.NullOrEmpty()
                ? null
                : DefDatabase<FleckDef>.GetNamedSilentFail(ext.fallbackFleckDefName);

            if (fallback != null)
            {
                SpawnDirectionalFleck(swing.Map, swing.Position, fallback, scale, swing.Rotation);
            }
        }

        internal static void SpawnDirectionalFleck(Map map, Vector3 pos, FleckDef fleckDef, float scale, float rotation)
        {
            if (map == null || fleckDef == null)
            {
                return;
            }

            FleckCreationData dataStatic = FleckMaker.GetDataStatic(pos, map, fleckDef, Mathf.Max(0.01f, scale));
            dataStatic.rotation = rotation;
            dataStatic.rotationRate = 0f;
            map.flecks.CreateFleck(dataStatic);
        }
    }
}
