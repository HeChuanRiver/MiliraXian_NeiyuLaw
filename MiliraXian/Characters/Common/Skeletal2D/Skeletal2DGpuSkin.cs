using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Skeletal2D
{
    /// <summary>
    /// GPU skinning for Skeletal2D rigs. Builds one static mesh per rig (uploaded once):
    /// rest positions in mesh x/z, UV0 = texture UVs, UV1 = up to 4 bone indices, UV2 = the
    /// matching normalized weights. Per frame only the bone transforms are uploaded, packed
    /// two Vector4 per bone for the shader's <c>_BoneTransforms</c> storage buffer:
    /// [2b] = (colX.x, colX.y, colY.x, colY.y), [2b+1] = (origin.x, origin.y, 0, 0).
    /// Vertices without weights keep their rest position in the shader.
    /// </summary>
    public static class Skeletal2DGpuSkin
    {
        public const int MaxInfluences = 4;
        public const int VectorsPerBone = 2;

        private sealed class CachedMesh
        {
            public Mesh mesh;
            public int version;
        }

        private static readonly Dictionary<Skeletal2DRigDef, CachedMesh> MeshCache = new();

        /// <summary>
        /// Shared static skinned mesh for the rig, or null if its asset failed to load. The mesh
        /// is rebuilt after the mesh JSON is reloaded by the debug action.
        /// </summary>
        public static Mesh GetMesh(Skeletal2DRigDef rig)
        {
            Skeletal2DJsonAsset<Skeletal2DSkinnedMesh.AssetData> asset = Skeletal2DSkinnedMesh.GetAsset(rig);
            Skeletal2DSkinnedMesh.AssetData data = asset?.Data;
            if (data == null)
            {
                return null;
            }
            if (MeshCache.TryGetValue(rig, out CachedMesh cached) && cached.version == asset.Version)
            {
                return cached.mesh;
            }
            Mesh mesh = Build(rig, data);
            if (cached?.mesh != null)
            {
                Object.Destroy(cached.mesh);
            }
            MeshCache[rig] = new CachedMesh { mesh = mesh, version = asset.Version };
            return mesh;
        }

        /// <summary>Packs bone transforms into <paramref name="destination"/> (length ≥ bones × 2).</summary>
        public static void PackBones(Skeletal2DBoneTransform[] bones, Vector4[] destination)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                Skeletal2DBoneTransform bone = bones[i];
                destination[i * VectorsPerBone] = new Vector4(bone.colX.x, bone.colX.y, bone.colY.x, bone.colY.y);
                destination[i * VectorsPerBone + 1] = new Vector4(bone.origin.x, bone.origin.y, 0f, 0f);
            }
        }

        private static Mesh Build(Skeletal2DRigDef rig, Skeletal2DSkinnedMesh.AssetData data)
        {
            var boneIndexByName = new Dictionary<string, int>(rig.bones.Count);
            for (int i = 0; i < rig.bones.Count; i++)
            {
                boneIndexByName[rig.bones[i].name] = i;
            }

            int count = data.vertices.Length;
            var positions = new List<Vector3>(count);
            var uvs = new List<Vector2>(count);
            var boneIndices = new List<Vector4>(count);
            var boneWeights = new List<Vector4>(count);
            var influences = new List<(int bone, float weight)>(8);
            float[] idx = new float[MaxInfluences];
            float[] wts = new float[MaxInfluences];
            int truncated = 0;

            for (int i = 0; i < count; i++)
            {
                positions.Add(new Vector3(data.vertices[i][0], 0f, data.vertices[i][1]));
                uvs.Add(new Vector2(data.uvs[i][0], data.uvs[i][1]));

                influences.Clear();
                Skeletal2DSkinnedMesh.WeightEntry[] entries = data.weights[i];
                if (entries != null)
                {
                    for (int k = 0; k < entries.Length; k++)
                    {
                        if (entries[k].weight <= 0f)
                        {
                            continue;
                        }
                        if (!boneIndexByName.TryGetValue(entries[k].bone ?? "", out int boneIndex))
                        {
                            Log.ErrorOnce(
                                $"[MiliraXian] Skeletal2D mesh '{rig.meshAssetPath}' references unknown bone '{entries[k].bone}'.",
                                GenText.StableStringHash(rig.meshAssetPath + entries[k].bone));
                            continue;
                        }
                        influences.Add((boneIndex, entries[k].weight));
                    }
                }
                if (influences.Count > MaxInfluences)
                {
                    truncated++;
                    influences.Sort((a, b) => b.weight.CompareTo(a.weight));
                    influences.RemoveRange(MaxInfluences, influences.Count - MaxInfluences);
                }

                float sum = 0f;
                for (int k = 0; k < influences.Count; k++)
                {
                    sum += influences[k].weight;
                }
                for (int k = 0; k < MaxInfluences; k++)
                {
                    bool used = k < influences.Count && sum > 0f;
                    idx[k] = used ? influences[k].bone : 0f;
                    wts[k] = used ? influences[k].weight / sum : 0f;
                }
                boneIndices.Add(new Vector4(idx[0], idx[1], idx[2], idx[3]));
                boneWeights.Add(new Vector4(wts[0], wts[1], wts[2], wts[3]));
            }

            if (truncated > 0)
            {
                Log.Warning(
                    $"[MiliraXian] Skeletal2D mesh '{rig.meshAssetPath}': {truncated} vertices have more than "
                    + $"{MaxInfluences} bone influences; kept the {MaxInfluences} heaviest and renormalized.");
            }

            var mesh = new Mesh { name = "Skeletal2DGpu_" + rig.defName };
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, boneIndices);
            mesh.SetUVs(2, boneWeights);
            mesh.SetTriangles(new List<int>(data.triangles), 0);
            // Skinning happens in the shader, so CPU-side bounds are only the rest pose. Use a
            // generous fixed box (twice the unit quad) so animated poses are never culled.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 0.1f, 2f));
            return mesh;
        }
    }
}
