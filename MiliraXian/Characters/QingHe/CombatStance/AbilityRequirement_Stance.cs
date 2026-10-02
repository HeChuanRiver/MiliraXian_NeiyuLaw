using MiliraXian.Characters.Common.Abilities;
using MiliraXian.Characters.QingHe.Hediffs;
using Verse;

namespace MiliraXian.Characters.QingHe.CombatStance
{
    public class AbilityRequirement_Stance : AbilityRequirement
    {
        public CombatStanceDef stance;

        public override bool Met(Pawn pawn)
        {
            return stance == null || MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance == stance;
        }

        public override string DisabledReason(Pawn pawn)
        {
            return "MX_QH_RequiresStance".Translate(stance?.label ?? "");
        }
    }
}
