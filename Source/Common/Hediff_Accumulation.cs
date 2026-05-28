using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class Hediff_Accumulation : HediffWithComps, IAccumulationHediff
    {
        private HediffComp_Accumulation Comp => GetComp<HediffComp_Accumulation>();

        public Pawn Caster => Comp?.Caster;

        public HediffDef Def => def;

        public bool CanAccumulate => Comp?.CanAccumulate ?? false;

        public override string LabelInBrackets
        {
            get
            {
                HediffComp_Accumulation comp = Comp;
                string baseLabel = base.LabelInBrackets;
                if (comp == null || !comp.ShowSeverityPercent)
                {
                    return baseLabel;
                }

                string percent = comp.Progress.ToStringPercent("F0");
                if (baseLabel.NullOrEmpty())
                {
                    return percent;
                }

                return $"{baseLabel}, {percent}";
            }
        }

        public override bool ShouldRemove => base.ShouldRemove || (Comp?.ShouldRemoveAtZero == true && Severity <= 0f);

        public override void Tick()
        {
            base.Tick();
            Comp?.TickStatusAccumulation();
        }

        public void AddAccumulation(Pawn newCaster, float severityOffset)
        {
            Comp?.AddAccumulation(newCaster, severityOffset);
        }
    }
}
