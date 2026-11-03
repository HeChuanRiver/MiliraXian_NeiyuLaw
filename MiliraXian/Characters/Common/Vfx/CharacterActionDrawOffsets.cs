using HarmonyLib;
using MiliraXian.Characters.Mingyuan;
using MiliraXian.Characters.QingHe.Abilities;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx;

[HarmonyPatch(typeof(Pawn_DrawTracker), "DrawPos", MethodType.Getter)]
internal static class Patch_CharacterActionDrawOffsets
{
    private static void Postfix(Pawn ___pawn, ref Vector3 __result)
    {
        if (___pawn is not { Spawned: true, Destroyed: false }) return;
        CompAbilityEffect_AscentSlash.ApplyActiveActionDrawPos(___pawn, ref __result);
        __result += CompMingyuanBurningPillarTornado.PullDrawOffset(___pawn);
    }
}
