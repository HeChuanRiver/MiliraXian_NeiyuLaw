using Verse;

namespace MiliraXian.Characters
{
    internal static class MXCharacterIdentityUtility
    {
        internal const string QinghePawnKindDefName = "MiliraXian_Qinghe";

        internal static bool IsQinghe(Pawn pawn)
        {
            return pawn?.kindDef?.defName == QinghePawnKindDefName;
        }
    }
}
