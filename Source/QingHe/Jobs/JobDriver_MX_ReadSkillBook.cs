using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Jobs
{
    public class JobDriver_MX_ReadSkillBook : JobDriver
    {
        private bool hasInInventory;
        private bool carrying;
        private bool isReading;

        private Thing_QingheMusicScoreBook Book => job.GetTarget(TargetIndex.A).Thing as Thing_QingheMusicScoreBook;

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
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            SetFinalizerJob(delegate(JobCondition condition)
            {
                if (!pawn.IsCarryingThing(Book))
                {
                    return null;
                }

                if (condition != JobCondition.Succeeded)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out _);
                    return null;
                }

                return HaulAIUtility.HaulToStorageJob(pawn, Book, forced: false);
            });

            foreach (Toil toil in PrepareToReadBook())
            {
                yield return toil;
            }

            yield return ReadBook();
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
                yield return Toils_Haul.StartCarryThing(TargetIndex.A, putRemainderInQueue: false, subtractNumTakenFromJobCount: false, failIfStackCountLessThanJobCount: false, reserve: true, canTakeFromInventory: true);
            }

            yield return CarryToReadingSpot().FailOnDestroyedOrNull(TargetIndex.A);
            yield return FindAdjacentReadingSurface();
        }

        private Toil ReadBook()
        {
            Toil toil = Toils_General.Wait(5000);
            toil.debugName = "ReadingQingheSkillBook";
            toil.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            toil.handlingFacing = true;
            toil.initAction = delegate
            {
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

                isReading = true;
                Book.Notify_ReadTick(pawn, delta);

                if (pawn.IsHashIntervalTick(600, delta))
                {
                    pawn.jobs.CheckForJobOverride(9.1f);
                }
            };
            toil.AddEndCondition(delegate
            {
                if (Book == null || Book.ScoreComp == null || !Book.ScoreComp.CanStudy(pawn, out _))
                {
                    return JobCondition.InterruptForced;
                }

                return JobCondition.Ongoing;
            });
            toil.AddFinishAction(delegate
            {
                if (isReading)
                {
                    WorkGiver_MX_ReadSkillBook.NotifyPawnReadSkillBook(pawn);
                    isReading = false;
                }

                if (Book != null && !Book.Destroyed)
                {
                    Book.IsOpen = false;
                }
                JoyUtility.TryGainRecRoomThought(pawn);
            });
            return toil;
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
                    return;
                }

                pawn.ReserveSittableOrSpot(cell, pawn.CurJob);
                pawn.Map.pawnDestinationReservationManager.Reserve(pawn, pawn.CurJob, cell);
                pawn.pather.StartPath(cell, PathEndMode.OnCell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
        }

        private Toil FindAdjacentReadingSurface()
        {
            Toil toil = ToilMaker.MakeToil("FindAdjacentReadingSurface");
            toil.initAction = delegate
            {
                Map map = pawn.Map;
                IntVec3 position = pawn.Position;
                Building seat = pawn.Position.GetFirstThing<Building>(pawn.Map);
                if (seat != null && seat.def.building != null && seat.def.building.isSittable)
                {
                    if (!TryFaceClosestSurface(position, map))
                    {
                        job.SetTarget(TargetIndex.B, position + seat.Rotation.FacingCell);
                        pawn.jobs.curDriver.rotateToFace = TargetIndex.B;
                    }
                    return;
                }

                TryFaceClosestSurface(position, map);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private bool TryGetClosestChairFreeSittingSpot(bool skipInteractionCells, out IntVec3 cell)
        {
            Thing chair = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
                PathEndMode.OnCell,
                TraverseParms.For(pawn),
                32f,
                t => ValidateChair(t, pawn, skipInteractionCells) && t.Position.GetDangerFor(pawn, t.Map) == Danger.None);

            if (chair != null)
            {
                return TryFindFreeSittingSpotOnThing(chair, pawn, skipInteractionCells, out cell);
            }

            cell = IntVec3.Invalid;
            return false;
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
            return t.def.building != null
                && t.def.building.isSittable
                && TryFindFreeSittingSpotOnThing(t, pawn, skipInteractionCells, out _)
                && !t.Fogged()
                && !t.IsForbidden(pawn)
                && pawn.CanReserve(t)
                && t.IsSociallyProper(pawn)
                && !t.IsBurning()
                && !t.HostileTo(pawn);
        }

        private static bool TryFindFreeSittingSpotOnThing(Thing t, Pawn pawn, bool skipInteractionCells, out IntVec3 cell)
        {
            foreach (IntVec3 occupied in t.OccupiedRect())
            {
                if ((!skipInteractionCells || !occupied.IsBuildingInteractionCell(pawn.Map))
                    && !occupied.Fogged(pawn.Map)
                    && pawn.CanReserveSittableOrSpot(occupied))
                {
                    cell = occupied;
                    return true;
                }
            }

            cell = IntVec3.Invalid;
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref carrying, "carrying", false);
            Scribe_Values.Look(ref hasInInventory, "hasInInventory", false);
        }
    }
}
