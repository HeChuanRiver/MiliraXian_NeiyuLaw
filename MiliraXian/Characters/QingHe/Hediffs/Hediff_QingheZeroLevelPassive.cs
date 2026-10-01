using Verse;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.QingHe;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class Hediff_QingheZeroLevelPassive : HediffWithComps
    {
        public override bool Visible => QinghePowerBalance.ZeroLevelPassivesEnabled;
    }

    public class Hediff_QingheHidden : HediffWithComps
    {
        public override bool Visible => false;
    }

    public class Hediff_QingheHiddenResource : Hediff_PawnSpecialResource
    {
        public override bool Visible => false;
    }
}
