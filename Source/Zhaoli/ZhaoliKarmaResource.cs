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
        public const string LinkTargetHediffDefName = "MXZL_ZhaoliKarmaLink";
        public const string OverflowBurdenHediffDefName = "MXZL_ZhaoliOverflowKarma";
        public const string LegacyShieldHediffDefName = "MXNL_NeiyuShield";
        public const int DormancyDurationTicks = 1800000;

        public static bool IsZhaoli(Pawn pawn)
        {
            return pawn?.kindDef?.defName == ZhaoliPawnKindDefName;
        }

        public static HediffComp_PawnSpecialResource GetKarmaComp(Pawn pawn)
        {
            return GetKarmaHediff(pawn)?.GetComp<HediffComp_PawnSpecialResource>();
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

        public static HediffComp_ZhaoliKarmaLinks GetLinkComp(Pawn pawn)
        {
            return GetKarmaHediff(pawn)?.GetComp<HediffComp_ZhaoliKarmaLinks>();
        }

        public static HediffComp_ZhaoliKarmaLinks EnsureLinkComp(Pawn pawn)
        {
            EnsureKarmaComp(pawn);
            return GetLinkComp(pawn);
        }

        public static HediffComp_ZhaoliKarmaLinkTarget GetLinkTargetComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            HediffDef linkDef = DefDatabase<HediffDef>.GetNamedSilentFail(LinkTargetHediffDefName);
            if (linkDef == null)
            {
                return null;
            }

            HediffWithComps hediff = pawn.health.hediffSet.GetFirstHediffOfDef(linkDef) as HediffWithComps;
            return hediff?.GetComp<HediffComp_ZhaoliKarmaLinkTarget>();
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

        public static bool HasLinkFrom(Pawn targetPawn, Pawn zhaoli)
        {
            HediffComp_ZhaoliKarmaLinkTarget linkTargetComp = GetLinkTargetComp(targetPawn);
            return linkTargetComp != null && linkTargetComp.Zhaoli == zhaoli;
        }

        public static bool HasOverflowBurden(Pawn pawn)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(OverflowBurdenHediffDefName);
            return hediffDef != null && (pawn?.health?.hediffSet?.HasHediff(hediffDef) ?? false);
        }

        public static void RemoveLegacyShield(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            HediffDef shieldDef = DefDatabase<HediffDef>.GetNamedSilentFail(LegacyShieldHediffDefName);
            if (shieldDef == null)
            {
                return;
            }

            Hediff shield = pawn.health.hediffSet.GetFirstHediffOfDef(shieldDef);
            if (shield != null)
            {
                pawn.health.RemoveHediff(shield);
            }
        }

        public static void ApplyOverflowBurden(Pawn pawn)
        {
            if (pawn?.health == null || HasOverflowBurden(pawn))
            {
                return;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(OverflowBurdenHediffDefName);
            if (hediffDef == null)
            {
                return;
            }

            pawn.health.GetOrAddHediff(hediffDef);
        }

        public static void RemoveOverflowBurden(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(OverflowBurdenHediffDefName);
            if (hediffDef == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        public static void RemoveTargetLinkHediff(Pawn pawn, Pawn zhaoli)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            HediffComp_ZhaoliKarmaLinkTarget linkTargetComp = GetLinkTargetComp(pawn);
            if (linkTargetComp == null || linkTargetComp.Zhaoli != zhaoli)
            {
                return;
            }

            Hediff hediff = linkTargetComp.parent;
            if (hediff != null && pawn.health.hediffSet.hediffs.Contains(hediff))
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private static void HandleOverflow(Pawn pawn, HediffComp_PawnSpecialResource comp)
        {
            if (pawn == null || comp == null || !comp.IsOverflowing)
            {
                return;
            }

            int overflowAmount = Mathf.Max(0, Mathf.CeilToInt(comp.CurrentValue - comp.MaxValue - 0.0001f));
            HediffComp_ZhaoliKarmaLinks linkComp = GetLinkComp(pawn);
            if (overflowAmount > 0 && linkComp != null && linkComp.TryDistributeOverflow(overflowAmount))
            {
                comp.SetValue(comp.MaxValue);
                return;
            }

            comp.SetValue(0f);
            ApplyDormancy(pawn, DormancyDurationTicks);
        }

        private static HediffWithComps GetKarmaHediff(Pawn pawn)
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

            return pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) as HediffWithComps;
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
            ZhaoliShieldLayerUtility.EnsureShieldComp(__instance);
            ZhaoliRebirthUtility.EnsureRebirthComp(__instance);
            ZhaoliKarmaUtility.RemoveLegacyShield(__instance);
        }
    }
}
