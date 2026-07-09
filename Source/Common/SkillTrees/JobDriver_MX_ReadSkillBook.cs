using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters
{
    public class JobDriver_MX_ReadSkillBook : JobDriver
    {
        private bool hasInInventory;
        private bool carrying;
        private bool isLearningDesire;
        private bool isReading;
        private bool forcedSkillReadUntilLearned;

        public const TargetIndex BookIndex = TargetIndex.A;
        public const TargetIndex SurfaceIndex = TargetIndex.B;

        private const int ManualReadTicks = 5000;
        private const int ChairSearchRadius = 32;
        private const int UrgentJobCheckIntervalTicks = 600;

        public Book Book => job.GetTarget(TargetIndex.A).Thing as Book;

        public bool IsReading => isReading;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Book, job, 1, 1, null, errorOnFailed);
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            job.count = 1;
            hasInInventory = pawn.inventory != null && pawn.inventory.Contains(Book);
            carrying = pawn?.carryTracker.CarriedThing == Book;
            isLearningDesire = pawn?.learning != null && pawn.learning.ActiveLearningDesires.Contains(LearningDesireDefOf.Reading);
            CacheForcedSkillReadTarget();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SetFinalizerJob(delegate(JobCondition condition)
            {
                ClearForcedSkillReadTarget();
                if (!pawn.IsCarryingThing(Book))
                {
                    return null;
                }
                if (condition != JobCondition.Succeeded)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out var _);
                    return null;
                }
                return HaulAIUtility.HaulToStorageJob(pawn, Book, forced: false);
            });

            foreach (Toil item in PrepareToReadBook())
            {
                yield return item;
            }

            int duration = job.playerForced ? ManualReadTicks : job.def.joyDuration;
            yield return ReadBook(duration);
        }

        private IEnumerable<Toil> PrepareToReadBook()
        {
            if (carrying)
            {
                yield break;
            }

            if (hasInInventory)
            {
                yield return Toils_Misc.TakeItemFromInventoryToCarrier(pawn, TargetIndex.A);
            }
            else
            {
                yield return Toils_Goto.GotoCell(Book.PositionHeld, PathEndMode.ClosestTouch)
                    .FailOnDestroyedOrNull(TargetIndex.A)
                    .FailOnSomeonePhysicallyInteracting(TargetIndex.A);
                yield return Toils_Haul.StartCarryThing(
                    TargetIndex.A,
                    putRemainderInQueue: false,
                    subtractNumTakenFromJobCount: false,
                    failIfStackCountLessThanJobCount: false,
                    reserve: true,
                    canTakeFromInventory: true);
            }

            yield return CarryToReadingSpot().FailOnDestroyedOrNull(TargetIndex.A);
            yield return FindAdjacentReadingSurface();
        }

        private Toil ReadBook(int duration)
        {
            Toil toil = Toils_General.Wait(duration);
            toil.debugName = "Reading";
            toil.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            toil.handlingFacing = true;
            toil.initAction = delegate
            {
                CacheForcedSkillReadTarget();
                Book.IsOpen = true;
                pawn.pather.StopDead();
                job.showCarryingInspectLine = false;
            };
            toil.tickIntervalAction = delegate(int delta)
            {
                if (job.GetTarget(TargetIndex.B).IsValid)
                {
                    pawn.rotationTracker.FaceCell(job.GetTarget(TargetIndex.B).Cell);
                }
                else if (Book.Spawned)
                {
                    pawn.rotationTracker.FaceCell(Book.Position);
                }
                else if (pawn.Rotation == Rot4.North)
                {
                    pawn.Rotation = new Rot4(Rand.Range(1, 4));
                }

                float readingBonus = BookUtility.GetReadingBonus(pawn);
                isReading = true;
                Book.OnBookReadTick(pawn, delta, readingBonus);
                pawn.skills?.Learn(SkillDefOf.Intellectual, 0.1f * delta);
                pawn.GainComfortFromCellIfPossible(delta);

                if (pawn.CurJob != null && pawn.needs?.joy != null)
                {
                    JoyTickFullJoyAction fullJoyAction = JoyTickFullJoyAction.GoToNextToil;
                    if (pawn.CurJob.playerForced || pawn.learning != null)
                    {
                        fullJoyAction = JoyTickFullJoyAction.None;
                    }
                    JoyUtility.JoyTickCheckEnd(pawn, delta, fullJoyAction, Book.JoyFactor * readingBonus);
                }

                if (isLearningDesire && job != null)
                {
                    if (pawn.needs?.learning != null)
                    {
                        LearningUtility.LearningTickCheckEnd(pawn, delta, job.playerForced);
                    }
                    else
                    {
                        pawn.jobs.curDriver.EndJobWith(JobCondition.Succeeded);
                    }
                }

                if (pawn.IsHashIntervalTick(UrgentJobCheckIntervalTicks, delta))
                {
                    pawn.jobs.CheckForJobOverride(9.1f);
                }
            };
            toil.AddEndCondition(delegate
            {
                if (!BookUtility.CanReadBook(Book, pawn, out _))
                {
                    return JobCondition.InterruptForced;
                }

                return forcedSkillReadUntilLearned && SkillBook?.CachedSkillStudyTargetLearned(pawn) == true
                    ? JobCondition.Succeeded
                    : JobCondition.Ongoing;
            });
            toil.AddFinishAction(delegate
            {
                Book.IsOpen = false;
                TaleRecorder.RecordTale(TaleDefOf.ReadBook, pawn, Book);
                JoyUtility.TryGainRecRoomThought(pawn);
                ClearForcedSkillReadTarget();
            });
            if ((isLearningDesire && !job.playerForced) || forcedSkillReadUntilLearned)
            {
                toil.defaultCompleteMode = ToilCompleteMode.Never;
            }
            toil.WithProgressBar(TargetIndex.A, GetReadProgress);
            return toil;
        }

        private float GetReadProgress()
        {
            Thing_MX_SkillBook skillBook = SkillBook;
            return skillBook == null ? 0f : skillBook.GetSkillStudyProgressPercent(pawn);
        }

        private Thing_MX_SkillBook SkillBook => Book as Thing_MX_SkillBook;

        private void CacheForcedSkillReadTarget()
        {
            if (!job.playerForced)
            {
                return;
            }

            forcedSkillReadUntilLearned = SkillBook?.CacheSkillStudyTargetsForJob(pawn) == true;
        }

        private void ClearForcedSkillReadTarget()
        {
            if (forcedSkillReadUntilLearned)
            {
                SkillBook?.ClearCachedSkillStudyTargets(pawn);
                forcedSkillReadUntilLearned = false;
            }
        }

        private Toil CarryToReadingSpot()
        {
            Toil toil = ToilMaker.MakeToil("CarryToReadingSpot");
            toil.initAction = delegate
            {
                if (!TryGetClosestChairFreeSittingSpot(skipInteractionCells: true, out IntVec3 cell)
                    && !TryGetClosestChairFreeSittingSpot(skipInteractionCells: false, out cell))
                {
                    cell = RCellFinder.SpotToChewStandingNear(pawn, Book, c => !c.Fogged(pawn.Map) && pawn.CanReserveSittableOrSpot(c));
                }
                if (!cell.IsValid)
                {
                    pawn.pather.StartPath(pawn.Position, PathEndMode.OnCell);
                }
                else
                {
                    pawn.ReserveSittableOrSpot(cell, pawn.CurJob);
                    pawn.Map.pawnDestinationReservationManager.Reserve(pawn, pawn.CurJob, cell);
                    pawn.pather.StartPath(cell, PathEndMode.OnCell);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
        }

        private bool TryGetClosestChairFreeSittingSpot(bool skipInteractionCells, out IntVec3 cell)
        {
            Thing thing = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
                PathEndMode.OnCell,
                TraverseParms.For(pawn),
                ChairSearchRadius,
                t => ValidateChair(t, pawn, skipInteractionCells) && t.Position.GetDangerFor(pawn, t.Map) == Danger.None);
            if (thing != null)
            {
                return TryFindFreeSittingSpotOnThing(thing, pawn, skipInteractionCells, out cell);
            }
            cell = IntVec3.Invalid;
            return false;
        }

        private Toil FindAdjacentReadingSurface()
        {
            Toil toil = ToilMaker.MakeToil("FindAdjacentReadingSurface");
            toil.initAction = delegate
            {
                Map map = pawn.Map;
                IntVec3 position = pawn.Position;
                Building firstThing = pawn.Position.GetFirstThing<Building>(pawn.Map);
                if (firstThing != null && firstThing.def.building != null && firstThing.def.building.isSittable)
                {
                    if (!TryFaceClosestSurface(position, map))
                    {
                        job.SetTarget(TargetIndex.B, position + firstThing.Rotation.FacingCell);
                        pawn.jobs.curDriver.rotateToFace = TargetIndex.B;
                    }
                }
                else
                {
                    TryFaceClosestSurface(position, map);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private bool TryFaceClosestSurface(IntVec3 pos, Map map)
        {
            for (int i = 0; i < 4; i++)
            {
                IntVec3 cell = pos + new Rot4(i).FacingCell;
                if (cell.GetSurfaceType(map) == SurfaceType.Eat)
                {
                    job.SetTarget(TargetIndex.B, cell);
                    pawn.jobs.curDriver.rotateToFace = TargetIndex.B;
                    return true;
                }
            }
            for (int i = 0; i < 4; i++)
            {
                IntVec3 cell = pos + new Rot4(i).FacingCell;
                if (cell.GetSurfaceType(map) == SurfaceType.Item)
                {
                    job.SetTarget(TargetIndex.B, cell);
                    pawn.jobs.curDriver.rotateToFace = TargetIndex.B;
                    return true;
                }
            }
            return false;
        }

        private static bool ValidateChair(Thing t, Pawn pawn, bool skipInteractionCells)
        {
            if (t.def.building == null || !t.def.building.isSittable)
            {
                return false;
            }
            if (!TryFindFreeSittingSpotOnThing(t, pawn, skipInteractionCells, out _))
            {
                return false;
            }
            if (t.Fogged() || t.IsForbidden(pawn) || !pawn.CanReserve(t) || !t.IsSociallyProper(pawn) || t.IsBurning() || t.HostileTo(pawn))
            {
                return false;
            }
            return true;
        }

        private static bool TryFindFreeSittingSpotOnThing(Thing t, Pawn pawn, bool skipInteractionCells, out IntVec3 cell)
        {
            foreach (IntVec3 occupiedCell in t.OccupiedRect())
            {
                if ((!skipInteractionCells || !occupiedCell.IsBuildingInteractionCell(pawn.Map))
                    && !occupiedCell.Fogged(pawn.Map)
                    && pawn.CanReserveSittableOrSpot(occupiedCell))
                {
                    cell = occupiedCell;
                    return true;
                }
            }
            cell = default;
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref carrying, "carrying", false);
            Scribe_Values.Look(ref hasInInventory, "hasInInventory", false);
            Scribe_Values.Look(ref isLearningDesire, "wasLearningDesire", false);
            Scribe_Values.Look(ref forcedSkillReadUntilLearned, "mx_forcedSkillReadUntilLearned", false);
        }
    }
}
