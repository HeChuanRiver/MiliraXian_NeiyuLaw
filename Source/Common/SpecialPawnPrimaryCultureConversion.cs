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

        private List<PendingCultureConversion> pendingConversions = new();
        private List<int> completedPawnIds = new();
        private HashSet<int> completedPawnIdSet = new();
        private int nextConversionTick = int.MaxValue;

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
            if (currentTick >= nextConversionTick)
            {
                ProcessPendingConversions(currentTick);
            }

            if (currentTick % CheckIntervalTicks == 0)
            {
                NeiyuSpecialPawnIntegration.AuditAllPlayerSpecialPawns(pawn => RegisterIfNeeded(pawn, currentTick));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingConversions, "pendingSpecialPawnPrimaryCultureConversions", LookMode.Deep);
            Scribe_Collections.Look(ref completedPawnIds, "completedSpecialPawnPrimaryCultureConversions", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pendingConversions ??= new();

                completedPawnIds ??= new();

                pendingConversions.RemoveAll(entry => entry == null || entry.pawn == null);
                completedPawnIdSet = new HashSet<int>(completedPawnIds);
                RecalculateNextConversionTick();
            }
        }

        public static void NotifyPawnAvailable(Pawn pawn)
        {
            if (pawn == null || Find.TickManager == null)
            {
                return;
            }

            Current.Game?.GetComponent<GameComponent_SpecialPawnPrimaryCultureConversion>()
                ?.RegisterIfNeeded(pawn, Find.TickManager.TicksGame);
        }

        private void RegisterIfNeeded(Pawn pawn, int currentTick)
        {
            if (!IsEligibleSpecialPawn(pawn))
            {
                return;
            }

            int pawnId = pawn.thingIDNumber;
            if (completedPawnIdSet.Contains(pawnId))
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
            nextConversionTick = System.Math.Min(nextConversionTick, currentTick + ConversionDelayTicks);
        }

        private void ProcessPendingConversions(int currentTick)
        {
            nextConversionTick = int.MaxValue;
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
                    completedPawnIdSet.Remove(pawn.thingIDNumber);
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
                    nextConversionTick = System.Math.Min(nextConversionTick, entry.conversionTick);
                    continue;
                }

                Ideo primaryIdeo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
                if (primaryIdeo == null || pawn.ideo == null || !pawn.ShouldHaveIdeo)
                {
                    nextConversionTick = System.Math.Min(nextConversionTick, currentTick + CheckIntervalTicks);
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

            completedPawnIdSet.Add(pawnId);
        }

        private void RecalculateNextConversionTick()
        {
            nextConversionTick = int.MaxValue;
            for (int index = 0; index < pendingConversions.Count; index++)
            {
                PendingCultureConversion entry = pendingConversions[index];
                if (entry != null)
                {
                    nextConversionTick = System.Math.Min(nextConversionTick, entry.conversionTick);
                }
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
