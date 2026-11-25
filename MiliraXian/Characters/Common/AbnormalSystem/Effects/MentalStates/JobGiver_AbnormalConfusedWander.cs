using RimWorld;
using Verse;
using Verse.AI;
using MiliraXian.Characters.Common.AbnormalSystem;

namespace MiliraXian.Characters.Common.AbnormalSystem.Effects.MentalStates
{
    public class JobGiver_AbnormalConfusedWander : JobGiver_Wander
    {
        public JobGiver_AbnormalConfusedWander()
        {
            wanderRadius = 6f;
            locomotionUrgency = LocomotionUrgency.Jog;
            ticksBetweenWandersRange = new IntRange(45, 90);
            maxDanger = Danger.Some;
            expiryInterval = 120;
        }

        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            MentalState_AbnormalConfused mentalState = pawn.MentalState as MentalState_AbnormalConfused;
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
