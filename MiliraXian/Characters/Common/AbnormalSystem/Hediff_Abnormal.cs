using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.AbnormalSystem
{
    public interface IAbnormalResult
    {
        void Initialize(Pawn instigator, HediffDef_Abnormal abnormalDef);
    }

    public class Hediff_Abnormal : HediffWithComps
    {
        private const int AccumulationLimitRefreshTicks = 60;

        private int ticksUntilDecay;
        private Pawn source;
        private Mote progressBarMote;
        private float cachedAccumulationLimit = -1f;
        private int accumulationLimitCachedAtTick = int.MinValue;
        /// <summary>Build-up while the effect is active; full at the limit × aggravationLimitFactor.</summary>
        private float aggravation;

        public HediffDef_Abnormal AbnormalDef => def as HediffDef_Abnormal;

        public Pawn Source => source;

        public float AccumulationLimit => GetAccumulationLimit(false);

        public float Progress
        {
            get
            {
                if (EffectActive)
                {
                    float aLimit = AggravationLimit;
                    return aLimit > 0f ? Mathf.Clamp01(aggravation / aLimit) : 0f;
                }
                float limit = AccumulationLimit;
                return limit > 0f ? Mathf.Clamp01(Severity / limit) : 0f;
            }
        }

        /// <summary>The abnormal's effect is on the pawn: hits fill the aggravation bar instead.</summary>
        public bool EffectActive => ActiveEffect != null;

        private Hediff ActiveEffect => AbnormalDef?.effectHediff == null ? null : pawn?.health?.hediffSet?.GetFirstHediffOfDef(AbnormalDef.effectHediff);

        public float AggravationLimit => AccumulationLimit * Mathf.Max(0f, AbnormalDef?.aggravationLimitFactor ?? 0f);

        public float Aggravation => aggravation;

        public override string LabelInBrackets
        {
            get
            {
                string baseLabel = base.LabelInBrackets;
                string percent = Progress.ToStringPercent("F0");
                return baseLabel.NullOrEmpty() ? percent : $"{baseLabel}, {percent}";
            }
        }

        public override bool ShouldRemove => (base.ShouldRemove || Severity <= 0f) && aggravation <= 0f && !EffectActive;

        public AbnormalApplyStatus ApplyAccumulation(Pawn newSource, float amount, float accumulationLimit)
        {
            if (amount <= 0f || accumulationLimit <= 0f)
            {
                return AbnormalApplyStatus.InvalidAmount;
            }

            source = newSource;
            ticksUntilDecay = Mathf.Max(0, AbnormalDef?.ticksUntilDecayAfterRefresh ?? 0);
            cachedAccumulationLimit = accumulationLimit;
            accumulationLimitCachedAtTick = CurrentTick;
            // The on-hit extras (blood loss, toxic build-up, hypothermia, mech energy) come with every hit,
            // the effect active or not.
            NotifyApplied(amount);
            Hediff effect = ActiveEffect;
            if (effect != null)
            {
                float aLimit = accumulationLimit * Mathf.Max(0f, AbnormalDef?.aggravationLimitFactor ?? 0f);
                if (aLimit <= 0f)
                {
                    return AbnormalApplyStatus.EffectAlreadyActive;
                }
                aggravation += amount;
                if (aggravation < aLimit)
                {
                    return AbnormalApplyStatus.AggravationApplied;
                }
                Aggravate(effect);
                return AbnormalApplyStatus.Aggravated;
            }

            Severity += amount;
            if (Severity < accumulationLimit)
            {
                return AbnormalApplyStatus.Applied;
            }

            Trigger();
            return AbnormalApplyStatus.Triggered;
        }

        /// <summary>
        /// The aggravation bar is full: what is left of the effect is settled at once by its parts
        /// (<see cref="IAbnormalAggravation"/>), plus the def's aggravation damage; then the effect ends
        /// (unless a part kept it) and the abnormal builds up from zero again.
        /// </summary>
        protected virtual void Aggravate(Hediff effect)
        {
            Pawn target = pawn;
            HediffDef_Abnormal abnormalDef = AbnormalDef;
            aggravation = 0f;
            if (target == null || target.Dead || abnormalDef == null || effect == null)
            {
                return;
            }
            int remaining = effect.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? 0;
            bool keep = false;
            if (effect is IAbnormalAggravation self)
            {
                self.Aggravate(source, remaining, ref keep);
            }
            if (effect is HediffWithComps withComps && withComps.comps != null)
            {
                for (int i = 0; i < withComps.comps.Count && !target.Dead; i++)
                {
                    if (withComps.comps[i] is IAbnormalAggravation part)
                    {
                        part.Aggravate(source, remaining, ref keep);
                    }
                }
            }
            if (!target.Dead && abnormalDef.aggravationDamageDef != null)
            {
                float amount = abnormalDef.aggravationDamageBase + abnormalDef.aggravationDamagePerSecond * remaining / 60f;
                if (amount > 0f)
                {
                    target.TakeDamage(new DamageInfo(abnormalDef.aggravationDamageDef, amount, abnormalDef.aggravationArmorPenetration, -1f, source));
                }
            }
            string text = abnormalDef.aggravationText.NullOrEmpty() ? abnormalDef.triggerText : abnormalDef.aggravationText;
            if (!text.NullOrEmpty() && target.Spawned && target.MapHeld != null)
            {
                MoteMaker.ThrowText(target.DrawPos + abnormalDef.triggerTextOffset, target.MapHeld, text, abnormalDef.triggerTextColor, abnormalDef.triggerTextDuration);
            }
            if (abnormalDef.triggerMoteDef != null && target.Spawned && target.MapHeld != null)
            {
                MoteMaker.MakeAttachedOverlay(target, abnormalDef.triggerMoteDef, abnormalDef.triggerMoteOffset, Mathf.Max(0.01f, abnormalDef.triggerMoteScale));
            }
            if (!keep && !target.Dead && target.health.hediffSet.hediffs.Contains(effect))
            {
                target.health.RemoveHediff(effect);
            }
        }

        public override void Tick()
        {
            base.Tick();
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            if (pawn.IsHashIntervalTick(60))
            {
                float limit = GetAccumulationLimit(true);
                if (limit <= 0f)
                {
                    pawn.health.RemoveHediff(this);
                    return;
                }

                if (Severity >= limit && !EffectActive)
                {
                    Trigger();
                    return;
                }
            }

            if (aggravation > 0f && !EffectActive)
            {
                // The effect ran out before the bar filled: the bar goes with it.
                aggravation = 0f;
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
            progressBarMote = null;
            RefreshProgressBarStackIndices(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksUntilDecay, "abnormalTicksUntilDecay", 0);
            Scribe_References.Look(ref source, "abnormalSource");
            Scribe_Values.Look(ref aggravation, "abnormalAggravation", 0f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                cachedAccumulationLimit = -1f;
                accumulationLimitCachedAtTick = int.MinValue;
            }
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
                aggravation = Mathf.Max(0f, aggravation - decay);
            }
        }

        private void ApplyFullDamage(Pawn target, HediffDef_Abnormal abnormalDef)
        {
            if (abnormalDef.fullDamageDef == null || abnormalDef.fullDamageAmount <= 0f)
            {
                return;
            }

            DamageInfo damageInfo = new(
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
                RefreshProgressBarStackIndices(pawn);
            }

            bar.SourceAbnormalDef = AbnormalDef;
            bar.Progress = Progress;
            bar.Maintain();
        }

        private static void RefreshProgressBarStackIndices(Pawn target)
        {
            List<Hediff> hediffs = target?.health?.hediffSet?.hediffs;
            if (hediffs == null)
            {
                return;
            }

            int stackIndex = 0;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_Abnormal abnormal = hediffs[i] as Hediff_Abnormal;
                if (abnormal == null)
                {
                    continue;
                }

                Mote_AbnormalBar bar = abnormal.progressBarMote as Mote_AbnormalBar;
                if (bar != null && !bar.Destroyed)
                {
                    bar.StackIndex = stackIndex;
                }
                stackIndex++;
            }
        }

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        private float GetAccumulationLimit(bool forceRefresh)
        {
            int currentTick = CurrentTick;
            if (forceRefresh
                || cachedAccumulationLimit < 0f
                || currentTick - accumulationLimitCachedAtTick >= AccumulationLimitRefreshTicks)
            {
                cachedAccumulationLimit = AbnormalSystem.GetAccumulationLimit(pawn, AbnormalDef);
                accumulationLimitCachedAtTick = currentTick;
            }

            return cachedAccumulationLimit;
        }
    }
}
