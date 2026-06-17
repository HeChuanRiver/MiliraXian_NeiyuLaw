using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_SpringRegen : HediffCompProperties
    {
        /// <summary>
        /// Trigger interval in ticks. (60 ticks = 1 second)
        /// </summary>
        public int healIntervalTicks = 180;

        /// <summary>
        /// How much injury severity is healed each trigger.
        /// </summary>
        public float healAmountPerTrigger = 2.0f;

        /// <summary>
        /// Whether permanent injuries (e.g. scars) can be healed.
        /// </summary>
        public bool healPermanentInjuries = false;

        public HediffCompProperties_SpringRegen()
        {
            compClass = typeof(HediffComp_SpringRegen);
        }
    }

    /// <summary>
    /// ???
    /// Every short interval, heal a certain amount of HP from one injury.
    /// Default behavior: heal the currently most severe non-permanent injury.
    /// </summary>
    public class HediffComp_SpringRegen : HediffComp
    {
        public HediffCompProperties_SpringRegen Props => (HediffCompProperties_SpringRegen)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn == null || Pawn.Dead || Pawn.health == null)
            {
                return;
            }

            int interval = Props.healIntervalTicks > 0 ? Props.healIntervalTicks : 60;
            if (!Pawn.IsHashIntervalTick(interval))
            {
                return;
            }

            TryHealOneInjury();
        }

        private float TryHealOneInjury()
        {
            List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
            if (hediffs == null || hediffs.Count == 0)
            {
                return 0f;
            }

            Hediff_Injury target = null;
            float maxSeverity = 0f;

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_Injury injury = hediffs[i] as Hediff_Injury;
                if (injury == null)
                {
                    continue;
                }

                if (injury.Severity <= 0f)
                {
                    continue;
                }

                if (!Props.healPermanentInjuries && injury.IsPermanent())
                {
                    continue;
                }

                if (injury.Severity > maxSeverity)
                {
                    maxSeverity = injury.Severity;
                    target = injury;
                }
            }

            float healAmount = Props.healAmountPerTrigger;
            var scaler = parent.TryGetComp<MiliraXian.Characters.HediffComp_PawnResourceScaling>();
            if (scaler != null)
            {
                healAmount = scaler.HealAmount > 0f ? scaler.HealAmount : healAmount;
            }

            if (target == null || healAmount <= 0f)
            {
                return 0f;
            }

            float before = target.Severity;
            target.Heal(healAmount);
            float healed = before - target.Severity;
            return healed > 0f ? healed : 0f;
        }
    }
}
