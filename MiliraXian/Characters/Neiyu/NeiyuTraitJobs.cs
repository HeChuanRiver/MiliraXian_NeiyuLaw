using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Neiyu
{
    public class JobGiver_NeiyuExtraNutrition : JobGiver_GetFood
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.IsColonistPlayerControlled || pawn.Drafted || pawn.Downed || pawn.InMentalState
                || pawn.needs?.food == null || pawn.needs.food.CurLevelPercentage < 0.95f
                || Hediff_NeiyuSoulRemnant.Get(pawn) is not { AutomaticNutritionWanted: > 0.0001f } soul) return null;
            Job job = base.TryGiveJob(pawn);
            if (job?.def != JobDefOf.Ingest) return job;
            Thing food = job.targetA.Thing;
            ThingDef foodDef = food is Building_NutrientPasteDispenser ? ThingDefOf.MealNutrientPaste : food.def;
            float nutrition = FoodUtility.GetNutrition(pawn, food, foodDef);
            if (nutrition <= 0f) { JobMaker.ReturnToPool(job); return null; }
            job.def = NeiyuTraitDefOf.MX_Neiyu_ExtraNutrition;
            job.count = Mathf.Max(1, Mathf.CeilToInt((pawn.needs.food.NutritionWanted + soul.AutomaticNutritionWanted) / nutrition));
            if (foodDef.ingestible.maxNumToIngestAtOnce > 0)
                job.count = Mathf.Min(job.count, foodDef.ingestible.maxNumToIngestAtOnce);
            return job;
        }
    }

    public class JobGiver_NeiyuIdleSleep : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.IsColonistPlayerControlled || pawn.Drafted || pawn.Downed || pawn.InMentalState
                || pawn.story?.traits?.HasTrait(NeiyuTraitDefOf.MX_Neiyu_IdleSleeper) != true
                || RestUtility.DisturbancePreventsLyingDown(pawn)) return null;
            Building_Bed bed = RestUtility.FindBedFor(pawn);
            if (bed == null) return null;
            Job job = JobMaker.MakeJob(NeiyuTraitDefOf.MX_Neiyu_IdleSleep, bed);
            job.forceSleep = true;
            return job;
        }
    }

    [HarmonyPatch(typeof(ThinkNode_ConditionalMustKeepLyingDown), "Satisfied")]
    internal static class Patch_NeiyuIdleSleepCanWork
    {
        private static void Postfix(Pawn pawn, ref bool __result)
        {
            // The normal LayDown driver already rechecks jobs. This condition otherwise
            // traps an asleep pawn before the main work/needs tree, even at full rest.
            if (!pawn.Downed && pawn.CurJobDef == NeiyuTraitDefOf.MX_Neiyu_IdleSleep) __result = false;
        }
    }

    public class JobGiver_NeiyuSoulRecovery : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) =>
            NeiyuSoulRecoveryUtility.IsRecovering(pawn) ? NeiyuSoulRecoveryUtility.MakeRecoveryJob(pawn) : null;
    }

    public class JobDriver_NeiyuSoulRecovery : JobDriver_LayDown
    {
        public override bool LookForOtherJobs => false;
        public override bool PlayerInterruptable => false;
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.AddEndCondition(() => NeiyuSoulRecoveryUtility.IsRecovering(pawn) ? JobCondition.Ongoing : JobCondition.Succeeded);
            // Recovery has already teleported the pawn into a free slot. Do not claim
            // another pawn's bed, path while unconscious, or restart a saved duration.
            yield return LayDownToil(Bed != null);
        }
    }
}
