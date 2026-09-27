using System.Collections.Generic;
using MiliraXian.Characters.Mingyuan;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    /// <summary>
    /// Repairs incomplete tracker graphs on persistent special pawns before vanilla,
    /// HAR, or the void framework dereference them.
    /// </summary>
    internal static class SpecialPawnCoreStateRepair
    {
        public static bool EnsureValidState(Pawn pawn, bool includeSpawnComponents, bool logRepair)
        {
            if (pawn == null || pawn.Destroyed || pawn.Discarded || pawn.kindDef?.race == null)
            {
                return false;
            }

            List<string> repairedParts = new List<string>();
            RepairHealthGraph(pawn, repairedParts);

            bool missingInitialComponents =
                pawn.ageTracker == null ||
                pawn.records == null ||
                pawn.inventory == null ||
                pawn.verbTracker == null ||
                pawn.carryTracker == null ||
                pawn.needs == null ||
                pawn.mindState == null ||
                pawn.ownership == null ||
                pawn.thinker == null ||
                pawn.jobs == null ||
                pawn.stances == null ||
                pawn.equipment == null ||
                pawn.apparel == null ||
                pawn.skills == null ||
                pawn.story == null;

            if (missingInitialComponents)
            {
                PawnComponentsUtility.CreateInitialComponents(pawn);
                repairedParts.Add("initial trackers");
            }

            RepairNeiyuVisualIdentity(pawn, repairedParts);
            RepairMingyuanVisualIdentity(pawn, repairedParts);

            if (includeSpawnComponents && (pawn.pather == null || pawn.rotationTracker == null))
            {
                PawnComponentsUtility.AddComponentsForSpawn(pawn);
                repairedParts.Add("spawn trackers");
            }

            if (repairedParts.Count == 0)
            {
                return true;
            }

            pawn.health?.Notify_HediffChanged(null);
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();

            if (logRepair)
            {
                Log.Warning("[MiliraXian.Characters.Neiyu] Repaired incomplete special pawn state on "
                            + pawn.ToStringSafe() + ": " + string.Join(", ", repairedParts));
            }

            return true;
        }

        private static void RepairNeiyuVisualIdentity(Pawn pawn, List<string> repairedParts)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || pawn.story == null)
            {
                return;
            }

            bool repaired = false;
            if (pawn.story.bodyType == null)
            {
                pawn.story.bodyType = BodyTypeDefOf.Female;
                repaired = true;
            }

            if (pawn.story.headType == null)
            {
                HeadTypeDef headType = DefDatabase<HeadTypeDef>.GetNamedSilentFail("MiliraNeiyuHead");
                if (headType != null)
                {
                    pawn.story.headType = headType;
                    repaired = true;
                }
            }

            if (pawn.story.hairDef == null)
            {
                pawn.story.hairDef = HairDefOf.Bald;
                repaired = true;
            }

            if (pawn.story.Childhood == null)
            {
                BackstoryDef childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail("MiliraXian_BackStoryChild_Neiyu_BS1");
                if (childhood != null)
                {
                    pawn.story.Childhood = childhood;
                    repaired = true;
                }
            }

            if (pawn.story.Adulthood == null)
            {
                BackstoryDef adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail("MiliraXian_BackStoryAdult_Neiyu_BS1");
                if (adulthood != null)
                {
                    pawn.story.Adulthood = adulthood;
                    repaired = true;
                }
            }

            if (repaired)
            {
                repairedParts.Add("Neiyu visual identity");
            }
        }

        private static void RepairMingyuanVisualIdentity(Pawn pawn, List<string> repairedParts)
        {
            if (!MingyuanUtility.IsMingyuan(pawn) || pawn.story == null)
            {
                return;
            }

            HeadTypeDef headType = DefDatabase<HeadTypeDef>.GetNamedSilentFail("MiliraMingyuanHead");
            if (headType == null || pawn.story.headType == headType)
            {
                return;
            }

            pawn.story.headType = headType;
            repairedParts.Add("Mingyuan visual identity");
        }

        private static void RepairHealthGraph(Pawn pawn, List<string> repairedParts)
        {
            bool repaired = false;
            if (pawn.health == null)
            {
                pawn.health = new Pawn_HealthTracker(pawn);
                repaired = true;
            }

            if (pawn.health.hediffSet == null)
            {
                pawn.health.hediffSet = new HediffSet(pawn);
                repaired = true;
            }

            HediffSet hediffSet = pawn.health.hediffSet;
            if (hediffSet.pawn != pawn)
            {
                hediffSet.pawn = pawn;
                repaired = true;
            }

            if (hediffSet.hediffs == null)
            {
                hediffSet.hediffs = new List<Hediff>();
                repaired = true;
            }

            int removed = hediffSet.hediffs.RemoveAll(hediff => hediff == null || hediff.def == null);
            if (removed > 0)
            {
                repaired = true;
            }

            for (int index = 0; index < hediffSet.hediffs.Count; index++)
            {
                Hediff hediff = hediffSet.hediffs[index];
                if (hediff.pawn != pawn)
                {
                    hediff.pawn = pawn;
                    repaired = true;
                }
            }

            if (repaired)
            {
                repairedParts.Add("health/hediffSet");
            }
        }
    }
}
