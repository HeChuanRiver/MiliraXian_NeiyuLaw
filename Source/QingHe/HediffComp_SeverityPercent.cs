using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_SeverityPercent : HediffCompProperties
    {
        public HediffCompProperties_SeverityPercent()
        {
            compClass = typeof(HediffComp_SeverityPercent);
        }
    }

    public class HediffComp_SeverityPercent : HediffComp
    {
        public override string CompLabelInBracketsExtra => parent.Severity.ToStringPercent("F0");
    }
}
