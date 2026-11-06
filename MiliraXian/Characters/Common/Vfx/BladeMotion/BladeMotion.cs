using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.BladeMotion
{
    /// <summary>One damaging cut inside an attack clip: clip seconds and the share of the verb's damage it deals.</summary>
    public class BladeStrike
    {
        public float start;
        public float end;
        public float damageFactor = 1f;
        /// <summary>Share of the on-hit resource gain (for example Qinghe's sword pressure).</summary>
        public float resourceFactor = 1f;

        public float Seconds => Mathf.Max(0f, end - start);
    }

    /// <summary>Scabbard at one clip time, body frame (right, up, forward), metres.</summary>
    public class BladeScabbardKey
    {
        public float time;
        public Vector3 mouth;
        public Vector3 end;
    }

    /// <summary>
    /// How motion metres are drawn on the map: x east as is, height shown as a northward offset,
    /// north-south offsets of the blade scaled, heights measured from an origin below the drawn
    /// body centre. One projection for the blade, scabbard, trails and the body lift.
    /// </summary>
    public struct BladeProjection
    {
        public float heightToScreen;
        public float depthToScreen;
        public float heightOrigin;

        public BladeProjection(float heightToScreen, float depthToScreen, float heightOrigin)
        {
            this.heightToScreen = heightToScreen;
            this.depthToScreen = depthToScreen;
            this.heightOrigin = heightOrigin;
        }
    }

    /// <summary>
    /// One motion clip, the single format of every character motion (weapon idles and attacks,
    /// ability aims, ability jobs). Times are seconds from the clip start.
    /// Blade, scabbard and trail tracks pose the held weapon; the body tracks turn the drawn
    /// sprite (<see cref="turn"/>) and lift it (<see cref="height"/>).
    /// An attack clip is a windup (played over the vanilla warmup stance), then one or more strikes
    /// whose damage sweeps along the blade's ground heading, then the return to the idle pose
    /// (played over the vanilla cooldown stance). A loop clip is an idle.
    /// </summary>
    public class BladeMotionClip
    {
        public float durationSeconds = 1f;
        public bool loop;
        public float windupSeconds;
        public List<BladeStrike> strikes = new();
        public BladeKeyframes blade;

        /// <summary>
        /// Body turn in degrees from the aim (clockwise from above when facing east; a west aim
        /// mirrors it). The sprite is drawn at the nearest of its four facings. Missing = no turn.
        /// </summary>
        public SimpleCurve turn;
        /// <summary>
        /// True: the blade and scabbard keys are relative to the turned body (they turn with it).
        /// False: they stay in the aim frame, the turn only changes the drawn sprite.
        /// </summary>
        public bool turnCarriesBlade = true;
        /// <summary>Body height in metres (negative = a crouch), drawn through the projection. Missing = none.</summary>
        public SimpleCurve height;
        /// <summary>
        /// Ground shift of the drawn body in metres along the aim (forward) and to its right (side,
        /// mirrored for a west aim like the body frame). Visual only: Position stays; the blade, being
        /// drawn relative to the body, moves with it, the shadow too. Missing = none.
        /// </summary>
        public SimpleCurve bodyForward;
        public SimpleCurve bodySide;
        /// <summary>
        /// Lean of the drawn body in degrees, in the aim frame: <see cref="tilt"/> towards the aim
        /// (positive) or away, <see cref="tiltSide"/> to the aim's right (positive, mirrored for a
        /// west aim) or left. The sprite is flat, so only the part of the lean across the screen
        /// shows, as a turn of the sprite: attacking east or west the forward lean, attacking north
        /// or south the side lean. The blade is not turned. Missing = none.
        /// </summary>
        public SimpleCurve tilt;
        public SimpleCurve tiltSide;

        public bool HasBodyTracks => height != null || bodyForward != null || bodySide != null;

        /// <summary>Clip seconds the blade trail is emitted over (start below 0 = no trail).</summary>
        public float trailStart = -1f;
        public float trailEnd = -1f;
        /// <summary>
        /// While the blade leaves the scabbard it points back through the body; over this window the
        /// trail's outer edge is pushed out to the radius the blade reaches at its end.
        /// </summary>
        public float drawPushStart = -1f;
        public float drawPushEnd = -1f;
        /// <summary>Clip second of the flash at the scabbard mouth (the blade fully sheathed); below 0 = none.</summary>
        public float flashTime = -1f;
        /// <summary>Share of the blade inside the scabbard over clip time; missing = always out.</summary>
        public SimpleCurve sheathed;
        /// <summary>Scabbard over clip time; missing = it rests at the waist.</summary>
        public List<BladeScabbardKey> scabbardKeys = new();

        public bool HasTrail => trailStart >= 0f && trailEnd > trailStart;

        public bool TryScabbard(float seconds, out Vector3 mouth, out Vector3 end)
        {
            mouth = end = default;
            if (scabbardKeys.NullOrEmpty())
            {
                return false;
            }
            int n = scabbardKeys.Count;
            if (seconds <= scabbardKeys[0].time)
            {
                mouth = scabbardKeys[0].mouth;
                end = scabbardKeys[0].end;
                return true;
            }
            for (int i = 1; i < n; i++)
            {
                BladeScabbardKey b = scabbardKeys[i];
                if (seconds <= b.time)
                {
                    BladeScabbardKey a = scabbardKeys[i - 1];
                    float k = b.time > a.time ? (seconds - a.time) / (b.time - a.time) : 1f;
                    mouth = Vector3.Lerp(a.mouth, b.mouth, k);
                    end = Vector3.Lerp(a.end, b.end, k);
                    return true;
                }
            }
            mouth = scabbardKeys[n - 1].mouth;
            end = scabbardKeys[n - 1].end;
            return true;
        }

        public float SheathedAt(float seconds)
        {
            return sheathed == null || sheathed.PointsCount == 0 ? 0f : Mathf.Clamp01(sheathed.Evaluate(seconds));
        }

        /// <summary>Clip seconds from the end of the windup until the motion has returned to idle.</summary>
        public float AfterWindupSeconds => Mathf.Max(0f, durationSeconds - windupSeconds);

        /// <summary>
        /// Clip second a counter-attack starts from (it skips the windup and the strikes before it);
        /// below 0 = the clip has no counter.
        /// </summary>
        public float counterFrom = -1f;

        /// <summary>Named damage of this attack's strikes and of its counter (shown in wounds and the combat log); null = the verb's own.</summary>
        public DamageDef damageDef;
        public DamageDef counterDamageDef;

        /// <summary>End of the last strike: from here on the attack is only the return to idle.</summary>
        public float LastStrikeEnd
        {
            get
            {
                float end = windupSeconds;
                if (strikes != null)
                {
                    foreach (BladeStrike s in strikes)
                    {
                        end = Mathf.Max(end, s.end);
                    }
                }
                return end;
            }
        }
    }

    /// <summary>The motion set of a blade weapon: its idle stance and its attacks.</summary>
    public class BladeMotionSetExtension : DefModExtension
    {
        /// <summary>Screen offset to the north per metre of height (the same as the trails).</summary>
        public float heightToScreen = 0.5f;
        /// <summary>Cross-fade when the pose source changes (idle to attack and back).</summary>
        public float blendSeconds = 0.08f;
        /// <summary>
        /// Screen scale of north-south offsets. The sprite is drawn flat, so a blade held behind or in
        /// front of the body shows shorter than one held to the side; below 1 keeps it on the body.
        /// </summary>
        public float depthToScreen = 1f;
        /// <summary>
        /// Height of the motion's origin (its y = 0, the bone data's pelvis) above the drawn body
        /// centre, metres; negative puts the hands and the scabbard at the sprite's waist.
        /// </summary>
        public float heightOrigin;

        public BladeProjection Projection => new(heightToScreen, depthToScreen, heightOrigin);

        public BladeMotionClip idle;
        public List<BladeMotionClip> attacks = new();
        /// <summary>The guard: raised into the block pose, then held (the last key) while the guard lasts.</summary>
        public BladeMotionClip guard;
        /// <summary>Recoil of the guard when a hit lands on it; it ends in the guard's held pose.</summary>
        public BladeMotionClip guardHit;
        /// <summary>
        /// The deflect before a counter: the blade knocks the attack aside, then winds up into the pose
        /// the attack clip's counter starts from. Played stretched over the parry stance.
        /// </summary>
        public BladeMotionClip parry;
        /// <summary>Draw a scabbard; it rests at the waist outside the clips' own scabbard keys.</summary>
        public bool scabbard;
        public Vector3 scabbardRestMouth;
        public Vector3 scabbardRestEnd;
        /// <summary>Scabbard tint; it is drawn with the blade texture (from the grip to the tip), darkened.</summary>
        public Color scabbardColor = new(0.24f, 0.16f, 0.14f, 1f);

        public BladeMotionClip Attack(int index)
        {
            if (attacks.NullOrEmpty())
            {
                return null;
            }
            int n = attacks.Count;
            return attacks[((index % n) + n) % n];
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (idle != null && (idle.blade == null || idle.durationSeconds <= 0f))
            {
                yield return "idle 片段需要 blade 关键帧和正的 durationSeconds。";
            }
            if (attacks != null)
            {
                foreach (BladeMotionClip a in attacks)
                {
                    if (a?.blade == null || a.windupSeconds <= 0f || a.durationSeconds <= a.windupSeconds || a.strikes.NullOrEmpty())
                    {
                        yield return "攻击片段需要 blade 关键帧和至少一段 strikes，且 0 < windupSeconds < durationSeconds。";
                        continue;
                    }
                    foreach (BladeStrike s in a.strikes)
                    {
                        if (s == null || s.start < a.windupSeconds || s.end <= s.start || s.end > a.durationSeconds || s.damageFactor < 0f)
                        {
                            yield return "攻击片段的每段 strike 必须落在举刀之后、片段之内，end > start，damageFactor 不能为负。";
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// A melee verb that plays a <see cref="BladeMotionClip"/> through the vanilla warmup and
    /// cooldown stances. The player reads the clip back from the stance's verb.
    /// </summary>
    public interface IBladeMotionVerb
    {
        /// <summary>The verb's attacks play blade motions now (other weapon effects should stand down).</summary>
        bool MotionActive { get; }

        /// <summary>
        /// The clip shown during this verb's warmup or cooldown stance, the aim it is cut along and an
        /// id shared by the warmup and the cooldown of one attack; null plays nothing.
        /// <paramref name="cooldownFrom"/> is the clip second the cooldown starts at (the windup end,
        /// or later for an attack that skips part of the clip).
        /// </summary>
        BladeMotionClip ClipFor(Stance_Busy stance, out Vector3 aim, out int attackId, out float cooldownFrom);
    }

    public static class BladeMotionUtility
    {
        /// <summary>
        /// Body frame of an aim on the ground: forward is the aim, right is to its right seen from
        /// above with north up, mirrored when facing west so motions turn with the pawn's sprite.
        /// </summary>
        public static void Basis(Vector3 aim, out Vector3 right, out Vector3 up, out Vector3 forward)
        {
            aim.y = 0f;
            if (aim.sqrMagnitude < 1e-6f)
            {
                aim = Vector3.right;
            }
            aim.Normalize();
            forward = aim;
            up = Vector3.up;
            right = new Vector3(aim.z, 0f, -aim.x);
            if (aim.x < 0f)
            {
                right = -right;
            }
        }

        public static Vector3 ToWorld(Vector3 local, Vector3 right, Vector3 up, Vector3 forward)
        {
            return right * local.x + up * local.y + forward * local.z;
        }

        /// <summary>Body turn of <paramref name="clip"/> at <paramref name="seconds"/>, degrees (0 without a turn track).</summary>
        public static float TurnDegrees(BladeMotionClip clip, float seconds)
        {
            return clip?.turn == null ? 0f : MonotoneCurve.Evaluate(clip.turn, seconds);
        }

        /// <summary>
        /// Frame the clip's blade and scabbard keys are written in: the aim frame, turned with the
        /// body when the clip says the turn carries the blade.
        /// </summary>
        public static void BodyBasis(BladeMotionClip clip, float seconds, Vector3 aim, out Vector3 right, out Vector3 up, out Vector3 forward)
        {
            Basis(aim, out right, out up, out forward);
            if (clip?.turn == null || !clip.turnCarriesBlade)
            {
                return;
            }
            float a = TurnDegrees(clip, seconds) * Mathf.Deg2Rad;
            Vector3 f = forward * Mathf.Cos(a) + right * Mathf.Sin(a);
            Vector3 r = right * Mathf.Cos(a) - forward * Mathf.Sin(a);
            forward = f;
            right = r;
        }

        /// <summary>
        /// Facing (<see cref="Vector3Utility.AngleFlat"/>, 0 = north, clockwise) of the drawn body:
        /// the aim turned by the clip's turn, mirrored for a west aim like the body frame.
        /// </summary>
        public static float FacingAngle(BladeMotionClip clip, float seconds, Vector3 aim)
        {
            float turn = TurnDegrees(clip, seconds);
            return aim.AngleFlat() + (aim.x < 0f ? -turn : turn);
        }

        /// <summary>Northward map offset of the drawn body from the clip's height track.</summary>
        public static float Lift(BladeMotionClip clip, float seconds, in BladeProjection projection)
        {
            return clip?.height == null ? 0f : MonotoneCurve.Evaluate(clip.height, seconds) * projection.heightToScreen;
        }

        /// <summary>
        /// Body offset of the clip at that second in world metres (ground shift in x/z, height in y),
        /// for effects drawn from the ground that must stay on the moving body (trails, flashes).
        /// </summary>
        public static Vector3 BodyOffsetWorld(BladeMotionClip clip, float seconds, Vector3 aim)
        {
            Vector3 v = GroundShift(clip, seconds, aim);
            if (clip?.height != null)
            {
                v.y = MonotoneCurve.Evaluate(clip.height, seconds);
            }
            return v;
        }

        /// <summary>Ground shift (world x, 0, z) of the drawn body from the clip's forward and side tracks, in the aim frame.</summary>
        public static Vector3 GroundShift(BladeMotionClip clip, float seconds, Vector3 aim)
        {
            if (clip == null || (clip.bodyForward == null && clip.bodySide == null))
            {
                return Vector3.zero;
            }
            Basis(aim, out Vector3 right, out _, out Vector3 forward);
            return forward * MonotoneCurve.Evaluate(clip.bodyForward, seconds) + right * MonotoneCurve.Evaluate(clip.bodySide, seconds);
        }

        /// <summary>
        /// Screen angle (degrees, positive = the top towards east) of the drawn sprite: the east-west
        /// part of the clip's lean, which is in the aim frame like the blade. Attacking east or west
        /// the forward lean shows and the side lean does not; attacking north or south the reverse.
        /// The drawn facing does not matter (a sprite turned away still leans towards the target).
        /// </summary>
        public static float ScreenTilt(BladeMotionClip clip, float seconds, Vector3 aim)
        {
            if (clip == null || (clip.tilt == null && clip.tiltSide == null))
            {
                return 0f;
            }
            Basis(aim, out Vector3 right, out _, out Vector3 forward);
            Vector3 lean = forward * MonotoneCurve.Evaluate(clip.tilt, seconds) + right * MonotoneCurve.Evaluate(clip.tiltSide, seconds);
            return lean.x;
        }

        /// <summary>World blade pose of <paramref name="clip"/> at <paramref name="seconds"/> for an aim.</summary>
        /// <param name="scabbardSet">Adds the set's scabbard and the sheathed share when the set draws a scabbard; null = none.</param>
        public static BladePose Pose(BladeMotionClip clip, float seconds, Vector3 aim, in BladeProjection projection, BladeMotionSetExtension scabbardSet)
        {
            BodyBasis(clip, seconds, aim, out Vector3 right, out Vector3 up, out Vector3 forward);
            clip.blade.Evaluate(seconds, out Vector3 grip, out Vector3 dir, out Vector3 edge);
            BladeMotionSetExtension set = scabbardSet;
            BladePose pose = new()
            {
                grip = ToWorld(grip, right, up, forward),
                dir = ToWorld(dir, right, up, forward),
                edge = ToWorld(edge, right, up, forward),
                heightToScreen = projection.heightToScreen,
                depthToScreen = projection.depthToScreen,
                heightOrigin = projection.heightOrigin,
            };
            if (set != null && set.scabbard)
            {
                if (!clip.TryScabbard(seconds, out Vector3 mouth, out Vector3 end))
                {
                    mouth = set.scabbardRestMouth;
                    end = set.scabbardRestEnd;
                }
                Vector3 m = ToWorld(mouth, right, up, forward);
                Vector3 e = ToWorld(end, right, up, forward);
                pose.hasScabbard = true;
                pose.scabbardMouth = m;
                pose.scabbardDir = e - m;
                pose.scabbardLength = (e - m).magnitude;
                // Flat side up when worn; while the blade is in, it follows the blade's edge.
                pose.sheathed = clip.SheathedAt(seconds);
                pose.scabbardEdge = pose.sheathed > 0f ? pose.edge : Vector3.Cross(Vector3.up, pose.scabbardDir);
                pose.scabbardColor = set.scabbardColor;
            }
            return pose;
        }

        /// <summary>World tip and inner point of the trail band at clip second <paramref name="seconds"/>.</summary>
        public static void TrailPoints(BladeMotionClip clip, float seconds, Vector3 right, Vector3 up, Vector3 forward,
            float bladeLength, float inner, out Vector3 innerPoint, out Vector3 tipPoint)
        {
            clip.blade.Evaluate(seconds, out Vector3 g, out Vector3 d, out _);
            Vector3 tip = g + d * bladeLength;
            Vector3 inn = g + d * (bladeLength * inner);
            if (clip.drawPushEnd > clip.drawPushStart && seconds >= clip.drawPushStart && seconds < clip.drawPushEnd)
            {
                // The blade is still leaving the scabbard: keep the tip's bearing round the body and
                // push it out to the radius it reaches at the end of the draw.
                clip.blade.Evaluate(clip.drawPushEnd, out Vector3 g1, out Vector3 d1, out _);
                Vector3 refTip = g1 + d1 * bladeLength;
                float rRef = new Vector2(refTip.x, refTip.z).magnitude;
                float r = new Vector2(tip.x, tip.z).magnitude;
                if (r > 1e-4f)
                {
                    float w = (clip.drawPushEnd - seconds) / (clip.drawPushEnd - clip.drawPushStart);
                    w = w * w * (3f - 2f * w);
                    float rOut = r + (Mathf.Max(r, rRef) - r) * w;
                    Vector3 radial = new(tip.x / r, 0f, tip.z / r);
                    Vector3 outTip = new(radial.x * rOut, tip.y, radial.z * rOut);
                    Vector3 pushedInner = outTip - radial * (bladeLength * (1f - inner));
                    inn = Vector3.Lerp(inn, pushedInner, w);
                    tip = outTip;
                }
            }
            innerPoint = ToWorld(inn, right, up, forward);
            tipPoint = ToWorld(tip, right, up, forward);
        }

        /// <summary>As above, in the clip's body frame at that second (turned with the body when the turn carries the blade).</summary>
        public static void TrailPoints(BladeMotionClip clip, float seconds, Vector3 aim, float bladeLength, float inner,
            out Vector3 innerPoint, out Vector3 tipPoint)
        {
            BodyBasis(clip, seconds, aim, out Vector3 right, out Vector3 up, out Vector3 forward);
            TrailPoints(clip, seconds, right, up, forward, bladeLength, inner, out innerPoint, out tipPoint);
        }

        /// <summary>Scabbard mouth of the clip at that second in world metres from the motion origin; false without scabbard keys.</summary>
        public static bool ScabbardMouth(BladeMotionClip clip, float seconds, Vector3 aim, out Vector3 mouth)
        {
            mouth = default;
            if (clip == null || !clip.TryScabbard(seconds, out Vector3 m, out _))
            {
                return false;
            }
            BodyBasis(clip, seconds, aim, out Vector3 right, out Vector3 up, out Vector3 forward);
            mouth = ToWorld(m, right, up, forward);
            return true;
        }

        /// <summary>
        /// Playback rate after the windup. The motion must be back at idle before the cooldown ends,
        /// so a cooldown shortened below the motion speeds the motion (and its damage) up.
        /// </summary>
        public static float AttackSpeed(BladeMotionClip clip, int cooldownTicks)
        {
            return AttackSpeed(clip, cooldownTicks, clip.windupSeconds);
        }

        /// <summary>As above for a cooldown that starts at clip second <paramref name="from"/>.</summary>
        public static float AttackSpeed(BladeMotionClip clip, int cooldownTicks, float from)
        {
            float motionTicks = Mathf.Max(0f, clip.durationSeconds - from) * 60f;
            return cooldownTicks <= 0 ? 1f : Mathf.Max(1f, motionTicks / cooldownTicks);
        }

        /// <summary>Ticks after the end of the windup at which clip second <paramref name="clipSeconds"/> plays.</summary>
        public static int TicksAfterWindup(BladeMotionClip clip, float clipSeconds, float speed)
        {
            return TicksAfter(clipSeconds, clip.windupSeconds, speed);
        }

        /// <summary>Ticks after clip second <paramref name="from"/> at which clip second <paramref name="clipSeconds"/> plays.</summary>
        public static int TicksAfter(float clipSeconds, float from, float speed)
        {
            return Mathf.Max(0, Mathf.RoundToInt((clipSeconds - from) * 60f / speed));
        }

        /// <summary>
        /// Ground heading of the blade (degrees, counter-clockwise from east, unwrapped) for each
        /// tick of one strike at the given playback rate, for <see cref="SweepDamage.SweepDamageSource.headings"/>.
        /// </summary>
        /// <param name="bladeLength">
        /// Above 0: the heading is the bearing of the blade tip from the pawn's place on the ground
        /// (grip offset and body shift included), so the damage follows the drawn blade. A blade held
        /// off to one side otherwise cut along its direction through the centre, not where it was drawn.
        /// 0: the blade direction (for clips whose grip stays at the body).
        /// </param>
        public static List<float> StrikeHeadings(BladeMotionClip clip, BladeStrike strike, Vector3 aim, float speed, float bladeLength = 0f)
        {
            int ticks = Mathf.Max(1, Mathf.RoundToInt(strike.Seconds * 60f / speed));
            List<float> headings = new(ticks + 1);
            float previous = 0f;
            for (int i = 0; i <= ticks; i++)
            {
                float t = strike.start + strike.Seconds * i / ticks;
                BodyBasis(clip, t, aim, out Vector3 right, out Vector3 up, out Vector3 forward);
                clip.blade.Evaluate(t, out Vector3 grip, out Vector3 dir, out _);
                Vector3 w = ToWorld(dir, right, up, forward);
                if (bladeLength > 0f)
                {
                    Vector3 tip = ToWorld(grip + dir * bladeLength, right, up, forward) + GroundShift(clip, t, aim);
                    tip.y = 0f;
                    // A tip right over the centre has no bearing: keep the direction then.
                    if (tip.sqrMagnitude > 0.04f)
                    {
                        w = tip;
                    }
                }
                float h = Mathf.Atan2(w.z, w.x) * Mathf.Rad2Deg;
                if (i > 0)
                {
                    h += 360f * Mathf.Round((previous - h) / 360f);
                }
                headings.Add(h);
                previous = h;
            }
            return headings;
        }

        /// <summary>
        /// Clip seconds shown for a warmup or cooldown stance on the visual clock; false once the
        /// attack motion has finished (the rest of the cooldown shows the idle).
        /// </summary>
        public static bool TryClipTime(Stance_Busy stance, BladeMotionClip clip, out float seconds)
        {
            return TryClipTime(stance, clip, clip.windupSeconds, out seconds, out _);
        }

        public static bool TryClipTime(Stance_Busy stance, BladeMotionClip clip, out float seconds, out float speed)
        {
            return TryClipTime(stance, clip, clip.windupSeconds, out seconds, out speed);
        }

        /// <param name="cooldownFrom">Clip second the cooldown starts at.</param>
        /// <param name="speed">Playback rate of the part after the windup (1 during the windup).</param>
        public static bool TryClipTime(Stance_Busy stance, BladeMotionClip clip, float cooldownFrom, out float seconds, out float speed)
        {
            seconds = 0f;
            speed = 1f;
            int now = Find.TickManager.TicksGame;
            float total = Mathf.Max(1f, now - stance.startedTick + stance.ticksLeft);
            float elapsed = Mathf.Clamp(VisualClock.Ticks - stance.startedTick, 0f, total);
            if (stance is Stance_Warmup)
            {
                seconds = elapsed / total * clip.windupSeconds;
                return true;
            }
            if (stance is Stance_Cooldown)
            {
                speed = AttackSpeed(clip, Mathf.RoundToInt(total), cooldownFrom);
                seconds = cooldownFrom + elapsed / 60f * speed;
                return seconds < clip.durationSeconds;
            }
            return false;
        }
    }
}
