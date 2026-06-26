using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class FlowerMandateEnhanceUtility
    {
        public static bool ActiveFor(Pawn pawn, AbilityDef mandateDef)
        {
            if (pawn == null || mandateDef == null)
            {
                return false;
            }

            HediffComp_FlowerResonance state = FlowerCourtUtility.GetSkillTreeState(pawn);
            if (state?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Run) != true)
            {
                return false;
            }

            HediffComp_FlowerChoices choices = FlowerCourtUtility.GetFlowerChoices(pawn);
            return choices?.SelectedFlowerMandate == mandateDef;
        }
    }
}
