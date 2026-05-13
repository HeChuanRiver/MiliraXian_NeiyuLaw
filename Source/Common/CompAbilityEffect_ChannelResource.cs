using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class CompProperties_ChannelResource : CompProperties_AbilityEffect
    {
        public List<ResourceTransactionEntry> perTick;
        public List<ResourceRequirementEntry> failConditions;

        public CompProperties_ChannelResource()
        {
            compClass = typeof(CompAbilityEffect_ChannelResource);
        }
    }

    public class CompAbilityEffect_ChannelResource : CompAbilityEffect
    {
        public new CompProperties_ChannelResource Props => (CompProperties_ChannelResource)props;

        public void Tick(Pawn pawn)
        {
            if (Props.perTick != null)
            {
                foreach (var entry in Props.perTick)
                {
                    ResourceTransactionUtility.ApplyTransaction(pawn, entry);
                }
            }
        }

        public bool CheckFailCondition(Pawn pawn)
        {
            if (Props.failConditions != null)
            {
                foreach (var cond in Props.failConditions)
                {
                    if (!ResourceTransactionUtility.HasEnough(pawn, cond.resourceDef, cond.minAmount))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
