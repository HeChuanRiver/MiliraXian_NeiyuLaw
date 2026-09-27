using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
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
