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
    }
}
