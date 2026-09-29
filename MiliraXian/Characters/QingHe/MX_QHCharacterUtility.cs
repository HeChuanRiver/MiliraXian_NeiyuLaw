using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class MX_QHCharacterUtility
    {
        public static bool IsQinghe(Pawn pawn)
        {
            return pawn?.kindDef == MX_QHDefOf.MiliraXian_Qinghe;
        }

        public static string TranslateIfKey(string text)
        {
            if (text.NullOrEmpty())
            {
                return text;
            }

            return Translator.CanTranslate(text) ? text.Translate().ToString() : text;
        }
    }
}
