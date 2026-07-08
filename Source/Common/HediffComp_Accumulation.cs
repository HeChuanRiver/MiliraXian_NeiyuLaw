using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_Accumulation : HediffCompProperties
    {
        public float maxSeverity = 1f;
        public int ticksUntilDecayAfterRefresh;
        public float severityDecayPerTick;
        public bool showSeverityPercent = true;
        public bool removeWhenSeverityZero = true;
        public ThingDef progressBarMoteDef;
        public DamageDef fullDamageDef;
        public float fullDamageAmount;
        public float fullDamageArmorPenetration;
        public HediffDef effectHediff;
        public HediffDef mechEffectHediff;
        public HediffDef resistanceHediff;
        public float effectSeverity = 1f;
        public int effectDurationTicks = -1;
        public bool removeOnFullAccumulation = true;
        public string fullAccumulationText;
        public Vector3 fullAccumulationTextOffset = Vector3.zero;
        public Color fullAccumulationTextColor = Color.white;
        public float fullAccumulationTextDuration = 1.2f;
        public ThingDef fullAccumulationMoteDef;
        public Vector3 fullAccumulationMoteOffset = new Vector3(0f, 0f, 0.85f);
        public float fullAccumulationMoteScale = 1f;

        public HediffCompProperties_Accumulation()
        {
            compClass = typeof(HediffComp_Accumulation);
        }
    }

    public class HediffComp_Accumulation : HediffComp
    {
        private int ticksUntilDecay;
        private Pawn caster;
        private Mote progressBarMote;

        private HediffCompProperties_Accumulation PropsAccumulation => (HediffCompProperties_Accumulation)props;

        public Pawn Caster => caster;

        public bool CanAccumulate => AccumulationUtility.CanAccumulate(Pawn, parent?.def);

        public bool ShowSeverityPercent => PropsAccumulation.showSeverityPercent;

        public bool ShouldRemoveAtZero => PropsAccumulation.removeWhenSeverityZero;

        public float Progress => PropsAccumulation.maxSeverity > 0f ? Mathf.Clamp01(parent.Severity / PropsAccumulation.maxSeverity) : parent.Severity;

        public HediffDef EffectHediff => AccumulationUtility.ResolveEffectHediff(Pawn, PropsAccumulation);

        public HediffDef ResistanceHediff => PropsAccumulation.resistanceHediff;

        public void AddAccumulation(Pawn newCaster, float severityOffset)
        {
            if (!CanAccumulate)
            {
                return;
            }

            caster = newCaster;
            ticksUntilDecay = PropsAccumulation.ticksUntilDecayAfterRefresh;
            parent.Severity = Mathf.Min(PropsAccumulation.maxSeverity, parent.Severity + severityOffset);
            if (parent.Severity >= PropsAccumulation.maxSeverity)
            {
                TriggerFullAccumulation();
            }
        }

        public void TickStatusAccumulation()
        {
            if (!CanAccumulate)
            {
                Pawn?.health?.RemoveHediff(parent);
                return;
            }

            MaintainProgressBarMote();
            TickDecay();
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (progressBarMote != null && !progressBarMote.Destroyed)
            {
                progressBarMote.Destroy(DestroyMode.Vanish);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksUntilDecay, "ticksUntilDecay", 0);
            Scribe_References.Look(ref caster, "caster");
        }

        private void TickDecay()
        {
            if (ticksUntilDecay > 0)
            {
                ticksUntilDecay--;
                return;
            }

            if (PropsAccumulation.severityDecayPerTick > 0f)
            {
                parent.Severity = Mathf.Max(0f, parent.Severity - PropsAccumulation.severityDecayPerTick);
            }
        }

        private void TriggerFullAccumulation()
        {
            Pawn target = Pawn;
            if (target == null || target.Dead || target.health?.hediffSet == null)
            {
                return;
            }

            ApplyFullDamage(target);
            ApplyEffectHediff(target);
            ThrowFullAccumulationText(target);
            ThrowFullAccumulationMote(target);
            if (PropsAccumulation.removeOnFullAccumulation && target.health.hediffSet.hediffs.Contains(parent))
            {
                target.health.RemoveHediff(parent);
            }
        }

        private void ApplyFullDamage(Pawn target)
        {
            if (PropsAccumulation.fullDamageDef == null || PropsAccumulation.fullDamageAmount <= 0f)
            {
                return;
            }

            DamageInfo damageInfo = new DamageInfo(PropsAccumulation.fullDamageDef, PropsAccumulation.fullDamageAmount, PropsAccumulation.fullDamageArmorPenetration, -1f, caster);
            target.TakeDamage(damageInfo);
        }

        private void ApplyEffectHediff(Pawn target)
        {
            HediffDef effectHediff = EffectHediff;
            if (effectHediff == null || target.health.hediffSet.GetFirstHediffOfDef(effectHediff) != null)
            {
                return;
            }

            int resistanceStageBeforeEffect = AccumulationUtility.GetResistanceStage(target, parent.def);
            Hediff effect = HediffMaker.MakeHediff(effectHediff, target);
            effect.Severity = PropsAccumulation.effectSeverity;
            target.health.AddHediff(effect);
            effect.TryGetComp<HediffComp_AccumulationEffect>()?.Initialize(parent.def, resistanceStageBeforeEffect);
            if (PropsAccumulation.effectDurationTicks > 0)
            {
                effect.TryGetComp<HediffComp_Disappears>()?.SetDuration(PropsAccumulation.effectDurationTicks);
            }
        }

        private void ThrowFullAccumulationText(Pawn target)
        {
            if (PropsAccumulation.fullAccumulationText.NullOrEmpty() || target == null || !target.Spawned || target.MapHeld == null)
            {
                return;
            }

            MoteMaker.ThrowText(target.DrawPos + PropsAccumulation.fullAccumulationTextOffset, target.MapHeld, PropsAccumulation.fullAccumulationText, PropsAccumulation.fullAccumulationTextColor, PropsAccumulation.fullAccumulationTextDuration);
        }

        private void ThrowFullAccumulationMote(Pawn target)
        {
            if (PropsAccumulation.fullAccumulationMoteDef == null || target == null || !target.Spawned || target.MapHeld == null)
            {
                return;
            }

            MoteMaker.MakeAttachedOverlay(
                target,
                PropsAccumulation.fullAccumulationMoteDef,
                PropsAccumulation.fullAccumulationMoteOffset,
                Mathf.Max(0.01f, PropsAccumulation.fullAccumulationMoteScale));
        }

        private void MaintainProgressBarMote()
        {
            ThingDef moteDef = PropsAccumulation.progressBarMoteDef;
            if (Pawn == null || !Pawn.Spawned || Pawn.MapHeld == null || moteDef == null)
            {
                return;
            }

            Mote_AccumulationBar bar = progressBarMote as Mote_AccumulationBar;
            if (bar == null || bar.Destroyed)
            {
                bar = ThingMaker.MakeThing(moteDef) as Mote_AccumulationBar;
                if (bar == null)
                {
                    return;
                }

                bar.Attach(Pawn, Pawn);
                bar.SourceHediffDef = parent.def;
                GenSpawn.Spawn(bar, Pawn.Position, Pawn.MapHeld, WipeMode.Vanish);
                progressBarMote = bar;
            }

            bar.SourceHediffDef = parent.def;
            bar.Progress = Progress;
            bar.Maintain();
        }
    }
}
