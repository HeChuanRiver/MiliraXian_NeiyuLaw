using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class CompProperties_AbilityEffectDebugMaxResource : CompProperties_AbilityEffect
    {
        public string resourceHediffDef;

        public CompProperties_AbilityEffectDebugMaxResource()
        {
            compClass = typeof(CompAbilityEffect_DebugMaxResource);
        }
    }

    /// <summary>
    /// Debug-only ability effect: always applies to caster and runs DebugEffect.
    /// </summary>
    public class CompAbilityEffect_DebugMaxResource : CompAbilityEffect
    {
        public new CompProperties_AbilityEffectDebugMaxResource Props => (CompProperties_AbilityEffectDebugMaxResource)props;

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return true;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return true;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn pawn = parent?.pawn;
            if (pawn == null)
            {
                return;
            }

            // Force base side effects to use caster as target.
            base.Apply(new LocalTargetInfo(pawn), dest);
            DebugMaxResource(pawn);
        }

        private void DebugMaxResource(Pawn pawn)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(Props.resourceHediffDef);
            if (def == null)
            {
                return;
            }

            var max = PawnSpecialResourceUtility.GetMaxResource(pawn, def);
            PawnSpecialResourceUtility.AddResource(pawn, def, max);
        }
    }
}