using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_AbnormalElectricLeak : HediffCompProperties_OnAbnormalApplied
    {
        public float energyLossPercentPerAccumulation = 0.0008f;

        public HediffCompProperties_AbnormalElectricLeak()
        {
            compClass = typeof(HediffComp_AbnormalElectricLeak);
        }
    }

    public class HediffComp_AbnormalElectricLeak : HediffComp_OnAbnormalApplied
    {
        private HediffCompProperties_AbnormalElectricLeak PropsElectricLeak => (HediffCompProperties_AbnormalElectricLeak)props;

        public override void NotifyApplied(Pawn source, float amount)
        {
            base.NotifyApplied(source, amount);
            AbnormalUtility.ReduceMechEnergyNeed(Pawn, amount * PropsElectricLeak.energyLossPercentPerAccumulation);
        }
    }
}
