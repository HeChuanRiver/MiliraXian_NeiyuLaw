using Verse;

namespace MiliraXian.Characters
{
    public class Hediff_AbnormalPlaceholderEffect : HediffWithComps
    {
        public override void Notify_Downed()
        {
            base.Notify_Downed();
            GetComp<HediffComp_AbnormalFeared>()?.NotifyPawnDowned();
            GetComp<HediffComp_AbnormalOverloaded>()?.NotifyPawnDowned();
        }
    }
}
