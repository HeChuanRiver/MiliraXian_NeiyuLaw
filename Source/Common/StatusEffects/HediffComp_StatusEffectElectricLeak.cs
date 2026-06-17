using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_StatusEffectElectricLeak : HediffCompProperties_OnAccumulated
    {
        public float energyLossPercentPerAccumulation = 0.08f;

        public HediffCompProperties_StatusEffectElectricLeak()
        {
            compClass = typeof(HediffComp_StatusEffectElectricLeak);
        }
    }

    public class HediffComp_StatusEffectElectricLeak : HediffComp_OnAccumulated
    {
        private HediffCompProperties_StatusEffectElectricLeak PropsElectricLeak => (HediffCompProperties_StatusEffectElectricLeak)props;

        public override void NotifyAccumulationApplied(Pawn caster, float finalSeverityOffset)
        {
            base.NotifyAccumulationApplied(caster, finalSeverityOffset);
            StatusEffectUtility.ReduceMechEnergyNeed(Pawn, finalSeverityOffset * PropsElectricLeak.energyLossPercentPerAccumulation);
        }
    }
}
