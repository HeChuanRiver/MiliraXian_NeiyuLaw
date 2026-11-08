using HarmonyLib;
using MiliraXian.Characters.Mingyuan;
using MiliraXian.Characters.Neiyu;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx;

[HarmonyPatch(typeof(Pawn_DrawTracker), "DrawPos", MethodType.Getter)]
internal static class Patch_CharacterActionDrawOffsets
{
    private static void Postfix(Pawn ___pawn, ref Vector3 __result)
    {
        if (___pawn is not { Spawned: true, Destroyed: false }) return;
        if (JobDriver_NeiyuPostureExecution.TryGetExecutionDrawPos(___pawn, out Vector3 executionPosition))
        {
            __result = executionPosition.WithY(__result.y);
            return;
        }
        __result += CompMingyuanBurningPillarTornado.PullDrawOffset(___pawn);
    }
}
