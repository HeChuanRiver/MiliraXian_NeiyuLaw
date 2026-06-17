using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_QingheSkillTreeGizmos : HediffCompProperties
    {
        public HediffCompProperties_QingheSkillTreeGizmos()
        {
            compClass = typeof(HediffComp_QingheSkillTreeGizmos);
        }
    }

    public class HediffComp_QingheSkillTreeGizmos : HediffComp
    {
        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            HediffComp_QingheSkillTreeState state = parent?.GetComp<HediffComp_QingheSkillTreeState>();
            foreach (Gizmo gizmo in QingheSkillTreeSystem.GetGizmos(Pawn, state))
            {
                yield return gizmo;
            }
        }
    }
}
