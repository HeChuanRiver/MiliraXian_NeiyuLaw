using HarmonyLib;
using MiliraXian.CharacterLib;
using RJW_Menstruation;
using Verse;

namespace MiliraXian.MenstruationCompat
{
    [StaticConstructorOnStartup]
    internal static class PregnancyBootstrap
    {
        static PregnancyBootstrap() => new Harmony("MiliraXian.MenstruationCompat").PatchAll(typeof(PregnancyBootstrap).Assembly);
    }

    [HarmonyPatch(typeof(PregnancyCommon), nameof(PregnancyCommon.BabyPawnKindDecider))]
    internal static class Patch_CharacterChildKind
    {
        private static void Postfix(Pawn mother, Pawn father, ref PawnKindDef __result)
        {
            PawnKindDef fallback = __result?.GetModExtension<CharacterProtectionExtension>()?.childKind;
            if (fallback == null) return;

            // Respect the dependency's race/kind repair API before selecting the other parent.
            PawnKindDef motherKind = Utility.GetRacesPawnKind(mother);
            PawnKindDef fatherKind = Utility.GetRacesPawnKind(father);
            PawnKindDef other = __result == motherKind && fatherKind != null ? fatherKind
                : __result == fatherKind && motherKind != null ? motherKind : null;
            __result = other?.GetModExtension<CharacterProtectionExtension>()?.childKind ?? other ?? fallback;
        }
    }
}
