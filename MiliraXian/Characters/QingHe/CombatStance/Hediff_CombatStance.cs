using Verse;

namespace MiliraXian.Characters.QingHe.CombatStance
{
    public class Hediff_CombatStance : HediffWithComps
    {
        public override HediffStage CurStage =>
            GetComp<HediffComp_CombatStance>()?.CurrentStance?.stanceEffect ?? base.CurStage;
    }
}
