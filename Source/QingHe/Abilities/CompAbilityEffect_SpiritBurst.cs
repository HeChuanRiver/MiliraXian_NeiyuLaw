using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Vfx;
using MiliraXian.Characters.QingHe.Things.Mote;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilitySpiritBurst : CompProperties_AbilityEffect
    {
        public HediffDef resourceCostDef;
        public float resourceCost = 0f;
        public string missingResourceMessage = "MX_QH_FlowerDecreeNotEnough";

        public float radius = 2.5f;

        public DamageDef damageDef;
        public float damageAmount = 16f;
        public float armorPenetration = 0.25f;

        public float stunDamageAmount = 8f;

        public float brainDestroyChance = 0.08f;
        public float enhancedPsychicSensitivityThreshold = 1f;
        public float enhancedPsychicDamageMultiplier = 10f;
        public List<HediffDef_Abnormal> enhancedFearAbnormals = new();
        public float enhancedFearAccumulationAmount = 150f;

        public string warmupCasterFx = "MX_QH_Effecter_SpiritBurstWarmupCaster";
        public string warmupTargetFx = "MX_QH_Effecter_SpiritBurstWarmupTarget";
        public string releaseCasterFx = "MX_QH_Effecter_SpiritBurstReleaseCaster";
        public string releaseTargetFx = "MX_QH_Effecter_SpiritBurstReleaseTarget";
        public string releaseFleck = "MX_QH_Fleck_SpiritBurstReleaseTarget";

        public string curveMote = "MX_QH_Mote_SpiritBurstCurvedTrail";
        public string curveLineFleck = "MX_QH_Fleck_SpiritBurstCurveLine";
        public string curveDistortFleck = "MX_QH_Fleck_SpiritBurstCurveDistortion";
        public float curveDistortWidth = 4.2f;
        public float curveDistortAlpha = 0.68f;
        public float curveWidth = 3.1f;
        public float curveDensity = 8.8f;
        public float curveWaveLen = 4.8f;
        public float curveAlpha = 2.1f;
        public int curveAfterLayers = 9;
        public int curveAfterGap = 1;
        public float curveAfterAlpha = 0.62f;
        public int curveMinSegs = 28;
        public int curveMaxSegs = 96;
        public int curveDistortStep = 3;
        public int curveAnimTicks = 126;
        public int curveGrowTicks = 14;

        public string releaseImpactFleck = "ExpandingDistortionRing";
        public float releaseImpactScale = 0.78f;
        public string hitFx = "MX_QH_Effecter_SpiritBurstHit";
        public string hitFleck = "MX_QH_Fleck_SpiritBurstHit";
        public string hitBurstFleck = "MX_QH_Fleck_SpiritBurstHitExplosion";
        public string brainBreakFx = "MX_QH_Effecter_SpiritBurstBrainBreak";

        public CompProperties_AbilitySpiritBurst()
        {
            compClass = typeof(CompAbilityEffect_SpiritBurst);
        }
    }

    public class CompAbilityEffect_SpiritBurst : CompAbilityEffect
    {
        public new CompProperties_AbilitySpiritBurst Props => (CompProperties_AbilitySpiritBurst)props;

        public override bool GizmoDisabled(out string reason)
        {
            Pawn pawn = parent?.pawn;
            if (NeedsResource(pawn))
            {
                reason = Props.missingResourceMessage.Translate();
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            Pawn pawn = parent?.pawn;
            if (NeedsResource(pawn))
            {
                if (throwMessages)
                {
                    Messages.Message(Props.missingResourceMessage.Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return true;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent?.pawn;
            if (Props.resourceCostDef != null && Props.resourceCost > 0f)
            {
                PawnSpecialResourceUtility.TryConsumeResource(pawn, Props.resourceCostDef, Props.resourceCost);
            }

            ResolvePulse(target);
            SpawnTrail(target);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            Map map = Find.CurrentMap;
            if (map == null || !target.Cell.IsValid)
            {
                return;
            }

            GenDraw.DrawRadiusRing(
                target.Cell,
                Props.radius,
                new Color(1f, 0.75f, 0.45f, 0.45f));
        }

        public override IEnumerable<PreCastAction> GetPreCastActions()
        {
            int warmupTicks = parent?.def?.verbProperties == null
                ? 0
                : Mathf.Max(0, parent.def.verbProperties.warmupTime.SecondsToTicks());
            if (warmupTicks <= 0)
            {
                yield break;
            }

            yield return new PreCastAction
            {
                ticksAwayFromCast = warmupTicks,
                action = delegate(LocalTargetInfo target, LocalTargetInfo dest)
                {
                    SpawnWarmupVisual(target, 1f);
                }
            };

            int half = Mathf.Max(1, warmupTicks / 2);
            if (half < warmupTicks)
            {
                yield return new PreCastAction
                {
                    ticksAwayFromCast = half,
                    action = delegate(LocalTargetInfo target, LocalTargetInfo dest)
                    {
                        SpawnWarmupVisual(target, 0.85f);
                    }
                };
            }
        }

        private bool NeedsResource(Pawn pawn)
        {
            return Props.resourceCostDef != null
                && Props.resourceCost > 0f
                && PawnSpecialResourceUtility.GetCurrentResource(pawn, Props.resourceCostDef) < Props.resourceCost;
        }

        private void ResolvePulse(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster?.MapHeld == null)
            {
                return;
            }

            Map map = caster.MapHeld;
            IntVec3 center = target.IsValid ? target.Cell : caster.Position;
            if (!center.IsValid || !center.InBounds(map))
            {
                center = caster.Position;
            }

            MX_QHGraphicsUtility.Fx(map, caster.Position, Props.releaseCasterFx, 1f);
            MX_QHGraphicsUtility.Fx(map, center, Props.releaseTargetFx, 1f);
            MX_QHGraphicsUtility.Fleck(map, center, Props.releaseFleck, Mathf.Max(0.85f, Props.radius * 0.65f));
            MX_QHGraphicsUtility.Fleck(map, center, Props.releaseImpactFleck, Mathf.Max(0.45f, Props.releaseImpactScale));

            DamageDef damageDef = Props.damageDef ?? MX_QHDefOf.MX_QH_NoteImpact ?? DamageDefOf.Cut;
            bool enhanced = MX_QHSkillUtility.HasAllFlowerMandates(MX_QH_HediffUtility.GetFlowerResonance(caster));
            float specialFactor = MX_QHSkillUtility.GetSpecialAbilityEffectFactor(caster);
            List<Pawn> victims = RadialUtility.CollectHostilePawns(map, center, caster, Props.radius);
            for (int i = 0; i < victims.Count; i++)
            {
                Pawn victim = victims[i];
                float damageAmount = Props.damageAmount * specialFactor;
                if (enhanced && victim.GetStatValue(StatDefOf.PsychicSensitivity) > Props.enhancedPsychicSensitivityThreshold)
                {
                    damageAmount *= Mathf.Max(0f, Props.enhancedPsychicDamageMultiplier);
                }

                victim.TakeDamage(new DamageInfo(damageDef, damageAmount, Props.armorPenetration, -1f, caster));

                if (Props.stunDamageAmount > 0f)
                {
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Stun, Props.stunDamageAmount, 0f, -1f, caster));
                }

                if (victim.Spawned && !victim.Destroyed && victim.MapHeld == map)
                {
                    MX_QHGraphicsUtility.Fx(map, victim.Position, Props.hitFx, 1f);
                    MX_QHGraphicsUtility.Fleck(map, victim.Position, Props.hitFleck, 0.72f);
                    MX_QHGraphicsUtility.Fleck(map, victim.Position, Props.hitBurstFleck, 0.64f);
                }

                TryBreakBrain(victim, map);
                ApplyEnhancedFear(enhanced, caster, victim, specialFactor);
            }
        }

        private void ApplyEnhancedFear(bool enhanced, Pawn caster, Pawn victim, float specialFactor)
        {
            if (!enhanced || caster == null || victim == null || victim.Dead || victim.Destroyed || Props.enhancedFearAbnormals == null || Props.enhancedFearAccumulationAmount <= 0f)
            {
                return;
            }

            float amount = Props.enhancedFearAccumulationAmount * specialFactor;
            for (int i = 0; i < Props.enhancedFearAbnormals.Count; i++)
            {
                HediffDef_Abnormal abnormal = Props.enhancedFearAbnormals[i];
                if (abnormal != null)
                {
                    AbnormalSystem.ApplyAccumulation(caster, victim, abnormal, amount);
                }
            }
        }

        private void TryBreakBrain(Pawn victim, Map map)
        {
            if (victim == null || victim.Dead || Rand.Value >= Mathf.Clamp01(Props.brainDestroyChance))
            {
                return;
            }

            BodyPartRecord brain = null;
            foreach (BodyPartRecord part in victim.health.hediffSet.GetNotMissingParts())
            {
                if (part.def == MX_QHDefOf.Brain)
                {
                    brain = part;
                    break;
                }
            }

            if (brain == null)
            {
                return;
            }

            victim.health.AddHediff(HediffDefOf.MissingBodyPart, brain);
            if (victim.Spawned && !victim.Destroyed && victim.MapHeld == map)
            {
                MX_QHGraphicsUtility.Fx(map, victim.Position, Props.brainBreakFx, 1f);
            }
        }

        private void SpawnTrail(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || caster.MapHeld == null)
            {
                return;
            }

            Map map = caster.MapHeld;
            IntVec3 cell = target.IsValid ? target.Cell : caster.Position;
            if (!cell.IsValid || !cell.InBounds(map))
            {
                cell = caster.Position;
            }

            ThingDef moteDef = Props.curveMote.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(Props.curveMote);
            if (moteDef == null)
            {
                return;
            }

            FleckDef lineFleck = Props.curveLineFleck.NullOrEmpty() ? null : DefDatabase<FleckDef>.GetNamedSilentFail(Props.curveLineFleck);
            if (lineFleck == null)
            {
                return;
            }

            FleckDef distortFleck = Props.curveDistortFleck.NullOrEmpty() ? null : DefDatabase<FleckDef>.GetNamedSilentFail(Props.curveDistortFleck);
            Mote_SpiritBurstCurvedTrail mote = ThingMaker.MakeThing(moteDef) as Mote_SpiritBurstCurvedTrail;
            if (mote == null)
            {
                return;
            }

            mote.Setup(
                new TargetInfo(caster),
                new TargetInfo(cell, map),
                lineFleck,
                distortFleck,
                Mathf.Max(0.03f, Props.radius * 0.022f),
                Props.curveDistortWidth,
                Props.curveDistortAlpha,
                Props.curveWidth,
                Props.curveDensity,
                Props.curveWaveLen,
                Props.curveAnimTicks,
                Props.curveGrowTicks,
                Props.curveAlpha,
                Props.curveAfterLayers,
                Props.curveAfterGap,
                Props.curveAfterAlpha,
                Props.curveMinSegs,
                Props.curveMaxSegs,
                Props.curveDistortStep);

            if (MX_QHGraphicsUtility.Mote(map, cell, mote))
            {
                mote.exactPosition = cell.ToVector3Shifted();
            }
        }

        private void SpawnWarmupVisual(LocalTargetInfo target, float intensity)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || caster.Map == null)
            {
                return;
            }

            Map map = caster.Map;
            IntVec3 cell = target.IsValid ? target.Cell : caster.Position;
            if (!cell.IsValid || !cell.InBounds(map))
            {
                cell = caster.Position;
            }

            MX_QHGraphicsUtility.Fx(map, caster.Position, Props.warmupCasterFx, 1f);
            MX_QHGraphicsUtility.Fx(map, cell, Props.warmupTargetFx, Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(intensity)));
        }
    }
}

