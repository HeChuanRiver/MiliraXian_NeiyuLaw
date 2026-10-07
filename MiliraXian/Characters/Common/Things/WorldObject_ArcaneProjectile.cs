using MiliraXian.Characters.Common.Vfx;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Things
{
    public sealed class ArcaneProjectileProperties : DefModExtension
    {
        public WorldObjectDef impactDef;
        public float flightDurationSeconds = 18f;
        public float arcHeight = 28f;
        public float trailProgressLength = 0.28f;
        public float sizeInTiles = 2.4f;
    }

    public sealed class WorldObject_ArcaneProjectile : WorldObject
    {
        private Pawn caster;
        private PlanetTile sourceTile = PlanetTile.Invalid;
        private PlanetTile targetTile = PlanetTile.Invalid;
        private int sourceLayerId;
        private int targetLayerId;
        private int launchTick = -1;
        private int durationTicks;
        private float arcHeight;
        private float trailLength;
        private float drawSize;
        private Vector3 origin;
        private Vector3 startDirection;
        private Vector3 rotationAxis;
        private float angleDegrees;
        private float startRadius;
        private float endRadius;
        private bool impacted;
        private float impactRadius = 5f;
        private WorldObjectDef impactDef;
        private ArcaneProjectileVfx visual;

        public PlanetTile SourceTile => sourceTile;
        public PlanetTile TargetTile => targetTile;
        public Vector3 Origin => origin;
        public float DrawSize => drawSize;
        public float TrailLength => trailLength;
        public float FlightProgress => Mathf.Clamp01((Find.TickManager.TicksGame - launchTick) / (float)Mathf.Max(1, durationTicks));
        public override Vector3 DrawPos => PositionAt(FlightProgress);
        public override bool ShowRelatedQuests => false;

        public void Initialize(Pawn sourcePawn, PlanetTile source, PlanetTile target, float radius)
        {
            caster = sourcePawn;
            sourceTile = source;
            targetTile = target;
            sourceLayerId = source.Layer.LayerID;
            targetLayerId = target.Layer.LayerID;
            Tile = source;
            launchTick = Find.TickManager.TicksGame;
            ArcaneProjectileProperties props = def.GetModExtension<ArcaneProjectileProperties>();
            impactRadius = Mathf.Max(0f, radius);
            impactDef = props?.impactDef;
            durationTicks = Mathf.Max(1, (props?.flightDurationSeconds ?? 18f).SecondsToTicks());
            arcHeight = Mathf.Max(0f, props?.arcHeight ?? 28f);
            trailLength = Mathf.Clamp(props?.trailProgressLength ?? 0.28f, 0.02f, 1f);
            drawSize = Mathf.Max(0.05f, source.Layer.AverageTileSize * (props?.sizeInTiles ?? 2.4f));

            // GetTileCenter is layer-local in 1.6; WorldObject.DrawPos adds Layer.Origin.
            origin = source.Layer.Origin;
            Vector3 start = Find.WorldGrid.GetTileCenter(source);
            Vector3 end = target.Layer.Origin + Find.WorldGrid.GetTileCenter(target) - origin;
            startRadius = start.magnitude;
            endRadius = end.magnitude;
            startDirection = start.normalized;
            Vector3 endDirection = end.normalized;
            angleDegrees = Mathf.Acos(Mathf.Clamp(Vector3.Dot(startDirection, endDirection), -1f, 1f)) * Mathf.Rad2Deg;
            rotationAxis = Vector3.Cross(startDirection, endDirection);
            if (rotationAxis.sqrMagnitude < 0.00000001f)
            {
                // Same-tile and antipodal launches need a stable, nonzero great-circle axis.
                WorldRendererUtility.GetTangentsToPlanet(startDirection, out rotationAxis, out _);
            }
            rotationAxis.Normalize();
        }

        public Vector3 PositionAt(float progress)
        {
            progress = Mathf.Clamp01(progress);
            Vector3 direction = Quaternion.AngleAxis(angleDegrees * progress, rotationAxis) * startDirection;
            float radius = Mathf.Lerp(startRadius, endRadius, progress);
            float height = Mathf.Sin(progress * Mathf.PI) * arcHeight;
            return origin + direction * (radius + height + 0.06f);
        }

        public static bool TileAvailable(PlanetTile tile, int layerId)
        {
            return tile.Valid && Find.World != null
                && Find.WorldGrid.PlanetLayers.TryGetValue(layerId, out PlanetLayer layer)
                && layer.InBounds(new PlanetTile(tile.tileId, layer));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref sourceTile, "sourceTile", PlanetTile.Invalid);
            Scribe_Values.Look(ref targetTile, "targetTile", PlanetTile.Invalid);
            Scribe_Values.Look(ref sourceLayerId, "sourceLayerId");
            Scribe_Values.Look(ref targetLayerId, "targetLayerId");
            Scribe_Values.Look(ref launchTick, "launchTick", -1);
            Scribe_Values.Look(ref durationTicks, "durationTicks");
            Scribe_Values.Look(ref arcHeight, "arcHeight");
            Scribe_Values.Look(ref trailLength, "trailLength", 0.28f);
            Scribe_Values.Look(ref drawSize, "drawSize", 1f);
            Scribe_Values.Look(ref origin, "origin", Vector3.zero);
            Scribe_Values.Look(ref startDirection, "startDirection", Vector3.zero);
            Scribe_Values.Look(ref rotationAxis, "rotationAxis", Vector3.up);
            Scribe_Values.Look(ref angleDegrees, "angleDegrees");
            Scribe_Values.Look(ref startRadius, "startRadius");
            Scribe_Values.Look(ref endRadius, "endRadius");
            Scribe_Values.Look(ref impacted, "impacted");
            Scribe_Values.Look(ref impactRadius, "impactRadius", 5f);
            Scribe_Defs.Look(ref impactDef, "impactDef");
            // Flights saved before the impact phase existed inherit the new definition.
            if (Scribe.mode == LoadSaveMode.PostLoadInit && impactDef == null)
                impactDef = def.GetModExtension<ArcaneProjectileProperties>()?.impactDef;
        }

        protected override void Tick()
        {
            base.Tick();
            if (Destroyed) return;
            if (impacted || launchTick < 0 || durationTicks <= 0
                || !TileAvailable(sourceTile, sourceLayerId) || !TileAvailable(targetTile, targetLayerId))
            {
                Destroy();
                return;
            }
            if (FlightProgress < 1f) return;
            impacted = true;
            Log.Message("Arcane projectile impact.");
            if (impactDef?.worldObjectClass == typeof(WorldObject_ArcaneImpact))
            {
                WorldObject_ArcaneImpact impact = (WorldObject_ArcaneImpact)WorldObjectMaker.MakeWorldObject(impactDef);
                impact.Initialize(targetTile, impactRadius);
                Find.WorldObjects.Add(impact);
            }
            else Log.Error("[WorldArcaneStrike] Missing ArcaneImpact world object definition.");
            Destroy();
        }

        public override void Draw()
        {
            if (Destroyed || impacted || durationTicks <= 0 || !WorldRendererUtility.WorldSelected) return;
            if (visual == null || visual.Disposed)
            {
                ArcaneImpactVfx.Preload(impactDef?.GetModExtension<ArcaneImpactProperties>());
                visual = new ArcaneProjectileVfx();
            }
            visual.Draw(this);
        }

        public override string GetInspectString()
        {
            return $"世界格 {sourceTile} → {targetTile}\n飞行进度：{FlightProgress:P0}";
        }

        public override void PostRemove()
        {
            visual?.Dispose();
            visual = null;
            caster = null;
            base.PostRemove();
        }
    }
}
