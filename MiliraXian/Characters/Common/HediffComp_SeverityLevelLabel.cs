using UnityEngine;
using Verse;
using MiliraXian.Characters.QingHe;

namespace MiliraXian.Characters.Common
{
    public class HediffCompProperties_SeverityLevelLabel : HediffCompProperties
    {
        public bool showMaxSeverity = true;
        public bool useQingheEffectiveMax;

        public HediffCompProperties_SeverityLevelLabel()
        {
            compClass = typeof(HediffComp_SeverityLevelLabel);
        }
    }

    /// <summary>
    /// Carrier for a level expressed as severity, where level 0 is a valid state (not yet advanced,
    /// or sealed down to 0). Vanilla removes a hediff once severity reaches zero, so without this
    /// the carrier gets deleted and recreated instead of simply resting at level 0.
    /// </summary>
    public class Hediff_SeverityLevel : HediffWithComps
    {
        public override bool ShouldRemove => false;
    }

    public class HediffComp_SeverityLevelLabel : HediffComp
    {
        public HediffCompProperties_SeverityLevelLabel Props => (HediffCompProperties_SeverityLevelLabel)props;

        /// <summary>
        /// Level 0 means nothing has been earned yet, so the carrier stays but shows nothing.
        /// </summary>
        public override bool CompDisallowVisible()
        {
            return Mathf.RoundToInt(parent?.Severity ?? 0f) <= 0;
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                int level = Mathf.Max(0, Mathf.RoundToInt(parent?.Severity ?? 0f));
                if (!Props.showMaxSeverity)
                {
                    return level.ToString();
                }

                int maxLevel = Props.useQingheEffectiveMax
                    ? MiliraXian.Characters.QingHe.QinghePowerBalance.MaxEffectiveLevel
                    : Mathf.Max(1, Mathf.RoundToInt(parent?.def?.maxSeverity ?? 1f));
                return level + "/" + maxLevel;
            }
        }
    }
}
