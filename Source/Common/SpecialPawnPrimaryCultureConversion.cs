using System.Collections.Generic;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class GameComponent_SpecialPawnPrimaryCultureConversion : GameComponent
    {
        private const int CheckIntervalTicks = 600;
        private const int ConversionDelayTicks = GenDate.TicksPerDay / 2;

        private List<PendingCultureConversion> pendingConversions = new List<PendingCultureConversion>();
        private List<int> completedPawnIds = new List<int>();

        public GameComponent_SpecialPawnPrimaryCultureConversion(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick % CheckIntervalTicks != 0)
            {
                return;
            }

            ProcessPendingConversions(currentTick);
            RegisterPlayerSpecialPawns(currentTick);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingConversions, "pendingSpecialPawnPrimaryCultureConversions", LookMode.Deep);
            Scribe_Collections.Look(ref completedPawnIds, "completedSpecialPawnPrimaryCultureConversions", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pendingConversions == null)
                {
                    pendingConversions = new List<PendingCultureConversion>();
                }

                if (completedPawnIds == null)
                {
                    completedPawnIds = new List<int>();
                }

                pendingConversions.RemoveAll(entry => entry == null || entry.pawn == null);
            }
        }

        private void RegisterPlayerSpecialPawns(int currentTick)
        {
            List<Pawn> playerPawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
            for (int index = 0; index < playerPawns.Count; index++)
            {
                RegisterIfNeeded(playerPawns[index], currentTick);
            }

            if (Find.WorldPawns == null)
            {
                return;
            }

            foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive)
            {
                if (pawn?.Faction == Faction.OfPlayer)
                {
                    RegisterIfNeeded(pawn, currentTick);
                }
            }
        }

        private void RegisterIfNeeded(Pawn pawn, int currentTick)
        {
            if (!IsEligibleSpecialPawn(pawn))
            {
                return;
            }

            int pawnId = pawn.thingIDNumber;
            if (completedPawnIds.Contains(pawnId))
            {
                return;
            }

            Ideo primaryIdeo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            if (primaryIdeo == null)
            {
                RemovePending(pawn);
                return;
            }

            if (pawn.Ideo == primaryIdeo)
            {
                MarkCompleted(pawnId);
                RemovePending(pawn);
                return;
            }

            if (GetPending(pawn) != null)
            {
                return;
            }

            pendingConversions.Add(new PendingCultureConversion
            {
                pawn = pawn,
                conversionTick = currentTick + ConversionDelayTicks
            });
        }

        private void ProcessPendingConversions(int currentTick)
        {
            for (int index = pendingConversions.Count - 1; index >= 0; index--)
            {
                PendingCultureConversion entry = pendingConversions[index];
                Pawn pawn = entry?.pawn;
                if (pawn == null || pawn.DestroyedOrNull() || pawn.Dead || !IsTrackedSpecialPawn(pawn))
                {
                    pendingConversions.RemoveAt(index);
                    continue;
                }

                if (pawn.Faction != Faction.OfPlayer)
                {
                    completedPawnIds.Remove(pawn.thingIDNumber);
                    pendingConversions.RemoveAt(index);
                    continue;
                }

                if (!IsEligibleSpecialPawn(pawn))
                {
                    pendingConversions.RemoveAt(index);
                    continue;
                }

                if (currentTick < entry.conversionTick)
                {
                    continue;
                }

                Ideo primaryIdeo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                if (primaryIdeo == null || pawn.ideo == null || !pawn.ShouldHaveIdeo)
                {
                    continue;
                }

                if (pawn.Ideo != primaryIdeo)
                {
                    pawn.ideo.SetIdeo(primaryIdeo);
                }

                MarkCompleted(pawn.thingIDNumber);
                pendingConversions.RemoveAt(index);
            }
        }

        private static bool IsEligibleSpecialPawn(Pawn pawn)
        {
            return pawn != null
                   && pawn.Faction == Faction.OfPlayer
                   && !pawn.Dead
                   && !pawn.DestroyedOrNull()
                   && pawn.ShouldHaveIdeo
                   && IsTrackedSpecialPawn(pawn);
        }

        private static bool IsTrackedSpecialPawn(Pawn pawn)
        {
            if (NeiyuEquipmentUtility.IsNeiyu(pawn))
            {
                return true;
            }

            return ZhaoliKarmaUtility.IsZhaoli(pawn)
                   && !ZhaoliScenarioUtility.IsHideoutState(pawn)
                   && !ZhaoliScenarioUtility.IsRaidState(pawn);
        }

        private PendingCultureConversion GetPending(Pawn pawn)
        {
            for (int index = 0; index < pendingConversions.Count; index++)
            {
                if (pendingConversions[index]?.pawn == pawn)
                {
                    return pendingConversions[index];
                }
            }

            return null;
        }

        private void RemovePending(Pawn pawn)
        {
            pendingConversions.RemoveAll(entry => entry?.pawn == pawn);
        }

        private void MarkCompleted(int pawnId)
        {
            if (!completedPawnIds.Contains(pawnId))
            {
                completedPawnIds.Add(pawnId);
            }
        }

        private class PendingCultureConversion : IExposable
        {
            public Pawn pawn;
            public int conversionTick = -1;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref conversionTick, "conversionTick", -1);
            }
        }
    }
}
