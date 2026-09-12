using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class Hediff_QingheZeroLevelPassive : HediffWithComps
    {
        public override bool Visible => QinghePowerBalance.ZeroLevelPassivesEnabled;
    }

    public class Hediff_QingheHiddenResource : HediffWithComps
    {
        public override bool Visible => false;

        public override int CurStageIndex
        {
            get
            {
                HediffComp_SwordPressure pressure = this.TryGetComp<HediffComp_SwordPressure>();
                if (pressure == null)
                {
                    return base.CurStageIndex;
                }

                // Read the resource directly so gains and consumption affect stats in the same tick.
                return def.StageAtSeverity(QinghePowerBalance.ZeroLevelPassivesEnabled ? pressure.CurrentResourceValue : 0f);
            }
        }
    }
}
