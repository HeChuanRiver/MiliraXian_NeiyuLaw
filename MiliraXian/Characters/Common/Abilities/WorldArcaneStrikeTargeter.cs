using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Common.Abilities
{
    [StaticConstructorOnStartup]
    internal sealed class WorldArcaneStrikeTargeter
    {
        private const float OverlayAltitude = 0.08f;

        private static readonly Material BorderMaterial = MaterialPool.MatFrom(
            GenDraw.LineTexPath, ShaderDatabase.WorldOverlayTransparent,
            new Color(0.3f, 0.9f, 1f), WorldMaterials.WorldLineRenderQueue);

        private readonly Ability ability;
        private readonly Map sourceMap;
        private readonly PlanetTile sourceTile;
        private readonly ImpactPreview preview;

        public WorldArcaneStrikeTargeter(Ability ability)
        {
            this.ability = ability;
            sourceMap = ability.pawn.Map;
            sourceTile = sourceMap.Tile;
            preview = new ImpactPreview(ability.def.EffectRadius);
        }

        public static bool CasterHasWorldTile(Ability ability, out string reason)
        {
            Pawn caster = ability?.pawn;
            Map map = caster?.Map;
            if (caster == null || caster.Destroyed || caster.Dead || caster.Downed
                || !caster.Spawned || map == null
                || Find.World == null || !Find.Maps.Contains(map)
                || !map.Tile.Valid || !Find.WorldGrid.InBounds(map.Tile))
            {
                reason = "施法者必须位于有效的世界地图地块中。";
                return false;
            }

            reason = null;
            return true;
        }

        public static bool CasterCanStart(Ability ability, out string reason)
        {
            if (!CasterHasWorldTile(ability, out reason))
            {
                return false;
            }
            Pawn caster = ability.pawn;
            if (caster.jobs == null || !caster.jobs.IsCurrentJobPlayerInterruptible()
                || (caster.CurJob?.def.forceCompleteBeforeNextJob ?? false))
            {
                reason = "施法者当前工作无法中断。";
                return false;
            }
            if (caster.InMentalState || caster.Deathresting)
            {
                reason = "施法者当前无法吟唱。";
                return false;
            }

            reason = null;
            return true;
        }

        public void BeginTargeting()
        {
            Find.DesignatorManager.Deselect();
            Find.Targeter.StopTargeting();
            Find.WorldTargeter.StopTargeting();
            CameraJumper.TryJump(sourceTile);

            // StopTargeting clears the action/update/label callbacks, but in this version
            // retains canSelectTarget. That callback owns only managed preview data,
            // never this session/Pawn. There is no range limit from sourceTile.
            Find.WorldTargeter.BeginTargeting(
                ConfirmTarget,
                canTargetTiles: true,
                mouseAttachment: ability.def.uiIcon,
                closeWorldTabWhenFinished: false,
                onUpdate: DrawPreview,
                extraLabelGetter: TargetLabel,
                canSelectTarget: preview.CanSelectTarget,
                showCancelButton: true);
        }

        private static bool IsSelectableTarget(GlobalTargetInfo target)
        {
            return target.IsValid && target.Tile.Valid && Find.World != null
                && Find.WorldGrid.InBounds(target.Tile);
        }

        private bool SourceStillAvailable()
        {
            return CasterCanStart(ability, out _)
                && ability.pawn.Map == sourceMap && sourceMap.Tile == sourceTile
                && !ability.GizmoDisabled(out _);
        }

        private void DrawPreview()
        {
            if (!SourceStillAvailable())
            {
                Find.WorldTargeter.StopTargeting();
                return;
            }
            preview.Draw();
        }

        private TaggedString TargetLabel(GlobalTargetInfo target)
        {
            if (!IsSelectableTarget(target))
            {
                return "请选择有效的世界地图地块。".Colorize(Color.red);
            }
            return $"世界格 {target.Tile.tileId} · 影响半径{preview.Radius:0.#}格".Colorize(Color.green);
        }

        private bool ConfirmTarget(GlobalTargetInfo target)
        {
            if (!SourceStillAvailable())
            {
                Find.WorldTargeter.StopTargeting();
                return false;
            }
            if (!IsSelectableTarget(target))
            {
                return false;
            }

            PlanetTile tile = target.Tile;
            Find.WorldTargeter.StopTargeting();
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                ArcaneImpactUtility.Confirmation(tile, preview.Radius),
                () => StartConfirmedCast(tile), destructive: true, title: "确认毁灭打击"));
            return true;
        }

        private void StartConfirmedCast(PlanetTile tile)
        {
            if (!SourceStillAvailable() || !IsSelectableTarget(new GlobalTargetInfo(tile)))
            {
                Messages.Message("施法者或目标已不可用，未发动打击。", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Pawn caster = ability.pawn;
            Job cast = ability.GetJob(caster.Position, LocalTargetInfo.Invalid);
            // Store only the tile, so removing a targeted world object cannot erase it.
            cast.globalTarget = new GlobalTargetInfo(tile);
            cast.playerForced = true;

            // Release the world callbacks before the job returns to the local map.
            Find.WorldTargeter.StopTargeting();
            caster.jobs.StartJob(cast, JobCondition.InterruptForced);
        }

        private sealed class ImpactPreview
        {
            private const int CircleSegments = 64;

            public readonly float Radius;
            private readonly Vector3[] circle = new Vector3[CircleSegments];
            private PlanetTile candidate = PlanetTile.Invalid;
            private int candidateFrame = -1;
            private int cachedTileId = -1;
            private int cachedLayerId = -1;

            public ImpactPreview(float radius)
            {
                Radius = radius;
            }

            public bool CanSelectTarget(GlobalTargetInfo target)
            {
                if (!IsSelectableTarget(target))
                {
                    candidate = PlanetTile.Invalid;
                    return false;
                }

                // Reuse the exact target chosen by WorldTargeter, including world objects.
                candidate = target.Tile;
                candidateFrame = Time.frameCount;
                return true;
            }

            public void Draw()
            {
                // Invalid/blocked hover skips the native predicate, so discard stale previews.
                if (!candidate.Valid || candidateFrame != Time.frameCount
                    || Mouse.IsInputBlockedNow || candidate.Layer != PlanetLayer.Selected)
                {
                    return;
                }

                PlanetLayer layer = candidate.Layer;
                if (cachedTileId != candidate.tileId || cachedLayerId != layer.LayerID)
                {
                    CacheCircle(layer);
                }

                for (int i = 0; i < CircleSegments; i++)
                {
                    GenDraw.DrawWorldLineBetween(circle[i], circle[(i + 1) % CircleSegments],
                        BorderMaterial, 0.4f);
                }
            }

            private void CacheCircle(PlanetLayer layer)
            {
                WorldGrid grid = Find.WorldGrid;
                Vector3 center = grid.GetTileCenter(candidate);
                Vector3 normal = center.normalized;
                WorldRendererUtility.GetTangentsToPlanet(center, out Vector3 first, out Vector3 second);

                // Convert world tiles to a spherical angle through the installed WorldGrid API.
                float radiusAngle = Mathf.Clamp(grid.TileRadiusToAngle(layer, Radius) * Mathf.Deg2Rad,
                    0f, Mathf.PI);
                float radial = Mathf.Sin(radiusAngle);
                float axial = Mathf.Cos(radiusAngle);
                float drawRadius = layer.Radius + OverlayAltitude;
                for (int i = 0; i < CircleSegments; i++)
                {
                    float angle = 2f * Mathf.PI * i / CircleSegments;
                    Vector3 tangent = first * Mathf.Cos(angle) + second * Mathf.Sin(angle);
                    circle[i] = (normal * axial + tangent * radial) * drawRadius;
                }

                cachedTileId = candidate.tileId;
                cachedLayerId = layer.LayerID;
            }
        }
    }
}
