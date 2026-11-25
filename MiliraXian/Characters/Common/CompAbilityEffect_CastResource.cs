using System.Collections.Generic;
using MiliraXian.Characters.Common.Abilities;
using RimWorld;
using Verse;
using Verse.AI;
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

        // Vanilla's driver only re-checks CanCast while the ability is not yet casting, and an
        // ability flagged abilityCasting counts as casting for the whole job, so once a queued cast
        // has its job nothing stops it. The pool must therefore be judged when the cast is accepted,
        // which means counting what earlier accepted casts are still going to spend.
        public override bool CanCast => CanAffordCast();

        /// <summary>
        /// A stance or perk requirement owns the refusal reason: it is the more specific condition
        /// and the player can act on it. When one is unmet this comp stays silent so the requirement
        /// patch still gets to write its own reason.
        /// </summary>
        private bool RequirementSpeaks => AbilityRequirementUtility.FirstUnmet(parent) != null;

        /// <summary>
        /// How much of this resource is already promised to casts that were accepted but have not
        /// paid yet: the current job and everything queued. Payment only answers for itself, so this
        /// is what the ordering decision needs and what a resource readout can quote.
        /// </summary>
        public static float CommittedAmountFor(Pawn pawn, HediffDef resourceDef, Ability except = null)
        {
            if (pawn?.jobs == null || resourceDef == null)
            {
                return 0f;
            }

            float committed = 0f;
            foreach (Job job in pawn.jobs.AllJobs())
            {
                Ability ability = job?.ability;
                if (ability == null || ability == except)
                {
                    continue;
                }

                committed += RequiredAmountFor(ability, resourceDef);
            }

            return committed;
        }

        private bool CanAffordCast()
        {
            if (RequirementSpeaks || Props.requirements == null)
            {
                return true;
            }

            for (int i = 0; i < Props.requirements.Count; i++)
            {
                ResourceRequirementEntry requirement = Props.requirements[i];
                if (!ResourceTransactionUtility.HasEnough(parent.pawn, requirement.resourceDef, NeededAmount(requirement)))
                {
                    return false;
                }
            }

            return true;
        }

        private float NeededAmount(ResourceRequirementEntry requirement)
        {
            return requirement.minAmount + CommittedAmountFor(parent.pawn, requirement.resourceDef, parent);
        }

        // Payment answers "can this cast cover its own cost right now". Queued casts were already
        // judged against the pool when they were accepted, so counting them here would fail the
        // acting cast because of work that comes after it.
        private bool CanPayNow()
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
                    float needed = NeededAmount(requirement);
                    if (!ResourceTransactionUtility.HasEnough(parent.pawn, requirement.resourceDef, needed))
                    {
                        // The refusal names this cast's own cost; the pooled need includes casts that
                        // are already accepted, and the available figure accounts for the same casts.
                        float available = ResourceTransactionUtility.GetCurrent(parent.pawn, requirement.resourceDef)
                            - CommittedAmountFor(parent.pawn, requirement.resourceDef, parent);
                        reason = MissingReason(requirement, requirement.minAmount, available);
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
            if (!CanPayNow())
            {
                ResourceRequirementEntry unmet = FirstUnmet();
                float available = unmet == null ? 0f : ResourceTransactionUtility.GetCurrent(parent.pawn, unmet.resourceDef);
                Messages.Message(MissingReason(unmet, unmet?.minAmount ?? 0f, available), parent.pawn, MessageTypeDefOf.RejectInput, historical: false);
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
        private static string MissingReason(ResourceRequirementEntry requirement, float cost, float available)
        {
            string key = requirement?.resourceDef?.defName + "NotEnough";
            if (!key.TryTranslate(out _))
            {
                key = "MX_Common_ResourceNotEnough";
            }

            return key.Translate(cost.ToString("0.#"), available.ToString("0.#")).ToString();
        }
    }
}
