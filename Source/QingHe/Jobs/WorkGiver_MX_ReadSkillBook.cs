using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Jobs
{
    public class WorkGiver_MX_ReadSkillBook : WorkGiver_Scanner
    {
        private const int AutomaticReadCooldownTicks = 30000;
        private static readonly Dictionary<int, int> nextAutomaticReadTickByPawn = new Dictionary<int, int>();

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (!CanConsiderPawn(pawn) || IsOnAutomaticReadCooldown(pawn))
            {
                yield break;
            }

            foreach (Thing_QingheMusicScoreBook book in pawn.Map.listerThings.GetThingsOfType<Thing_QingheMusicScoreBook>())
            {
                if (book != null)
                {
                    yield return book;
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Thing_QingheMusicScoreBook book = t as Thing_QingheMusicScoreBook;
            if (!CanConsiderPawn(pawn) || book == null)
            {
                return false;
            }

            if (!forced && IsOnAutomaticReadCooldown(pawn))
            {
                return false;
            }

            if (book.Destroyed
                || book.IsForbidden(pawn)
                || book.IsBurning()
                || book.Map != pawn.Map
                || !pawn.CanReserveAndReach(book, PathEndMode.Touch, Danger.Some, 1, 1))
            {
                return false;
            }

            if (pawn.Map.designationManager.DesignationOn(book, DesignationDefOf.Haul) != null)
            {
                return false;
            }

            return book.ScoreComp != null && book.ScoreComp.CanStudy(pawn, out _);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(MX_QHDefOf.MX_QH_ReadSkillBook, t);
        }

        private static bool CanConsiderPawn(Pawn pawn)
        {
            return pawn != null
                && pawn.Map != null
                && !pawn.Downed
                && MX_QHCharacterUtility.IsQinghe(pawn);
        }

        public static void NotifyPawnReadSkillBook(Pawn pawn)
        {
            if (pawn == null || Current.Game == null)
            {
                return;
            }

            nextAutomaticReadTickByPawn[pawn.thingIDNumber] = Find.TickManager.TicksGame + AutomaticReadCooldownTicks;
        }

        private static bool IsOnAutomaticReadCooldown(Pawn pawn)
        {
            if (pawn == null || Current.Game == null)
            {
                return false;
            }

            int pawnId = pawn.thingIDNumber;
            int nextTick;
            if (!nextAutomaticReadTickByPawn.TryGetValue(pawnId, out nextTick))
            {
                return false;
            }

            if (Find.TickManager.TicksGame < nextTick)
            {
                return true;
            }

            nextAutomaticReadTickByPawn.Remove(pawnId);
            return false;
        }
    }
}
