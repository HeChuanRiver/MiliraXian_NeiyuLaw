using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Jobs
{
    public class JobDriver_StudyMusicScoreAtLotusPond : JobDriver
    {
        private const TargetIndex PondIndex = TargetIndex.A;
        private const TargetIndex ScoreIndex = TargetIndex.B;
        private const TargetIndex HaulCellIndex = TargetIndex.C;
        private const int StudyTicks = 600;

        private Thing Score => job.GetTarget(ScoreIndex).Thing;
        private Building_LotusPond LotusPond => job.GetTarget(PondIndex).Thing as Building_LotusPond;
        private Comp_QingheMusicScore ScoreComp => Score?.TryGetComp<Comp_QingheMusicScore>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(LotusPond, job, 1, -1, null, errorOnFailed)
                   && pawn.Reserve(Score, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !MX_QHUtility.IsQinghe(pawn));
            this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
            this.FailOnDespawnedNullOrForbidden(PondIndex);
            this.FailOnBurningImmobile(PondIndex);
            this.FailOn(() => ScoreComp == null || ScoreComp.UnlocksTree == null);

            yield return Toils_General.DoAtomic(delegate
            {
                job.count = 1;
            });
            yield return Toils_Reserve.Reserve(ScoreIndex);
            yield return Toils_Goto.GotoThing(ScoreIndex, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(ScoreIndex)
                .FailOnSomeonePhysicallyInteracting(ScoreIndex);
            yield return TryCarryScoreToil();
            yield return Toils_Goto.GotoThing(PondIndex, PathEndMode.InteractionCell);
            yield return DropCarriedScoreToil();
            yield return Toils_General.Wait(StudyTicks)
                .FailOnDestroyedNullOrForbidden(ScoreIndex)
                .FailOnDestroyedNullOrForbidden(PondIndex)
                .FailOnCannotTouch(PondIndex, PathEndMode.InteractionCell)
                .WithProgressBarToilDelay(PondIndex);

            Toil finish = ToilMaker.MakeToil("FinishStudyingQingheMusicScore");
            finish.initAction = delegate
            {
                Comp_QingheMusicScore scoreComp = ScoreComp;
                HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
                if (scoreComp == null || state == null)
                {
                    Messages.Message("清荷暂时无法研读这份曲谱。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                state.UnlockTree(scoreComp.UnlocksTree);
                if (scoreComp.ExperienceGain > 0f)
                {
                    state.AddExperience(scoreComp.ExperienceGain);
                }

                string scoreLabel = Score?.LabelNoCount ?? scoreComp.parent.LabelNoCount;
                Messages.Message("清荷研读了《" + scoreLabel + "》，解锁曲谱集：" + scoreComp.UnlocksTreeLabel + "。", pawn, MessageTypeDefOf.PositiveEvent, historical: false);
                Thing carried = pawn.carryTracker?.CarriedThing;
                if (carried != null && carried.TryGetComp<Comp_QingheMusicScore>() == scoreComp)
                {
                    carried.Destroy();
                }
                else
                {
                    Score?.Destroy();
                }
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        private Toil TryCarryScoreToil()
        {
            Toil toil = ToilMaker.MakeToil("TryCarryQingheMusicScore");
            toil.initAction = delegate
            {
                Thing score = Score;
                if (score == null || score.Destroyed || !score.Spawned)
                {
                    FailStudyJob("曲谱不在地图上，无法拾取。");
                    return;
                }

                if (pawn.carryTracker.AvailableStackSpace(score.def) <= 0)
                {
                    FailStudyJob("清荷已经携带了其他物品，无法拿起曲谱。");
                    return;
                }

                int taken = pawn.carryTracker.TryStartCarry(score, 1);
                if (taken <= 0 || pawn.carryTracker.CarriedThing == null)
                {
                    FailStudyJob("清荷未能拿起曲谱。");
                    return;
                }

                job.SetTarget(ScoreIndex, pawn.carryTracker.CarriedThing);
                job.count = 1;
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private Toil DropCarriedScoreToil()
        {
            Toil toil = ToilMaker.MakeToil("DropQingheMusicScoreAtLotusPond");
            toil.initAction = delegate
            {
                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried == null || carried.TryGetComp<Comp_QingheMusicScore>() == null)
                {
                    FailStudyJob("清荷没有携带可研读的曲谱。");
                    return;
                }

                IntVec3 cell = job.GetTarget(HaulCellIndex).Cell;
                if (!pawn.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Near, out Thing dropped))
                {
                    FailStudyJob("清荷无法在荷池旁放下曲谱。");
                    return;
                }

                job.SetTarget(ScoreIndex, dropped);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private void FailStudyJob(string reason)
        {
            Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
            Log.Warning("[MiliraXian] Qinghe music score study job failed: " + reason + " Pawn=" + pawn + " Job=" + job);
            pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
        }
    }
}
