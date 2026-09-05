using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters
{
    [StaticConstructorOnStartup]
    public static class CommonPatches
    {
        private static readonly Harmony patcher = new("MiliraXian.Characters.Common");

        static CommonPatches()
        {
            patcher.Patch(
                AccessTools.Method(typeof(JoyGiver_Read), nameof(JoyGiver_Read.TryGiveJob)),
                postfix: new HarmonyMethod(typeof(CommonPatches), nameof(Patch_ReadGiver_TryGiveJob_Postfix)));

            patcher.Patch(
                AccessTools.Method(typeof(LearningGiver_Reading), nameof(LearningGiver_Reading.TryGiveJob)),
                postfix: new HarmonyMethod(typeof(CommonPatches), nameof(Patch_ReadGiver_TryGiveJob_Postfix)));

            patcher.Patch(
                AccessTools.Method(typeof(Book), nameof(Book.PawnReadNow)),
                prefix: new HarmonyMethod(typeof(CommonPatches), nameof(Patch_Book_PawnReadNow_Prefix)));
        }

        public static void Patch_ReadGiver_TryGiveJob_Postfix(ref Job __result)
        {
            if (__result == null || __result.def != JobDefOf.Reading)
            {
                return;
            }

            Book book = __result.GetTarget(TargetIndex.A).Thing as Book;
            if (book is Thing_MX_SkillBook && MX_CommonDefOf.MX_ReadSkillBook != null)
            {
                __result.def = MX_CommonDefOf.MX_ReadSkillBook;
            }
        }

        public static bool Patch_Book_PawnReadNow_Prefix(Book __instance, Pawn pawn)
        {
            if (__instance is not Thing_MX_SkillBook || MX_CommonDefOf.MX_ReadSkillBook == null)
            {
                return true;
            }

            Job job = JobMaker.MakeJob(MX_CommonDefOf.MX_ReadSkillBook, __instance);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            return false;
        }
    }
}
