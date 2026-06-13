using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerBellElectricLeak : HediffCompProperties_OnAccumulated
    {
        public float energyLossPercentPerAccumulation = 0.08f;

        public HediffCompProperties_FlowerBellElectricLeak()
        {
            compClass = typeof(HediffComp_FlowerBellElectricLeak);
        }
    }

    public class HediffComp_FlowerBellElectricLeak : HediffComp_OnAccumulated
    {
        private HediffCompProperties_FlowerBellElectricLeak PropsElectricLeak => (HediffCompProperties_FlowerBellElectricLeak)props;

        public override void NotifyAccumulationApplied(Pawn caster, float finalSeverityOffset)
        {
            base.NotifyAccumulationApplied(caster, finalSeverityOffset);
            MX_QHUtility.ReduceMechEnergyNeed(Pawn, finalSeverityOffset * PropsElectricLeak.energyLossPercentPerAccumulation);
        }
    }
}
