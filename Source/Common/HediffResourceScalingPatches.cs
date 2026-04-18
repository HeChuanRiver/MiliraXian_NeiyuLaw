using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    [StaticConstructorOnStartup]
    public static class HediffResourceScalingPatches
    {
        private static readonly Harmony patcher = new Harmony("MiliraXian.Characters.HediffResourceScaling");

        static HediffResourceScalingPatches()
        {
            patcher.Patch(
                AccessTools.PropertyGetter(typeof(Hediff), nameof(Hediff.PainOffset)),
                postfix: new HarmonyMethod(typeof(HediffResourceScalingPatches), nameof(Postfix_PainOffset)));

            patcher.Patch(
                AccessTools.PropertyGetter(typeof(Hediff), nameof(Hediff.PainFactor)),
                postfix: new HarmonyMethod(typeof(HediffResourceScalingPatches), nameof(Postfix_PainFactor)));

            patcher.Patch(
                AccessTools.PropertyGetter(typeof(Hediff), nameof(Hediff.BleedRate)),
                postfix: new HarmonyMethod(typeof(HediffResourceScalingPatches), nameof(Postfix_BleedRate)));

            patcher.Patch(
                AccessTools.PropertyGetter(typeof(Hediff), nameof(Hediff.CurStage)),
                postfix: new HarmonyMethod(typeof(HediffResourceScalingPatches), nameof(Postfix_CurStage)));
        }

        public static void Postfix_PainOffset(Hediff __instance, ref float __result)
        {
            var scaler = __instance.TryGetComp<HediffComp_PawnResourceScaling>();
            if (scaler?.Props.painOffset != null)
            {
                __result = scaler.PainOffset;
            }
        }

        public static void Postfix_PainFactor(Hediff __instance, ref float __result)
        {
            var scaler = __instance.TryGetComp<HediffComp_PawnResourceScaling>();
            if (scaler?.Props.painFactor != null)
            {
                __result = scaler.PainFactor;
            }
        }

        public static void Postfix_BleedRate(Hediff __instance, ref float __result)
        {
            var scaler = __instance.TryGetComp<HediffComp_PawnResourceScaling>();
            if (scaler?.Props.bleedRate != null)
            {
                __result = scaler.BleedRate;
            }
        }

        public static void Postfix_CurStage(Hediff __instance, ref HediffStage __result)
        {
            if (__result == null) return;

            var scaler = __instance.TryGetComp<HediffComp_PawnResourceScaling>();
            if (scaler == null) return;

            bool hasScale = scaler.Props.statOffsetMultiplier != null || scaler.Props.statFactorMultiplier != null;
            if (!hasScale) return;

            __result = scaler.GetScaledStage(__result);
        }
    }
}
