using MiliraXian.Characters.QingHe.Things.Weapons;
using UnityEngine;
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
        private const int TuneCooldownTicks = 60000;

        private FlowerBellResonance resonance = FlowerBellResonance.Spring;
        private int pendingTuneResonance = -1;
        private int tuneCooldownUntilTick = -1;

        public FlowerBellResonance Resonance => resonance;

        public int TuneCooldownRemainingTicks => Mathf.Max(0, tuneCooldownUntilTick - Find.TickManager.TicksGame);

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref resonance, "mx_qh_resonance", FlowerBellResonance.Spring);
            Scribe_Values.Look(ref pendingTuneResonance, "mx_qh_pendingTuneResonance", -1);
            Scribe_Values.Look(ref tuneCooldownUntilTick, "mx_qh_tuneCooldownUntilTick", -1);
        }

        public void SetResonance(FlowerBellResonance value)
        {
            resonance = value;
        }

        public void BeginTuning(FlowerBellResonance value)
        {
            pendingTuneResonance = (int)value;
        }

        public void CompleteTuning()
        {
            if (pendingTuneResonance < 0)
            {
                return;
            }

            resonance = (FlowerBellResonance)pendingTuneResonance;
            pendingTuneResonance = -1;
            tuneCooldownUntilTick = Find.TickManager.TicksGame + TuneCooldownTicks;
        }
    }
}
