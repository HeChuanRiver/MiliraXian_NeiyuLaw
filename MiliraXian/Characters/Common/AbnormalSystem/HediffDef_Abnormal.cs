using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.AbnormalSystem
{
    public class HediffDef_Abnormal : HediffDef
    {
        public float baseAccumulationLimit = 100f;
        public StatDef accumulationLimitFactorStat;
        public StatDef applicationEfficiencyFactorStat;
        public int ticksUntilDecayAfterRefresh;
        public float accumulationDecayPerTick;
        public ThingDef progressBarMoteDef;
        public DamageDef fullDamageDef;
        public float fullDamageAmount;
        public float fullDamageArmorPenetration;
        public HediffDef effectHediff;
        public float effectSeverity = 1f;
        public int effectDurationTicks = -1;
        public bool removeOnTriggered = true;
        public string triggerText;
        public Vector3 triggerTextOffset = Vector3.zero;
        public Color triggerTextColor = Color.white;
        public float triggerTextDuration = 1.2f;
        public ThingDef triggerMoteDef;
        public Vector3 triggerMoteOffset = new(0f, 0f, 0.85f);
        public float triggerMoteScale = 1f;

        // --- aggravation (RangedRework.md 3.4, owner 11-11): build-up while the effect is active
        /// <summary>
        /// While the effect is active, hits fill a second bar this share of the normal limit; full, the
        /// effect aggravates (settles what is left of it at once, <see cref="IAbnormalAggravation"/>).
        /// 0 = no aggravation (the hits still give their on-hit extras).
        /// </summary>
        public float aggravationLimitFactor = 0.5f;
        /// <summary>Floating text on aggravation (falls back to <see cref="triggerText"/>).</summary>
        public string aggravationText;
        /// <summary>Optional damage on aggravation: base + per second of the effect left (the shatter of a freeze).</summary>
        public DamageDef aggravationDamageDef;
        public float aggravationDamageBase;
        public float aggravationDamagePerSecond;
        public float aggravationArmorPenetration;

        public HediffDef_Abnormal()
        {
            hediffClass = typeof(Hediff_Abnormal);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (!typeof(Hediff_Abnormal).IsAssignableFrom(hediffClass))
            {
                yield return $"hediffClass {hediffClass} must inherit {nameof(Hediff_Abnormal)}";
            }

            if (baseAccumulationLimit <= 0f)
            {
                yield return "baseAccumulationLimit must be greater than zero";
            }

            if (accumulationLimitFactorStat == null)
            {
                yield return "accumulationLimitFactorStat is null";
            }

            if (applicationEfficiencyFactorStat == null)
            {
                yield return "applicationEfficiencyFactorStat is null";
            }
        }
    }
}
