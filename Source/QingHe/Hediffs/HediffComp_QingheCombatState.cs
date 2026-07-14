using MiliraXian.Characters.QingHe.Things.Weapons;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_QingheCombatState : HediffCompProperties
    {
        public HediffCompProperties_QingheCombatState()
        {
            compClass = typeof(HediffComp_QingheCombatState);
        }
    }

    public class HediffComp_QingheCombatState : HediffComp
    {
        private FlowerBellResonance resonance = FlowerBellResonance.Spring;
        private bool extraBuildingDamage;

        public FlowerBellResonance Resonance => resonance;

        public bool ExtraBuildingDamage => extraBuildingDamage;

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref resonance, "mx_qh_resonance", FlowerBellResonance.Spring);
            Scribe_Values.Look(ref extraBuildingDamage, "mx_qh_extraBuildingDamage", false);
        }

        public void SetResonance(FlowerBellResonance value)
        {
            resonance = value;
        }

        public void ToggleExtraBuildingDamage()
        {
            extraBuildingDamage = !extraBuildingDamage;
        }
    }
}
