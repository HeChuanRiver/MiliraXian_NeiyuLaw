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
    }

    /// <summary>A job driver that animates the held blade itself while it runs.</summary>
    public interface IBladePoseSource
    {
        bool TryGetBladePose(out BladePose pose);
    }

    /// <summary>Blade pose of a pawn from something other than its job driver (for example an ability warmup).</summary>
    public delegate bool PawnBladePoseProvider(Pawn pawn, out BladePose pose);

    /// <summary>Extra blade pose sources, asked in order when the job driver does not pose the blade.</summary>
    public static class BladePoseProviders
    {
        private static readonly List<PawnBladePoseProvider> Providers = new();

        public static void Register(PawnBladePoseProvider provider)
        {
            if (provider != null && !Providers.Contains(provider))
            {
                Providers.Add(provider);
            }
        }

        public static bool TryGet(Pawn pawn, out BladePose pose)
        {
            if (pawn?.jobs?.curDriver is IBladePoseSource source && source.TryGetBladePose(out pose))
            {
                return true;
            }
            for (int i = 0; i < Providers.Count; i++)
            {
                if (Providers[i](pawn, out pose))
                {
                    return true;
                }
            }
            pose = default;
            return false;
        }
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
