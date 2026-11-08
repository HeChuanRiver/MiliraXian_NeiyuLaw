using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.BladeMotion
{
    /// <summary>
    /// What a pawn is playing at this moment: one clip at one clip second along one aim. Every
    /// motion source (weapon attacks and idles, ability aims, ability jobs) reduces to this; the
    /// player evaluates it the same way for the blade, the drawn facing and the body lift.
    /// </summary>
    public struct MotionPlayback
    {
        public BladeMotionClip clip;
        public float seconds;
        public Vector3 aim;
        public BladeProjection projection;
        /// <summary>Draw the blade's scabbard (when the blade has one) and hide the sheathed share.</summary>
        public bool scabbard;
        /// <summary>Blade drawn instead of the held weapon's (a motion tied to one weapon); null = the held weapon's.</summary>
        public BladeBody.BladeGraphicExtension blade;
        /// <summary>Cross-fade into this playback when the source changes; 0 = cut.</summary>
        public float blendSeconds;
        /// <summary>Identity of the playing motion: a change of either starts a cross-fade.</summary>
        public object source;
        public int sourceId;
        /// <summary>
        /// The clip's height is absolute: vanilla flight's own lift is taken off the body lift
        /// (an airborne cast written from the flying height).
        /// </summary>
        public bool heightIncludesFlight;
        /// <summary>
        /// Map offset (x, 0, z) of the whole drawn body outside the clip, for a dash the job drives
        /// (Position has already moved; the body is drawn back along the way). Unlike the clip's own
        /// ground shift it is not taken off by <see cref="BladeMotionPlayer.BodyOffset"/>: effects that
        /// follow the body's place on the ground travel with it.
        /// </summary>
        public Vector3 travel;
    }

    /// <summary>The drawn body of a playing motion at this moment.</summary>
    public struct MotionBody
    {
        public BladeMotionClip clip;
        public float seconds;
        public bool hasFacing;
        public Rot4 facing;
        /// <summary>Northward map offset from the height track (without vanilla flight's own lift).</summary>
        public float lift;
        /// <summary>Ground shift (world x, 0, z) from the forward and side tracks.</summary>
        public Vector3 shift;
        /// <summary>Screen angle of the sprite, degrees.</summary>
        public float tilt;
        public Vector3 aim;
        /// <summary>Map offset of the body from a dash (see <see cref="MotionPlayback.travel"/>).</summary>
        public Vector3 travel;
    }

    /// <summary>A guard stance that plays its guard clips while it lasts, with any weapon.</summary>
    public interface IGuardMotionStance
    {
        /// <param name="set">The held weapon's motion set; null when it has none.</param>
        bool TryGetGuardPlayback(BladeMotionSetExtension set, out MotionPlayback playback);
    }

    /// <summary>A job driver that plays a motion while it runs (the Job side of the bridge).</summary>
    public interface IMotionPlaybackSource
    {
        bool TryGetPlayback(out MotionPlayback playback);
    }

    /// <summary>A motion played outside the pawn's job, read from its vanilla state (for example an ability warmup stance).</summary>
    public delegate bool PawnMotionSource(Pawn pawn, out MotionPlayback playback);

    /// <summary>
    /// The motion sources, asked in order: the job driver, then registered state sources (ability
    /// warmups, weapon attacks, weapon idles). Pure reads: safe from the parallel render pass.
    /// </summary>
    public static class MotionSources
    {
        private static readonly List<PawnMotionSource> Sources = new();

        /// <param name="last">Asked after every other source (fallbacks such as idles).</param>
        public static void Register(PawnMotionSource source, bool last = false)
        {
            if (source == null || Sources.Contains(source))
            {
                return;
            }
            if (last)
            {
                Sources.Add(source);
            }
            else
            {
                Sources.Insert(0, source);
            }
        }

        public static bool TryGet(Pawn pawn, out MotionPlayback playback)
        {
            playback = default;
            if (pawn == null || !pawn.Spawned)
            {
                return false;
            }
            if (pawn.jobs?.curDriver is IMotionPlaybackSource job && job.TryGetPlayback(out playback) && playback.clip != null)
            {
                return true;
            }
            for (int i = 0; i < Sources.Count; i++)
            {
                if (Sources[i](pawn, out playback) && playback.clip != null)
                {
                    return true;
                }
            }
            playback = default;
            return false;
        }
    }
}
