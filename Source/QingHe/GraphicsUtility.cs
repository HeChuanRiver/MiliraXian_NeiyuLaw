using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class GraphicsUtility
    {
        private const string FieldEdgeTexPath = "Misc/FieldEdge";
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly List<IntVec3> ringDrawCells = new List<IntVec3>();
        private static readonly List<Matrix4x4> instancingMatrices = new List<Matrix4x4>();
        private static readonly bool[] rotNeeded = new bool[4];
        private static readonly Dictionary<int, Material> fieldEdgeMaterialsByColor = new Dictionary<int, Material>();
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

        public static Material FieldEdgeMaterial(Color color, int renderQueue = 2900)
        {
            color.a = Mathf.Round(Mathf.Clamp01(color.a) * 31f) / 31f;

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

            Color32 color32 = color;
            int key = Gen.HashCombineInt(renderQueue, color32.r);
            key = Gen.HashCombineInt(key, color32.g);
            key = Gen.HashCombineInt(key, color32.b);
            key = Gen.HashCombineInt(key, color32.a);

            if (!fieldEdgeMaterialsByColor.TryGetValue(key, out Material material))
            {
                material = new Material(ShaderDatabase.Transparent)
                {
                    mainTexture = fieldEdgeTexture,
                    color = color32,
                    renderQueue = renderQueue,
                    enableInstancing = true
                };
                fieldEdgeMaterialsByColor[key] = material;
            }

            return material;
        }

        public static void DrawRadiusRingWithMaterial(
            IntVec3 center,
            float radius,
            Material material,
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

            DrawFieldEdgesWithMaterial(ringDrawCells, material, map, altOffset, ignoreBorderCells, renderQueue);
        }

        public static void DrawFieldEdgesWithMaterial(
            List<IntVec3> cells,
            Material material,
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
                fieldGrid = new BoolGrid(map);
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
                Graphics.DrawMeshInstanced(MeshPool.plane10, 0, material, instancingMatrices);
            }
        }
    }
}
