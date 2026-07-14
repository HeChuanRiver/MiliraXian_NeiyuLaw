using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public interface IAbnormalResult
    {
        void Initialize(Pawn instigator, HediffDef_Abnormal abnormalDef);
    }

    public class Hediff_Abnormal : HediffWithComps
    {
        private int ticksUntilDecay;
        private Pawn source;
        private Mote progressBarMote;

        public HediffDef_Abnormal AbnormalDef => def as HediffDef_Abnormal;

        public Pawn Source => source;

        public float AccumulationLimit => AbnormalSystem.GetAccumulationLimit(pawn, AbnormalDef);

        public float Progress
        {
            get
            {
                float limit = AccumulationLimit;
                return limit > 0f ? Mathf.Clamp01(Severity / limit) : 0f;
            }
        }

        public override string LabelInBrackets
        {
            get
            {
                string baseLabel = base.LabelInBrackets;
                string percent = Progress.ToStringPercent("F0");
                return baseLabel.NullOrEmpty() ? percent : $"{baseLabel}, {percent}";
            }
        }

        public override bool ShouldRemove => base.ShouldRemove || Severity <= 0f;

        public bool ApplyAccumulation(Pawn newSource, float amount, float accumulationLimit)
        {
            if (amount <= 0f || accumulationLimit <= 0f)
            {
                return false;
            }

            source = newSource;
            ticksUntilDecay = Mathf.Max(0, AbnormalDef?.ticksUntilDecayAfterRefresh ?? 0);
            Severity += amount;
            NotifyApplied(amount);
            if (Severity < accumulationLimit)
            {
                return false;
            }

            Trigger();
            return true;
        }

        public override void Tick()
        {
            base.Tick();
            if (pawn?.health?.hediffSet == null || !pawn.health.hediffSet.hediffs.Contains(this))
            {
                return;
            }

            if (pawn.IsHashIntervalTick(60))
            {
                float limit = AccumulationLimit;
                if (limit <= 0f)
                {
                    pawn.health.RemoveHediff(this);
                    return;
                }

                if (Severity >= limit)
                {
                    Trigger();
                    return;
                }
            }

            MaintainProgressBarMote();
            TickDecay();
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (progressBarMote != null && !progressBarMote.Destroyed)
            {
                progressBarMote.Destroy(DestroyMode.Vanish);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksUntilDecay, "abnormalTicksUntilDecay", 0);
            Scribe_References.Look(ref source, "abnormalSource");
        }

        protected virtual void Trigger()
        {
            Pawn target = pawn;
            HediffDef_Abnormal abnormalDef = AbnormalDef;
            if (target == null || target.Dead || target.health?.hediffSet == null || abnormalDef == null)
            {
                return;
            }

            ApplyFullDamage(target, abnormalDef);
            ApplyEffect(target, abnormalDef);
            PlayTriggerVisuals(target, abnormalDef);
            if (abnormalDef.removeOnTriggered && target.health.hediffSet.hediffs.Contains(this))
            {
                target.health.RemoveHediff(this);
            }
        }

        protected virtual void NotifyApplied(float amount)
        {
            if (comps == null)
            {
                return;
            }

            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] is HediffComp_OnAbnormalApplied listener)
                {
                    listener.NotifyApplied(source, amount);
                }
            }
        }

        private void TickDecay()
        {
            if (ticksUntilDecay > 0)
            {
                ticksUntilDecay--;
                return;
            }

            float decay = AbnormalDef?.accumulationDecayPerTick ?? 0f;
            if (decay > 0f)
            {
                Severity = Mathf.Max(0f, Severity - decay);
            }
        }

        private void ApplyFullDamage(Pawn target, HediffDef_Abnormal abnormalDef)
        {
            if (abnormalDef.fullDamageDef == null || abnormalDef.fullDamageAmount <= 0f)
            {
                return;
            }

            DamageInfo damageInfo = new DamageInfo(
                abnormalDef.fullDamageDef,
                abnormalDef.fullDamageAmount,
                abnormalDef.fullDamageArmorPenetration,
                -1f,
                source);
            target.TakeDamage(damageInfo);
        }

        private void ApplyEffect(Pawn target, HediffDef_Abnormal abnormalDef)
        {
            if (abnormalDef.effectHediff == null || target.health.hediffSet.GetFirstHediffOfDef(abnormalDef.effectHediff) != null)
            {
                return;
            }

            Hediff effect = HediffMaker.MakeHediff(abnormalDef.effectHediff, target);
            effect.Severity = abnormalDef.effectSeverity;
            if (effect is IAbnormalResult result)
            {
                result.Initialize(source, abnormalDef);
            }

            target.health.AddHediff(effect);
            if (abnormalDef.effectDurationTicks > 0)
            {
                effect.TryGetComp<HediffComp_Disappears>()?.SetDuration(abnormalDef.effectDurationTicks);
            }
        }

        private void PlayTriggerVisuals(Pawn target, HediffDef_Abnormal abnormalDef)
        {
            if (!abnormalDef.triggerText.NullOrEmpty() && target.Spawned && target.MapHeld != null)
            {
                MoteMaker.ThrowText(
                    target.DrawPos + abnormalDef.triggerTextOffset,
                    target.MapHeld,
                    abnormalDef.triggerText,
                    abnormalDef.triggerTextColor,
                    abnormalDef.triggerTextDuration);
            }

            if (abnormalDef.triggerMoteDef != null && target.Spawned && target.MapHeld != null)
            {
                MoteMaker.MakeAttachedOverlay(
                    target,
                    abnormalDef.triggerMoteDef,
                    abnormalDef.triggerMoteOffset,
                    Mathf.Max(0.01f, abnormalDef.triggerMoteScale));
            }
        }

        private void MaintainProgressBarMote()
        {
            ThingDef moteDef = AbnormalDef?.progressBarMoteDef;
            if (pawn == null || !pawn.Spawned || pawn.MapHeld == null || moteDef == null)
            {
                return;
            }

            Mote_AbnormalBar bar = progressBarMote as Mote_AbnormalBar;
            if (bar == null || bar.Destroyed)
            {
                bar = ThingMaker.MakeThing(moteDef) as Mote_AbnormalBar;
                if (bar == null)
                {
                    return;
                }

                bar.Attach(pawn, pawn);
                bar.SourceAbnormalDef = AbnormalDef;
                GenSpawn.Spawn(bar, pawn.Position, pawn.MapHeld, WipeMode.Vanish);
                progressBarMote = bar;
            }

            bar.SourceAbnormalDef = AbnormalDef;
            bar.Progress = Progress;
            bar.Maintain();
        }
    }
}
