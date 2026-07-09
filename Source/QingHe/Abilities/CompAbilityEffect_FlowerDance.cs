using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityFlowerDance : CompProperties_AbilityEffect
    {
        public HediffDef hediffDef;
        public HediffDef resourceCostDef;
        public float resourceCost = 1f;
        public int durationTicks = 900;
        public string missingResourceMessage = "MX_QH_FlowerDanceMissingResource";

        public CompProperties_AbilityFlowerDance()
        {
            compClass = typeof(CompAbilityEffect_FlowerDance);
        }
    }

    public class CompAbilityEffect_FlowerDance : CompAbilityEffect
    {
        public new CompProperties_AbilityFlowerDance Props => (CompProperties_AbilityFlowerDance)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (Props.resourceCostDef != null
                && Props.resourceCost > 0f
                && PawnSpecialResourceUtility.GetCurrentResource(parent.pawn, Props.resourceCostDef) < Props.resourceCost)
            {
                reason = Props.missingResourceMessage.Translate();
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            if (caster?.health?.hediffSet == null)
            {
                return;
            }

            if (Props.resourceCostDef != null && Props.resourceCost > 0f)
            {
                if (!PawnSpecialResourceUtility.TryConsumeResource(caster, Props.resourceCostDef, Props.resourceCost))
                {
                    Messages.Message(Props.missingResourceMessage.Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
            }

            HediffDef hediffDef = Props.hediffDef ?? MX_QHDefOf.MX_QH_FlowerDance;
            if (hediffDef == null)
            {
                return;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(hediffDef, caster);
                caster.health.AddHediff(hediff);
            }

            float specialFactor = MX_QHSkillUtility.GetSpecialAbilityEffectFactor(caster);
            hediff.Severity = specialFactor;

            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears != null && Props.durationTicks > 0)
            {
                disappears.SetDuration(Mathf.RoundToInt(Props.durationTicks * specialFactor));
            }

            if (hediff is HediffWithComps hediffWithComps)
            {
                hediffWithComps.GetComp<HediffComp_FlowerDance>()?.NotifyRefreshed();
            }
        }
    }
}
