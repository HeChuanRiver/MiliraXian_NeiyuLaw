using System.Collections.Generic;
using AriandelLibrary;
using HarmonyLib;
using MiliraXian.Characters.Mingyuan;
using MiliraXian.Characters.QingHe;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    internal static class NeiyuSpecialPawnIntegration
    {
        public const int ValidationIntervalTicks = 600;

        private const string AriandelPackageId = "Ariandel.AriandelLibrary";
        private static readonly HashSet<int> WarnedDuplicatePawnIds = new();

        public static void TryRegister(Pawn pawn)
        {
            if (pawn == null || pawn.DestroyedOrNull())
            {
                return;
            }

            if (!IsSupportedSpecialPawn(pawn))
            {
                return;
            }

            if (!SpecialPawnCoreStateRepair.EnsureValidState(pawn, pawn.Spawned, logRepair: true) || pawn.Dead)
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

            AriandelLibrary_GameComponent library = AriandelLibrary_GameComponent.Instance;
            SpecialPawnManager manager = library?.SpecialPawns;
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

            if (realID != pawn.ThingID && WarnedDuplicatePawnIds.Add(pawn.thingIDNumber))
            {
                Log.Warning("[MiliraXian.Characters.Neiyu] Special pawn staticID already mapped to another pawn. staticID="
                            + staticID + ", existing=" + realID + ", current=" + pawn.ThingID);
            }
        }

        public static void AuditAllPlayerSpecialPawns(System.Action<Pawn> observer = null)
        {
            List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
            for (int index = 0; index < pawns.Count; index++)
            {
                AuditPawn(pawns[index], observer);
            }

            if (Find.WorldPawns == null)
            {
                return;
            }

            foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive)
            {
                if (pawn?.Faction == Faction.OfPlayer)
                {
                    AuditPawn(pawn, observer);
                }
            }
        }

        private static void AuditPawn(Pawn pawn, System.Action<Pawn> observer)
        {
            if (pawn == null || pawn.Faction != Faction.OfPlayer || !IsSupportedSpecialPawn(pawn))
            {
                return;
            }

            TryRegister(pawn);
            observer?.Invoke(pawn);
        }

        private static bool IsSupportedSpecialPawn(Pawn pawn)
        {
            return NeiyuEquipmentUtility.IsNeiyu(pawn)
                   || ZhaoliKarmaUtility.IsZhaoli(pawn)
                   || MX_QHCharacterUtility.IsQinghe(pawn)
                   || MingyuanUtility.IsMingyuan(pawn);
        }

        internal static void RepairBeforeSpawn(Pawn pawn)
        {
            if (pawn == null || pawn.DestroyedOrNull() || !IsSupportedSpecialPawn(pawn))
            {
                return;
            }

            SpecialPawnCoreStateRepair.EnsureValidState(pawn, includeSpawnComponents: false, logRepair: true);
        }

        internal static void ClearRuntimeState()
        {
            WarnedDuplicatePawnIds.Clear();
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

            return AriandelLibrary_GameComponent.Instance?.SpecialPawns != null;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_Pawn_SpawnSetup_SpecialPawnRegistration
    {
        public static void Prefix(Pawn __instance)
        {
            NeiyuSpecialPawnIntegration.RepairBeforeSpawn(__instance);
        }

        public static void Postfix(Pawn __instance)
        {
            NeiyuSpecialPawnIntegration.TryRegister(__instance);
            GameComponent_SpecialPawnPrimaryCultureConversion.NotifyPawnAvailable(__instance);
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
                GameComponent_SpecialPawnPrimaryCultureConversion.NotifyPawnAvailable(__instance);
            }
        }
    }

    public class GameComponent_NeiyuSpecialPawnIntegration : GameComponent
    {
        public GameComponent_NeiyuSpecialPawnIntegration(Game game)
        {
        }

    }
}
