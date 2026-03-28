using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_Tempest : HediffCompProperties_PawnSpecialResource
    {
        public int recoverTimerMaxTicks = 360;

        public HediffCompProperties_Tempest()
        {
            compClass = typeof(HediffComp_Tempest);
        }
    }
    
    public class HediffComp_Tempest : HediffComp_PawnSpecialResource
    {
        private const float GainPerTick = 0.01f;
        private const float LossPerTick = -0.02f;

        private int recoverTimer;

        public HediffCompProperties_Tempest Props => (HediffCompProperties_Tempest)props;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref recoverTimer, "recoverTimer", 0);
        }

        public void NotifyRecoverEvent()
        {
            recoverTimer = Mathf.Max(0, Props?.recoverTimerMaxTicks ?? 0);
        }
        
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            var pawn = parent?.pawn;
            if (pawn == null || pawn.Dead)
            {
                return;
            }

            var threshold = EleganceUtility.GetTempestRecoverThreshold(pawn);
            var delta = EleganceUtility.GetPercent(pawn) > threshold ? GainPerTick : LossPerTick;

            if (recoverTimer > 0)
            {
                recoverTimer--;
                if (delta < 0f)
                {
                    delta = 0f;
                }
            }

            AddValue(delta);
        }
    }
}