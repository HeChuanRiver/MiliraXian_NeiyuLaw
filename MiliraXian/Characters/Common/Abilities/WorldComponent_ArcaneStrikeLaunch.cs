using System;
using System.Collections.Generic;
using MiliraXian.Characters.Common.Things;
using MiliraXian.Characters.Common.Vfx;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Common.Abilities
{
    public sealed class ArcaneStrikeLaunchProperties : DefModExtension
    {
        public WorldObjectDef projectileDef;
        public float beamDurationSeconds = 4f;
        public float beamHeight = 32f;
        public float beamWidth = 2.7f;
        public SoundDef launchSound;
    }

    // The finished chant hands off once. Only this component owns the local launch phase.
    public sealed class WorldComponent_ArcaneStrikeLaunch : WorldComponent
    {
        private List<ArcaneLaunchState> launches = new();

        public WorldComponent_ArcaneStrikeLaunch(World world) : base(world) { }

        public void BeginLaunch(JobDriver_WorldArcaneStrike chant)
        {
            ArcaneStrikeLaunchProperties props = chant.Caster.CurJob.ability.def
                .GetModExtension<ArcaneStrikeLaunchProperties>();
            if (props?.projectileDef?.worldObjectClass != typeof(WorldObject_ArcaneProjectile))
            {
                Log.Error("[WorldArcaneStrike] Missing ArcaneProjectile world object definition.");
                return;
            }
            for (int i = 0; i < launches.Count; i++)
                if (launches[i].Caster == chant.Caster) return;

            ArcaneLaunchState launch = new()
            {
                Caster = chant.Caster,
                SourceMap = chant.SourceMap,
                SourceTile = chant.SourceTile,
                TargetTile = chant.TargetTile,
                SourceLayerId = chant.SourceTile.Layer.LayerID,
                TargetLayerId = chant.TargetTile.Layer.LayerID,
                Position = chant.Caster.DrawPos,
                Cell = chant.Caster.Position,
                ProjectileDef = props.projectileDef,
                ImpactRadius = chant.Caster.CurJob.ability.def.EffectRadius,
                StartTick = Find.TickManager.TicksGame,
                DurationTicks = Mathf.Max(1, props.beamDurationSeconds.SecondsToTicks()),
                Height = Mathf.Max(1f, props.beamHeight),
                Width = Mathf.Max(0.1f, props.beamWidth)
            };
            launches.Add(launch);
            CameraJumper.TryJump(launch.Cell, launch.SourceMap, CameraJumper.MovementMode.Cut);
            props.launchSound?.PlayOneShot(SoundInfo.InMap(new TargetInfo(launch.Cell, launch.SourceMap)));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref launches, "arcaneStrikeLaunches", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && launches == null)
                launches = new List<ArcaneLaunchState>();
        }

        public override void WorldComponentTick()
        {
            for (int i = launches.Count - 1; i >= 0; i--)
            {
                ArcaneLaunchState launch = launches[i];
                if (launch == null || !launch.Available)
                {
                    launch?.ClearReferences();
                    launches.RemoveAt(i);
                    continue;
                }
                if (launch.Progress < 1f) continue;

                // Remove the pending phase before handing off, so it cannot fire twice.
                launches.RemoveAt(i);
                WorldObject_ArcaneProjectile projectile = null;
                try
                {
                    projectile = (WorldObject_ArcaneProjectile)WorldObjectMaker.MakeWorldObject(launch.ProjectileDef);
                    projectile.Initialize(launch.Caster, launch.SourceTile, launch.TargetTile, launch.ImpactRadius);
                    Find.WorldObjects.Add(projectile);
                    ArcaneProjectileVfx.FrameFlight(projectile);
                }
                catch (Exception exception)
                {
                    if (projectile != null && !projectile.Destroyed) projectile.Destroy();
                    Log.Error("[WorldArcaneStrike] Projectile launch failed: " + exception);
                }
                finally
                {
                    launch.ClearReferences();
                }
            }
        }

        public override void WorldComponentUpdate()
        {
            if (Current.ProgramState != ProgramState.Playing || WorldRendererUtility.WorldSelected) return;
            for (int i = 0; i < launches.Count; i++)
            {
                ArcaneLaunchState launch = launches[i];
                if (launch != null && launch.Available && launch.SourceMap == Find.CurrentMap)
                {
                    ArcaneLaunchVfx.Draw(launch);
                    if (!Find.TickManager.Paused && launch.Progress < 0.8f)
                        Find.CameraDriver.shaker.SetMinShake(0.06f * Mathf.Sin(launch.Progress * Mathf.PI));
                }
            }
        }
    }

    public sealed class ArcaneLaunchState : IExposable
    {
        public Pawn Caster;
        public Map SourceMap;
        public PlanetTile SourceTile = PlanetTile.Invalid;
        public PlanetTile TargetTile = PlanetTile.Invalid;
        public int SourceLayerId;
        public int TargetLayerId;
        public WorldObjectDef ProjectileDef;
        public Vector3 Position;
        public IntVec3 Cell;
        public int StartTick;
        public int DurationTicks;
        public float Height;
        public float Width;
        public float ImpactRadius = 5f;

        public float Progress => Mathf.Clamp01((Find.TickManager.TicksGame - StartTick) / (float)Mathf.Max(1, DurationTicks));
        public bool Available => Caster != null && !Caster.Destroyed && !Caster.Dead && !Caster.Downed
            && Caster.Spawned && !Caster.InMentalState && !Caster.Deathresting
            && SourceMap != null && Find.Maps.Contains(SourceMap) && Caster.Map == SourceMap
            && SourceMap.Tile == SourceTile && Cell.InBounds(SourceMap) && ProjectileDef != null
            && DurationTicks > 0 && WorldObject_ArcaneProjectile.TileAvailable(SourceTile, SourceLayerId)
            && WorldObject_ArcaneProjectile.TileAvailable(TargetTile, TargetLayerId);

        public void ExposeData()
        {
            Scribe_References.Look(ref Caster, "caster");
            Scribe_References.Look(ref SourceMap, "sourceMap");
            Scribe_Defs.Look(ref ProjectileDef, "projectileDef");
            Scribe_Values.Look(ref SourceTile, "sourceTile", PlanetTile.Invalid);
            Scribe_Values.Look(ref TargetTile, "targetTile", PlanetTile.Invalid);
            Scribe_Values.Look(ref SourceLayerId, "sourceLayerId");
            Scribe_Values.Look(ref TargetLayerId, "targetLayerId");
            Scribe_Values.Look(ref Position, "position", Vector3.zero);
            Scribe_Values.Look(ref Cell, "cell");
            Scribe_Values.Look(ref StartTick, "startTick");
            Scribe_Values.Look(ref DurationTicks, "durationTicks");
            Scribe_Values.Look(ref Height, "height", 32f);
            Scribe_Values.Look(ref Width, "width", 2.7f);
            Scribe_Values.Look(ref ImpactRadius, "impactRadius", 5f);
        }

        public void ClearReferences()
        {
            Caster = null;
            SourceMap = null;
            ProjectileDef = null;
        }
    }
}
