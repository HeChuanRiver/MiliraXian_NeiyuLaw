using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class CompProperties_CastResource : CompProperties_AbilityEffect
    {
        public List<ResourceRequirementEntry> requirements;
        public List<ResourceTransactionEntry> onCast;

        public CompProperties_CastResource()
        {
            compClass = typeof(CompAbilityEffect_CastResource);
        }
    }

    public class CompAbilityEffect_CastResource : CompAbilityEffect
    {
        public new CompProperties_CastResource Props => (CompProperties_CastResource)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (Props.requirements != null)
            {
                foreach (var req in Props.requirements)
                {
                    if (!ResourceTransactionUtility.HasEnough(parent.pawn, req.resourceDef, req.minAmount))
                    {
                        reason = req.disabledReasonKey?.Translate().ToString() ?? "MX_Common_ResourceNotEnough".Translate().ToString();
                        return true;
                    }
                }
            }

            reason = null;
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (Props.onCast != null)
            {
                foreach (var entry in Props.onCast)
                {
                    ResourceTransactionUtility.ApplyTransaction(parent.pawn, entry);
                }
            }
        }
    }
}
