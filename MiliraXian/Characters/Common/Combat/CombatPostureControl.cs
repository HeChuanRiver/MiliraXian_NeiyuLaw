using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Common.Combat;

[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
internal static class Patch_PostureOrderedJob
{
    // An uninterruptible Job still accepts queued orders through this method.
    private static bool Prefix(Pawn ___pawn) => !CombatPosture.ControlsActions(___pawn);
}

[HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
internal static class Patch_PostureThinkTree
{
    private static bool Prefix(Pawn ___pawn, ref ThinkTreeDef thinkTree, ref ThinkResult __result)
    {
        if (!CombatPosture.ControlsActions(___pawn)) return true;
        thinkTree = null;
        __result = ThinkResult.NoJob;
        return false;
    }
}

[HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextConstantThinkTreeJob")]
internal static class Patch_PostureConstantThinkTree
{
    private static bool Prefix(Pawn ___pawn, ref ThinkResult __result)
    {
        if (!CombatPosture.ControlsActions(___pawn)) return true;
        __result = ThinkResult.NoJob;
        return false;
    }
}

[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
internal static class Patch_PostureDirectJob
{
    private static bool Prefix(Pawn ___pawn, Job newJob) =>
        newJob?.def == CombatPosture.Config.brokenJob || !CombatPosture.ControlsActions(___pawn);
}

[HarmonyPatch]
internal static class Patch_PostureJobTick
{
    // The job which delivered the blocked attack can still be installed until
    // the posture comp claims the execution and installs its own hold job.
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(JobDriver), nameof(JobDriver.DriverTick));
        yield return AccessTools.Method(typeof(JobDriver), nameof(JobDriver.DriverTickInterval));
    }

    private static bool Prefix(JobDriver __instance) => __instance is IPostureExecutionDriver
        || __instance.job?.def == CombatPosture.Config.brokenJob || !CombatPosture.ControlsActions(__instance.pawn);
}

[HarmonyPatch]
internal static class Patch_PosturePath
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StartPath));
        // PatherTick can relocate a colliding pawn even after StopDead.
        yield return AccessTools.Method(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.PatherTick));
    }

    private static bool Prefix(Pawn ___pawn) => !CombatPosture.ControlsActions(___pawn)
        && ___pawn.jobs?.curDriver is not IPostureExecutionDriver { DrivesExecutorPosition: true };
}

[HarmonyPatch(typeof(Thing), nameof(Thing.Rotation), MethodType.Setter)]
internal static class Patch_PostureRotation
{
    private static bool Prefix(Thing __instance) =>
        __instance is not Pawn pawn || !CombatPosture.ControlsActions(pawn);
}

[HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
internal static class Patch_PostureDraft
{
    private static bool Prefix(Pawn_DraftController __instance) => !CombatPosture.ControlsActions(__instance.pawn);
}

[HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn),
    new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
internal static class Patch_PostureStartCast
{
    private static bool Prefix(Verb __instance) => !CombatPosture.ControlsActions(__instance.CasterPawn);
}

[HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
internal static class Patch_PostureContinueCast
{
    private static bool Prefix(Verb __instance)
    {
        if (!CombatPosture.ControlsActions(__instance.CasterPawn)) return true;
        __instance.Reset();
        return false;
    }
}

[HarmonyPatch]
internal static class Patch_PostureActivateAbility
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Ability), nameof(Ability.Activate),
            [typeof(LocalTargetInfo), typeof(LocalTargetInfo)]);
        yield return AccessTools.Method(typeof(Ability), nameof(Ability.Activate), [typeof(GlobalTargetInfo)]);
    }

    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Ability __instance) => !CombatPosture.ControlsActions(__instance.pawn);
}
