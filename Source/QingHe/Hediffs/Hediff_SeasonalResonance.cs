using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.QingHe.Defs;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class Hediff_SeasonalResonance : HediffWithComps
    {
        public FlowerBellResonance Resonance => def == MX_QHDefOf.MX_QH_ResonanceSpring ? FlowerBellResonance.Spring
            : def == MX_QHDefOf.MX_QH_ResonanceSummer ? FlowerBellResonance.Summer
            : def == MX_QHDefOf.MX_QH_ResonanceAutumn ? FlowerBellResonance.Autumn
            : def == MX_QHDefOf.MX_QH_ResonanceWinter ? FlowerBellResonance.Winter
            : FlowerBellResonance.None;

        public override string Description => base.Description + "\n\n"
            + ("MX_QH_SwordResonanceDescription" + Resonance).Translate() + "\n\n"
            + ("MX_QH_FlowerBellResonanceDescription" + Resonance).Translate();

        public override void PostRemoved()
        {
            base.PostRemoved();
            HediffComp_QingheCombatState.GetFor(pawn)?.NotifyResonanceRemoved(this);
        }
    }
}
