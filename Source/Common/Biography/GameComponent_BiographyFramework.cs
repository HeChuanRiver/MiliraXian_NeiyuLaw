using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Biography
{
    public sealed class GameComponent_BiographyFramework : GameComponent
    {
        public GameComponent_BiographyFramework(Game game)
        {
        }

        public override void StartedNewGame()
        {
            BiographyFrameworkUtility.ClearCache();
        }

        public override void LoadedGame()
        {
            BiographyFrameworkUtility.ClearCache();
        }

        // Compatibility shell. Cultivation evaluates conditions on demand; the former
        // periodic scan of every map and caravan is no longer needed.
    }

    public static class BiographyFrameworkUtility
    {
        private static ConditionalWeakTable<Pawn, Hediff_BiographyTracker> trackers = new();
        internal static void ClearCache() => trackers = new();
        internal static void Invalidate(Pawn pawn) { if (pawn != null) trackers.Remove(pawn); }
        public static Hediff_BiographyTracker GetTracker(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || BiographyDefOf.MX_BiographyTracker == null)
            {
                return null;
            }

            if (trackers.TryGetValue(pawn, out var cached)) return cached;
            var tracker = pawn.health.hediffSet.GetFirstHediffOfDef(BiographyDefOf.MX_BiographyTracker) as Hediff_BiographyTracker;
            if (tracker != null) trackers.Add(pawn, tracker);
            return tracker;
        }

        public static Hediff_BiographyTracker GetOrCreateTracker(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || (!NeiyuEquipmentUtility.IsNeiyu(pawn)
                && !BiographyDatabase.TryGet(pawn.kindDef, out BiographyExtension _)))
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
