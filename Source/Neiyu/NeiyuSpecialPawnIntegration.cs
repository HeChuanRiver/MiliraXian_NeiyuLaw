using System.Collections.Generic;
using AriandelLibrary;
using HarmonyLib;
using MiliraXian.Characters.QingHe;
using MiliraXian.Characters.Zhaoli;
using ALVoidPawnManager = AriandelLibrary.AriandelLibrary_GameComponent_VoidPawnManager;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    internal static class NeiyuSpecialPawnIntegration
    {
        public const int ValidationIntervalTicks = 600;

        private const string AriandelPackageId = "Ariandel.AriandelLibrary";

        public static void TryRegister(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.DestroyedOrNull())
            {
                return;
            }

            if (!IsSupportedSpecialPawn(pawn))
            {
                return;
            }

            if (pawn.Faction != Faction.OfPlayer)
            {
                return;
            }

            if (!ShouldRunIntegration())
            {
                return;
            }

            ALVoidPawnManager manager = ALVoidPawnManager.Instance;
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

        public static void TryRegisterAllPlayerSpecialPawns()
        {
            if (!ShouldRunIntegration())
            {
                return;
            }

            List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
            for (int index = 0; index < pawns.Count; index++)
            {
                TryRegister(pawns[index]);
            }

            if (Find.WorldPawns == null)
            {
                return;
            }

            foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive)
            {
                if (pawn?.Faction == Faction.OfPlayer)
                {
                    TryRegister(pawn);
                }
            }
        }

        private static bool IsSupportedSpecialPawn(Pawn pawn)
        {
            return NeiyuEquipmentUtility.IsNeiyu(pawn) || ZhaoliKarmaUtility.IsZhaoli(pawn) || MX_QHUtility.IsQinghe(pawn);
        }

        private static bool ShouldRunIntegration()
        {
            if (!ModsConfig.IsActive(AriandelPackageId))
            {
                return false;
            }

            if (NeiyuLawMod.Instance != null && NeiyuLawMod.Instance.Settings != null &&
                !NeiyuLawMod.Instance.Settings.EnableAriandelSpecialPawnIntegration)
            {
                return false;
            }

            return ALVoidPawnManager.Instance != null;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_Pawn_SpawnSetup_SpecialPawnRegistration
    {
        public static void Postfix(Pawn __instance)
        {
            NeiyuSpecialPawnIntegration.TryRegister(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction), new[] { typeof(Faction), typeof(Pawn) })]
    internal static class Patch_Pawn_SetFaction_SpecialPawnRegistration
    {
        public static void Postfix(Pawn __instance, Faction newFaction)
        {
            if (newFaction == Faction.OfPlayer)
            {
                NeiyuSpecialPawnIntegration.TryRegister(__instance);
            }
        }
    }

    public class GameComponent_NeiyuSpecialPawnIntegration : GameComponent
    {
        public GameComponent_NeiyuSpecialPawnIntegration(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            if (Find.TickManager.TicksGame % NeiyuSpecialPawnIntegration.ValidationIntervalTicks != 0)
            {
                return;
            }

            NeiyuSpecialPawnIntegration.TryRegisterAllPlayerSpecialPawns();
        }
    }
}
