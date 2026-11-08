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
    /// The part of the blade inside a scabbard is left out, and the scabbard is drawn with the
    /// blade's own texture (grip to tip), tinted.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BladeRenderer
    {
        /// <summary>
        /// Altitude per metre of north-south depth. Pawn layers (altitude above the pawn's base,
        /// PawnRenderUtility.AltitudeForLayer): body 0, body apparel about 0.007-0.009, head 0.018,
        /// hair 0.023. With 0.05 a point 0.2 m north of the body centre is level with the body and
        /// one 0.2 m south with the head, so things worn at the hip (0.15-0.2 m out) go behind or in
        /// front of the body like the sprite's own parts; the old 0.02 needed 0.8 m.
        /// </summary>
        private const float AltitudePerMetre = 0.05f;
        /// <summary>Altitude of the body centre plane above the body's base altitude (just over the body apparel).</summary>
        private const float MidAltitude = 0.0095f;
        /// <summary>Steps of the sheathed share that get their own cut mesh.</summary>
        private const int SheatheSteps = 64;
        private const int ScabbardMeshKey = -1;

        private static readonly Dictionary<(BladeGraphicExtension, int), Mesh> Meshes = new();
        private static readonly Dictionary<(Texture2D, Color), Material> Materials = new();
        private static readonly int BladeReveal = Shader.PropertyToID("_BladeReveal");

        /// <param name="bodyCenter">Drawn body centre on the map; its altitude is the body's base altitude.</param>
        /// <param name="opacity">Share of the blade drawn (<see cref="BladeOpacity"/>): it dissolves by a noise, nothing at 0.</param>
        public static void Draw(BladeGraphicExtension ext, in BladePose pose, Vector3 bodyCenter, float opacity = 1f)
        {
            if (opacity <= 0.001f)
            {
                return;
            }
            dissolve = opacity;
            try
            {
                DrawBlade(ext, pose, bodyCenter);
            }
            finally
            {
                dissolve = 1f;
            }
        }

        /// <summary>Opacity of the blade being drawn (set by <see cref="Draw"/> for its quads).</summary>
        private static float dissolve = 1f;
        private static readonly int BladeDissolve = Shader.PropertyToID("_BladeDissolve");

        private static void DrawBlade(BladeGraphicExtension ext, in BladePose pose, Vector3 bodyCenter)
        {
            Color scabbardColor = pose.scabbardColor;
            Material mat = MaterialFor(ext, Color.white);
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
            float edgeSign = Mathf.Sign(ext.edgeSide == 0f ? 1f : ext.edgeSide);

            int q = Mathf.Clamp(Mathf.RoundToInt(pose.sheathed * SheatheSteps), 0, SheatheSteps);
            float cutV = ext.gripUV.y + (ext.tipUV.y - ext.gripUV.y) * (1f - q / (float)SheatheSteps);
            if (cutV > ext.uvMin.y + 1e-3f)
            {
                Vector3 dir = pose.dir.normalized;
                Vector3 edge = (pose.edge - Vector3.Dot(pose.edge, dir) * dir).normalized * edgeSign;
                Mesh mesh = MeshFor(ext, q, ext.uvMin.y, q == 0 ? ext.uvMax.y : Mathf.Min(cutV, ext.uvMax.y));
                // The reveal glow runs along the blade from the grip (0) to the tip (1), in texture
                // coordinates; only the blade carries it, not the scabbard.
                Vector4 reveal = default;
                float bladeLength = Vector2.Dot(tip - grip, a);
                if (pose.reveal > 0f && bladeLength > 1e-3f)
                {
                    reveal = new Vector4(a.x * w / bladeLength, a.y * h / bladeLength,
                        -Vector2.Dot(grip, a) / bladeLength, Mathf.Clamp01(pose.reveal));
                }
                DrawQuad(mesh, mat, w, h, a, n, grip, mpp, pose.grip, dir, edge, pose, bodyCenter, reveal);
            }

            if (pose.hasScabbard && pose.scabbardLength > 1e-3f)
            {
                // Grip-to-tip of the texture laid from the mouth to the end of the scabbard.
                float gripToTipPx = Vector2.Dot(tip - grip, a);
                if (gripToTipPx > 1e-3f)
                {
                    Material smat = MaterialFor(ext, scabbardColor.a <= 0f ? new Color(0.24f, 0.16f, 0.14f, 1f) : scabbardColor);
                    Vector3 sdir = pose.scabbardDir.normalized;
                    Vector3 sedge = pose.scabbardEdge - Vector3.Dot(pose.scabbardEdge, sdir) * sdir;
                    if (sedge.sqrMagnitude < 1e-6f)
                    {
                        sedge = Vector3.Cross(sdir, Vector3.up);
                        if (sedge.sqrMagnitude < 1e-6f)
                        {
                            sedge = Vector3.right;
                        }
                    }
                    sedge = sedge.normalized * edgeSign;
                    float smpp = pose.scabbardLength / gripToTipPx;
                    Mesh smesh = MeshFor(ext, ScabbardMeshKey, ext.gripUV.y, ext.uvMax.y);
                    DrawQuad(smesh, smat, w, h, a, n, grip, smpp, pose.scabbardMouth, sdir, sedge, pose, bodyCenter);
                }
            }
        }

        /// <summary>Lays texture pixel <paramref name="gripPx"/> on <paramref name="at"/>, the blade axis along <paramref name="dir"/>.</summary>
        /// <param name="reveal">The shader's _BladeReveal (w = lit fraction; 0 = no reveal glow).</param>
        private static void DrawQuad(Mesh mesh, Material mat, float w, float h, Vector2 a, Vector2 n, Vector2 gripPx, float mpp,
            Vector3 at, Vector3 dir, Vector3 edge, in BladePose pose, Vector3 bodyCenter, Vector4 reveal = default)
        {
            // World offset of a texture pixel P: at + dir * ((P - G) . a) * mpp + edge * ((P - G) . n) * mpp.
            Vector3 du = dir * (a.x * w * mpp) + edge * (n.x * w * mpp);
            Vector3 dv = dir * (a.y * h * mpp) + edge * (n.y * h * mpp);
            Vector3 o = at - dir * (Vector2.Dot(gripPx, a) * mpp) - edge * (Vector2.Dot(gripPx, n) * mpp);

            float k = pose.heightToScreen;
            float d = pose.DepthScale;
            Vector3 cu = ToMapLinear(du, k, d);
            Vector3 cv = ToMapLinear(dv, k, d);
            Vector3 origin = bodyCenter + new Vector3(0f, MidAltitude, pose.heightOrigin * k) + ToMapLinear(o, k, d);

            Matrix4x4 m = Matrix4x4.identity;
            m.SetColumn(0, new Vector4(cu.x, cu.y, cu.z, 0f));
            m.SetColumn(1, new Vector4(0f, 1e-3f, 0f, 0f));
            m.SetColumn(2, new Vector4(cv.x, cv.y, cv.z, 0f));
            m.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
            if (reveal.w <= 0f && dissolve >= 1f)
            {
                Graphics.DrawMesh(mesh, m, mat, 0);
                return;
            }
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.SetVector(BladeReveal, reveal);
            block.SetFloat(BladeDissolve, dissolve);
            Graphics.DrawMesh(mesh, m, mat, 0, null, 0, block);
            block.Clear();
        }

        /// <summary>
        /// Map positions of the grip and the blade tip, drawn as <see cref="Draw"/> lays them (the
        /// same projection, origin height and depth scale).
        /// </summary>
        public static void GetGripAndTip(BladeGraphicExtension ext, in BladePose pose, Vector3 bodyCenter,
            out Vector3 grip, out Vector3 tip)
        {
            Texture texture = MaterialFor(ext, Color.white)?.mainTexture;
            Vector2 size = texture == null ? Vector2.one : new Vector2(texture.width, texture.height);
            Vector2 axis = Vector2.Scale(ext.tipUV - ext.pommelUV, size);
            Vector2 delta = Vector2.Scale(ext.tipUV - ext.gripUV, size);
            Vector3 direction = pose.dir.normalized;
            Vector3 edge = (pose.edge - Vector3.Dot(pose.edge, direction) * direction).normalized
                * Mathf.Sign(ext.edgeSide == 0f ? 1f : ext.edgeSide);
            Vector2 side = new(axis.y, -axis.x);
            float scale = ext.length / Mathf.Max(.0001f, axis.sqrMagnitude);
            Vector3 tipOffset = direction * (Vector2.Dot(delta, axis) * scale)
                + edge * (Vector2.Dot(delta, side) * scale);
            float k = pose.heightToScreen;
            float d = pose.DepthScale;
            Vector3 center = bodyCenter + new Vector3(0f, MidAltitude, pose.heightOrigin * k);
            grip = center + ToMapLinear(pose.grip, k, d);
            tip = center + ToMapLinear(pose.grip + tipOffset, k, d);
        }

        /// <summary>World offset (x east, y height, z north) to map offset (x, altitude, z).</summary>
        private static Vector3 ToMapLinear(Vector3 v, float heightToScreen, float depthToScreen)
        {
            return new Vector3(v.x, -v.z * AltitudePerMetre, v.z * depthToScreen + v.y * heightToScreen);
        }

        /// <summary>Map position of a body-relative world offset (x east, y height, z north), as the blade is drawn.</summary>
        public static Vector3 BodyOffsetToMap(Vector3 bodyCenter, Vector3 offset, in BladePose pose)
        {
            return new Vector3(bodyCenter.x + offset.x, bodyCenter.y,
                bodyCenter.z + offset.z * pose.DepthScale + (offset.y + pose.heightOrigin) * pose.heightToScreen);
        }

        /// <summary>A quad over the calibrated texture area between two v values, vertices at (u, 0, v).</summary>
        private static Mesh MeshFor(BladeGraphicExtension ext, int key, float loY, float hiY)
        {
            if (Meshes.TryGetValue((ext, key), out Mesh mesh) && mesh != null)
            {
                return mesh;
            }
            float lx = ext.uvMin.x;
            float hx = ext.uvMax.x;
            mesh = new Mesh { name = "MX_BladeBody" };
            mesh.SetVertices(new List<Vector3>
            {
                new(lx, 0f, loY),
                new(lx, 0f, hiY),
                new(hx, 0f, hiY),
                new(hx, 0f, loY),
            });
            mesh.SetUVs(0, new List<Vector2>
            {
                new(lx, loY),
                new(lx, hiY),
                new(hx, hiY),
                new(hx, loY),
            });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            Meshes[(ext, key)] = mesh;
            return mesh;
        }

        private static Material MaterialFor(BladeGraphicExtension ext, Color color)
        {
            Texture2D tex = ext?.Texture;
            if (tex == null)
            {
                return null;
            }
            if (Materials.TryGetValue((tex, color), out Material cached))
            {
                return cached;
            }
            Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail("MX_BladeBody")?.Shader;
            Material mat = null;
            if (shader != null)
            {
                mat = MaterialPool.MatFrom(tex, shader, color);
            }
            else
            {
                Log.ErrorOnce("[MiliraXian] Blade body: missing shader 'MX_BladeBody'.", 0x51c7b0);
            }
            Materials[(tex, color)] = mat;
            return mat;
        }
    }
}
