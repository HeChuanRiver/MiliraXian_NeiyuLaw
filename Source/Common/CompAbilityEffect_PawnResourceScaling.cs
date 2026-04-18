using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class CompProperties_AbilityPawnResourceScaling : CompProperties_AbilityEffect
    {
        public ScaledValue damageAmount;
        public ScaledValue armorPenetration;
        public ScaledValue radius;
        public ScaledValue knockbackDistance;
        public ScaledValue durationTicks;
        public ScaledValue stunDurationTicks;
        public ScaledValue cooldownTicks;
        public ScaledValue healAmount;
        public ScaledValue shieldValue;
        public ScaledValue maxEnergy;
        public ScaledValue damagePerShieldPoint;
        public ScaledValue regenPerSecond;
        public ScaledValue slowSeverity;
        public ScaledValue bleedSeverity;
        public ScaledValue resourceGain;
        public ScaledValue resourceCost;
        public ScaledValue severityPerPulse;
        public ScaledValue hediffSeverityFactor;

        public CompProperties_AbilityPawnResourceScaling()
        {
            compClass = typeof(CompAbilityEffect_PawnResourceScaling);
        }
    }

    public class CompAbilityEffect_PawnResourceScaling : CompAbilityEffect
    {
        public new CompProperties_AbilityPawnResourceScaling Props
            => (CompProperties_AbilityPawnResourceScaling)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
        }

        public float DamageAmount => Props.damageAmount?.GetValue(parent.pawn) ?? 0f;
        public float ArmorPenetration => Props.armorPenetration?.GetValue(parent.pawn) ?? 0f;
        public float Radius => Props.radius?.GetValue(parent.pawn) ?? 0f;
        public float KnockbackDistance => Props.knockbackDistance?.GetValue(parent.pawn) ?? 0f;
        public float DurationTicks => Props.durationTicks?.GetValue(parent.pawn) ?? 0f;
        public float StunDurationTicks => Props.stunDurationTicks?.GetValue(parent.pawn) ?? 0f;
        public float CooldownTicks => Props.cooldownTicks?.GetValue(parent.pawn) ?? 0f;
        public float HealAmount => Props.healAmount?.GetValue(parent.pawn) ?? 0f;
        public float ShieldValue => Props.shieldValue?.GetValue(parent.pawn) ?? 0f;
        public float MaxEnergy => Props.maxEnergy?.GetValue(parent.pawn) ?? 0f;
        public float DamagePerShieldPoint => Props.damagePerShieldPoint?.GetValue(parent.pawn) ?? 0f;
        public float RegenPerSecond => Props.regenPerSecond?.GetValue(parent.pawn) ?? 0f;
        public float SlowSeverity => Props.slowSeverity?.GetValue(parent.pawn) ?? 0f;
        public float BleedSeverity => Props.bleedSeverity?.GetValue(parent.pawn) ?? 0f;
        public float ResourceGain => Props.resourceGain?.GetValue(parent.pawn) ?? 0f;
        public float ResourceCost => Props.resourceCost?.GetValue(parent.pawn) ?? 0f;
        public float SeverityPerPulse => Props.severityPerPulse?.GetValue(parent.pawn) ?? 0f;
        public float HediffSeverityFactor => Props.hediffSeverityFactor?.GetValue(parent.pawn) ?? 0f;
    }
}
