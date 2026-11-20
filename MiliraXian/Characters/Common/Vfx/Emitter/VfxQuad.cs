using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>Which basis a quad lies in.</summary>
    internal enum VfxPlane
    {
        /// <summary>Lying on the map floor (XZ), normal up. Decals, petals, feet rings.</summary>
        Ground,

        /// <summary>Standing upright in world space (XY), face toward +Z. Upright sprites, text, bodies.</summary>
        Vertical,

        /// <summary>Facing the camera; the caller supplies the basis (<c>camera.transform.right/up</c>).</summary>
        Billboard,
    }

    /// <summary>
    /// Everything one quad needs. Pivot is normalised inside the quad: (0.5, 0.5) is the centre,
    /// (0.5, 0) sits the anchor on the bottom edge (feet on the ground).
    ///
    /// Use <see cref="Of"/> rather than <c>default</c>: a zeroed size or a zeroed uv rect draws nothing.
    /// </summary>
    internal struct VfxQuadOptions
    {
        public VfxPlane Plane;
        public float RotationDegrees;
        public Vector2 Size;
        public Vector2 Pivot;
        public Vector2 UvMin;
        public Vector2 UvMax;
        public Color Color;

        /// <summary>
        /// Only for textures that are not authored upright (v = 1 at the top / at the north edge).
        /// Prefer fixing the texture; a flip at the call site is the thing this layer exists to avoid.
        /// </summary>
        public bool FlipU;

        /// <inheritdoc cref="FlipU"/>
        public bool FlipV;

        /// <summary>Ground only: shrinks the north-south extent, for a sprite seen at a slant.</summary>
        public float GroundSquash;

        internal static VfxQuadOptions Of(Vector2 size)
        {
            return new VfxQuadOptions
            {
                Plane = VfxPlane.Ground,
                RotationDegrees = 0f,
                Size = size,
                Pivot = new Vector2(0.5f, 0.5f),
                UvMin = Vector2.zero,
                UvMax = Vector2.one,
                Color = Color.white,
                FlipU = false,
                FlipV = false,
                GroundSquash = 1f,
            };
        }

        internal static VfxQuadOptions Picture(Vector2 size)
        {
            VfxQuadOptions o = Of(size);
            o.Plane = VfxPlane.Vertical;
            o.Pivot = new Vector2(0.5f, 0.5f);
            return o;
        }
    }

    /// <summary>
    /// The primitive layer's single way to put a texture on the screen.
    ///
    /// It owns the four conventions that used to be re-decided at every call site:
    /// <list type="bullet">
    /// <item>the plane basis (ground / upright / camera-facing),</summary>
    /// <item>the uv orientation — v = 1 is the top of the picture, v = 0 the bottom (for a ground
    /// quad: v = 1 faces north), which is how this mod's textures are authored,</item>
    /// <item>the corner order and triangle winding (BL, TL, TR, BR, as the existing helpers use),</item>
    /// <item>the pivot, so "place a sprite with its feet on this cell" is a field, not arithmetic.</item>
    /// </list>
    /// </summary>
    internal static class VfxQuad
    {
        // Same corner order and winding as MapComponent_QingheVfx.AddQuad and
        // SpringFlowVfx.AddFieldQuad, so a migrated effect keeps its culling and its look.
        internal static void Add(VfxMeshWriter writer, Vector3 center, in VfxQuadOptions options)
        {
            Vector3 right;
            Vector3 up;
            Basis(options.Plane, options.RotationDegrees, out right, out up);
            AddWithBasis(writer, center, right, up, options);
        }

        /// <summary>A camera-facing quad; the basis is the camera's own right/up.</summary>
        internal static void AddBillboard(VfxMeshWriter writer, Vector3 center, Vector3 right, Vector3 up,
            in VfxQuadOptions options)
        {
            AddWithBasis(writer, center, right.normalized, up.normalized, options);
        }

        /// <summary>
        /// The escape hatch: the caller already knows the four world-space corners (BL, TL, TR, BR).
        /// Only uv, colour and flips are taken from the options.
        /// </summary>
        internal static void AddWorldCorners(VfxMeshWriter writer, Vector3 bottomLeft, Vector3 topLeft,
            Vector3 topRight, Vector3 bottomRight, in VfxQuadOptions options)
        {
            Vector2 uvBL;
            Vector2 uvTL;
            Vector2 uvTR;
            Vector2 uvBR;
            CornerUvs(options, out uvBL, out uvTL, out uvTR, out uvBR);
            writer.AddQuad(bottomLeft, topLeft, topRight, bottomRight, uvBL, uvTL, uvTR, uvBR, options.Color);
        }

        private static void AddWithBasis(VfxMeshWriter writer, Vector3 center, Vector3 right, Vector3 up,
            in VfxQuadOptions options)
        {
            float x0 = -options.Pivot.x * options.Size.x;
            float x1 = (1f - options.Pivot.x) * options.Size.x;
            float y0 = -options.Pivot.y * options.Size.y;
            float y1 = (1f - options.Pivot.y) * options.Size.y;

            Vector3 bottomLeft = center + right * x0 + up * y0;
            Vector3 topLeft = center + right * x0 + up * y1;
            Vector3 topRight = center + right * x1 + up * y1;
            Vector3 bottomRight = center + right * x1 + up * y0;

            // The ground squash is the existing helpers' behaviour exactly: the world-north/south
            // displacement is scaled, the east/west one is not (a decal seen at a slant).
            if (options.Plane == VfxPlane.Ground && options.GroundSquash != 1f)
            {
                SquashZ(ref bottomLeft, center.z, options.GroundSquash);
                SquashZ(ref topLeft, center.z, options.GroundSquash);
                SquashZ(ref topRight, center.z, options.GroundSquash);
                SquashZ(ref bottomRight, center.z, options.GroundSquash);
            }

            Vector2 uvBL;
            Vector2 uvTL;
            Vector2 uvTR;
            Vector2 uvBR;
            CornerUvs(options, out uvBL, out uvTL, out uvTR, out uvBR);
            writer.AddQuad(bottomLeft, topLeft, topRight, bottomRight, uvBL, uvTL, uvTR, uvBR, options.Color);
        }

        private static void SquashZ(ref Vector3 point, float centerZ, float squash)
        {
            point.z = centerZ + (point.z - centerZ) * squash;
        }

        private static void CornerUvs(in VfxQuadOptions options, out Vector2 bl, out Vector2 tl,
            out Vector2 tr, out Vector2 br)
        {
            float u0 = options.FlipU ? 1f : 0f;
            float u1 = 1f - u0;
            float v0 = options.FlipV ? 1f : 0f;
            float v1 = 1f - v0;
            Vector2 min = options.UvMin;
            Vector2 size = options.UvMax - options.UvMin;
            bl = new Vector2(min.x + size.x * u0, min.y + size.y * v0);
            tl = new Vector2(min.x + size.x * u0, min.y + size.y * v1);
            tr = new Vector2(min.x + size.x * u1, min.y + size.y * v1);
            br = new Vector2(min.x + size.x * u1, min.y + size.y * v0);
        }

        private static void Basis(VfxPlane plane, float degrees, out Vector3 right, out Vector3 up)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            if (plane == VfxPlane.Ground)
            {
                // Right along the floor, "up" toward north: rotating tilts the decal on the ground.
                right = new Vector3(c, 0f, s);
                up = new Vector3(-s, 0f, c);
            }
            else
            {
                // Upright plane facing +Z; rotating spins the sprite in its own plane.
                right = new Vector3(c, s, 0f);
                up = new Vector3(-s, c, 0f);
            }
        }
    }
}
