using System.Collections.Generic;
using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>
    /// Runs the Sakura Dance timeline in place. A ground cast and an airborne cast use different
    /// motions: in the air the first two cuts are made while dropping to the ground and the third
    /// one jumps. Each cut, at its time, spawns a sweep damage source and a visual ring. The motion
    /// (blade, body turn and lift) is a shared clip played by <see cref="BladeMotionPlayer"/>;
    /// Position, shadow and selection stay on the ground.
    /// </summary>
    public class JobDriver_SakuraDance : JobDriver, IMotionPlaybackSource
    {
        private int danceStartTick = -1;
        private Vector3 aim = Vector3.right;
        private Vector3 origin;
        private int spawnedMask;
        private bool airborne;
        private bool landed = true;
        /// <summary>Cast under the open sky in full rain, a thunderstorm or the clear-sky chance: the blade will catch a thunder.</summary>
        private bool thunderEligible;
        /// <summary>The blade carries the caught thunder.</summary>
        private bool thunderCharged;
        /// <summary>Tick the release cut was spawned with the thunder; -1 = not yet.</summary>
        private int thunderReleaseTick = -1;
        private int thunderReleaseIndex = -1;
        private int thunderCatchTick = -1;
        /// <summary>Cuts whose whirlwind has been spawned (it leads the cut, at the take-off crouch).</summary>
        private int updraftMask;
        /// <summary>Motion time of this cast's thunder catch, picked at the start; negative = none.</summary>
        private float thunderCatchAt = -1f;
        /// <summary>Cast to return a caught enemy thunder (<see cref="SakuraThunderReturn"/>): no sky bolt.</summary>
        private bool returnedThunder;

        public override bool PlayerInterruptable => false;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private CompProperties_AbilitySakuraDance Props => job.ability?.CompOfType<CompAbilityEffect_SakuraDance>()?.Props;

        private SakuraDanceMotion Motion
        {
            get
            {
                CompProperties_AbilitySakuraDance props = Props;
                if (props == null)
                {
                    return null;
                }
                return airborne && props.airMotion != null ? props.airMotion : props.groundMotion;
            }
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            danceStartTick = Find.TickManager.TicksGame;
            origin = pawn.Position.ToVector3Shifted();
            IntVec3 target = job.GetTarget(TargetIndex.A).Cell;
            Vector3 dir = target.IsValid ? (target.ToVector3Shifted() - origin) : pawn.Rotation.FacingCell.ToVector3();
            dir.y = 0f;
            aim = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.right;
            spawnedMask = 0;
            // The vanilla cast job tries to take off (tryStartFlying, chance 1), so a pawn that was on
            // the ground is only part way up when the ability fires (warmup < 50-tick takeoff).
            // Only a pawn at full flight height counts as an airborne cast; everyone else is landed.
            Pawn_FlightTracker flight = pawn.flight;
            airborne = flight != null && flight.Flying && flight.PositionOffsetFactor >= 0.99f && Props?.airMotion != null;
            landed = !airborne;
            if (!airborne && flight != null && flight.Flying)
            {
                flight.ForceLand();
            }
            Props?.castSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            // A thunder caught off an enemy's Lightning Reversal is carried from the first cut, whatever the sky.
            returnedThunder = job.count == SakuraThunderReturn.JobMarker;
            thunderEligible = Props?.thunderSweepDamage != null
                && (returnedThunder
                    || SakuraThunderUtility.CanCatch(pawn, Props, Gen.HashCombineInt(danceStartTick, pawn.thingIDNumber ^ 0x5A17)));
            // The bolt comes at a random one of the motion's catch times (before the first or the third
            // cut). Picked from the cast tick and the pawn, not the shared Rand stream, so a pushed
            // or reseeded Rand state around the cast cannot pin it to the same entry every time.
            SakuraDanceMotion m = Motion;
            if (m == null)
            {
                thunderCatchAt = -1f;
            }
            else if (returnedThunder && !m.thunderCatchTimes.NullOrEmpty())
            {
                // Before the first cut: all three cuts carry it.
                thunderCatchAt = m.thunderCatchTimes[0];
            }
            else if (!m.thunderCatchTimes.NullOrEmpty())
            {
                int pick = Mathf.Abs(Gen.HashCombineInt(danceStartTick, pawn.thingIDNumber)) % m.thunderCatchTimes.Count;
                thunderCatchAt = m.thunderCatchTimes[pick];
            }
            else
            {
                thunderCatchAt = m.thunderCatchTime;
            }
            if (thunderEligible && Prefs.DevMode)
            {
                Log.Message($"[MiliraXian] Sakura Dance: thunder catch at {thunderCatchAt:0.##} s ({(airborne ? "air" : "ground")}).");
            }
            thunderCharged = false;
            thunderReleaseTick = -1;
            thunderReleaseIndex = -1;
            thunderCatchTick = -1;
            updraftMask = 0;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil dance = ToilMaker.MakeToil("SakuraDance");
            dance.initAction = delegate
            {
                // Notify_Starting runs before the toils are built, so the duration follows the variant
                // and the thunder slow-down.
                SakuraDanceMotion motion = Motion;
                pawn.jobs.curDriver.ticksLeftThisToil = motion == null ? 138 : RealTicks(motion.durationSeconds);
            };
            dance.tickAction = DanceTick;
            dance.defaultCompleteMode = ToilCompleteMode.Delay;
            dance.defaultDuration = 138;
            dance.handlingFacing = true;
            yield return dance;
        }

        private void DanceTick()
        {
            CompProperties_AbilitySakuraDance props = Props;
            SakuraDanceMotion motion = Motion;
            if (props == null || motion == null || pawn.Map == null)
            {
                return;
            }
            int elapsed = Find.TickManager.TicksGame - danceStartTick;
            if (!landed && motion.landTime >= 0f && elapsed >= RealTicks(motion.landTime))
            {
                // The air motion has carried the pawn down itself; vanilla flight ends here.
                landed = true;
                pawn.flight?.ForceLand();
            }
            if (thunderEligible && !thunderCharged && thunderCatchAt >= 0f
                && elapsed >= RealTicks(thunderCatchAt))
            {
                CatchThunder(props);
            }
            if (thunderCharged)
            {
                pawn.Map.GetComponent<MapComponent_SakuraDanceVisuals>()?.EnsureCharged(pawn);
            }
            for (int i = 0; i < props.cuts.Count && i < 31; i++)
            {
                SakuraCutProps cut = props.cuts[i];
                // Real ticks after the start: the thunder slow-down pushes later cuts back.
                int cutTick = RealTicks(motion.CutTime(cut, i));
                if (cut.updraft != null && (updraftMask & (1 << i)) == 0
                    && elapsed >= RealTicks(motion.CutTime(cut, i) + cut.updraft.delay))
                {
                    // Like the FXR (spawned at the ground dummy 220, then not following the body),
                    // the whirlwind rises from the ground under the pawn, whatever height the jump
                    // is at, and stays there.
                    // It runs on the motion clock, so a catch hit-stop freezes it with the body.
                    updraftMask |= 1 << i;
                    int upTick = Find.TickManager.TicksGame;
                    pawn.Map.GetComponent<MapComponent_SakuraDanceVisuals>()?.AddUpdraft(props, cut.updraft,
                        origin, aim, upTick, MotionClockFrom(upTick));
                }
                if ((spawnedMask & (1 << i)) != 0)
                {
                    continue;
                }
                if (elapsed >= cutTick)
                {
                    spawnedMask |= 1 << i;
                    SpawnCut(props, motion, cut, i, danceStartTick + cutTick);
                }
            }
        }

        private void SpawnCut(CompProperties_AbilitySakuraDance props, SakuraDanceMotion motion, SakuraCutProps cut, int index, int cutStartTick)
        {
            Map map = pawn.Map;
            // The ring stays where it was cut while the body keeps moving, so it is centred on the
            // body height at the middle of the spin (the third cut is made while still rising).
            float midSpin = motion.CutTime(cut, index) + cut.spinSeconds * 0.5f;
            float height = MonotoneCurve.Evaluate(motion.height, midSpin) + props.ringHeightOffset;
            SakuraRingFrame frame = SakuraRingFrame.For(cut, origin, aim, height);
            // Every cut after the catch carries the thunder (its own factors); the release cut also stuns.
            bool charged = thunderCharged;
            bool release = charged && cut.thunderRelease;
            float radius = cut.radius * (charged ? cut.thunderRadiusFactor : 1f);

            // The ring spins on the motion clock, so a cut made during the thunder slow-down sweeps
            // (and is drawn) slowly too, in step with the blade. One heading per real tick.
            int spinTicks = cut.SpinTicks;
            float cutStartMotion = MotionSeconds(cutStartTick);
            List<float> headings = new(spinTicks + 1);
            float prev = 0f;
            for (int k = 0; k <= spinTicks * 20; k++)
            {
                float ct = Mathf.Min(MotionSeconds(cutStartTick + k) - cutStartMotion, cut.spinSeconds);
                float h = SakuraDanceGeometry.HeadHeading(frame, cut, ct);
                if (k > 0)
                {
                    h = prev + Mathf.DeltaAngle(prev, h);
                }
                headings.Add(h);
                prev = h;
                if (ct >= cut.spinSeconds - 1e-4f)
                {
                    break;
                }
            }

            float factor = MX_QH_PerkUtility.GetSpellEffectFactor(pawn)
                * pawn.GetStatValue(StatDefOf.MeleeDamageFactor, cacheStaleAfterTicks: -1);
            SweepDamageDef sweepDef = release ? props.thunderSweepDamage : props.sweepDamage;
            if (sweepDef != null)
            {
                MapComponent_SweepDamage.Add(map, new SweepDamageSource
                {
                    def = sweepDef,
                    caster = pawn,
                    weaponDef = pawn.equipment?.Primary?.def,
                    damageDef = charged ? props.thunderCutDamageDef ?? props.cutDamageDef : props.cutDamageDef,
                    posture = cut.posture + (charged ? cut.thunderPosture : 0f),
                    center = new Vector3(frame.center.x, 0f, frame.center.z),
                    radius = radius,
                    damage = cut.damage * factor * (charged ? cut.thunderDamageFactor : 1f),
                    armorPenetration = cut.armorPenetration,
                    startTick = cutStartTick,
                    headings = headings,
                });
            }

            MapComponent_SakuraDanceVisuals visuals = map.GetComponent<MapComponent_SakuraDanceVisuals>();
            visuals?.AddCut(props, cut, frame, cutStartTick, charged, charged ? cut.thunderRadiusFactor : 1f,
                MotionClockFrom(cutStartTick));
            props.cutSound?.PlayOneShot(new TargetInfo(pawn.Position, map));
            if (release)
            {
                thunderReleaseTick = cutStartTick;
                thunderReleaseIndex = index;
                props.thunderReleaseSound?.PlayOneShot(new TargetInfo(pawn.Position, map));
            }
        }

        private void CatchThunder(CompProperties_AbilitySakuraDance props)
        {
            thunderCharged = true;
            thunderCatchTick = Find.TickManager.TicksGame;
            if (returnedThunder)
            {
                // The thunder came off an enemy's blade, not the sky: no bolt, flash or thunderclap.
                pawn.Map.GetComponent<MapComponent_SakuraDanceVisuals>()?.EnsureCharged(pawn);
                return;
            }
            Map map = pawn.Map;
            // The sky flash lasts exactly as long as the bolt.
            int flashTicks = Mathf.Max(4, Mathf.RoundToInt(props.thunderBoltSeconds * 60f));
            map.weatherManager.eventHandler.AddEvent(new WeatherEvent_SakuraThunderFlash(map, pawn.Position, props.thunderCatchSound, flashTicks));
            map.GetComponent<MapComponent_SakuraDanceVisuals>()?.EnsureCharged(pawn);
        }

        /// <summary>The blade shows electricity: charged, and not yet past the spark tail after the release cut.</summary>
        internal bool ThunderVisible(float visualTicks, out CompProperties_AbilitySakuraDance props)
        {
            props = Props;
            if (!thunderCharged || props == null)
            {
                return false;
            }
            if (thunderReleaseTick < 0 || thunderReleaseIndex < 0 || thunderReleaseIndex >= props.cuts.Count)
            {
                return true;
            }
            float end = props.cuts[thunderReleaseIndex].spinSeconds + props.thunderSparkAfterRelease;
            return (visualTicks - thunderReleaseTick) / 60f <= end;
        }

        /// <summary>
        /// The dance as a motion playback (the Job side of the bridge): the variant's clip on the
        /// dance clock (with the thunder hit-stop), along the aim. The shared player draws the
        /// blade in the turned body frame, turns the sprite and lifts the body (the clip's height is
        /// absolute, so vanilla flight's own lift is taken off).
        /// </summary>
        public bool TryGetPlayback(out MotionPlayback playback)
        {
            return TryGetPlaybackAt(MotionSeconds(VisualClock.Ticks), out playback);
        }

        private bool TryGetPlaybackAt(float seconds, out MotionPlayback playback)
        {
            playback = default;
            CompProperties_AbilitySakuraDance props = Props;
            SakuraDanceMotion motion = Motion;
            if (props == null || motion == null || danceStartTick < 0)
            {
                return false;
            }
            playback = new MotionPlayback
            {
                clip = motion,
                seconds = seconds,
                aim = aim,
                projection = props.Projection,
                source = motion,
                sourceId = danceStartTick,
                heightIncludesFlight = true,
            };
            return true;
        }

        /// <summary>Held blade now (the same pose the player draws).</summary>
        public bool TryGetBladePose(out BladePose pose)
        {
            return TryGetBladeAt(MotionSeconds(VisualClock.Ticks), out pose, out _);
        }

        /// <summary>
        /// Blade pose and drawn body centre at <paramref name="seconds"/> into the dance, for effects
        /// that stay where they were made (the bolt hits where the blade was, discharges stay put).
        /// </summary>
        internal bool TryGetBladeAt(float seconds, out BladePose pose, out Vector3 body)
        {
            pose = default;
            body = pawn.DrawPos;
            if (!TryGetPlaybackAt(seconds, out MotionPlayback at) || at.clip.blade == null
                || !TryGetPlayback(out MotionPlayback now))
            {
                return false;
            }
            pose = BladeMotionUtility.Pose(at.clip, at.seconds, at.aim, at.projection, null);
            body.z += BladeMotionPlayer.BodyLiftAt(pawn, at, seconds) - BladeMotionPlayer.BodyLift(pawn, now);
            return true;
        }

        /// <summary>
        /// Motion time (seconds of the written curves) at <paramref name="ticks"/> game ticks (fractional
        /// for drawing). With the thunder, the motion plays slowly for a moment after the catch.
        /// </summary>
        internal float MotionSeconds(float ticks)
        {
            // The motion starts at skipSeconds: its opening hold is played by the aim (warmup) instead.
            float real = Mathf.Max(0f, (ticks - danceStartTick) / 60f) + (Motion?.skipSeconds ?? 0f);
            if (!SlowActive(out float catchTime, out float slow, out float power))
            {
                return real;
            }
            if (real <= catchTime)
            {
                return real;
            }
            // Hit-stop: stops dead at the catch, eases back to full speed at the end of the window.
            float window = slow * power;
            if (real <= catchTime + window)
            {
                return catchTime + slow * Mathf.Pow((real - catchTime) / window, power);
            }
            return real - (window - slow);
        }

        /// <summary>Game ticks after the start at which the motion reaches <paramref name="motionSeconds"/>.</summary>
        private int RealTicks(float motionSeconds)
        {
            float real = motionSeconds;
            if (SlowActive(out float catchTime, out float slow, out float power) && motionSeconds > catchTime)
            {
                float window = slow * power;
                real = motionSeconds <= catchTime + slow
                    ? catchTime + window * Mathf.Pow((motionSeconds - catchTime) / slow, 1f / power)
                    : motionSeconds + window - slow;
            }
            return Mathf.Max(0, Mathf.RoundToInt((real - (Motion?.skipSeconds ?? 0f)) * 60f));
        }

        private bool SlowActive(out float catchTime, out float slow, out float power)
        {
            SakuraDanceMotion motion = Motion;
            catchTime = thunderCatchAt;
            slow = motion?.thunderSlowSeconds ?? 0f;
            power = Mathf.Clamp(motion?.thunderSlowPower ?? 1f, 1f, 10f);
            return thunderEligible && catchTime >= 0f && slow > 0f && power > 1f;
        }

        /// <summary>
        /// The motion clock as a stand-alone function: effects outlive the job (trails keep fading
        /// after landing), and the driver's props are gone once the job has ended, so they must not
        /// call back into the driver.
        /// </summary>
        internal System.Func<float, float> MotionClockFrom(int startTick)
        {
            int start = danceStartTick;
            float skip = Motion?.skipSeconds ?? 0f;
            bool slowOn = SlowActive(out float catchTime, out float slow, out float power);
            float origin = Clock(startTick);
            return ticks => Clock(ticks) - origin;

            float Clock(float ticks)
            {
                float real = Mathf.Max(0f, (ticks - start) / 60f) + skip;
                if (!slowOn || real <= catchTime)
                {
                    return real;
                }
                float window = slow * power;
                return real <= catchTime + window
                    ? catchTime + slow * Mathf.Pow((real - catchTime) / window, power)
                    : real - (window - slow);
            }
        }

        internal int DanceStartTick => danceStartTick;
        internal int ThunderCatchTick => thunderCatchTick;

        /// <summary>The thunder was caught off an enemy's blade: no sky bolt is drawn.</summary>
        internal bool ReturnedThunder => returnedThunder;
        internal SakuraDanceMotion CurrentMotion => Motion;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref danceStartTick, "mx_qh_sakura_startTick", -1);
            Scribe_Values.Look(ref aim, "mx_qh_sakura_aim", Vector3.right);
            Scribe_Values.Look(ref origin, "mx_qh_sakura_origin");
            Scribe_Values.Look(ref spawnedMask, "mx_qh_sakura_spawned");
            Scribe_Values.Look(ref airborne, "mx_qh_sakura_airborne");
            Scribe_Values.Look(ref landed, "mx_qh_sakura_landed", true);
            Scribe_Values.Look(ref thunderEligible, "mx_qh_sakura_thunderEligible");
            Scribe_Values.Look(ref thunderCharged, "mx_qh_sakura_thunderCharged");
            Scribe_Values.Look(ref thunderReleaseTick, "mx_qh_sakura_thunderReleaseTick", -1);
            Scribe_Values.Look(ref thunderReleaseIndex, "mx_qh_sakura_thunderReleaseIndex", -1);
            Scribe_Values.Look(ref thunderCatchTick, "mx_qh_sakura_thunderCatchTick", -1);
            Scribe_Values.Look(ref updraftMask, "mx_qh_sakura_updraft");
            Scribe_Values.Look(ref thunderCatchAt, "mx_qh_sakura_thunderCatchAt", -1f);
            Scribe_Values.Look(ref returnedThunder, "mx_qh_sakura_returnedThunder");
        }
    }
}
