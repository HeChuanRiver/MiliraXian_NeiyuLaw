using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Biography
{
    public sealed class GameComponent_BiographyFramework : GameComponent
    {
        private const int EvaluationIntervalTicks = 2500;

        private int nextEvaluationTick;

        public GameComponent_BiographyFramework(Game game)
        {
        }

        public override void StartedNewGame()
        {
            EvaluateAllActivePawns(sendNotifications: false);
            ScheduleNextEvaluation();
        }

        public override void LoadedGame()
        {
            EvaluateAllActivePawns(sendNotifications: false);
            ScheduleNextEvaluation();
        }

        public override void GameComponentTick()
        {
            if (!BiographyDatabase.HasAnyConfigurations || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick < nextEvaluationTick)
            {
                return;
            }

            nextEvaluationTick = currentTick + EvaluationIntervalTicks;
            EvaluateAllActivePawns(sendNotifications: true);
        }

        private static void EvaluateAllActivePawns(bool sendNotifications)
        {
            if (!BiographyDatabase.HasAnyConfigurations)
            {
                return;
            }

            List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null && BiographyDatabase.TryGet(pawn.kindDef, out BiographyExtension extension))
                {
                    Hediff_BiographyTracker tracker = BiographyFrameworkUtility.GetOrCreateTracker(pawn);
                    tracker?.EvaluateUnlocks(extension, sendNotifications);
                }
            }
        }

        private void ScheduleNextEvaluation()
        {
            nextEvaluationTick = (Find.TickManager?.TicksGame ?? 0) + EvaluationIntervalTicks;
        }
    }

    public static class BiographyFrameworkUtility
    {
        public static Hediff_BiographyTracker GetTracker(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || BiographyDefOf.MX_BiographyTracker == null)
            {
                return null;
            }

            return pawn.health.hediffSet.GetFirstHediffOfDef(BiographyDefOf.MX_BiographyTracker)
                as Hediff_BiographyTracker;
        }

        public static Hediff_BiographyTracker GetOrCreateTracker(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || !BiographyDatabase.TryGet(pawn.kindDef, out BiographyExtension _))
            {
                return null;
            }

            Hediff_BiographyTracker existing = GetTracker(pawn);
            if (existing != null)
            {
                return existing;
            }

            if (BiographyDefOf.MX_BiographyTracker == null)
            {
                Log.ErrorOnce("Could not resolve HediffDef MX_BiographyTracker.", 197450311);
                return null;
            }

            Hediff added = pawn.health.AddHediff(BiographyDefOf.MX_BiographyTracker);
            Hediff_BiographyTracker tracker = added as Hediff_BiographyTracker;
            if (tracker == null)
            {
                Log.ErrorOnce(
                    "MX_BiographyTracker did not create Hediff_BiographyTracker for " + pawn.ToStringSafe() + ".",
                    197450312);
                return null;
            }

            tracker.Severity = 1f;
            return tracker;
        }

        public static Hediff_BiographyTracker EnsureAndEvaluate(Pawn pawn, bool sendNotifications)
        {
            if (!BiographyDatabase.TryGet(pawn?.kindDef, out BiographyExtension extension))
            {
                return null;
            }

            Hediff_BiographyTracker tracker = GetOrCreateTracker(pawn);
            tracker?.EvaluateUnlocks(extension, sendNotifications);
            return tracker;
        }
    }
}
