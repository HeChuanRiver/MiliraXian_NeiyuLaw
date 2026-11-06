using System;
using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.BladeMotion
{
    /// <summary>
    /// The one player of character motions. Each frame it asks <see cref="MotionSources"/> what the
    /// pawn is playing and evaluates that one clip for everything the motion drives: the held blade
    /// and scabbard (cross-faded when the source changes), the drawn facing and the body lift.
    /// Nothing is saved. The weapon source is built in: a warmup or cooldown stance of an
    /// <see cref="IBladeMotionVerb"/> plays its attack clip; otherwise a drafted or meleeing pawn
    /// whose weapon carries a <see cref="BladeMotionSetExtension"/> holds the idle stance, if a
    /// registered gate allows it.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BladeMotionPlayer
    {
        private sealed class State
        {
            public object source;
            public int sourceId;
            public float switchTicks;
            public BladePose from;
            public BladePose last;
            public bool hasLast;
            public int lastFrame = -1;
        }

        private static readonly List<Func<Pawn, bool>> IdleGates = new();
        private static readonly Dictionary<int, State> States = new();
        private static int pruneFrame;

        static BladeMotionPlayer()
        {
            MotionSources.Register(WeaponSource, last: true);
        }

        /// <summary>Allows the idle stance for pawns the gate accepts (for example one character in one combat stance).</summary>
        public static void RegisterIdleGate(Func<Pawn, bool> gate)
        {
            if (gate != null && !IdleGates.Contains(gate))
            {
                IdleGates.Add(gate);
            }
        }

        private static bool IdleAllowed(Pawn pawn)
        {
            for (int i = 0; i < IdleGates.Count; i++)
            {
                if (IdleGates[i](pawn))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool InCombat(Pawn pawn)
        {
            return pawn.Drafted
                || pawn.CurJobDef == JobDefOf.AttackMelee
                || pawn.stances?.curStance is Stance_Busy { verb: { verbProps.IsMeleeAttack: true } };
        }

        /// <summary>The weapon's attack (through the verb's stances) or its idle.</summary>
        private static bool WeaponSource(Pawn pawn, out MotionPlayback playback)
        {
            playback = default;
            BladeMotionSetExtension set = pawn.equipment?.Primary?.def.GetModExtension<BladeMotionSetExtension>();
            if (set == null)
            {
                return false;
            }
            if (pawn.stances?.curStance is IGuardMotionStance guard && guard.TryGetGuardPlayback(set, out playback))
            {
                return true;
            }
            if (pawn.stances?.curStance is Stance_Busy busy && busy.verb is IBladeMotionVerb motionVerb)
            {
                BladeMotionClip clip = motionVerb.ClipFor(busy, out Vector3 aim, out int attackId, out float cooldownFrom);
                if (clip?.blade != null && BladeMotionUtility.TryClipTime(busy, clip, cooldownFrom, out float seconds, out _))
                {
                    // Warmup and the cooldown after it are one continuous attack: no cross-fade between them.
                    playback = new MotionPlayback
                    {
                        clip = clip,
                        seconds = seconds,
                        aim = aim,
                        projection = set.Projection,
                        scabbardSet = set,
                        blendSeconds = set.blendSeconds,
                        source = clip,
                        sourceId = attackId,
                    };
                    return true;
                }
            }
            if (set.idle?.blade == null || !InCombat(pawn) || !IdleAllowed(pawn))
            {
                return false;
            }
            float t = VisualClock.Ticks / 60f;
            if (set.idle.loop && set.idle.durationSeconds > 0f)
            {
                t = Mathf.Repeat(t + pawn.thingIDNumber * 0.37f, set.idle.durationSeconds);
            }
            playback = new MotionPlayback
            {
                clip = set.idle,
                seconds = t,
                aim = pawn.Rotation.FacingCell.ToVector3(),
                projection = set.Projection,
                scabbardSet = set,
                blendSeconds = set.blendSeconds,
                source = set.idle,
                sourceId = pawn.Rotation.AsInt,
            };
            return true;
        }

        /// <summary>
        /// The drawn body of <paramref name="pawn"/> now: facing (when the motion turns it), lift,
        /// ground shift and tilt. False when it plays no motion with body tracks. Pure: safe from
        /// the parallel render pass.
        /// </summary>
        public static bool TryGetBody(Pawn pawn, out MotionBody body)
        {
            body = default;
            if (!MotionSources.TryGet(pawn, out MotionPlayback p)
                || (p.clip.turn == null && !p.clip.HasBodyTracks && p.clip.tilt == null && p.clip.tiltSide == null
                    && p.travel == Vector3.zero))
            {
                return false;
            }
            body.travel = p.travel;
            body.clip = p.clip;
            body.seconds = p.seconds;
            if (p.clip.turn != null)
            {
                body.hasFacing = true;
                body.facing = Rot4.FromAngleFlat(BladeMotionUtility.FacingAngle(p.clip, p.seconds, p.aim));
            }
            body.lift = BodyLiftAt(pawn, p, p.seconds);
            body.shift = BladeMotionUtility.GroundShift(p.clip, p.seconds, p.aim);
            body.tilt = BladeMotionUtility.ScreenTilt(p.clip, p.seconds, p.aim);
            body.aim = p.aim;
            return true;
        }

        /// <summary>
        /// Map offset the motion adds to the drawn body now (ground shift plus lift); subtract it from
        /// <c>DrawPos</c> for the pawn's place on the ground. Zero without a motion. Pure.
        /// </summary>
        public static Vector3 BodyOffset(Pawn pawn)
        {
            return TryGetBody(pawn, out MotionBody body) ? new Vector3(body.shift.x, 0f, body.shift.z + body.lift) : Vector3.zero;
        }

        /// <summary>Northward map lift of the drawn body by its motion's height track at the playback's second.</summary>
        public static float BodyLift(Pawn pawn, in MotionPlayback p)
        {
            return BodyLiftAt(pawn, p, p.seconds);
        }

        /// <summary>The same lift at another second of the same playback.</summary>
        public static float BodyLiftAt(Pawn pawn, in MotionPlayback p, float seconds)
        {
            if (p.clip?.height == null)
            {
                return 0f;
            }
            float lift = BladeMotionUtility.Lift(p.clip, seconds, p.projection);
            if (p.heightIncludesFlight && pawn.flight != null && pawn.flight.Flying)
            {
                lift -= VanillaFlightLift * pawn.flight.PositionOffsetFactor;
            }
            return lift;
        }



        /// <summary>Screen lift of a flying pawn in vanilla (Pawn_DrawTracker: 0.6 cells north at full flight).</summary>
        public const float VanillaFlightLift = 0.6f;

        /// <summary>Held blade of <paramref name="pawn"/> now, cross-faded when the playing motion changes.</summary>
        public static bool TryGetBladePose(Pawn pawn, out BladePose pose)
        {
            pose = default;
            if (pawn == null || !pawn.Spawned)
            {
                return false;
            }
            PruneStates();
            States.TryGetValue(pawn.thingIDNumber, out State state);
            if (!MotionSources.TryGet(pawn, out MotionPlayback p) || p.clip.blade == null)
            {
                if (state != null)
                {
                    state.hasLast = false;
                    state.source = null;
                }
                return false;
            }
            BladePose target = BladeMotionUtility.Pose(p.clip, p.seconds, p.aim, p.projection, p.scabbardSet);
            state ??= States[pawn.thingIDNumber] = new State();
            float now = VisualClock.Ticks;
            if (!ReferenceEquals(state.source, p.source) || state.sourceId != p.sourceId)
            {
                state.from = state.hasLast ? state.last : target;
                state.switchTicks = now;
                state.source = p.source;
                state.sourceId = p.sourceId;
            }
            float blendTicks = Mathf.Max(0f, p.blendSeconds) * 60f;
            float k = blendTicks <= 0f ? 1f : Mathf.Clamp01((now - state.switchTicks) / blendTicks);
            pose = k >= 1f ? target : Blend(state.from, target, Mathf.SmoothStep(0f, 1f, k));
            state.last = pose;
            state.hasLast = true;
            state.lastFrame = Time.frameCount;
            return true;
        }

        private static BladePose Blend(in BladePose a, in BladePose b, float t)
        {
            Vector3 dir = Vector3.Slerp(a.dir.normalized, b.dir.normalized, t);
            Vector3 edge = Vector3.Slerp(a.edge.normalized, b.edge.normalized, t);
            BladePose p = new()
            {
                grip = Vector3.Lerp(a.grip, b.grip, t),
                dir = dir,
                edge = edge,
                heightToScreen = b.heightToScreen,
                depthToScreen = b.depthToScreen,
                heightOrigin = b.heightOrigin,
                sheathed = Mathf.Lerp(a.sheathed, b.sheathed, t),
                hasScabbard = b.hasScabbard,
                scabbardColor = b.scabbardColor,
            };
            if (b.hasScabbard)
            {
                if (a.hasScabbard)
                {
                    Vector3 ma = a.scabbardMouth;
                    Vector3 ea = a.scabbardMouth + a.scabbardDir;
                    Vector3 mb = b.scabbardMouth;
                    Vector3 eb = b.scabbardMouth + b.scabbardDir;
                    p.scabbardMouth = Vector3.Lerp(ma, mb, t);
                    p.scabbardDir = Vector3.Lerp(ea, eb, t) - p.scabbardMouth;
                    p.scabbardLength = p.scabbardDir.magnitude;
                    p.scabbardEdge = Vector3.Slerp(a.scabbardEdge.normalized, b.scabbardEdge.normalized, t);
                }
                else
                {
                    p.scabbardMouth = b.scabbardMouth;
                    p.scabbardDir = b.scabbardDir;
                    p.scabbardLength = b.scabbardLength;
                    p.scabbardEdge = b.scabbardEdge;
                }
            }
            return p;
        }

        /// <summary>Drops the cross-fade state of pawns not drawn for a while.</summary>
        private static void PruneStates()
        {
            int frame = Time.frameCount;
            if (frame - pruneFrame < 600 || States.Count == 0)
            {
                return;
            }
            pruneFrame = frame;
            List<int> stale = null;
            foreach (KeyValuePair<int, State> kv in States)
            {
                if (frame - kv.Value.lastFrame > 600)
                {
                    (stale ??= new List<int>()).Add(kv.Key);
                }
            }
            if (stale != null)
            {
                foreach (int id in stale)
                {
                    States.Remove(id);
                }
            }
        }
    }
}
