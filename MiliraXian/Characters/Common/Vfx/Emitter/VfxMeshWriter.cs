using System.Collections.Generic;
using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Vertex sink for the primitive layer: the four arrays a VFX mesh needs, owned by whoever
    /// is building one stroke this frame.
    ///
    /// It is an instance, not a static shared buffer, so two renderers can never corrupt each
    /// other's geometry when one throws halfway through building a quad.
    /// </summary>
    internal sealed class VfxMeshWriter
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<Color> colors = new();
        private readonly List<int> triangles = new();

        internal int VertexCount => vertices.Count;

        internal bool IsEmpty => vertices.Count == 0;

        internal void Clear()
        {
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
        }

        /// <summary>
        /// Appends one quad, given its four corners counter-clockwise as seen from the front
        /// (bottom-left, top-left, top-right, bottom-right) and a uv per corner.
        /// </summary>
        internal void AddQuad(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight,
            Vector2 uvBottomLeft, Vector2 uvTopLeft, Vector2 uvTopRight, Vector2 uvBottomRight, Color color)
        {
            AddQuad(bottomLeft, topLeft, topRight, bottomRight,
                uvBottomLeft, uvTopLeft, uvTopRight, uvBottomRight, color, color, color, color);
        }

        /// <summary>
        /// The same quad, with one colour per corner — a streak fades from its head to its tail.
        /// Corners are (bottom-left, top-left, top-right, bottom-right) in the same order.
        /// </summary>
        internal void AddQuad(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight,
            Vector2 uvBottomLeft, Vector2 uvTopLeft, Vector2 uvTopRight, Vector2 uvBottomRight,
            Color colorBottomLeft, Color colorTopLeft, Color colorTopRight, Color colorBottomRight)
        {
            int b = vertices.Count;
            vertices.Add(bottomLeft);
            vertices.Add(topLeft);
            vertices.Add(topRight);
            vertices.Add(bottomRight);
            uvs.Add(uvBottomLeft);
            uvs.Add(uvTopLeft);
            uvs.Add(uvTopRight);
            uvs.Add(uvBottomRight);
            colors.Add(colorBottomLeft);
            colors.Add(colorTopLeft);
            colors.Add(colorTopRight);
            colors.Add(colorBottomRight);
            triangles.Add(b);
            triangles.Add(b + 1);
            triangles.Add(b + 2);
            triangles.Add(b);
            triangles.Add(b + 2);
            triangles.Add(b + 3);
        }

        /// <summary>Pushes the buffered geometry into a mesh, replacing what the mesh held before.</summary>
        internal void Upload(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
    }
}
