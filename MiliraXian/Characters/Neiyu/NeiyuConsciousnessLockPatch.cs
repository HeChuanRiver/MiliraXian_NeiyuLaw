using HarmonyLib;
using MiliraXian.Characters.QingHe;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    [HarmonyPatch(typeof(PawnCapacityUtility), nameof(PawnCapacityUtility.CalculateCapacityLevel))]
    internal static class Patch_PawnCapacityUtility_CalculateCapacityLevel_SpecialPawnConsciousness
    {
        [HarmonyPostfix]
        private static void Postfix(HediffSet diffSet, PawnCapacityDef capacity, ref float __result)
        {
            if (capacity != PawnCapacityDefOf.Consciousness)
            {
                return;
            }

            Pawn pawn = diffSet?.pawn;
            float minimumConsciousness = MinimumConsciousnessFromSettings();
            if (pawn != null && NeiyuEquipmentUtility.IsNeiyu(pawn))
            {
                minimumConsciousness = NeiyuPowerBalance.LimitConsciousnessMinimum(minimumConsciousness);
            }
            if (ZhaoliKarmaUtility.IsZhaoli(pawn) && ZhaoliPowerBalance.Sealed)
                minimumConsciousness = 0f;
            if (minimumConsciousness <= 0f || __result >= minimumConsciousness)
            {
                return;
            }

            if (pawn == null || pawn.Dead || !IsSupportedSpecialPawn(pawn))
            {
                return;
            }

            __result = minimumConsciousness;
        }

        private static bool IsSupportedSpecialPawn(Pawn pawn)
        {
            return NeiyuEquipmentUtility.IsNeiyu(pawn)
                || ZhaoliKarmaUtility.IsZhaoli(pawn)
                || MX_QHCharacterUtility.IsQinghe(pawn);
        }

        private static float MinimumConsciousnessFromSettings()
        {
            NeiyuLawSettings settings = NeiyuLawMod.Instance?.Settings;
            if (settings == null)
            {
                return 0f;
            }

            return settings.ConsciousnessLockMode switch
            {
                SpecialPawnConsciousnessLockMode.Lock100 => 1f,
                SpecialPawnConsciousnessLockMode.Lock35 => 0.35f,
                _ => 0f,
            };
        }
    }
}
