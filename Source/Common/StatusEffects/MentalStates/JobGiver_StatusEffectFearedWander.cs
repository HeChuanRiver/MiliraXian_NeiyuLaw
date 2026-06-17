using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters
{
    public class JobGiver_StatusEffectFearedWander : JobGiver_Wander
    {
        public JobGiver_StatusEffectFearedWander()
        {
            wanderRadius = 6f;
            locomotionUrgency = LocomotionUrgency.Jog;
            ticksBetweenWandersRange = new IntRange(45, 90);
            maxDanger = Danger.Some;
            expiryInterval = 120;
        }

        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            MentalState_StatusEffectFeared mentalState = pawn.MentalState as MentalState_StatusEffectFeared;
            if (mentalState?.wanderRoot.IsValid == true)
            {
                return mentalState.wanderRoot;
            }

            return pawn.Position;
        }

        protected override void DecorateGotoJob(Verse.AI.Job job)
        {
            base.DecorateGotoJob(job);
            job.checkOverrideOnExpire = true;
        }
    }
}
