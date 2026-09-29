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
    }
}
