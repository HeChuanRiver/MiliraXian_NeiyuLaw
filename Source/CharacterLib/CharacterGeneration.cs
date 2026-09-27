using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.CharacterLib
{
    public class CharacterAppearanceExtension : DefModExtension
    {
        public string firstNameKey;
        public string lastNameKey;
        public string nickNameKey;
        public HeadTypeDef headType;
        public bool disableRandomTraits = true;
    }

    // Backstories, traits, age, sex and starting hediffs are vanilla PawnKindDef data.
    // Only the translated full name, head selection and random-trait opt-out need a hook.
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
    internal static class CharacterGeneration
    {
        private static void Prefix(ref PawnGenerationRequest request)
        {
            var appearance = request.KindDef?.GetModExtension<CharacterAppearanceExtension>();
            if (appearance?.disableRandomTraits != true) return;
            request.MaximumAgeTraits = 0;
            request.MinimumAgeTraits = 0;
        }

        private static void Postfix(Pawn __result)
        {
            Pawn pawn = __result;
            var appearance = pawn?.kindDef?.GetModExtension<CharacterAppearanceExtension>();
            if (appearance == null || pawn.story == null) return;
            NameTriple old = pawn.Name as NameTriple;
            pawn.Name = new NameTriple(Resolve(appearance.firstNameKey, old?.First ?? pawn.LabelShort),
                Resolve(appearance.nickNameKey, old?.Nick ?? pawn.LabelShort), Resolve(appearance.lastNameKey, old?.Last ?? ""));
            if (appearance.headType != null) pawn.story.headType = appearance.headType;
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private static string Resolve(string key, string fallback) => !string.IsNullOrEmpty(key) && key.CanTranslate() ? key.Translate().ToString() : fallback;
    }
}
