using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.MentalStates
{
    public class JobGiver_FlowerBellFearedWander : JobGiver_Wander
    {
        public JobGiver_FlowerBellFearedWander()
        {
            wanderRadius = 6f;
            locomotionUrgency = LocomotionUrgency.Jog;
            ticksBetweenWandersRange = new IntRange(45, 90);
            maxDanger = Danger.Some;
            expiryInterval = 120;
        }

        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            MentalState_FlowerBellFeared mentalState = pawn.MentalState as MentalState_FlowerBellFeared;
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
