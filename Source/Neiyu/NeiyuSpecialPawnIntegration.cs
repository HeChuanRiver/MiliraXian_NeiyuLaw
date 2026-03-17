using AriandelLibrary;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    internal static class NeiyuSpecialPawnIntegration
    {
        private const string AriandelPackageId = "Ariandel.AriandelLibrary";

        public static void TryRegister(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.DestroyedOrNull())
            {
                return;
            }

            if (!NeiyuEquipmentUtility.IsNeiyu(pawn))
            {
                return;
            }

            if (pawn.Faction != Faction.OfPlayer)
            {
                return;
            }

            if (!ModsConfig.IsActive(AriandelPackageId))
            {
                return;
            }

            if (NeiyuLawMod.Instance != null && NeiyuLawMod.Instance.Settings != null &&
                !NeiyuLawMod.Instance.Settings.EnableAriandelSpecialPawnIntegration)
            {
                return;
            }

            VoidPawnManager manager = VoidPawnManager.Instance;
            if (manager == null)
            {
                return;
            }

            string staticID = SpecialPawnRegistry.GetStaticID(pawn.kindDef);
            if (string.IsNullOrEmpty(staticID))
            {
                return;
            }

            string realID = manager.GetRealID(staticID);
            if (string.IsNullOrEmpty(realID))
            {
                manager.RegisterSpecialPawn(staticID, pawn);
                return;
            }

            if (realID != pawn.ThingID)
            {
                Log.Warning("[MiliraXian.Characters.Neiyu] Special pawn staticID already mapped to another pawn. staticID="
                            + staticID + ", existing=" + realID + ", current=" + pawn.ThingID);
            }
        }
    }
}