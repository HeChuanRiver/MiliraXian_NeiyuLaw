using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilityHengZhi : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;
        public int postCastDelayTicks = 0;

        public float radius = 3.9f;
        public DamageDef damageDef = null;
        public float damageAmount = 24f;
        public float armorPenetration = 0.55f;
        public float knockbackDistance = 4f;
        public float bluntDamageAmount = 10f;
        public float bluntArmorPenetration = 0.15f;
        public float damageFactorMax = 1f;
        public float knockbackFactorMax = 0.5f;

        public float eleganceGainPerTarget = 3f;
        public float eleganceGainMax = 24f;

        public string warmupFx = "MX_QH_Effecter_HengZhiWarmup";
        public string warmupFleck = "MX_QH_Fleck_HengZhiWarmup";
        public string releaseFx = "MX_QH_Effecter_HengZhiRelease";
        public string releaseFleck = "MX_QH_Fleck_HengZhiRelease";
        public string hitFx = "MX_QH_Effecter_HengZhiHit";
        public string hitFleck = "MX_QH_Fleck_HengZhiHit";

        public CompProperties_AbilityHengZhi()
        {
            compClass = typeof(CompAbilityEffect_HengZhi);
        }
    }

    public class CompAbilityEffect_HengZhi : CompAbilityEffect
    {
        private new CompProperties_AbilityHengZhi Props
        {
            get { return (CompProperties_AbilityHengZhi)props; }
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
                reason = "需要装备琵琶形态。";
                return true;
            }

            reason = null;
            return false;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            var caster = parent?.pawn;
            if (caster == null || !caster.Spawned)
            {
                return;
            }

            GenDraw.DrawRadiusRing(caster.Position, Props.radius, Color.cyan);
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
                    SpawnWarmupVisual(1f);
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
                        SpawnWarmupVisual(0.85f);
                    }
                };
            }
        }

        private void SpawnWarmupVisual(float intensity)
        {
            var caster = parent?.pawn;
            if (caster == null || !caster.Spawned || caster.Map == null)
            {
                return;
            }

            var scale = Mathf.Max(0.35f, Props.radius * 0.28f * Mathf.Max(0.1f, intensity));
            GraphicsUtility.Fx(caster.Map, caster.Position, Props.warmupFx, 1f);
            GraphicsUtility.Fleck(caster.Map, caster.Position, Props.warmupFleck, scale);
        }
    }
}