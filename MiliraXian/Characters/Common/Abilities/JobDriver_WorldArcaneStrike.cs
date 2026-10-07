using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using MiliraXian.Characters.Common.Vfx;

namespace MiliraXian.Characters.Common.Abilities
{
    internal enum WorldArcaneStrikeState
    {
        None,
        ReturningToCaster,
        Chanting,
        Finished,
        Cancelled
    }

    public sealed class JobDriver_WorldArcaneStrike : JobDriver
    {
        private Map sourceMap;
        private PlanetTile sourceTile = PlanetTile.Invalid;
        private WorldArcaneStrikeState state;
        private int stateStartTick = -1;
        private int chantDurationTicks;
        private ArcaneCircleVFXController circleVfx;

        // The enclosing Pawn/JobTracker saves caster, and Job saves ability/globalTarget.
        // Do not keep another copy of either reference or of the target tile.
        public Pawn Caster => pawn;
        public PlanetTile SourceTile => sourceTile;
        public PlanetTile TargetTile => job?.globalTarget.Tile ?? PlanetTile.Invalid;
        public Map SourceMap => sourceMap;
        public bool IsChanting => state == WorldArcaneStrikeState.Chanting && !ended;
        public float ChantProgress => state == WorldArcaneStrikeState.Chanting
            ? Mathf.Clamp01(1f - ticksLeftThisToil / (float)Mathf.Max(1, chantDurationTicks))
            : 0f;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            sourceMap = pawn.Map;
            sourceTile = sourceMap?.Tile ?? PlanetTile.Invalid;
            chantDurationTicks = Mathf.Max(1, (job.ability?.def.verbProperties.warmupTime ?? 60f).SecondsToTicks());
            state = WorldArcaneStrikeState.ReturningToCaster;
            stateStartTick = Find.TickManager.TicksGame;
        }

        public override string GetReport()
        {
            if (state != WorldArcaneStrikeState.Chanting)
            {
                return base.GetReport();
            }
            float elapsedSeconds = ChantProgress * chantDurationTicks / 60f;
            return $"[Debug] Chanting {ChantProgress:P0} · {elapsedSeconds:0.0}/{chantDurationTicks / 60f:0.0}秒"
                + $" · World Tile {sourceTile} → {TargetTile}";
        }

        public override void ExposeData()
        {
            // Read our fields before base rebuilds toils in PostLoadInit.
            Scribe_References.Look(ref sourceMap, "arcaneStrikeSourceMap");
            Scribe_Values.Look(ref sourceTile, "arcaneStrikeSourceTile", PlanetTile.Invalid);
            Scribe_Values.Look(ref state, "arcaneStrikeState", WorldArcaneStrikeState.None);
            Scribe_Values.Look(ref stateStartTick, "arcaneStrikeStateStartTick", -1);
            Scribe_Values.Look(ref chantDurationTicks, "arcaneStrikeChantDurationTicks", 0);
            base.ExposeData();
            // Unity objects are not saved. LoadedGame rebuilds the visual from this progress.
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(FinishCast);
            AddFailCondition(CastIsInvalid);

            Toil returnToCaster = ToilMaker.MakeToil("WorldArcaneStrike_ReturnToCaster");
            returnToCaster.defaultCompleteMode = ToilCompleteMode.Instant;
            returnToCaster.initAction = delegate
            {
                CameraJumper.TryJumpAndSelect(pawn, CameraJumper.MovementMode.Cut);
                if (Find.CurrentMap != sourceMap || WorldRendererUtility.WorldSelected)
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            yield return returnToCaster;

            Toil chant = ToilMaker.MakeToil("WorldArcaneStrike_Chant");
            chant.defaultCompleteMode = ToilCompleteMode.Delay;
            chant.defaultDuration = Mathf.Max(1, chantDurationTicks);
            chant.initAction = delegate
            {
                state = WorldArcaneStrikeState.Chanting;
                stateStartTick = Find.TickManager.TicksGame;
                pawn.pather.StopDead();
                RefreshChantVisuals();
            };
            chant.tickAction = delegate
            {
                pawn.pather.StopDead();
                RefreshChantVisuals();
            };
            chant.WithProgressBar(TargetIndex.None, () => ChantProgress, alwaysShow: true);
            yield return chant;

            Toil launchReady = ToilMaker.MakeToil("WorldArcaneStrike_LaunchReady");
            launchReady.defaultCompleteMode = ToilCompleteMode.Instant;
            launchReady.initAction = delegate
            {
                state = WorldArcaneStrikeState.Finished;
                stateStartTick = Find.TickManager.TicksGame;
                Log.Message("WorldArcaneStrike launch ready.");
                Find.World.GetComponent<WorldComponent_ArcaneStrikeLaunch>().BeginLaunch(this);
            };
            yield return launchReady;
        }

        private bool CastIsInvalid()
        {
            Ability ability = job?.ability;
            return pawn == null || pawn.Destroyed || pawn.Dead || pawn.Downed || !pawn.Spawned
                || pawn.InMentalState || pawn.Deathresting
                || sourceMap == null || !Find.Maps.Contains(sourceMap) || pawn.Map != sourceMap
                || sourceMap.Tile != sourceTile || pawn.Position != job.targetA.Cell
                || !sourceTile.Valid || !TargetTile.Valid || Find.World == null
                || !Find.WorldGrid.InBounds(TargetTile)
                || chantDurationTicks <= 0
                || ability == null || ability.pawn != pawn || !ability.CanCast.Accepted
                || pawn.abilities == null || !pawn.abilities.AllAbilitiesForReading.Contains(ability);
        }

        public void RefreshChantVisuals()
        {
            if (IsChanting && circleVfx == null)
            {
                circleVfx = ArcaneCircleVFXController.Create(this);
            }
        }

        private void FinishCast(JobCondition condition)
        {
            state = condition == JobCondition.Succeeded && state == WorldArcaneStrikeState.Finished
                ? WorldArcaneStrikeState.Finished : WorldArcaneStrikeState.Cancelled;
            circleVfx?.Cleanup();
            circleVfx = null;
            sourceMap = null;
            sourceTile = PlanetTile.Invalid;
            stateStartTick = -1;
            chantDurationTicks = 0;
            if (job != null)
            {
                job.globalTarget = GlobalTargetInfo.Invalid;
                job.targetA = LocalTargetInfo.Invalid;
                job.ability = null;
                job.verbToUse = null;
            }
        }
    }
}
