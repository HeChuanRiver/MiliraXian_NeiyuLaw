using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_DuanHun : JobDriver_CastAbility
    {
        private CompProperties_AbilityDuanHun Props
        {
            get
            {
                var ability = job?.ability;
                if (ability?.def?.comps == null)
                {
                    return null;
                }

                for (var i = 0; i < ability.def.comps.Count; i++)
                {
                    var p = ability.def.comps[i] as CompProperties_AbilityDuanHun;
                    if (p != null)
                    {
                        return p;
                    }
                }

                return null;
            }
        }

        public override string GetReport()
        {
            return "MX_QH_ReportDuanHun".Translate().ToString();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (var toil in base.MakeNewToils())
            {
                yield return toil;
            }

            var props = Props;
            var delay = props?.postCastDelayTicks ?? 0;
            if (delay > 0)
            {
                var wait = ToilMaker.MakeToil("QHEleganceDuanHun_PostCastDelay");
                wait.defaultCompleteMode = ToilCompleteMode.Delay;
                wait.defaultDuration = delay;
                yield return wait;
            }

            var pulse = ToilMaker.MakeToil("QHEleganceDuanHun_ResolvePulse");
            pulse.initAction = delegate
            {
                var p = Props;
                if (p == null || !MX_QHUtility.HasRequiredWeapon(pawn, p.requiredWeapon) || pawn?.MapHeld == null)
                {
                    return;
                }

                var map = pawn.MapHeld;
                var center = job?.targetA.IsValid == true ? job.targetA.Cell : pawn.Position;
                if (!center.IsValid || !center.InBounds(map))
                {
                    center = pawn.Position;
                }

                var damageFactor = EleganceUtility.FactorLinear(p.damageFactorMax, pawn);
                var slowDuration = Mathf.Max(1, Mathf.RoundToInt(p.slowDurationTicks * EleganceUtility.FactorLinear(p.slowDurationFactorMax, pawn)));
                GraphicsUtility.Fx(map, pawn.Position, p.releaseCasterFx, 1f);
                GraphicsUtility.Fx(map, center, p.releaseTargetFx, 1f);
                GraphicsUtility.Fleck(map, center, p.releaseFleck, Mathf.Max(0.85f, p.radius * 0.65f));
                GraphicsUtility.Fleck(map, center, p.releaseImpactFleck, Mathf.Max(0.45f, p.releaseImpactScale));

                var damageDef = p.damageDef ?? MX_QHDefOf.MX_Desynced ?? DamageDefOf.Cut;
                var victims = RadialUtility.CollectHostilePawns(map, center, pawn, p.radius);
                var affected = 0;

                for (var i = 0; i < victims.Count; i++)
                {
                    var victim = victims[i];
                    victim.TakeDamage(new DamageInfo(damageDef, p.damageAmount * damageFactor, p.armorPenetration, -1f, pawn));

                    if (p.stunDamageAmount > 0f)
                    {
                        victim.TakeDamage(new DamageInfo(DamageDefOf.Stun, p.stunDamageAmount, 0f, -1f, pawn));
                    }

                    MX_QHUtility.ApplyBleed(victim, p.bleedDamageAmount);
                    MX_QHUtility.TryApplyOrRefreshHediff(victim, p.slowHediff, p.slowSeverity, slowDuration);

                    if (victim.Spawned && !victim.Destroyed && victim.MapHeld == map)
                    {
                        GraphicsUtility.Fx(map, victim.Position, p.hitFx, 1f);
                        GraphicsUtility.Fleck(map, victim.Position, p.hitFleck, 0.72f);
                        GraphicsUtility.Fleck(map, victim.Position, p.hitBurstFleck, 0.64f);
                    }

                    if (!victim.Dead && Rand.Value < Mathf.Clamp01(p.brainDestroyChance))
                    {
                        BodyPartRecord brain = null;
                        foreach (var part in victim.health.hediffSet.GetNotMissingParts())
                        {
                            if (part.def == DefDatabase<BodyPartDef>.GetNamedSilentFail("Brain"))
                            {
                                brain = part;
                                break;
                            }
                        }

                        if (brain != null)
                        {
                            victim.health.AddHediff(HediffDefOf.MissingBodyPart, brain);
                            if (victim.Spawned && !victim.Destroyed && victim.MapHeld == map)
                            {
                                GraphicsUtility.Fx(map, victim.Position, p.brainBreakFx, 1f);
                            }
                        }
                    }

                    affected++;
                }

                if (affected > 0)
                {
                    EleganceUtility.NotifyDecayEvent(pawn);
                }

                EleganceUtility.AddElegance(pawn, p.eleganceGainOnCast + p.eleganceGainPerTarget * affected);
            };
            pulse.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pulse;

            var animTicks = props != null ? Mathf.Max(1, props.curveAnimTicks) : 1;
            var trail = ToilMaker.MakeToil("QHEleganceDuanHun_ReleaseTrailAnim");
            trail.defaultCompleteMode = ToilCompleteMode.Delay;
            trail.defaultDuration = animTicks;
            trail.initAction = delegate
            {
                var p = Props;
                if (p == null || pawn == null || !pawn.Spawned || pawn.MapHeld == null)
                {
                    return;
                }

                var map = pawn.MapHeld;
                var cell = job?.targetA.IsValid == true ? job.targetA.Cell : pawn.Position;
                if (!cell.IsValid || !cell.InBounds(map))
                {
                    cell = pawn.Position;
                }

                var moteDef = p.curveMote.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(p.curveMote);
                if (moteDef == null)
                {
                    moteDef = MX_QHDefOf.MX_QH_Mote_DuanHunCurvedTrail;
                }
                if (moteDef == null)
                {
                    return;
                }

                var lineFleck = p.curveLineFleck.NullOrEmpty() ? null : DefDatabase<FleckDef>.GetNamedSilentFail(p.curveLineFleck);
                if (lineFleck == null)
                {
                    return;
                }

                var distortFleck = p.curveDistortFleck.NullOrEmpty() ? null : DefDatabase<FleckDef>.GetNamedSilentFail(p.curveDistortFleck);
                var mote = ThingMaker.MakeThing(moteDef) as Mote_QHCurvedDistortionTrail;
                if (mote == null)
                {
                    return;
                }

                mote.Setup(
                    new TargetInfo(pawn),
                    new TargetInfo(cell, map),
                    lineFleck,
                    distortFleck,
                    Mathf.Max(0.03f, p.radius * 0.022f),
                    p.curveDistortWidth,
                    p.curveDistortAlpha,
                    p.curveWidth,
                    p.curveDensity,
                    p.curveWaveLen,
                    p.curveAnimTicks,
                    p.curveGrowTicks,
                    p.curveAlpha,
                    p.curveAfterLayers,
                    p.curveAfterGap,
                    p.curveAfterAlpha,
                    p.curveMinSegs,
                    p.curveMaxSegs,
                    p.curveDistortStep);

                if (!GraphicsUtility.Mote(map, cell, mote))
                {
                    return;
                }

                mote.exactPosition = cell.ToVector3Shifted();
            };
            yield return trail;
        }
    }
}
