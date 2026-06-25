using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerResonanceGizmos : HediffCompProperties
    {
        public HediffCompProperties_FlowerResonanceGizmos()
        {
            compClass = typeof(HediffComp_FlowerResonanceGizmos);
        }
    }

    public class HediffComp_FlowerResonanceGizmos : HediffComp
    {
        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            HediffComp_FlowerResonance state = parent?.GetComp<HediffComp_FlowerResonance>();
            foreach (Gizmo gizmo in QingheSkillTreeSystem.GetGizmos(Pawn, state))
            {
                yield return gizmo;
            }
        }
    }
}
