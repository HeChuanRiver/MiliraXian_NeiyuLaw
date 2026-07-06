using System.Collections.Generic;
using MiliraXian.Characters;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_SkillTreeStateGizmos : HediffCompProperties
    {
        public HediffCompProperties_SkillTreeStateGizmos()
        {
            compClass = typeof(HediffComp_SkillTreeStateGizmos);
        }
    }

    public class HediffComp_SkillTreeStateGizmos : HediffComp, ISkillTreeStateListener
    {
        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            HediffComp_SkillTreeState state = parent?.GetComp<HediffComp_SkillTreeState>();
            foreach (Gizmo gizmo in MX_QHSkillSystem.GetGizmos(Pawn, state))
            {
                yield return gizmo;
            }
        }

        public void Notify_SkillTreeStateChanged(Pawn pawn, HediffComp_SkillTreeState state)
        {
            MX_QHSkillSystem.SyncChoices(pawn, state);
        }
    }
}


