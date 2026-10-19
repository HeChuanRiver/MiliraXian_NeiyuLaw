using System;
using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Neiyu
{
    [DefOf]
    public static class NeiyuTraitDefOf
    {
        public static TraitDef MX_Neiyu_SoulRemnant;
        public static TraitDef MX_Neiyu_IdleSleeper;
        public static TraitDef MX_Neiyu_Nishang;
        public static HediffDef MX_Neiyu_SoulRemnantState;
        public static HediffDef MX_Neiyu_SoulRecovery;
        public static JobDef MX_Neiyu_ExtraNutrition;
        public static JobDef MX_Neiyu_IdleSleep;
        public static JobDef MX_Neiyu_RecoverSoul;

        static NeiyuTraitDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(NeiyuTraitDefOf));
    }

    public class NeiyuSoulRemnantExtension : DefModExtension
    {
        public float dailyStorageLimit = 5f;
        public float dailyAutomaticNutrition = 2f;
        [MustTranslate] public string extraNutritionLabel;
        [MustTranslate] public string extraNutritionDescription;
        [MustTranslate] public string nutritionStatusFormat;
    }

    public class Hediff_NeiyuSoulRemnant : HediffWithComps
    {
        private bool allowExtraNutrition;
        private int nutritionDay = -1;
        private float nutritionStoredToday;

        private NeiyuSoulRemnantExtension Rules => def.GetModExtension<NeiyuSoulRemnantExtension>();
        public bool Active => CharacterTraitEffects.Grants(pawn, def);
        // Severity is the sole saved authority for cumulative nutrition; stages are authored in XML.
        public override bool ShouldRemove => false;
        public override bool Visible => Active;
        public override HediffStage CurStage => Active ? base.CurStage : null;
        private int CurrentDay => GenLocalDate.Year(pawn) * GenDate.DaysPerYear + GenLocalDate.DayOfYear(pawn);
        public float StoredToday => CurrentDay > nutritionDay ? 0f : nutritionStoredToday;
        public float AutomaticNutritionWanted => Active && allowExtraNutrition && !NeiyuSoulRecoveryUtility.IsRecovering(pawn)
            ? Mathf.Max(0f, Mathf.Min(Rules.dailyAutomaticNutrition - StoredToday, def.maxSeverity - Severity)) : 0f;

        public void RecordSurplus(float nutrition, bool automatic)
        {
            if (!Active || nutrition <= 0f || !float.IsFinite(nutrition)) return;
            int day = CurrentDay;
            if (day > nutritionDay)
            {
                nutritionDay = day;
                nutritionStoredToday = 0f;
            }
            float limit = automatic ? Mathf.Min(Rules.dailyStorageLimit, Rules.dailyAutomaticNutrition) : Rules.dailyStorageLimit;
            float stored = Mathf.Min(nutrition, Mathf.Max(0f, limit - nutritionStoredToday), def.maxSeverity - Severity);
            if (stored <= 0f) return;
            nutritionStoredToday += stored;
            Severity += stored;
        }

        public void ResetBodyGrowth()
        {
            // Death resets only body growth, never perk/cultivation progress, abilities or
            // today's already-used allowance. Repeated deaths cannot reopen the daily cap.
            Severity = 0f;
        }

        public override string LabelInBrackets => $"{Severity:0.##}/{def.maxSeverity:0.##}";

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Active || pawn.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = Rules.extraNutritionLabel,
                defaultDesc = Rules.extraNutritionDescription + "\n\n" + string.Format(Rules.nutritionStatusFormat,
                    Severity.ToString("0.##"), def.maxSeverity.ToString("0.##"), StoredToday.ToString("0.##"), Rules.dailyStorageLimit.ToString("0.##")),
                icon = TexCommand.ForbidOff,
                isActive = () => allowExtraNutrition,
                toggleAction = ToggleExtraNutrition
            };
        }

        private void ToggleExtraNutrition()
        {
            allowExtraNutrition = !allowExtraNutrition;
            if (!allowExtraNutrition && pawn.CurJobDef == NeiyuTraitDefOf.MX_Neiyu_ExtraNutrition)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref allowExtraNutrition, "allowExtraNutrition", false);
            Scribe_Values.Look(ref nutritionDay, "nutritionDay", -1);
            Scribe_Values.Look(ref nutritionStoredToday, "nutritionStoredToday", 0f);
        }

        internal static Hediff_NeiyuSoulRemnant Get(Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(NeiyuTraitDefOf.MX_Neiyu_SoulRemnantState) as Hediff_NeiyuSoulRemnant;
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    internal static class Patch_NeiyuSoulNutrition
    {
        private static void Prefix(Pawn ingester, ref float nutritionWanted,
            out (Hediff_NeiyuSoulRemnant soul, float foodDeficit, bool automatic) __state)
        {
            var soul = Hediff_NeiyuSoulRemnant.Get(ingester);
            Need_Food food = ingester?.needs?.food;
            bool automatic = ingester?.CurJobDef == NeiyuTraitDefOf.MX_Neiyu_ExtraNutrition;
            float deficit = food == null ? 0f : Mathf.Max(0f, food.MaxLevel - food.CurLevel);
            __state = (soul, deficit, automatic);
            if (automatic && soul?.Active == true)
                nutritionWanted = Mathf.Max(nutritionWanted, deficit + soul.AutomaticNutritionWanted);
        }

        private static void Postfix(Thing __instance, Pawn ingester, float __result,
            (Hediff_NeiyuSoulRemnant soul, float foodDeficit, bool automatic) __state)
        {
            if (__state.soul?.Active != true || ingester.Dead || !__instance.def.IsNutritionGivingIngestible) return;
            // Thing.Ingested returns actual nutrition before callers fill/clamp the food need.
            // Ordinary sustenance always wins; only otherwise-wasted surplus becomes growth.
            __state.soul.RecordSurplus(Mathf.Max(0f, __result - __state.foodDeficit), __state.automatic);
        }
    }

    public class Hediff_NeiyuSoulRecovery : HediffWithComps
    {
        public override void PostRemoved()
        {
            base.PostRemoved();
            if (pawn.CurJobDef == NeiyuTraitDefOf.MX_Neiyu_RecoverSoul)
                pawn.jobs.EndCurrentJob(JobCondition.Succeeded, startNewJob: false);
        }
    }

    internal static class NeiyuSoulRecoveryUtility
    {
        internal static bool IsRecovering(Pawn pawn) =>
            pawn?.health?.hediffSet?.HasHediff(NeiyuTraitDefOf.MX_Neiyu_SoulRecovery) == true;

        internal static bool TryPreventDeath(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || Hediff_NeiyuSoulRemnant.Get(pawn) is not { Active: true } soul) return false;
            bool alreadyRecovering = IsRecovering(pawn);
            CharacterHealthUtility.RestoreForRebirth(pawn);
            if (alreadyRecovering) return true;

            soul.ResetBodyGrowth();
            CharacterHealthUtility.ClearControlStates(pawn);
            // A pawn killed by starvation must not rebuild malnutrition throughout the
            // uninterruptible sleep. This restores sustenance, never stored body growth.
            if (pawn.needs?.food is { } food) food.CurLevel = Mathf.Max(food.CurLevel, food.MaxLevel * 0.5f);
            pawn.jobs?.StopAll();
            if (pawn.drafter != null) pawn.drafter.Drafted = false;
            Building_Bed bed = FindRecoveryBed(pawn, out IntVec3 slot);
            if (bed != null) MoveToBed(pawn, bed, slot);
            // Without any usable bed, recovery still preserves the pawn and runs on the
            // ground (or in its world holder) rather than silently allowing death.
            pawn.health.AddHediff(NeiyuTraitDefOf.MX_Neiyu_SoulRecovery);
            if (pawn.Spawned && pawn.jobs != null)
                pawn.jobs.StartJob(MakeRecoveryJob(pawn), JobCondition.InterruptForced);
            return true;
        }

        internal static Job MakeRecoveryJob(Pawn pawn)
        {
            Building_Bed bed = pawn.CurrentBed();
            // Immediately after teleport the posture has not changed to InBed yet.
            if (bed == null && pawn.Spawned)
                foreach (Thing thing in pawn.Position.GetThingList(pawn.Map))
                    if (thing is Building_Bed candidate) { bed = candidate; break; }
            Job job = bed != null
                ? JobMaker.MakeJob(NeiyuTraitDefOf.MX_Neiyu_RecoverSoul, bed)
                : JobMaker.MakeJob(NeiyuTraitDefOf.MX_Neiyu_RecoverSoul, pawn.Position);
            job.forceSleep = true;
            job.ignoreForbidden = true;
            return job;
        }

        private static Building_Bed FindRecoveryBed(Pawn pawn, out IntVec3 slot)
        {
            Building_Bed owned = pawn.ownership?.OwnedBed;
            if (UsableSlot(owned, pawn, out slot)) return owned;
            Building_Bed best = null;
            float distance = float.MaxValue;
            slot = IntVec3.Invalid;
            foreach (Map map in Find.Maps)
            {
                bool comparableTiles = pawn.Tile.Valid && map.Tile.Valid && pawn.Tile.Layer == map.Tile.Layer;
                float mapDistance = pawn.MapHeld == map ? 0f
                    : 1000000f + (comparableTiles ? Find.WorldGrid.ApproxDistanceInTiles(pawn.Tile, map.Tile) * 1000f : 1000000f);
                foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Bed))
                {
                    var bed = (Building_Bed)thing;
                    if (!UsableSlot(bed, pawn, out IntVec3 candidate)) continue;
                    float candidateDistance = mapDistance + (pawn.MapHeld == map ? pawn.PositionHeld.DistanceToSquared(candidate) : 0f);
                    if (candidateDistance >= distance) continue;
                    best = bed;
                    slot = candidate;
                    distance = candidateDistance;
                }
            }
            return best;
        }

        private static bool UsableSlot(Building_Bed bed, Pawn pawn, out IntVec3 slot)
        {
            slot = IntVec3.Invalid;
            if (bed == null || !bed.Spawned || bed.IsBurning() || bed.Faction != pawn.Faction
                || bed.ForPrisoners != pawn.IsPrisoner || bed.ForSlaves != pawn.IsSlave
                || !RestUtility.CanUseBedEver(pawn, bed.def)) return false;
            if (bed.IsOwner(pawn, out int? assigned))
            {
                int index = assigned ?? 0;
                Pawn occupant = bed.GetCurOccupant(index);
                if (occupant != null && occupant != pawn) return false;
                slot = bed.GetSleepingSlotPos(index);
                return true;
            }
            if (!bed.Medical && bed.OwnersForReading.Count > 0) return false;
            for (int i = 0; i < bed.SleepingSlotsCount; i++)
            {
                if (bed.GetCurOccupant(i) is { } occupant && occupant != pawn) continue;
                slot = bed.GetSleepingSlotPos(i);
                return true;
            }
            return false;
        }

        private static void MoveToBed(Pawn pawn, Building_Bed bed, IntVec3 slot)
        {
            if (pawn.Spawned && pawn.Map == bed.Map)
            {
                pawn.Position = slot;
                pawn.Notify_Teleported();
                return;
            }
            pawn.GetCaravan()?.RemovePawn(pawn);
            if (pawn.Spawned) pawn.DeSpawn();
            pawn.holdingOwner?.Remove(pawn);
            if (pawn.IsWorldPawn()) Find.WorldPawns.RemovePawn(pawn);
            GenSpawn.Spawn(pawn, slot, bed.Map);
            pawn.Notify_Teleported();
        }
    }
}
