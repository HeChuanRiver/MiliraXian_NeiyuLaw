using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Mingyuan;
using MiliraXian.Characters.Neiyu;
using Verse;

namespace MiliraXian.Characters.Common
{
    // One entry for effects that preserve a living pawn before Kill's irreversible side
    // effects. Corpse resurrection (e.g. Zhaoli) has a different lifecycle and stays there.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_CharacterFatalInterception
    {
        private static readonly HashSet<Pawn> intercepting = new();

        [HarmonyPriority(Priority.High)]
        private static bool Prefix(Pawn __instance)
        {
            if (!intercepting.Add(__instance)) return false;
            try
            {
                return !NeiyuSoulRecoveryUtility.TryPreventDeath(__instance)
                    && !MingyuanRebirthUtility.TryInterceptFatalDamage(__instance);
            }
            finally { intercepting.Remove(__instance); }
        }
    }
}
