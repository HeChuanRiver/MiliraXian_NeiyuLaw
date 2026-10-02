using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.Common.Abilities
{
    /// <summary>
    /// One condition on whether an ability may be used. Requirements are orthogonal — a stance
    /// requirement and a perk requirement say nothing about each other — so they compose as a list
    /// rather than through ability subclasses.
    /// </summary>
    public abstract class AbilityRequirement
    {
        public abstract bool Met(Pawn pawn);

        /// <summary>Why the ability refused to cast; the gizmo is already hidden by then.</summary>
        public abstract string DisabledReason(Pawn pawn);
    }

    public class AbilityRequirementExtension : DefModExtension
    {
        public List<AbilityRequirement> requirements;
    }
}
