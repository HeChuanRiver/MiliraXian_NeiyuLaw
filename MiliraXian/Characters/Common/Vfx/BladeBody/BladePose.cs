using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.BladeBody
{
    /// <summary>
    /// The blade at one moment, in world metres relative to the drawn body centre
    /// (x = east, y = height, z = north). <see cref="dir"/> runs from the grip to the tip,
    /// <see cref="edge"/> is the cutting edge side, perpendicular to it.
    /// </summary>
    public struct BladePose
    {
        public Vector3 grip;
        public Vector3 dir;
        public Vector3 edge;
        /// <summary>Screen offset to the north per metre of height (the same as the trails).</summary>
        public float heightToScreen;
        /// <summary>Screen scale of north-south offsets from the body (0 = unset, treated as 1).</summary>
        public float depthToScreen;
        /// <summary>Height of the motion's origin above the drawn body centre, metres (negative = lower).</summary>
        public float heightOrigin;
        /// <summary>Lit fraction of the blade measured from grip to tip (0 = no light; the shader's reveal glow).</summary>
        public float reveal;

        public float lengthScale;
        public float LengthScale => lengthScale > 0f ? lengthScale : 1f;
        public float DepthScale => depthToScreen > 0f ? depthToScreen : 1f;
        /// <summary>Share of the blade (grip to tip) inside the scabbard; that part is not drawn. 0 = fully out.</summary>
        public float sheathed;
        /// <summary>A scabbard is drawn from <see cref="scabbardMouth"/> along <see cref="scabbardDir"/>.</summary>
        public bool hasScabbard;
        public Color scabbardColor;
        public Vector3 scabbardMouth;
        public Vector3 scabbardDir;
        public Vector3 scabbardEdge;
        public float scabbardLength;
    }

    /// <summary>
    /// A job driver that poses the held blade itself, outside the motion clips (a procedural blade
    /// path such as Neiyu's single execution). Asked before <see cref="BladeMotion.BladeMotionPlayer"/>.
    /// </summary>
    public interface IBladePoseSource
    {
        bool TryGetBladePose(out BladePose pose);
    }

    /// <summary>
    /// One key of a hand-written blade motion, in the body frame (right, up, forward).
    /// yaw: 0 = forward, positive toward the right. pitch: positive up.
    /// roll: turn of the edge about the blade, from the horizontal side that points toward +yaw.
    /// reach: arm extension, share of the arm length (the hand moves out from the shoulder).
    /// offset: extra hand offset in metres, for poses the arm rule does not cover.
    /// </summary>
    public class BladeKey
    {
        public float time;
        public float yaw;
        public float pitch;
        public float roll;
        public float reach = 1f;
        public Vector3 offset;
    }

    /// <summary>
    /// Blade keyframes; every channel eases between keys with the monotone cubic. The hand is
    /// placed at the end of the arm: shoulder + arm direction × armLength × reach. The arm points
    /// the way the blade does, with only <see cref="armPitchShare"/> of its pitch (the wrist takes
    /// the rest), so a swing sweeps the hand round the body instead of turning the blade in place.
    /// </summary>
    public class BladeKeyframes
    {
        /// <summary>Shoulder of the sword arm, metres from the drawn body centre (right, up, forward).</summary>
        public Vector3 shoulder = new(0.12f, 0.25f, 0f);
        public float armLength = 0.55f;
        public float armPitchShare = 0.5f;
        public List<BladeKey> keys = new();

        private SimpleCurve ox, oy, oz, yaw, pitch, roll, reach;

        /// <summary>Body-frame pose at <paramref name="seconds"/>: grip, blade direction and edge as (right, up, forward).</summary>
        public void Evaluate(float seconds, out Vector3 grip, out Vector3 dir, out Vector3 edge)
        {
            if (ox == null)
            {
                Build();
            }
            float y = MonotoneCurve.Evaluate(yaw, seconds) * Mathf.Deg2Rad;
            float p = MonotoneCurve.Evaluate(pitch, seconds) * Mathf.Deg2Rad;
            float r = MonotoneCurve.Evaluate(roll, seconds) * Mathf.Deg2Rad;
            dir = Direction(y, p);
            Vector3 side = new(Mathf.Cos(y), 0f, -Mathf.Sin(y));
            Vector3 lift = Vector3.Cross(dir, side);
            edge = side * Mathf.Cos(r) + lift * Mathf.Sin(r);

            Vector3 arm = Direction(y, p * armPitchShare);
            float extend = Mathf.Max(0f, MonotoneCurve.Evaluate(reach, seconds));
            Vector3 offset = new(MonotoneCurve.Evaluate(ox, seconds), MonotoneCurve.Evaluate(oy, seconds), MonotoneCurve.Evaluate(oz, seconds));
            grip = shoulder + arm * (armLength * extend) + offset;
        }

        private static Vector3 Direction(float yawRad, float pitchRad)
        {
            return new Vector3(Mathf.Sin(yawRad) * Mathf.Cos(pitchRad), Mathf.Sin(pitchRad), Mathf.Cos(yawRad) * Mathf.Cos(pitchRad));
        }

        private void Build()
        {
            ox = new SimpleCurve();
            oy = new SimpleCurve();
            oz = new SimpleCurve();
            yaw = new SimpleCurve();
            pitch = new SimpleCurve();
            roll = new SimpleCurve();
            reach = new SimpleCurve();
            foreach (BladeKey k in keys)
            {
                ox.Add(k.time, k.offset.x, sort: false);
                oy.Add(k.time, k.offset.y, sort: false);
                oz.Add(k.time, k.offset.z, sort: false);
                yaw.Add(k.time, k.yaw, sort: false);
                pitch.Add(k.time, k.pitch, sort: false);
                roll.Add(k.time, k.roll, sort: false);
                reach.Add(k.time, k.reach, sort: false);
            }
            foreach (SimpleCurve c in new[] { ox, oy, oz, yaw, pitch, roll, reach })
            {
                c.SortPoints();
            }
        }
    }
}
