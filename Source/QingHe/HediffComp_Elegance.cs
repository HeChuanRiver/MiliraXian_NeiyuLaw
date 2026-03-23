using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_Elegance : HediffCompProperties_PawnSpecialResource
    {
        public HediffCompProperties_Elegance()
        {
            compClass = typeof(HediffComp_Elegance);
        }
    }

    public class HediffComp_Elegance : HediffComp_PawnSpecialResource
    {
        private const int EngageCombatWindowTicks = 360;

        public HediffCompProperties_Elegance Props => (HediffCompProperties_Elegance)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = parent?.pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                return;
            }

            if (IsInPreciseCombat(pawn))
            {
                AddValue(0.02f);
            }
            else
            {
                AddValue(-0.03f);
            }
        }

        private bool IsInPreciseCombat(Pawn pawn)
        {
            if (pawn.InAggroMentalState)
            {
                return true;
            }

            Pawn_MindState mindState = pawn.mindState;
            Thing enemyTarget = mindState?.enemyTarget;
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            bool forcedNormalSpeed = Find.TickManager?.slower?.ForcedNormalSpeed ?? false;

            if (enemyTarget != null && !enemyTarget.Destroyed && enemyTarget.Spawned)
            {
                bool hostile = pawn.HostileTo(enemyTarget);
                bool recentlyEngaged = currentTick - mindState.lastEngageTargetTick <= EngageCombatWindowTicks;
                bool targetValid = !(enemyTarget is Pawn enemyPawn) || !enemyPawn.Downed;
                if (hostile && targetValid && (recentlyEngaged || forcedNormalSpeed))
                {
                    return true;
                }
            }

            // Fallback: hostile nearby in same room/region.
            return GenAI.InDangerousCombat(pawn);
        }
    }
}
