using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters
{
    public class MentalState_AbnormalFeared : MentalState
    {
        public IntVec3 wanderRoot = IntVec3.Invalid;

        public override void PreStart()
        {
            base.PreStart();
            wanderRoot = pawn?.Position ?? IntVec3.Invalid;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref wanderRoot, "wanderRoot");
        }

        public override RandomSocialMode SocialModeMax()
        {
            return RandomSocialMode.Off;
        }
    }
}
