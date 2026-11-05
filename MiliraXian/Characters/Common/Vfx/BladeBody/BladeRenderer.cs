using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.BladeBody
{
    /// <summary>
    /// Draws a weapon texture as a rigid blade posed in 3D. The map image of the blade is an affine
    /// function of the texture coordinates (height shows as a northward offset, depth as altitude),
    /// so one quad and one matrix per frame are enough: foreshortening, the blade turning edge-on
    /// and the mirrored back face all come out of the matrix. Parts of the blade north of the body
    /// are drawn below the body's altitude and parts to the south above it; with the depth-writing
    /// cutout shader the blade is cut per pixel where it passes through the body.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BladeRenderer
    {
        /// <summary>Altitude per metre of north-south depth (layers are about 0.037 apart).</summary>
        private const float AltitudePerMetre = 0.02f;
        /// <summary>Altitude of the body centre plane above the body's base altitude (between body and head).</summary>
        private const float MidAltitude = 0.016f;

        private static readonly Dictionary<BladeGraphicExtension, Mesh> Meshes = new();
        private static readonly Dictionary<string, Material> Materials = new();

        /// <param name="bodyCenter">Drawn body centre on the map; its altitude is the body's base altitude.</param>
        public static void Draw(BladeGraphicExtension ext, in BladePose pose, Vector3 bodyCenter)
        {
            Material mat = MaterialFor(ext.texPath);
            Texture tex = mat?.mainTexture;
            if (tex == null)
            {
                return;
            }
            float w = tex.width;
            float h = tex.height;
            Vector2 pommel = Vector2.Scale(ext.pommelUV, new Vector2(w, h));
            Vector2 tip = Vector2.Scale(ext.tipUV, new Vector2(w, h));
            Vector2 grip = Vector2.Scale(ext.gripUV, new Vector2(w, h));
            Vector2 axisPx = tip - pommel;
            float lenPx = axisPx.magnitude;
            if (lenPx < 1e-3f)
            {
                return;
            }
            Vector2 a = axisPx / lenPx;
            Vector2 n = new(a.y, -a.x); // toward larger u when the blade points up the texture
            float mpp = ext.length / lenPx;

            Vector3 dir = pose.dir.normalized;
            Vector3 edge = (pose.edge - Vector3.Dot(pose.edge, dir) * dir).normalized * Mathf.Sign(ext.edgeSide == 0f ? 1f : ext.edgeSide);
            // World offset of a texture pixel P: grip + dir * ((P - G) . a) * mpp + edge * ((P - G) . n) * mpp.
            Vector3 du = dir * (a.x * w * mpp) + edge * (n.x * w * mpp);
            Vector3 dv = dir * (a.y * h * mpp) + edge * (n.y * h * mpp);
            Vector3 o = pose.grip - dir * (Vector2.Dot(grip, a) * mpp) - edge * (Vector2.Dot(grip, n) * mpp);

            float k = pose.heightToScreen;
            Vector3 cu = ToMapLinear(du, k);
            Vector3 cv = ToMapLinear(dv, k);
            Vector3 origin = bodyCenter + new Vector3(0f, MidAltitude, 0f) + ToMapLinear(o, k);

            Matrix4x4 m = Matrix4x4.identity;
            m.SetColumn(0, new Vector4(cu.x, cu.y, cu.z, 0f));
            m.SetColumn(1, new Vector4(0f, 1e-3f, 0f, 0f));
            m.SetColumn(2, new Vector4(cv.x, cv.y, cv.z, 0f));
            m.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
            Graphics.DrawMesh(MeshFor(ext), m, mat, 0);
        }

        /// <summary>World offset (x east, y height, z north) to map offset (x, altitude, z).</summary>
        private static Vector3 ToMapLinear(Vector3 v, float heightToScreen)
        {
            return new Vector3(v.x, -v.z * AltitudePerMetre, v.z + v.y * heightToScreen);
        }

        /// <summary>A quad over the calibrated texture area, vertices at (u, 0, v).</summary>
        private static Mesh MeshFor(BladeGraphicExtension ext)
        {
            if (Meshes.TryGetValue(ext, out Mesh mesh) && mesh != null)
            {
                return mesh;
            }
            Vector2 lo = ext.uvMin;
            Vector2 hi = ext.uvMax;
            mesh = new Mesh { name = "MX_BladeBody" };
            mesh.SetVertices(new List<Vector3>
            {
                new(lo.x, 0f, lo.y),
                new(lo.x, 0f, hi.y),
                new(hi.x, 0f, hi.y),
                new(hi.x, 0f, lo.y),
            });
            mesh.SetUVs(0, new List<Vector2>
            {
                new(lo.x, lo.y),
                new(lo.x, hi.y),
                new(hi.x, hi.y),
                new(hi.x, lo.y),
            });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            Meshes[ext] = mesh;
            return mesh;
        }

        private static Material MaterialFor(string texPath)
        {
            if (texPath.NullOrEmpty())
            {
                return null;
            }
            if (Materials.TryGetValue(texPath, out Material cached))
            {
                return cached;
            }
            Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail("MX_BladeBody")?.Shader;
            Texture2D tex = ContentFinder<Texture2D>.Get(texPath, reportFailure: false);
            Material mat = null;
            if (shader != null && tex != null)
            {
                mat = MaterialPool.MatFrom(tex, shader, Color.white);
            }
            else
            {
                Log.ErrorOnce($"[MiliraXian] Blade body: missing shader 'MX_BladeBody' or texture '{texPath}'.", GenText.StableStringHash("MX_BladeBody|" + texPath));
            }
            Materials[texPath] = mat;
            return mat;
        }
    }
}
