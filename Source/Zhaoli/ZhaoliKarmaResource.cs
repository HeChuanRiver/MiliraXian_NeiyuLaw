using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    public static class ZhaoliKarmaUtility
    {
        public const string ZhaoliPawnKindDefName = "MiliraXian_Zhaoli";
        public const string KarmaHediffDefName = "MXZL_ZhaoliKarma";
        public const string DormancyHediffDefName = "MXZL_ZhaoliDormancy";
        public const int DormancyDurationTicks = 1800000;

        public static bool IsZhaoli(Pawn pawn)
        {
            return pawn?.kindDef?.defName == ZhaoliPawnKindDefName;
        }

        public static HediffComp_PawnSpecialResource GetKarmaComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(KarmaHediffDefName);
            if (hediffDef == null)
            {
                return null;
            }

            HediffWithComps hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) as HediffWithComps;
            return hediff?.GetComp<HediffComp_PawnSpecialResource>();
        }

        public static HediffComp_PawnSpecialResource EnsureKarmaComp(Pawn pawn)
        {
            HediffComp_PawnSpecialResource comp = GetKarmaComp(pawn);
            if (comp != null || pawn?.health == null)
            {
                return comp;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(KarmaHediffDefName);
            if (hediffDef == null)
            {
                return null;
            }

            Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
            pawn.health.AddHediff(hediff);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_PawnSpecialResource>();
        }

        public static float GetCurrentKarma(Pawn pawn)
        {
            return GetKarmaComp(pawn)?.CurrentValue ?? 0f;
        }

        public static float GetMaxKarma(Pawn pawn)
        {
            return GetKarmaComp(pawn)?.MaxValue ?? 0f;
        }

        public static void AddKarma(Pawn pawn, float value)
        {
            HediffComp_PawnSpecialResource comp = EnsureKarmaComp(pawn);
            if (comp == null)
            {
                return;
            }

            comp.AddValue(value);
            HandleOverflow(pawn, comp);
        }

        public static bool TryConsumeKarma(Pawn pawn, float value)
        {
            HediffComp_PawnSpecialResource comp = EnsureKarmaComp(pawn);
            return comp != null && comp.TryConsume(value);
        }

        public static bool IsDormant(Pawn pawn)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(DormancyHediffDefName);
            return hediffDef != null && (pawn?.health?.hediffSet?.HasHediff(hediffDef) ?? false);
        }

        private static void HandleOverflow(Pawn pawn, HediffComp_PawnSpecialResource comp)
        {
            if (pawn == null || comp == null || !comp.IsOverflowing)
            {
                return;
            }

            comp.SetValue(0f);
            ApplyDormancy(pawn, DormancyDurationTicks);
        }

        private static void ApplyDormancy(Pawn pawn, int durationTicks)
        {
            if (pawn?.health == null)
            {
                return;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(DormancyHediffDefName);
            if (hediffDef == null)
            {
                return;
            }

            bool forceDowned = pawn.health.forceDowned;
            pawn.health.forceDowned = true;
            Hediff hediff = pawn.health.GetOrAddHediff(hediffDef);
            pawn.health.Notify_HediffChanged(hediff);
            pawn.health.forceDowned = forceDowned;
            HediffWithComps hediffWithComps = hediff as HediffWithComps;
            hediffWithComps?.GetComp<HediffComp_Disappears>()?.SetDuration(durationTicks);
            hediffWithComps?.GetComp<HediffComp_ZhaoliDormancy>()?.ForceSleepNow();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_Pawn_SpawnSetup_ZhaoliKarma
    {
        public static void Postfix(Pawn __instance)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(__instance))
            {
                return;
            }

            ZhaoliKarmaUtility.EnsureKarmaComp(__instance);
        }
    }
}
