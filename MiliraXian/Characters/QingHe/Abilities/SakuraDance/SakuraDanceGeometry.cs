using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>
    /// The ring frame of one cut in world metres (1 cell = 1 m). World vectors here are
    /// (x = east, y = height, z = north). The body frame is right / up / forward of the aim
    /// direction; a west-facing cast mirrors "right", which also flips the spin.
    /// </summary>
    public readonly struct SakuraRingFrame
    {
        /// <summary>Ring centre: ground x/z of the cast plus height in y.</summary>
        public readonly Vector3 center;
        /// <summary>World images of ring-local x and z (the ring plane).</summary>
        public readonly Vector3 axisA;
        public readonly Vector3 axisB;
        /// <summary>World image of ring-local y (ring normal).</summary>
        public readonly Vector3 axisN;

        public SakuraRingFrame(Vector3 center, Vector3 axisA, Vector3 axisB, Vector3 axisN)
        {
            this.center = center;
            this.axisA = axisA;
            this.axisB = axisB;
            this.axisN = axisN;
        }

        public static void BodyBasis(Vector3 aim, out Vector3 right, out Vector3 up, out Vector3 forward)
        {
            aim.y = 0f;
            if (aim.sqrMagnitude < 1e-6f)
            {
                aim = Vector3.right;
            }
            aim.Normalize();
            forward = aim;
            up = Vector3.up;
            // Right of a forward on the ground, seen from above with north up.
            right = new Vector3(aim.z, 0f, -aim.x);
            if (aim.x < 0f)
            {
                right = -right;
            }
        }

        public static Vector3 BodyToWorld(Vector3 local, Vector3 right, Vector3 up, Vector3 forward)
        {
            return right * local.x + up * local.y + forward * local.z;
        }

        public static SakuraRingFrame For(SakuraCutProps cut, Vector3 groundOrigin, Vector3 aim, float originHeight)
        {
            BodyBasis(aim, out Vector3 right, out Vector3 up, out Vector3 forward);
            float cx = Mathf.Cos(cut.tiltX * Mathf.Deg2Rad);
            float sx = Mathf.Sin(cut.tiltX * Mathf.Deg2Rad);
            float cz = Mathf.Cos(cut.tiltZ * Mathf.Deg2Rad);
            float sz = Mathf.Sin(cut.tiltZ * Mathf.Deg2Rad);
            // Rx * Rz applied to the local unit axes.
            Vector3 a = new(cz, cx * sz, sx * sz);
            Vector3 b = new(0f, -sx, cx);
            Vector3 n = new(-sz, cx * cz, sx * cz);
            Vector3 center = groundOrigin + new Vector3(0f, originHeight, 0f) + BodyToWorld(cut.offset, right, up, forward);
            return new SakuraRingFrame(
                center,
                BodyToWorld(a, right, up, forward),
                BodyToWorld(b, right, up, forward),
                BodyToWorld(n, right, up, forward));
        }

        /// <summary>World point at ring angle <paramref name="degrees"/> (local x cos, z -sin), radius r, height h above the plane.</summary>
        public Vector3 Point(float degrees, float r, float h = 0f)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return center + axisA * (r * Mathf.Cos(rad)) - axisB * (r * Mathf.Sin(rad)) + axisN * h;
        }

        /// <summary>Maps a ring-local vector (x, y, z) to world.</summary>
        public Vector3 LocalToWorld(Vector3 local)
        {
            return axisA * local.x + axisN * local.y + axisB * local.z;
        }
    }

    public static class SakuraDanceGeometry
    {
        /// <summary>Map draw position of a world point: height shown as an offset to the north.</summary>
        public static Vector3 ToMap(Vector3 world, float heightToScreen, float altitude)
        {
            return new Vector3(world.x, altitude, world.z + world.y * heightToScreen);
        }

        public static float RingAngle(SakuraCutProps cut, float localSeconds)
        {
            return cut.startAngle + cut.angularSpeed * localSeconds;
        }

        /// <summary>Ground heading (degrees, CCW from east) of the ring head seen from the ring centre.</summary>
        public static float HeadHeading(in SakuraRingFrame frame, SakuraCutProps cut, float localSeconds)
        {
            Vector3 head = frame.Point(RingAngle(cut, localSeconds), cut.radius) - frame.center;
            return Mathf.Atan2(head.z, head.x) * Mathf.Rad2Deg;
        }
    }
}
