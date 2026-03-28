using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilityDuanHun : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;
        public int postCastDelayTicks = 0;

        public float radius = 2.5f;

        public DamageDef damageDef = null;
        public float damageAmount = 16f;
        public float armorPenetration = 0.25f;

        public float stunDamageAmount = 8f;
        public float bleedDamageAmount = 5f;

        public HediffDef slowHediff;
        public float slowSeverity = 1f;
        public int slowDurationTicks = 1800;

        public float brainDestroyChance = 0.08f;

        public float eleganceGainOnCast = 8f;
        public float eleganceGainPerTarget = 2f;

        public string warmupCasterFx = "MX_QH_Effecter_DuanHunWarmupCaster";
        public string warmupTargetFx = "MX_QH_Effecter_DuanHunWarmupTarget";
        public string releaseCasterFx = "MX_QH_Effecter_DuanHunReleaseCaster";
        public string releaseTargetFx = "MX_QH_Effecter_DuanHunReleaseTarget";
        public string releaseFleck = "MX_QH_Fleck_DuanHunReleaseTarget";

        public string curveMote = "MX_QH_Mote_DuanHunCurvedTrail";
        public string curveLineFleck = "MX_QH_Fleck_DuanHunCurveLine";
        public string curveDistortFleck = "MX_QH_Fleck_DuanHunCurveDistortion";
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
        public string hitFx = "MX_QH_Effecter_DuanHunHit";
        public string hitFleck = "MX_QH_Fleck_DuanHunHit";
        public string hitBurstFleck = "MX_QH_Fleck_DuanHunHitExplosion";
        public string hitDistortFleck = "ExpandingDistortionRing";
        public string brainBreakFx = "MX_QH_Effecter_DuanHunBrainBreak";

        public CompProperties_AbilityDuanHun()
        {
            compClass = typeof(CompAbilityEffect_DuanHun);
        }
    }

    public class CompAbilityEffect_DuanHun : CompAbilityEffect
    {
        private new CompProperties_AbilityDuanHun Props
        {
            get { return (CompProperties_AbilityDuanHun)props; }
        }

        public override bool ShouldHideGizmo
        {
            get
            {
                return !MX_QHUtility.HasRequiredWeapon(parent != null ? parent.pawn : null, Props.requiredWeapon);
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (!MX_QHUtility.HasRequiredWeapon(parent != null ? parent.pawn : null, Props.requiredWeapon))
            {
                reason = "需要装备竹笛形态。";
                return true;
            }

            reason = null;
            return false;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return;
            }

            GenDraw.DrawRadiusRing(target.Cell, Props.radius, Color.yellow);
        }

        public override IEnumerable<PreCastAction> GetPreCastActions()
        {
            var warmupTicks = 0;
            if (parent?.def?.verbProperties != null)
            {
                warmupTicks = Mathf.Max(0, parent.def.verbProperties.warmupTime.SecondsToTicks());
            }
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

            var half = Mathf.Max(1, warmupTicks / 2);
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

        private void SpawnWarmupVisual(LocalTargetInfo target, float intensity)
        {
            var caster = parent?.pawn;
            if (caster == null || !caster.Spawned || caster.Map == null)
            {
                return;
            }

            var map = caster.Map;
            var cell = target.IsValid ? target.Cell : caster.Position;
            if (!cell.IsValid || !cell.InBounds(map))
            {
                cell = caster.Position;
            }

            GraphicsUtility.Fx(map, caster.Position, Props.warmupCasterFx, 1f);
            GraphicsUtility.Fx(map, cell, Props.warmupTargetFx, Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(intensity)));
        }
    }
}