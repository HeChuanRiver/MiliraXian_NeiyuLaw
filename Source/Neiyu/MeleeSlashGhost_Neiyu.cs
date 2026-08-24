using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    public class MXMeleeSlashGhostExtension : DefModExtension
    {
        public string effecterDefName = "MXNL_Effecter_NeiyuSwordSlashGhost";
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

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Verb_MeleeAttack, SlashState> StateByVerb =
            new();

        [HarmonyPostfix]
        public static void Postfix(Verb_MeleeAttack __instance, bool __result)
        {
            if (__instance == null)
            {
                return;
            }

            Pawn caster = __instance.CasterPawn;
            if (caster == null || !caster.Spawned)
            {
                return;
            }

            ThingWithComps weapon = __instance.EquipmentSource;
            if (weapon?.def == null)
            {
                return;
            }

            MXMeleeSlashGhostExtension ext = weapon.def.GetModExtension<MXMeleeSlashGhostExtension>();
            if (ext == null)
            {
                return;
            }

            if (!__result && !ext.playOnMiss)
            {
                return;
            }

            Map map = caster.MapHeld;
            if (map == null)
            {
                return;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            int minInterval = Mathf.Max(0, ext.minIntervalTicks);
            SlashState state = StateByVerb.GetValue(__instance, CreateState);
            if (minInterval > 0 && state.HasFxTick && now - state.LastFxTick < minInterval)
            {
                return;
            }

            state.LastFxTick = now;
            state.HasFxTick = true;

            LocalTargetInfo curTarget = __instance.CurrentTarget;
            IntVec3 targetCell = curTarget.IsValid ? curTarget.Cell : caster.Position;
            if (!targetCell.IsValid || !targetCell.InBounds(map))
            {
                targetCell = caster.Position;
            }

            float scale = ext.scaleRange.RandomInRange;
            float rotation = ComputeSlashRotation(caster, curTarget) + ext.facingAngleOffset;
            Vector3 spawnPos = curTarget.IsValid ? curTarget.CenterVector3 : targetCell.ToVector3Shifted();

            if (ext.cycleFleckDefNames != null && ext.cycleFleckDefNames.Count > 0)
            {
                int nextIndex = state.NextCycleIndex;
                string fleckName = ext.cycleFleckDefNames[nextIndex % ext.cycleFleckDefNames.Count];
                state.NextCycleIndex = (nextIndex + 1) % Mathf.Max(1, ext.cycleFleckDefNames.Count);

                FleckDef cycleFleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(fleckName);
                if (cycleFleckDef != null)
                {
                    SpawnDirectionalFleck(map, spawnPos, cycleFleckDef, scale, rotation);
                    return;
                }
            }

            EffecterDef fxDef = ext.effecterDefName.NullOrEmpty()
                ? null
                : DefDatabase<EffecterDef>.GetNamedSilentFail(ext.effecterDefName);

            if (fxDef != null)
            {
                TargetInfo source = new(caster.Position, map);
                TargetInfo target = new(targetCell, map);
                Effecter fx = fxDef.Spawn(source, target, scale);
                if (fx != null)
                {
                    fx.Cleanup();
                }
                return;
            }

            FleckDef fallback = ext.fallbackFleckDefName.NullOrEmpty()
                ? null
                : DefDatabase<FleckDef>.GetNamedSilentFail(ext.fallbackFleckDefName);

            if (fallback != null)
            {
                SpawnDirectionalFleck(map, spawnPos, fallback, scale, rotation);
            }
        }

        private static SlashState CreateState(Verb_MeleeAttack verb)
        {
            return new SlashState();
        }

        private static float ComputeSlashRotation(Pawn caster, LocalTargetInfo target)
        {
            if (caster == null)
            {
                return 0f;
            }

            Vector3 origin = caster.DrawPos;
            Vector3 dest = target.IsValid ? target.CenterVector3 : origin;
            Vector3 vec = dest - origin;
            if (vec.x * vec.x + vec.z * vec.z < 0.0001f)
            {
                return caster.Rotation.AsAngle;
            }

            return Mathf.Atan2(0f - vec.z, vec.x) * 57.29578f;
        }

        private static void SpawnDirectionalFleck(Map map, Vector3 pos, FleckDef fleckDef, float scale, float rotation)
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
