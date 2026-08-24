using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityEyeOfHeart : CompProperties_AbilityEffect
    {
        public HediffDef stateHediff;
        public int durationTicks = 60;

        public CompProperties_AbilityEyeOfHeart()
        {
            compClass = typeof(CompAbilityEffect_EyeOfHeart);
        }
    }

    public class CompAbilityEffect_EyeOfHeart : CompAbilityEffect
    {
        public new CompProperties_AbilityEyeOfHeart Props => (CompProperties_AbilityEyeOfHeart)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            HediffDef stateDef = Props.stateHediff ?? MX_QHDefOf.MX_QH_EyeOfHeartState;
            if (caster?.health?.hediffSet == null || stateDef == null)
            {
                return;
            }

            Hediff state = caster.health.hediffSet.GetFirstHediffOfDef(stateDef) ?? caster.health.AddHediff(stateDef);
            state?.TryGetComp<HediffComp_Disappears>()?.SetDuration(Mathf.Max(1, Props.durationTicks));
        }
    }
}
