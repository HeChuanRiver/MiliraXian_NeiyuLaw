using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_EyeOfHeart : HediffCompProperties
    {
        public float shieldRestoreFraction = 0.1f;
        public float swordPressureGain = 1f;

        public HediffCompProperties_EyeOfHeart()
        {
            compClass = typeof(HediffComp_EyeOfHeart);
        }
    }

    public class HediffComp_EyeOfHeart : HediffComp
    {
        private bool triggered;

        public HediffCompProperties_EyeOfHeart Props => (HediffCompProperties_EyeOfHeart)props;

        public bool TryTrigger(DamageInfo dinfo)
        {
            if (dinfo.Amount <= 0f)
            {
                return false;
            }

            return TryTrigger(dinfo.Instigator);
        }

        public bool TryTrigger(Thing instigator)
        {
            Pawn pawn = Pawn;
            if (triggered || pawn == null || instigator == null || instigator == pawn)
            {
                return false;
            }

            if (!GenHostility.HostileTo(pawn, instigator))
            {
                return false;
            }

            triggered = true;
            pawn.GetComp<CompDivineProtectionShield>()?.RestoreFraction(Mathf.Max(0f, Props.shieldRestoreFraction));
            MX_QH_HediffUtility.EnsureSwordPressure(pawn)?.AddPoints(Mathf.Max(0f, Props.swordPressureGain));
            if (parent != null && pawn.health?.hediffSet != null)
            {
                pawn.health.RemoveHediff(parent);
            }
            return true;
        }
    }
}
