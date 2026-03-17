using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class MX_QHUtility
    {
        public const string PawnKindDef_Qinghe = "MiliraXian_QingHe";

        public static bool IsQinghe(Pawn pawn)
        {
            return pawn?.kindDef.defName == PawnKindDef_Qinghe;
        }
    }
}