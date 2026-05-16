using System.Collections.Generic;
using MiliraXian.Characters;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_FlowerMandate_Chrysanthemum : JobDriver_CastAbility
    {
        private CompProperties_AbilityFlowerMandate_Chrysanthemum Props
        {
            get
            {
                RimWorld.Ability ability = job?.ability;
                if (ability?.def?.comps == null)
                {
                    return null;
                }

                for (int i = 0; i < ability.def.comps.Count; i++)
                {
                    if (ability.def.comps[i] is CompProperties_AbilityFlowerMandate_Chrysanthemum props)
                    {
                        return props;
                    }
                }

                return null;
            }
        }

        public override string GetReport()
        {
            return "正在引导秋令音律";
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (Toil toil in base.MakeNewToils())
            {
                yield return toil;
            }

            CompProperties_AbilityFlowerMandate_Chrysanthemum props = Props;
            int delay = props?.postCastDelayTicks ?? 0;
            if (delay > 0)
            {
                Toil wait = ToilMaker.MakeToil("QHFlowerMandate_Chrysanthemum_PostCastDelay");
                wait.defaultCompleteMode = ToilCompleteMode.Delay;
                wait.defaultDuration = delay;
                yield return wait;
            }

            Toil pulse = ToilMaker.MakeToil("QHFlowerMandate_Chrysanthemum_ResolvePulse");
            pulse.initAction = delegate
            {
                ResolvePulse();
            };
            pulse.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pulse;

            int animTicks = props != null ? Mathf.Max(1, props.curveAnimTicks) : 1;
            Toil trail = ToilMaker.MakeToil("QHFlowerMandate_Chrysanthemum_ReleaseTrailAnim");
            trail.defaultCompleteMode = ToilCompleteMode.Delay;
            trail.defaultDuration = animTicks;
            trail.initAction = delegate
            {
                SpawnTrail();
            };
            yield return trail;
        }

        private void ResolvePulse()
        {
            CompProperties_AbilityFlowerMandate_Chrysanthemum props = Props;
            if (props == null || pawn?.MapHeld == null)
            {
                return;
            }

            Map map = pawn.MapHeld;
            IntVec3 center = job?.targetA.IsValid == true ? job.targetA.Cell : pawn.Position;
            if (!center.IsValid || !center.InBounds(map))
            {
                center = pawn.Position;
            }

            GraphicsUtility.Fx(map, pawn.Position, props.releaseCasterFx, 1f);
            GraphicsUtility.Fx(map, center, props.releaseTargetFx, 1f);
            GraphicsUtility.Fleck(map, center, props.releaseFleck, Mathf.Max(0.85f, props.radius * 0.65f));
            GraphicsUtility.Fleck(map, center, props.releaseImpactFleck, Mathf.Max(0.45f, props.releaseImpactScale));

            DamageDef damageDef = props.damageDef ?? MX_QHDefOf.MX_QH_NoteImpact ?? DamageDefOf.Cut;
            List<Pawn> victims = RadialUtility.CollectHostilePawns(map, center, pawn, props.radius);
            int affected = 0;

            for (int i = 0; i < victims.Count; i++)
            {
                Pawn victim = victims[i];
                victim.TakeDamage(new DamageInfo(damageDef, props.damageAmount, props.armorPenetration, -1f, pawn));

                if (props.stunDamageAmount > 0f)
                {
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Stun, props.stunDamageAmount, 0f, -1f, pawn));
                }

                MX_QHUtility.ApplyBleed(victim, props.bleedDamageAmount);
                MX_QHUtility.TryApplyOrRefreshHediff(victim, props.slowHediff, props.slowSeverity, props.slowDurationTicks);

                if (victim.Spawned && !victim.Destroyed && victim.MapHeld == map)
                {
                    GraphicsUtility.Fx(map, victim.Position, props.hitFx, 1f);
                    GraphicsUtility.Fleck(map, victim.Position, props.hitFleck, 0.72f);
                    GraphicsUtility.Fleck(map, victim.Position, props.hitBurstFleck, 0.64f);
                }

                TryBreakBrain(victim, props, map);
                affected++;
            }

            if (affected > 0)
            {
                EleganceUtility.NotifyDecayEvent(pawn);
            }

            EleganceUtility.AddElegance(pawn, props.eleganceGainOnCast + props.eleganceGainPerTarget * affected);
        }

        private void TryBreakBrain(Pawn victim, CompProperties_AbilityFlowerMandate_Chrysanthemum props, Map map)
        {
            if (victim == null || victim.Dead || Rand.Value >= Mathf.Clamp01(props.brainDestroyChance))
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
                GraphicsUtility.Fx(map, victim.Position, props.brainBreakFx, 1f);
            }
        }

        private void SpawnTrail()
        {
            CompProperties_AbilityFlowerMandate_Chrysanthemum props = Props;
            if (props == null || pawn == null || !pawn.Spawned || pawn.MapHeld == null)
            {
                return;
            }

            Map map = pawn.MapHeld;
            IntVec3 cell = job?.targetA.IsValid == true ? job.targetA.Cell : pawn.Position;
            if (!cell.IsValid || !cell.InBounds(map))
            {
                cell = pawn.Position;
            }

            ThingDef moteDef = props.curveMote.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(props.curveMote);
            if (moteDef == null)
            {
                return;
            }

            FleckDef lineFleck = props.curveLineFleck.NullOrEmpty() ? null : DefDatabase<FleckDef>.GetNamedSilentFail(props.curveLineFleck);
            if (lineFleck == null)
            {
                return;
            }

            FleckDef distortFleck = props.curveDistortFleck.NullOrEmpty() ? null : DefDatabase<FleckDef>.GetNamedSilentFail(props.curveDistortFleck);
            Mote_FlowerMandate_ChrysanthemumCurvedTrail mote = ThingMaker.MakeThing(moteDef) as Mote_FlowerMandate_ChrysanthemumCurvedTrail;
            if (mote == null)
            {
                return;
            }

            mote.Setup(
                new TargetInfo(pawn),
                new TargetInfo(cell, map),
                lineFleck,
                distortFleck,
                Mathf.Max(0.03f, props.radius * 0.022f),
                props.curveDistortWidth,
                props.curveDistortAlpha,
                props.curveWidth,
                props.curveDensity,
                props.curveWaveLen,
                props.curveAnimTicks,
                props.curveGrowTicks,
                props.curveAlpha,
                props.curveAfterLayers,
                props.curveAfterGap,
                props.curveAfterAlpha,
                props.curveMinSegs,
                props.curveMaxSegs,
                props.curveDistortStep);

            if (GraphicsUtility.Mote(map, cell, mote))
            {
                mote.exactPosition = cell.ToVector3Shifted();
            }
        }
    }
}

