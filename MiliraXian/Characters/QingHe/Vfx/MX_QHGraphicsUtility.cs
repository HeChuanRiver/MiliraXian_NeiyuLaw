using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Vfx
{
    [StaticConstructorOnStartup]
    public static class MX_QHGraphicsUtility
    {
        private const string FieldEdgeTexPath = "Misc/FieldEdge";
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly List<IntVec3> ringDrawCells = new();
        private static readonly List<Matrix4x4> instancingMatrices = new();
        private static readonly bool[] rotNeeded = new bool[4];
        private static readonly Dictionary<int, Material> fieldEdgeMaterialsByRenderQueue = new();
        private static readonly MaterialPropertyBlock fieldEdgePropertyBlock = new();
        private static readonly Dictionary<int, List<RelativeFieldEdge>> relativeFieldEdgesByCellCount = new();
        private static readonly IntVec3[] cardinalOffsets =
        {
            IntVec3.North,
            IntVec3.East,
            IntVec3.South,
            IntVec3.West
        };
        private static Texture fieldEdgeTexture;
        private static BoolGrid fieldGrid;
        private static bool maxRadiusMessaged;

        public static void Fx(Map map, IntVec3 cell, string defName, float scale = 1f)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map) || defName.NullOrEmpty())
            {
                return;
            }

            var def = DefDatabase<EffecterDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return;
            }

            var fx = def.Spawn(cell, map, Mathf.Max(0.01f, scale));
            fx?.Trigger(new TargetInfo(cell, map), new TargetInfo(cell, map));
            fx?.Cleanup();
        }

        public static void Fleck(Map map, IntVec3 cell, string defName, float scale = 1f)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map) || defName.NullOrEmpty())
            {
                return;
            }

            var def = DefDatabase<FleckDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return;
            }

            FleckMaker.Static(cell, map, def, Mathf.Max(0.01f, scale));
        }

        public static void Fleck(Map map, Vector3 worldPos, string defName, float scale = 1f)
        {
            if (map == null || defName.NullOrEmpty())
            {
                return;
            }

            var def = DefDatabase<FleckDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return;
            }

            FleckMaker.Static(worldPos, map, def, Mathf.Max(0.01f, scale));
        }

        public static void Overlay(Map map, TargetInfo source, TargetInfo target, string defName)
        {
            if (map == null || defName.NullOrEmpty() || !source.IsValid || !target.IsValid)
            {
                return;
            }

            if (source.Map != map || target.Map != map)
            {
                return;
            }

            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return;
            }

            MoteMaker.MakeInteractionOverlay(def, source, target);
        }

        public static bool Mote(Map map, IntVec3 cell, Thing mote, WipeMode wipeMode = WipeMode.Vanish)
        {
            if (map == null || mote == null || !cell.IsValid || !cell.InBounds(map))
            {
                return false;
            }

            GenSpawn.Spawn(mote, cell, map, wipeMode);
            return true;
        }

        public static Material FieldEdgeMaterial(int renderQueue = 2900)
        {
            if (fieldEdgeTexture == null)
            {
                var baseMaterial = MatLoader.LoadMat(FieldEdgeTexPath, renderQueue);
                if (baseMaterial == null)
                {
                    return null;
                }

                fieldEdgeTexture = baseMaterial.mainTexture;
                fieldEdgeTexture.wrapMode = TextureWrapMode.Clamp;
            }

            if (!fieldEdgeMaterialsByRenderQueue.TryGetValue(renderQueue, out Material material))
            {
                material = new Material(ShaderDatabase.Transparent)
                {
                    mainTexture = fieldEdgeTexture,
                    color = Color.white,
                    renderQueue = renderQueue,
                    enableInstancing = true
                };
                fieldEdgeMaterialsByRenderQueue[renderQueue] = material;
            }

            return material;
        }

        public static void DrawRadiusRingWithMaterial(
            IntVec3 center,
            float radius,
            Material material,
            Color color,
            Map map = null,
            Func<IntVec3, bool> predicate = null,
            float? altOffset = null,
            HashSet<IntVec3> ignoreBorderCells = null,
            int renderQueue = 2900)
        {
            if (radius > GenRadial.MaxRadialPatternRadius)
            {
                if (!maxRadiusMessaged)
                {
                    Log.Error("Cannot draw radius ring of radius " + radius.ToString() + ": not enough squares in the precalculated list.");
                    maxRadiusMessaged = true;
                }
                return;
            }

            if (predicate == null && ignoreBorderCells == null)
            {
                if (map == null)
                {
                    map = Find.CurrentMap;
                }

                if (map != null)
                {
                    DrawCachedRadiusEdges(center, radius, material, color, map, altOffset);
                    return;
                }
            }

            ringDrawCells.Clear();
            int count = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < count; i++)
            {
                IntVec3 cell = center + GenRadial.RadialPattern[i];
                if (predicate == null || predicate(cell))
                {
                    ringDrawCells.Add(cell);
                }
            }

            DrawFieldEdgesWithMaterial(ringDrawCells, material, color, map, altOffset, ignoreBorderCells, renderQueue);
        }

        public static void DrawFieldEdgesWithMaterial(
            List<IntVec3> cells,
            Material material,
            Color color,
            Map map = null,
            float? altOffset = null,
            HashSet<IntVec3> ignoreBorderCells = null,
            int renderQueue = 2900)
        {
            if (cells.NullOrEmpty() || material == null)
            {
                return;
            }

            if (map == null)
            {
                map = Find.CurrentMap;
            }
            if (map == null)
            {
                return;
            }

            if (fieldGrid == null)
            {
                fieldGrid = new(map);
            }
            else
            {
                fieldGrid.ClearAndResizeTo(map);
            }

            int mapWidth = map.Size.x;
            int mapHeight = map.Size.z;
            int count = cells.Count;
            float y = altOffset ?? (Rand.ValueSeeded(material.color.ToOpaque().GetHashCode()) * 0.03658537f / 10f);

            for (int i = 0; i < count; i++)
            {
                IntVec3 cell = cells[i];
                if (cell.InBounds(map))
                {
                    fieldGrid[cell.x, cell.z] = true;
                }
            }

            instancingMatrices.Clear();
            for (int i = 0; i < count; i++)
            {
                IntVec3 cell = cells[i];
                if (!cell.InBounds(map))
                {
                    continue;
                }

                rotNeeded[0] = cell.z < mapHeight - 1
                    && !fieldGrid[cell.x, cell.z + 1]
                    && (ignoreBorderCells == null || !ignoreBorderCells.Contains(cell + IntVec3.North));
                rotNeeded[1] = cell.x < mapWidth - 1
                    && !fieldGrid[cell.x + 1, cell.z]
                    && (ignoreBorderCells == null || !ignoreBorderCells.Contains(cell + IntVec3.East));
                rotNeeded[2] = cell.z > 0
                    && !fieldGrid[cell.x, cell.z - 1]
                    && (ignoreBorderCells == null || !ignoreBorderCells.Contains(cell + IntVec3.South));
                rotNeeded[3] = cell.x > 0
                    && !fieldGrid[cell.x - 1, cell.z]
                    && (ignoreBorderCells == null || !ignoreBorderCells.Contains(cell + IntVec3.West));

                for (int j = 0; j < 4; j++)
                {
                    if (rotNeeded[j])
                    {
                        instancingMatrices.Add(Matrix4x4.TRS(
                            cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays) + new Vector3(0f, y, 0f),
                            new Rot4(j).AsQuat,
                            Vector3.one));
                    }
                }
            }

            if (instancingMatrices.Count > 0)
            {
                fieldEdgePropertyBlock.Clear();
                fieldEdgePropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
                Graphics.DrawMeshInstanced(MeshPool.plane10, 0, material, instancingMatrices, fieldEdgePropertyBlock);
                fieldEdgePropertyBlock.Clear();
            }
        }

        private static void DrawCachedRadiusEdges(IntVec3 center, float radius, Material material, Color color, Map map, float? altOffset)
        {
            if (material == null)
            {
                return;
            }

            int cellCount = GenRadial.NumCellsInRadius(radius);
            if (!relativeFieldEdgesByCellCount.TryGetValue(cellCount, out List<RelativeFieldEdge> edges))
            {
                edges = BuildRelativeFieldEdges(cellCount);
                relativeFieldEdgesByCellCount[cellCount] = edges;
            }

            float y = altOffset ?? (Rand.ValueSeeded(material.color.ToOpaque().GetHashCode()) * 0.03658537f / 10f);
            instancingMatrices.Clear();
            for (int i = 0; i < edges.Count; i++)
            {
                RelativeFieldEdge edge = edges[i];
                IntVec3 cell = center + edge.cellOffset;
                IntVec3 neighbor = cell + cardinalOffsets[edge.rotation];
                if (!cell.InBounds(map) || !neighbor.InBounds(map))
                {
                    continue;
                }

                instancingMatrices.Add(Matrix4x4.TRS(
                    cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays) + new Vector3(0f, y, 0f),
                    new Rot4(edge.rotation).AsQuat,
                    Vector3.one));
            }

            if (instancingMatrices.Count > 0)
            {
                fieldEdgePropertyBlock.Clear();
                fieldEdgePropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
                Graphics.DrawMeshInstanced(MeshPool.plane10, 0, material, instancingMatrices, fieldEdgePropertyBlock);
                fieldEdgePropertyBlock.Clear();
            }
        }

        private static List<RelativeFieldEdge> BuildRelativeFieldEdges(int cellCount)
        {
            HashSet<IntVec3> cells = new();
            for (int i = 0; i < cellCount; i++)
            {
                cells.Add(GenRadial.RadialPattern[i]);
            }

            List<RelativeFieldEdge> edges = new();
            for (int i = 0; i < cellCount; i++)
            {
                IntVec3 cell = GenRadial.RadialPattern[i];
                for (int rotation = 0; rotation < cardinalOffsets.Length; rotation++)
                {
                    if (!cells.Contains(cell + cardinalOffsets[rotation]))
                    {
                        edges.Add(new RelativeFieldEdge(cell, rotation));
                    }
                }
            }

            return edges;
        }

        private struct RelativeFieldEdge
        {
            public readonly IntVec3 cellOffset;
            public readonly int rotation;

            public RelativeFieldEdge(IntVec3 cellOffset, int rotation)
            {
                this.cellOffset = cellOffset;
                this.rotation = rotation;
            }
        }
    }
}
