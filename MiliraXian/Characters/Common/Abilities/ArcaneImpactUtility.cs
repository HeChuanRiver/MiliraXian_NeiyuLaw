using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MiliraXian.Characters.Common.Things;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Abilities
{
    // One circle calculation serves the confirmation and the impact. Distances are angular,
    // matching WorldArcaneStrike's preview, rather than counting hex-grid neighbours.
    internal static class ArcaneImpactUtility
    {
        // Verified in Assembly-CSharp 1.6.9676.17735. Tile exposes biome/mutator changes
        // publicly, but provides no method to invalidate these three derived caches.
        private static readonly AccessTools.FieldRef<Tile, bool?> SecondaryCached =
            AccessTools.FieldRefAccess<Tile, bool?>("tmpHasSecondaryBiome");
        private static readonly AccessTools.FieldRef<Tile, BiomeDef> SecondaryBiome =
            AccessTools.FieldRefAccess<Tile, BiomeDef>("tmpSecondaryBiome");
        private static readonly AccessTools.FieldRef<Tile, Hilliness?> HillsCached =
            AccessTools.FieldRefAccess<Tile, Hilliness?>("hillinessLabelCached");

        internal static HashSet<PlanetTile> CollectTiles(PlanetTile center, float radius)
        {
            HashSet<PlanetTile> result = new();
            if (!center.Valid || !Find.WorldGrid.InBounds(center)) return result;
            WorldGrid grid = Find.WorldGrid;
            Vector3 direction = grid.GetTileCenter(center).normalized;
            float angle = Mathf.Clamp(grid.TileRadiusToAngle(center.Layer, radius) * Mathf.Deg2Rad, 0f, Mathf.PI);
            float threshold = Mathf.Cos(angle);
            Queue<PlanetTile> pending = new();
            HashSet<PlanetTile> visited = new() { center };
            List<PlanetTile> neighbours = new();
            pending.Enqueue(center);
            while (pending.Count > 0)
            {
                PlanetTile tile = pending.Dequeue();
                if (tile != center && Vector3.Dot(direction, grid.GetTileCenter(tile).normalized) < threshold - 0.0000001f)
                    continue;
                result.Add(tile);
                grid.GetTileNeighbors(tile, neighbours);
                for (int i = 0; i < neighbours.Count; i++)
                    if (visited.Add(neighbours[i])) pending.Enqueue(neighbours[i]);
            }
            return result;
        }

        private static bool AffectedObject(WorldObject obj, HashSet<PlanetTile> tiles)
        {
            return obj != null && !obj.Destroyed && tiles.Contains(obj.Tile)
                && obj is not WorldObject_ArcaneProjectile && obj is not WorldObject_ArcaneImpact
                && (obj is MapParent || obj is Caravan || obj is IThingHolder);
        }

        private static bool Holding(WorldObject obj)
        {
            return obj is MapParent && obj is not DestroyedSettlement && obj is not AbandonedSettlement
                && !obj.Destroyed && obj.Faction != null;
        }

        internal static string Confirmation(PlanetTile target, float radius)
        {
            HashSet<PlanetTile> tiles = CollectTiles(target, radius);
            StringBuilder text = new();
            text.Append($"目标：世界格 {target.tileId}\n影响半径：{radius:0.#} 个世界格（{tiles.Count} 个地块）\n\n");
            text.Append("范围内的地块将永久化为焦土。据点、已加载地图、商队及运输队将被摧毁，范围内单位承受致命打击。既有免死与复活机制仍可生效。\n\n");
            int count = 0, friendly = 0;
            foreach (WorldObject obj in Find.WorldObjects.AllWorldObjects)
            {
                if (!AffectedObject(obj, tiles)) continue;
                count++;
                if (obj.Faction != null && (obj.Faction.IsPlayer || !obj.Faction.HostileTo(Faction.OfPlayer))) friendly++;
                if (count <= 12) text.AppendLine($"• {obj.LabelCap}（{obj.Faction?.Name ?? "无派系"}）");
            }
            if (count == 0) text.AppendLine("当前范围内未发现据点或运输队。");
            if (count > 12) text.AppendLine($"另有 {count - 12} 个目标。");
            if (friendly > 0) text.AppendLine($"\n警告：其中 {friendly} 个目标属于玩家或非敌对派系，也将被摧毁。");
            text.Append("\n仅当被摧毁据点所属派系已无任何据点时，该派系才会灭亡。吟唱和飞行期间进入范围的目标也会受影响。\n\n确认发动毁灭打击？");
            return text.ToString();
        }

        internal static void Apply(WorldObject_ArcaneImpact impact, ArcaneImpactProperties props)
        {
            if (props?.scorchedBiome == null || props.scorchedVacuumBiome == null)
                throw new InvalidOperationException("Arcane impact requires both scorched biome definitions.");
            HashSet<PlanetTile> tiles = CollectTiles(impact.Tile, impact.ImpactRadius);
            HashSet<PlanetTile> recache = new(tiles);
            List<PlanetTile> neighbours = new();
            bool terrainChanged = false, hillsChanged = false, roadsChanged = false, riversChanged = false;
            bool landmarksChanged = false;
            foreach (PlanetTile tile in tiles)
            {
                Tile data = Find.WorldGrid[tile];
                bool vacuum = data.PrimaryBiome.inVacuum;
                BiomeDef scorchedBiome = vacuum ? props.scorchedVacuumBiome : props.scorchedBiome;
                terrainChanged |= data.PrimaryBiome != scorchedBiome;
                hillsChanged |= data.hilliness != Hilliness.Flat;
                landmarksChanged |= data.Landmark != null;
                data.PrimaryBiome = scorchedBiome;
                data.hilliness = Hilliness.Flat;
                // Water-covered surface tiles must become land, otherwise local generation
                // and world coast rendering would continue treating the ash as ocean.
                if (data is SurfaceTile surface)
                {
                    float elevation = Mathf.Max(100f, data.elevation);
                    terrainChanged |= data.elevation != elevation;
                    data.elevation = elevation;
                    roadsChanged |= surface.potentialRoads?.Count > 0;
                    riversChanged |= surface.potentialRivers?.Count > 0;
                    Find.WorldGrid.GetTileNeighbors(tile, neighbours);
                    for (int i = 0; i < neighbours.Count; i++)
                    {
                        PlanetTile neighbour = neighbours[i];
                        if (Find.WorldGrid[neighbour] is SurfaceTile adjacent)
                        {
                            roadsChanged |= adjacent.potentialRoads?.RemoveAll(link => link.neighbor == tile) > 0;
                            riversChanged |= adjacent.potentialRivers?.RemoveAll(link => link.neighbor == tile) > 0;
                            if (adjacent.potentialRoads?.Count == 0) adjacent.potentialRoads = null;
                            if (adjacent.potentialRivers?.Count == 0) adjacent.potentialRivers = null;
                            recache.Add(neighbour);
                        }
                    }
                    // Native world UI treats a non-null road list as containing at least
                    // one link. Represent removed roads/rivers as absent, including neighbours.
                    surface.potentialRoads = null;
                    surface.potentialRivers = null;
                    surface.riverDist = 0;
                }
                data.swampiness = 0f;
                data.mutatorsNullable?.Clear();
                SecondaryCached(data) = false;
                SecondaryBiome(data) = null;
                HillsCached(data) = null;
                Find.World.landmarks?.RemoveLandmark(tile);
            }
            bool passabilityChanged = false;
            foreach (PlanetTile tile in recache)
            {
                Find.WorldPathGrid.RecalculatePerceivedMovementDifficultyAt(tile, out bool changed);
                passabilityChanged |= changed;
            }
            // ClearCache flood-fills every planet layer and invalidates its finder. Native
            // path recalculation needs this only when a tile actually changes passability.
            if (passabilityChanged) Find.WorldReachability.ClearCache();
            else
                foreach (PlanetTile tile in tiles) tile.Layer.FastTileFinder.DirtyTile(tile);
            // A cleared finder rebuilds on its next Query/Closest, as in native path changes.
            // Do not force a second whole-layer rebuild at the moment of impact.
            PlanetLayer layer = impact.Tile.Layer;
            if (terrainChanged || landmarksChanged) layer.SetDirty<WorldDrawLayer_Terrain>();
            if (hillsChanged) layer.SetDirty<WorldDrawLayer_Hills>();
            if (roadsChanged) layer.SetDirty<WorldDrawLayer_Roads>();
            if (riversChanged) layer.SetDirty<WorldDrawLayer_Rivers>();
            // Nearby landmarks may orient to newly changed coast biomes.
            if (terrainChanged || landmarksChanged) layer.SetDirty<WorldDrawLayer_Landmarks>();
            // Keep mod-defined draw layers notified; their dependencies cannot be inferred
            // from the vanilla layers. Native object removal refreshes world-object icons.
            foreach (WorldDrawLayer drawLayer in layer.WorldDrawLayers)
                if (drawLayer.GetType().Assembly != typeof(WorldDrawLayer).Assembly) drawLayer.SetDirty();

            // Snapshot before Kill/Destroy: native callbacks may remove caravans, maps or
            // other quest objects from their owner collections during this operation.
            List<WorldObject> objects = Find.WorldObjects.AllWorldObjects.FindAll(obj => AffectedObject(obj, tiles));
            HashSet<Faction> factions = new();
            foreach (WorldObject obj in objects)
                if (Holding(obj)) factions.Add(obj.Faction);
            List<Map> maps = Find.Maps.FindAll(map => tiles.Contains(map.Tile));
            // Pocket maps inherit the destruction of their enclosing map, even when their
            // own synthetic tile is not on the struck planet layer.
            for (int i = 0; i < maps.Count; i++)
                foreach (PocketMapParent pocket in Find.World.pocketMaps)
                    if (pocket.sourceMap == maps[i] && pocket.Map != null && !maps.Contains(pocket.Map)) maps.Add(pocket.Map);
            HashSet<Pawn> victims = new();
            List<Pawn> mapPawns = new();
            foreach (Map map in maps)
            {
                ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.Pawn), mapPawns);
                foreach (Pawn pawn in mapPawns) victims.Add(pawn);
            }
            List<Thing> held = new();
            foreach (WorldObject obj in objects)
                if (obj is IThingHolder holder)
                {
                    ThingOwnerUtility.GetAllThingsRecursively(holder, held);
                    foreach (Thing thing in held)
                        if (thing is Pawn pawn) victims.Add(pawn);
                }

            int failures = 0;
            foreach (Pawn pawn in victims)
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed) continue;
                try { pawn.Kill(null); } // Preserve all existing native/mod death and rebirth hooks.
                catch (Exception exception) { failures++; Log.Error($"[WorldArcaneStrike] Death callback failed for {pawn}: {exception}"); }
            }
            // Live pawns kept by immunity/rebirth hooks must not be destroyed with a
            // container or silently re-factioned by map abandonment.
            foreach (Pawn pawn in victims)
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed) continue;
                try
                {
                    Faction faction = pawn.Faction;
                    PawnKindDef kind = pawn.kindDef;
                    if (pawn.Spawned) pawn.DeSpawn();
                    pawn.holdingOwner?.Remove(pawn);
                    if (!pawn.IsWorldPawn()) Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                    if (pawn.Faction != faction) pawn.SetFaction(faction);
                    if (pawn.kindDef != kind) pawn.ChangeKind(kind);
                }
                catch (Exception exception) { failures++; Log.Error($"[WorldArcaneStrike] Immune pawn handoff failed for {pawn}: {exception}"); }
            }
            // Children first, so native parent removal never abandons live pocket-map occupants.
            for (int i = maps.Count - 1; i >= 0; i--)
            {
                Map map = maps[i];
                if (!Find.Maps.Contains(map)) continue;
                try
                {
                    if (map.Parent is PocketMapParent) PocketMapUtility.DestroyPocketMap(map);
                    else Current.Game.DeinitAndRemoveMap(map, notifyPlayer: false);
                }
                catch (Exception exception) { failures++; Log.Error($"[WorldArcaneStrike] Map removal failed for {map}: {exception}"); }
            }
            int destroyed = 0;
            foreach (WorldObject obj in objects)
            {
                try
                {
                    if (!obj.Destroyed) obj.Destroy();
                    if (obj.Destroyed) destroyed++;
                }
                catch (Exception exception) { failures++; Log.Error($"[WorldArcaneStrike] World object removal failed for {obj}: {exception}"); }
            }
            StringBuilder extinct = new();
            foreach (Faction faction in factions)
            {
                if (faction.defeated || Find.WorldObjects.AllWorldObjects.Exists(obj => Holding(obj) && obj.Faction == faction)) continue;
                // Keep the faction object for quest, relation and save references, as native
                // settlement defeat does; removing it from FactionManager breaks those references.
                faction.defeated = true;
                extinct.AppendLine(faction.Name);
            }
            string message = $"世界格 {impact.Tile.tileId} 周围 {tiles.Count} 个地块已化为焦土，{destroyed} 个据点或运输队被摧毁。";
            if (extinct.Length > 0) message += "\n\n以下派系已无据点：\n" + extinct;
            if (failures > 0) message += $"\n有 {failures} 次模组回调未能完成，请查看日志；部分毁灭结算可能未完成。";
            Find.LetterStack.ReceiveLetter("奥术毁灭打击", message, LetterDefOf.NegativeEvent, new GlobalTargetInfo(impact.Tile));
            Log.Message($"[WorldArcaneStrike] Scorched {tiles.Count} tiles; destroyed {destroyed} world objects; callback failures: {failures}.");
        }
    }

    public sealed class BiomeWorker_ArcaneScorched : BiomeWorker
    {
        public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile) => float.MinValue;
    }
}
