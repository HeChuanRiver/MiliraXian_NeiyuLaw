using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.QingHe.Defs;
using System.Runtime.CompilerServices;
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
        private static readonly ConditionalWeakTable<Pawn, HediffComp_QingheCombatState> managers = new();

        private Hediff_SeasonalResonance currentResonance;
        private int pendingTuneResonance = -1;
        private int tuneCooldownUntilTick = -1;

        public int TuneCooldownRemainingTicks => Mathf.Max(0, tuneCooldownUntilTick - Find.TickManager.TicksGame);

        public Hediff_SeasonalResonance CurrentResonance => currentResonance;

        public static HediffComp_QingheCombatState GetFor(Pawn pawn)
        {
            return pawn != null && managers.TryGetValue(pawn, out var manager) ? manager : null;
        }

        private void Register()
        {
            managers.Remove(Pawn);
            managers.Add(Pawn, this);
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            Register();
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            RemoveCurrentResonance();
            if (GetFor(Pawn) == this)
            {
                managers.Remove(Pawn);
            }
        }

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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Register();
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            SyncResonanceHediff();
        }

        private void SyncResonanceHediff()
        {
            if (!MX_QHSkillUtility.HasSeasonalResonance(Pawn))
            {
                pendingTuneResonance = -1;
                RemoveCurrentResonance();
                return;
            }

            if (currentResonance == null)
            {
                currentResonance = (Hediff_SeasonalResonance)Pawn.health.AddHediff(MX_QHDefOf.MX_QH_ResonanceSpring);
            }
        }

        public void BeginTuning(FlowerBellResonance value)
        {
            if (!MX_QHSkillUtility.HasSeasonalResonance(Pawn)
                || value < FlowerBellResonance.Spring || value > FlowerBellResonance.Winter)
            {
                return;
            }
            pendingTuneResonance = (int)value;
        }

        public void CompleteTuning()
        {
            if (!MX_QHSkillUtility.HasSeasonalResonance(Pawn))
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
