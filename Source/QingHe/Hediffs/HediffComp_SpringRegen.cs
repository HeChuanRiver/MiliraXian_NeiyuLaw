using System.Collections.Generic;
using UnityEngine;
using RimWorld;
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

        /// <summary>
        /// Whether missing body parts can be restored after injuries are healed.
        /// </summary>
        public bool restoreMissingParts = false;

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

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            if (Pawn == null || Pawn.Dead || Pawn.health == null)
            {
                return;
            }

            int interval = Props.healIntervalTicks > 0 ? Props.healIntervalTicks : 60;
            if (!Pawn.IsHashIntervalTick(interval, delta))
            {
                return;
            }

            float healed = TryHealOneInjury();
            if (healed <= 0f && Props.restoreMissingParts)
            {
                TryRestoreOneMissingPart();
            }
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

            if (target == null || Props.healAmountPerTrigger <= 0f)
            {
                return 0f;
            }

            float before = target.Severity;
            target.Heal(Props.healAmountPerTrigger * Mathf.Max(0f, parent.Severity));
            float healed = before - target.Severity;
            return healed > 0f ? healed : 0f;
        }

        private bool TryRestoreOneMissingPart()
        {
            List<Hediff_MissingPart> missingParts = Pawn.health.hediffSet.GetMissingPartsCommonAncestors();
            if (missingParts == null || missingParts.Count == 0)
            {
                return false;
            }

            Hediff_MissingPart target = null;
            for (int i = 0; i < missingParts.Count; i++)
            {
                Hediff_MissingPart missingPart = missingParts[i];
                if (missingPart?.Part == null)
                {
                    continue;
                }

                target = missingPart;
                break;
            }

            if (target == null)
            {
                return false;
            }

            Pawn.health.RestorePart(target.Part);
            return true;
        }
    }
}
