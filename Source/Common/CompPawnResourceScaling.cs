using Verse;

namespace MiliraXian.Characters
{
    public class CompProperties_PawnResourceScaling : CompProperties
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

        public CompProperties_PawnResourceScaling()
        {
            compClass = typeof(CompPawnResourceScaling);
        }
    }

    public class CompPawnResourceScaling : ThingComp
    {
        public CompProperties_PawnResourceScaling Props
            => (CompProperties_PawnResourceScaling)props;

        private Pawn ownerPawn;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            ownerPawn = parent as Pawn;
        }

        public float DamageAmount => Props.damageAmount?.GetValue(ownerPawn) ?? 0f;
        public float ArmorPenetration => Props.armorPenetration?.GetValue(ownerPawn) ?? 0f;
        public float Radius => Props.radius?.GetValue(ownerPawn) ?? 0f;
        public float KnockbackDistance => Props.knockbackDistance?.GetValue(ownerPawn) ?? 0f;
        public float DurationTicks => Props.durationTicks?.GetValue(ownerPawn) ?? 0f;
        public float StunDurationTicks => Props.stunDurationTicks?.GetValue(ownerPawn) ?? 0f;
        public float CooldownTicks => Props.cooldownTicks?.GetValue(ownerPawn) ?? 0f;
        public float HealAmount => Props.healAmount?.GetValue(ownerPawn) ?? 0f;
        public float ShieldValue => Props.shieldValue?.GetValue(ownerPawn) ?? 0f;
        public float MaxEnergy => Props.maxEnergy?.GetValue(ownerPawn) ?? 0f;
        public float DamagePerShieldPoint => Props.damagePerShieldPoint?.GetValue(ownerPawn) ?? 0f;
        public float RegenPerSecond => Props.regenPerSecond?.GetValue(ownerPawn) ?? 0f;
        public float SlowSeverity => Props.slowSeverity?.GetValue(ownerPawn) ?? 0f;
        public float BleedSeverity => Props.bleedSeverity?.GetValue(ownerPawn) ?? 0f;
        public float ResourceGain => Props.resourceGain?.GetValue(ownerPawn) ?? 0f;
        public float ResourceCost => Props.resourceCost?.GetValue(ownerPawn) ?? 0f;
        public float SeverityPerPulse => Props.severityPerPulse?.GetValue(ownerPawn) ?? 0f;
        public float HediffSeverityFactor => Props.hediffSeverityFactor?.GetValue(ownerPawn) ?? 0f;
    }
}
