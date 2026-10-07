using System;
using MiliraXian.Characters.Common.Abilities;
using MiliraXian.Characters.Common.Vfx;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Common.Things
{
    public sealed class ArcaneImpactProperties : DefModExtension
    {
        public BiomeDef scorchedBiome;
        public BiomeDef scorchedVacuumBiome;
        public float durationSeconds = 20f;
        public float cloudHeightInTiles = 9f;
        public SoundDef impactSound;
    }

    public sealed class WorldObject_ArcaneImpact : WorldObject
    {
        private int startTick = -1;
        private int durationTicks;
        private int layerId;
        private float impactRadius = 5f;
        private float cloudHeight;
        private bool applied;
        private ArcaneImpactVfx visual;

        public float ImpactRadius => impactRadius;
        public float CloudHeight => cloudHeight;
        public float Seconds => Mathf.Max(0, Find.TickManager.TicksGame - startTick) / 60f;
        public float Progress => Mathf.Clamp01((Find.TickManager.TicksGame - startTick) / (float)Mathf.Max(1, durationTicks));
        public override bool ShowRelatedQuests => false;

        public void Initialize(PlanetTile target, float radius)
        {
            Tile = target;
            layerId = target.Layer.LayerID;
            impactRadius = Mathf.Max(0f, radius);
            ArcaneImpactProperties props = def.GetModExtension<ArcaneImpactProperties>();
            durationTicks = Mathf.Max(1, (props?.durationSeconds ?? 20f).SecondsToTicks());
            cloudHeight = target.Layer.AverageTileSize * Mathf.Max(1f, props?.cloudHeightInTiles ?? 9f);
            startTick = Find.TickManager.TicksGame;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "startTick", -1);
            Scribe_Values.Look(ref durationTicks, "durationTicks");
            Scribe_Values.Look(ref layerId, "layerId");
            Scribe_Values.Look(ref impactRadius, "impactRadius", 5f);
            Scribe_Values.Look(ref cloudHeight, "cloudHeight");
            Scribe_Values.Look(ref applied, "applied");
        }

        protected override void Tick()
        {
            base.Tick();
            if (Destroyed) return;
            if (startTick < 0 || durationTicks <= 0 || !WorldObject_ArcaneProjectile.TileAvailable(Tile, layerId))
            {
                Destroy();
                return;
            }
            if (!applied)
            {
                // The effect alone owns impact settlement. Loading cannot repeat it,
                // including when another mod throws from a native death/quest callback.
                applied = true;
                ArcaneImpactProperties props = def.GetModExtension<ArcaneImpactProperties>();
                try { ArcaneImpactUtility.Apply(this, props); }
                catch (Exception exception)
                {
                    Log.Error("[WorldArcaneStrike] Impact settlement was interrupted: " + exception);
                    Messages.Message("奥术打击结算被模组回调中断，部分效果可能未完成，请查看日志。", MessageTypeDefOf.RejectInput, false);
                }
                ArcaneProjectileVfx.FrameImpact(this);
                props?.impactSound?.PlayOneShot(SoundInfo.OnCamera());
            }
            if (Progress >= 1f) Destroy();
        }

        public override void Draw()
        {
            if (Destroyed || startTick < 0 || !WorldRendererUtility.WorldSelected || Tile.Layer != PlanetLayer.Selected) return;
            if (visual == null || visual.Disposed) visual = new ArcaneImpactVfx();
            visual.Draw(this);
        }

        public override void PostRemove()
        {
            visual?.Dispose();
            visual = null;
            base.PostRemove();
        }
    }
}
