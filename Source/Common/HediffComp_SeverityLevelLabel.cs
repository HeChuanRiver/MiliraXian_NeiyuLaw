using UnityEngine;
using Verse;

namespace MiliraXian.Characters
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

    public class HediffComp_SeverityLevelLabel : HediffComp
    {
        public HediffCompProperties_SeverityLevelLabel Props => (HediffCompProperties_SeverityLevelLabel)props;

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
