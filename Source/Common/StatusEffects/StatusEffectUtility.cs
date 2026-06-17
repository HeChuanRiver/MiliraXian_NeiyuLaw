using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public static class StatusEffectUtility
    {
        private static NeedDef mechEnergyNeedDef;

        public static void ApplyBleed(Pawn pawn, float bleedDamage)
        {
            if (pawn?.health?.hediffSet == null || bleedDamage <= 0f)
            {
                return;
            }

            BodyPartRecord part = pawn.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Cut, BodyPartHeight.Undefined, BodyPartDepth.Outside);
            if (part == null)
            {
                return;
            }

            Hediff_Injury injury = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part) as Hediff_Injury;
            if (injury == null)
            {
                return;
            }

            injury.Severity = Mathf.Max(0.1f, bleedDamage);
            pawn.health.AddHediff(injury, part);
        }

        public static void ReduceMechEnergyNeed(Pawn pawn, float percent)
        {
            if (pawn?.needs == null || percent <= 0f)
            {
                return;
            }

            NeedDef needDef = MechEnergyNeedDef;
            Need need = needDef != null ? pawn.needs.TryGetNeed(needDef) : null;
            if (need == null)
            {
                return;
            }

            need.CurLevelPercentage = Mathf.Max(0f, need.CurLevelPercentage - percent);
        }

        private static NeedDef MechEnergyNeedDef
        {
            get
            {
                if (mechEnergyNeedDef == null)
                {
                    mechEnergyNeedDef = DefDatabase<NeedDef>.GetNamedSilentFail("MechEnergy");
                }

                return mechEnergyNeedDef;
            }
        }
    }
}
