using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public class CompProperties_AbilityMingyuanAbsorb : CompProperties_AbilityEffect
    {
        public float radius = 10f;
        public float arcDegrees = 108f;
        public float damagePerLayer = 0.3f;
        public CompProperties_AbilityMingyuanAbsorb() => compClass = typeof(CompAbilityEffect_MingyuanAbsorb);
    }

    public class CompAbilityEffect_MingyuanAbsorb : CompAbilityEffect_MingyuanPowerLimited
    {
        private CompProperties_AbilityMingyuanAbsorb PropsAbsorb => (CompProperties_AbilityMingyuanAbsorb)props;

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (parent.pawn?.Spawned != true) return;
            Vector3 direction = (target.CenterVector3 - parent.pawn.DrawPos).Yto0().normalized;
            if (direction.sqrMagnitude > 0.001f)
                MingyuanBowVisualDrawer.DrawSectorWarning(parent.pawn.DrawPos, direction, PropsAbsorb.radius, PropsAbsorb.arcDegrees, 0.8f);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
            => base.Valid(target, throwMessages) && target.Cell != parent.pawn.Position;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (MingyuanPowerBalance.Sealed) return;
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Spawned != true || caster.Map == null) return;
            Vector3 origin = caster.Position.ToVector3Shifted();
            Vector3 direction = (target.CenterVector3 - origin).Yto0().normalized;
            if (direction.sqrMagnitude < 0.001f) return;
            float minimumDot = Mathf.Cos(PropsAbsorb.arcDegrees * 0.5f * Mathf.Deg2Rad);
            List<Thing> targets = CombatTargetSnapshot.Rent(GenRadial.RadialDistinctThingsAround(caster.Position, caster.Map, PropsAbsorb.radius, true));
            int visualCount = 0;
            try
            {
                foreach (Thing thing in targets)
                {
                    if (!MingyuanUtility.IsHostilePawn(thing, caster, out Pawn victim) || !victim.Spawned || victim.Map != caster.Map) continue;
                    Vector3 offset = (victim.Position.ToVector3Shifted() - origin).Yto0();
                    if (offset.sqrMagnitude <= 0.001f || offset.sqrMagnitude > PropsAbsorb.radius * PropsAbsorb.radius
                        || Vector3.Dot(direction, offset.normalized) + 0.0001f < minimumDot) continue;
                    float layers = 0f;
                    bool hadTimeBurn = MingyuanUtility.HasHediff(victim, MingyuanUtility.TimeBurnFrozenDef);
                    var hediffs = victim.health.hediffSet.hediffs;
                    // Remove every matching instance before damage can trigger death,
                    // transfer, or a queued Time Burn execution.
                    for (int i = hediffs.Count - 1; i >= 0; i--)
                        if (hediffs[i].def == MingyuanUtility.LifeBurnDef)
                        {
                            layers += Mathf.Max(0f, hediffs[i].Severity);
                            victim.health.RemoveHediff(hediffs[i]);
                        }
                    bool cancelled = Current.Game?.GetComponent<GameComponent_MingyuanTimeBurn>()?.Cancel(victim) == true;
                    if (layers <= 0f && !hadTimeBurn && !cancelled) continue;
                    if (visualCount++ < 12)
                        MingyuanSkillVfx.Play(caster.Map, victim.DrawPos, MingyuanSkillVisualKind.Absorb, 1f, caster.DrawPos);
                    MingyuanUtility.ApplyTrueDamage(victim, DamageDefOf.Burn, layers * PropsAbsorb.damagePerLayer, caster);
                }
            }
            finally { CombatTargetSnapshot.Return(targets); }
        }
    }
}
