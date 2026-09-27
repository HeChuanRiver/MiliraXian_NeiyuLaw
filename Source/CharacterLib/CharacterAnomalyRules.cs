using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.CharacterLib
{
    internal enum CharacterRitualReason { Protected }

    [HarmonyPatch(typeof(PsychicRitualRoleDef), "PawnCanDo",
        new[] { typeof(PsychicRitualRoleDef.Context), typeof(Pawn), typeof(TargetInfo), typeof(AnyEnum) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
    internal static class Patch_CharacterRitualRole
    {
        private static void Postfix(PsychicRitualRoleDef __instance, Pawn pawn, ref AnyEnum reason, ref bool __result)
        {
            if (!__result || CharacterProtection.For(pawn)?.protectAnomaly != true) return;
            string role = __instance.defName;
            if (role == "Invoker" || role == "Defender" || role.StartsWith("Chanter", StringComparison.Ordinal)) return;
            __result = false;
            reason = AnyEnum.FromEnum(CharacterRitualReason.Protected);
        }
    }
    [HarmonyPatch(typeof(PsychicRitualRoleDef), nameof(PsychicRitualRoleDef.PawnCannotDoReason))]
    internal static class Patch_CharacterRitualReason
    {
        private static bool Prefix(AnyEnum reason, ref TaggedString __result)
        {
            if (reason.As<CharacterRitualReason>() != CharacterRitualReason.Protected) return true;
            __result = "不能将此角色作为仪式祭品或受术者。";
            return false;
        }
    }
    [HarmonyPatch(typeof(CompTargetEffect_MutateIntoFleshbeast), nameof(CompTargetEffect_MutateIntoFleshbeast.DoEffectOn))]
    internal static class Patch_CharacterFleshbeastReflect
    {
        [ThreadStatic] private static bool reflecting;
        private static bool Prefix(CompTargetEffect_MutateIntoFleshbeast __instance, Pawn user, Thing target)
        {
            if (reflecting || CharacterProtection.For(target as Pawn)?.protectAnomaly != true) return true;
            if (user != null && !user.Dead)
            {
                try { reflecting = true; __instance.DoEffectOn(user, user); }
                finally { reflecting = false; }
            }
            return false;
        }
    }
    [HarmonyPatch(typeof(CompAbilityEffect_PsychicSlaughter), nameof(CompAbilityEffect_PsychicSlaughter.Apply))]
    internal static class Patch_CharacterSlaughterReflect
    {
        private static void Prefix(CompAbilityEffect_PsychicSlaughter __instance, ref LocalTargetInfo target)
        {
            Pawn caster = __instance.parent?.pawn;
            if (caster != null && caster != target.Pawn && CharacterProtection.For(target.Pawn)?.protectAnomaly == true)
                target = caster; // Let the public vanilla effect handle death, filth, meat and goodwill.
        }
    }
    [HarmonyPatch(typeof(CompObelisk_Duplicator), "TriggerInteractionEffect")]
    internal static class Patch_CharacterDuplicator
    {
        private static bool Prefix(Pawn interactor) => CharacterProtection.For(interactor)?.protectAnomaly != true;
    }
    [HarmonyPatch(typeof(AnomalyUtility), nameof(AnomalyUtility.TryDuplicatePawn))]
    internal static class Patch_CharacterDuplicate
    {
        private static bool Prefix(Pawn originalPawn, ref Pawn duplicatePawn, ref bool __result)
        {
            if (CharacterProtection.For(originalPawn)?.protectAnomaly != true) return true;
            duplicatePawn = null;
            __result = false;
            return false;
        }
    }
}
