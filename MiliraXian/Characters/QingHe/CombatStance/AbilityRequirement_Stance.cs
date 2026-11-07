using MiliraXian.Characters.Common.Abilities;
using MiliraXian.Characters.QingHe.Hediffs;
using Verse;

namespace MiliraXian.Characters.QingHe.CombatStance
{
    public class AbilityRequirement_Stance : AbilityRequirement
    {
        public CombatStanceDef stance;

        // Decorative mode seals every stance skill, whatever the stance.
        public override bool Met(Pawn pawn)
        {
            return !QinghePowerBalance.Sealed
                && (stance == null || MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance == stance);
        }

        public override string DisabledReason(Pawn pawn)
        {
            return QinghePowerBalance.Sealed ? "MX_Power_AbilitiesSealed".Translate()
                : "MX_QH_RequiresStance".Translate(stance?.label ?? "");
        }
    }
}
