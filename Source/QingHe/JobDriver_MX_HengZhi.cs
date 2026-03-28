using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_HengZhi : JobDriver_CastAbility
    {
        private CompProperties_AbilityHengZhi Props
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
                    var p = ability.def.comps[i] as CompProperties_AbilityHengZhi;
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
            return "正在引导横指冲击";
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (var toil in base.MakeNewToils())
            {
                yield return toil;
            }

            var delay = Props?.postCastDelayTicks ?? 0;
            if (delay > 0)
            {
                var wait = ToilMaker.MakeToil("QHEleganceHengZhi_PostCastDelay");
                wait.defaultCompleteMode = ToilCompleteMode.Delay;
                wait.defaultDuration = delay;
                yield return wait;
            }

            var pulse = ToilMaker.MakeToil("QHEleganceHengZhi_ResolvePulse");
            pulse.initAction = delegate
            {
                var p = Props;
                if (p == null || !MX_QHUtility.HasRequiredWeapon(pawn, p.requiredWeapon) || pawn?.MapHeld == null)
                {
                    return;
                }

                var map = pawn.MapHeld;
                GraphicsUtility.Fx(map, pawn.Position, p.releaseFx, 1f);
                GraphicsUtility.Fleck(map, pawn.Position, p.releaseFleck, Mathf.Max(0.75f, p.radius * 0.42f));

                var damageDef = p.damageDef ?? MX_QHDefOf.MX_Dehydrate ?? DamageDefOf.Blunt;
                var victims = RadialUtility.CollectHostilePawns(map, pawn.Position, pawn, p.radius);
                var hitCount = 0;

                for (var i = 0; i < victims.Count; i++)
                {
                    var victim = victims[i];
                    victim.TakeDamage(new DamageInfo(damageDef, p.damageAmount, p.armorPenetration, -1f, pawn));

                    if (p.bluntDamageAmount > 0f)
                    {
                        victim.TakeDamage(new DamageInfo(MX_QHDefOf.MX_Desynced ?? DamageDefOf.Blunt, p.bluntDamageAmount, p.bluntArmorPenetration, -1f, pawn));
                    }

                    var cell = victim.Position;
                    if (cell.IsValid && cell.InBounds(map))
                    {
                        GraphicsUtility.Fx(map, cell, p.hitFx, 1f);
                        GraphicsUtility.Fleck(map, cell, p.hitFleck, 0.62f);
                    }

                    MX_QHUtility.TryKnockback(victim, pawn.Position, p.knockbackDistance);
                    hitCount++;
                }

                if (hitCount <= 0)
                {
                    return;
                }

                EleganceUtility.NotifyDecayEvent(pawn);
                EleganceUtility.AddElegance(pawn, Mathf.Min(p.eleganceGainMax, p.eleganceGainPerTarget * hitCount));
            };
            pulse.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pulse;
        }
    }
}