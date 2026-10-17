using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.Common.Skeletal2D
{
    /// <summary>
    /// Mesh asset bound to a Skeletal2DRigDef. Mesh data (rest positions, UVs, triangles,
    /// per-vertex bone weights by bone name) is authored in Blender and exported as JSON;
    /// rig.meshAssetPath locates the file relative to the mod root. One asset per path,
    /// reloadable via the "Reload Skeletal2D assets" debug action. Skinning runs on the GPU
    /// (see Skeletal2DGpuSkin). Asset JSON shape:
    /// { "vertices": [[x,y],...], "uvs": [[u,v],...], "triangles": [a,b,c,...],
    ///   "weights": [[{"bone":"PetalC","weight":0.8},...], ...] } (one entry per vertex).
    /// </summary>
    public static class Skeletal2DSkinnedMesh
    {
        internal sealed class AssetData
        {
            public float[][] vertices { get; set; }
            public float[][] uvs { get; set; }
            public int[] triangles { get; set; }
            public WeightEntry[][] weights { get; set; }
        }

        internal sealed class WeightEntry
        {
            public string bone { get; set; }
            public float weight { get; set; }
        }

        private static readonly Dictionary<string, Skeletal2DJsonAsset<AssetData>> Assets = new();

        /// <summary>The rig's mesh asset, or null if the rig has no mesh path or no bones.</summary>
        internal static Skeletal2DJsonAsset<AssetData> GetAsset(Skeletal2DRigDef rig)
        {
            string path = rig.meshAssetPath ?? "";
            if (path.NullOrEmpty() || rig.bones.NullOrEmpty())
            {
                Log.ErrorOnce(
                    $"[MiliraXian] Skeletal2D rig '{rig.defName}' has no meshAssetPath or no bones.",
                    GenText.StableStringHash(rig.defName));
                return null;
            }
            if (!Assets.TryGetValue(path, out Skeletal2DJsonAsset<AssetData> asset))
            {
                asset = new Skeletal2DJsonAsset<AssetData>(path, Validate);
                Assets[path] = asset;
            }
            return asset;
        }

        private static string Validate(AssetData data)
        {
            if (data.vertices == null || data.uvs == null || data.triangles == null || data.weights == null)
            {
                return "missing vertices, uvs, triangles or weights";
            }
            if (data.uvs.Length != data.vertices.Length || data.weights.Length != data.vertices.Length)
            {
                return $"vertices ({data.vertices.Length}), uvs ({data.uvs.Length}) and weights ({data.weights.Length}) differ in length";
            }
            if (data.triangles.Length % 3 != 0)
            {
                return $"triangle index count {data.triangles.Length} is not a multiple of 3";
            }
            return null;
        }
    }
}
