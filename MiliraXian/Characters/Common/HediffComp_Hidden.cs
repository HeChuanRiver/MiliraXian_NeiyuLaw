using Verse;

namespace MiliraXian.Characters.Common
{
    public class HediffCompProperties_Hidden : HediffCompProperties
    {
        public HediffCompProperties_Hidden()
        {
            compClass = typeof(HediffComp_Hidden);
        }
    }

    /// <summary>
    /// Marker for carrier hediffs that should never surface on the health tab. Needed when no
    /// functional comp on the hediff already disallows visibility (e.g. vanilla Disappears does not).
    /// </summary>
    public class HediffComp_Hidden : HediffComp
    {
        public override bool CompDisallowVisible()
        {
            return true;
        }
    }
}
