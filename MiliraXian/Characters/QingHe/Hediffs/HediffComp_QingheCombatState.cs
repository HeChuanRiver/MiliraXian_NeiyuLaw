using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.QingHe.Defs;
using UnityEngine;
using Verse;
using MiliraXian.Characters.QingHe;

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

        private Hediff_SeasonalResonance currentResonance;
        private int pendingTuneResonance = -1;
        private int tuneCooldownUntilTick = -1;

        public int TuneCooldownRemainingTicks => Mathf.Max(0, tuneCooldownUntilTick - Find.TickManager.TicksGame);

        public Hediff_SeasonalResonance CurrentResonance => currentResonance;

        public void NotifyResonanceRemoved(Hediff_SeasonalResonance removed)
        {
            if (currentResonance == removed)
            {
                currentResonance = null;
            }
        }

        private void RemoveCurrentResonance()
        {
            Hediff_SeasonalResonance removed = currentResonance;
            currentResonance = null;
            if (removed != null)
            {
                Pawn.health.RemoveHediff(removed);
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            RemoveCurrentResonance();
        }

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref currentResonance, "currentResonance");
            Scribe_Values.Look(ref pendingTuneResonance, "mx_qh_pendingTuneResonance", -1);
            Scribe_Values.Look(ref tuneCooldownUntilTick, "mx_qh_tuneCooldownUntilTick", -1);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            SyncResonanceHediff();
        }

        private void SyncResonanceHediff()
        {
            if (!MX_QH_PerkUtility.HasSeasonalResonance(Pawn))
            {
                pendingTuneResonance = -1;
                RemoveCurrentResonance();
            }
        }

        public void BeginTuning(FlowerBellResonance value)
        {
            if (!MX_QH_PerkUtility.HasSeasonalResonance(Pawn)
                || value < FlowerBellResonance.Spring || value > FlowerBellResonance.Winter)
            {
                return;
            }
            pendingTuneResonance = (int)value;
        }

        public void CompleteTuning()
        {
            if (!MX_QH_PerkUtility.HasSeasonalResonance(Pawn))
            {
                pendingTuneResonance = -1;
                return;
            }
            if (pendingTuneResonance < 0)
            {
                return;
            }

            HediffDef next = (FlowerBellResonance)pendingTuneResonance switch
            {
                FlowerBellResonance.Spring => MX_QHDefOf.MX_QH_ResonanceSpring,
                FlowerBellResonance.Summer => MX_QHDefOf.MX_QH_ResonanceSummer,
                FlowerBellResonance.Autumn => MX_QHDefOf.MX_QH_ResonanceAutumn,
                FlowerBellResonance.Winter => MX_QHDefOf.MX_QH_ResonanceWinter,
                _ => null
            };
            if (next == null)
            {
                pendingTuneResonance = -1;
                return;
            }
            RemoveCurrentResonance();
            currentResonance = (Hediff_SeasonalResonance)Pawn.health.AddHediff(next);
            pendingTuneResonance = -1;
            tuneCooldownUntilTick = Find.TickManager.TicksGame + TuneCooldownTicks;
        }
    }
}
