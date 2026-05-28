using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class Hediff_AccumulationResistance : HediffWithComps
    {
        private HediffComp_AccumulationResistance ResistanceComp => GetComp<HediffComp_AccumulationResistance>();

        public int CurrentStage => ResistanceComp?.CurrentStage ?? Mathf.Max(1, Mathf.RoundToInt(Severity));

        public float AccumulationMultiplier => ResistanceComp?.AccumulationMultiplier ?? 1f;

        public override string LabelInBrackets
        {
            get
            {
                string stageLabel = $"{CurrentStage}/{(ResistanceComp?.MaxStage ?? CurrentStage)}";
                string baseLabel = base.LabelInBrackets;
                if (baseLabel.NullOrEmpty())
                {
                    return stageLabel;
                }

                return $"{baseLabel}, {stageLabel}";
            }
        }

        public override void Tick()
        {
            base.Tick();
        }
    }

}
