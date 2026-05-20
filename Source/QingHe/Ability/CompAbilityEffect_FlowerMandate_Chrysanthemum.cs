using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilityFlowerMandate_Chrysanthemum : CompProperties_AbilityEffect
    {
        public HediffDef resourceCostDef;
        public float resourceCost = 0f;
        public string missingResourceMessage = "花令不足。";

        public float radius = 2.5f;

        public DamageDef damageDef;
        public float damageAmount = 16f;
        public float armorPenetration = 0.25f;

        public float stunDamageAmount = 8f;
        public float bleedDamageAmount = 5f;

        public HediffDef slowHediff;
        public float slowSeverity = 1f;
        public int slowDurationTicks = 1800;

        public float brainDestroyChance = 0.08f;

        public string warmupCasterFx = "MX_QH_Effecter_FlowerMandate_ChrysanthemumWarmupCaster";
        public string warmupTargetFx = "MX_QH_Effecter_FlowerMandate_ChrysanthemumWarmupTarget";
        public string releaseCasterFx = "MX_QH_Effecter_FlowerMandate_ChrysanthemumReleaseCaster";
        public string releaseTargetFx = "MX_QH_Effecter_FlowerMandate_ChrysanthemumReleaseTarget";
        public string releaseFleck = "MX_QH_Fleck_FlowerMandate_ChrysanthemumReleaseTarget";

        public string curveMote = "MX_QH_Mote_FlowerMandate_ChrysanthemumCurvedTrail";
        public string curveLineFleck = "MX_QH_Fleck_FlowerMandate_ChrysanthemumCurveLine";
        public string curveDistortFleck = "MX_QH_Fleck_FlowerMandate_ChrysanthemumCurveDistortion";
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
        public string hitFx = "MX_QH_Effecter_FlowerMandate_ChrysanthemumHit";
        public string hitFleck = "MX_QH_Fleck_FlowerMandate_ChrysanthemumHit";
        public string hitBurstFleck = "MX_QH_Fleck_FlowerMandate_ChrysanthemumHitExplosion";
        public string brainBreakFx = "MX_QH_Effecter_FlowerMandate_ChrysanthemumBrainBreak";

        public CompProperties_AbilityFlowerMandate_Chrysanthemum()
        {
            compClass = typeof(CompAbilityEffect_FlowerMandate_Chrysanthemum);
        }
    }

    public class CompAbilityEffect_FlowerMandate_Chrysanthemum : CompAbilityEffect
    {
        public new CompProperties_AbilityFlowerMandate_Chrysanthemum Props => (CompProperties_AbilityFlowerMandate_Chrysanthemum)props;

        public override bool GizmoDisabled(out string reason)
        {
            Pawn pawn = parent?.pawn;
            if (NeedsResource(pawn))
            {
                reason = Props.missingResourceMessage;
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
                    Messages.Message(Props.missingResourceMessage, pawn, MessageTypeDefOf.RejectInput, historical: false);
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
            if (target.IsValid)
            {
                GenDraw.DrawRadiusRing(target.Cell, Props.radius, new Color(1f, 0.75f, 0.45f, 0.45f));
            }
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

            GraphicsUtility.Fx(map, caster.Position, Props.releaseCasterFx, 1f);
            GraphicsUtility.Fx(map, center, Props.releaseTargetFx, 1f);
            GraphicsUtility.Fleck(map, center, Props.releaseFleck, Mathf.Max(0.85f, Props.radius * 0.65f));
            GraphicsUtility.Fleck(map, center, Props.releaseImpactFleck, Mathf.Max(0.45f, Props.releaseImpactScale));

            DamageDef damageDef = Props.damageDef ?? MX_QHDefOf.MX_QH_NoteImpact ?? DamageDefOf.Cut;
            List<Pawn> victims = RadialUtility.CollectHostilePawns(map, center, caster, Props.radius);
            for (int i = 0; i < victims.Count; i++)
            {
                Pawn victim = victims[i];
                victim.TakeDamage(new DamageInfo(damageDef, Props.damageAmount, Props.armorPenetration, -1f, caster));

                if (Props.stunDamageAmount > 0f)
                {
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Stun, Props.stunDamageAmount, 0f, -1f, caster));
                }

                MX_QHUtility.ApplyBleed(victim, Props.bleedDamageAmount);
                MX_QHUtility.TryApplyOrRefreshHediff(victim, Props.slowHediff, Props.slowSeverity, Props.slowDurationTicks);

                if (victim.Spawned && !victim.Destroyed && victim.MapHeld == map)
                {
                    GraphicsUtility.Fx(map, victim.Position, Props.hitFx, 1f);
                    GraphicsUtility.Fleck(map, victim.Position, Props.hitFleck, 0.72f);
                    GraphicsUtility.Fleck(map, victim.Position, Props.hitBurstFleck, 0.64f);
                }

                TryBreakBrain(victim, map);
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
                if (part.def == DefDatabase<BodyPartDef>.GetNamedSilentFail("Brain"))
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
                GraphicsUtility.Fx(map, victim.Position, Props.brainBreakFx, 1f);
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
            Mote_FlowerMandate_ChrysanthemumCurvedTrail mote = ThingMaker.MakeThing(moteDef) as Mote_FlowerMandate_ChrysanthemumCurvedTrail;
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

            if (GraphicsUtility.Mote(map, cell, mote))
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

            GraphicsUtility.Fx(map, caster.Position, Props.warmupCasterFx, 1f);
            GraphicsUtility.Fx(map, cell, Props.warmupTargetFx, Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(intensity)));
        }
    }
}

