using System.Collections.Generic;
using MiliraXian.Characters.Common.Abilities;
using RimWorld;
using Verse;
namespace MiliraXian.Characters.Common
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

        /// <summary>
        /// The minimum this ability demands of a resource, or 0 when it does not require it.
        /// Abilities that pay at their own moment read the amount from here so the cost is declared once.
        /// </summary>
        public float RequiredAmount(HediffDef resourceDef)
        {
            if (resourceDef == null || Props.requirements == null)
            {
                return 0f;
            }

            for (int i = 0; i < Props.requirements.Count; i++)
            {
                if (Props.requirements[i].resourceDef == resourceDef)
                {
                    return Props.requirements[i].minAmount;
                }
            }

            return 0f;
        }

        public static float RequiredAmountFor(Ability ability, HediffDef resourceDef)
        {
            return ability?.CompOfType<CompAbilityEffect_CastResource>()?.RequiredAmount(resourceDef) ?? 0f;
        }

        // Resource pools are spent by other casts while this one is queued or warming up.
        // JobDriver_CastAbility fails the job on !CanCast every tick, so this gate is what stops an
        // unaffordable cast from warming up and then burning its cooldown.
        public override bool CanCast => CanPay();

        /// <summary>
        /// A stance or perk requirement owns the refusal reason: it is the more specific condition
        /// and the player can act on it. When one is unmet this comp stays silent so the requirement
        /// patch still gets to write its own reason.
        /// </summary>
        private bool RequirementSpeaks => AbilityRequirementUtility.FirstUnmet(parent) != null;

        private bool CanPay()
        {
            if (RequirementSpeaks || Props.requirements == null)
            {
                return true;
            }

            for (int i = 0; i < Props.requirements.Count; i++)
            {
                ResourceRequirementEntry requirement = Props.requirements[i];
                if (!ResourceTransactionUtility.HasEnough(parent.pawn, requirement.resourceDef, requirement.minAmount))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (RequirementSpeaks)
            {
                reason = null;
                return false;
            }

            if (Props.requirements != null)
            {
                for (int i = 0; i < Props.requirements.Count; i++)
                {
                    ResourceRequirementEntry requirement = Props.requirements[i];
                    if (!ResourceTransactionUtility.HasEnough(parent.pawn, requirement.resourceDef, requirement.minAmount))
                    {
                        reason = MissingReason(requirement);
                        return true;
                    }
                }
            }

            reason = null;
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // Pay only when the requirement still holds: the effect comps of this ability run after
            // this one, and paying nothing must not let them act as if the cast were paid for.
            if (!CanPay())
            {
                Messages.Message(MissingReason(FirstUnmet()), parent.pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (Props.onCast == null)
            {
                return;
            }

            foreach (ResourceTransactionEntry entry in Props.onCast)
            {
                ResourceTransactionUtility.ApplyTransaction(parent.pawn, entry);
            }
        }

        private ResourceRequirementEntry FirstUnmet()
        {
            if (Props.requirements == null)
            {
                return null;
            }

            for (int i = 0; i < Props.requirements.Count; i++)
            {
                ResourceRequirementEntry requirement = Props.requirements[i];
                if (!ResourceTransactionUtility.HasEnough(parent.pawn, requirement.resourceDef, requirement.minAmount))
                {
                    return requirement;
                }
            }

            return null;
        }

        /// <summary>
        /// The resource owns its wording: the key is built from its defName, so a new resource only
        /// needs its own "&lt;defName&gt;NotEnough" line in Keyed. Falls back to the generic line.
        /// </summary>
        private static string MissingReason(ResourceRequirementEntry requirement)
        {
            string key = requirement?.resourceDef?.defName + "NotEnough";
            if (!key.TryTranslate(out _))
            {
                key = "MX_Common_ResourceNotEnough";
            }

            return key.Translate((requirement?.minAmount ?? 0f).ToString("0.#")).ToString();
        }
    }
}
