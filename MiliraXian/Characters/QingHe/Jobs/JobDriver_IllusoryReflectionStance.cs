using System.Collections.Generic;
using MiliraXian.Characters.Common.Combat;
using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.QingHe.Abilities;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Jobs
{
    /// <summary>Phases of Mirror Flower, Water Moon, in order.</summary>
    public enum MirrorPhase
    {
        Windup,
        Stance,
        Shatter,
        CutDelay,
        Cut,
        ReformDelay,
        Recover,
        /// <summary>No hit before the stance ended: the water drains and the weapon returns.</summary>
        Exit,
        Done,
    }

    /// <summary>
    /// Mirror Flower, Water Moon (镜花水月). One state machine: windup, stance (until hit or the
    /// duration ends), then on a hit shatter, a hidden beat, a 360° tilted circle cut, another hidden
    /// beat and the re-form; without a hit a short exit. Untouchable from the start to Done. Only the
    /// stance counters, once. The cut's damage follows the trail's heading curve (a sweep), so a
    /// target is struck when the trail passes it; the damage disc is the whole radius, near targets
    /// included, while the trail itself is only drawn as an outer band.
    /// The ability's cooldown starts when the job ends.
    /// </summary>
    public class JobDriver_IllusoryReflectionStance : JobDriver
    {
        private MirrorPhase phase = MirrorPhase.Windup;
        private int phaseStartTick = -1;
        private int stanceStartTick = -1;
        private Vector3 aim = Vector3.right;
        private int stanceFacingInt = Rot4.South.AsInt;
        private bool counterQueued;
        private bool pressureGiven;
        private bool cooldownStarted;
        private int cutStartTick = -1;

        public override bool PlayerInterruptable => false;

        public Rot4 StanceFacing => new(stanceFacingInt);

        public MirrorPhase Phase => phase;

        /// <summary>Ground direction the stance was cast toward (the trail starts at its back-left).</summary>
        public Vector3 Aim => aim;

        /// <summary>Game tick the current phase started.</summary>
        public int PhaseStartTick => phaseStartTick;

        /// <summary>From the windup to Done every damage is absorbed, no posture lands and no execution.</summary>
        public bool Untouchable => phase != MirrorPhase.Done;

        public int StanceStartTick => stanceStartTick;

        /// <summary>Game tick the cut started; -1 before it.</summary>
        public int CutStartTick => cutStartTick;

        public CompProperties_AbilityIllusoryReflection Properties => Props;

        /// <summary>Seconds into the current phase on the interpolated visual clock (never negative).</summary>
        public float PhaseSeconds => Mathf.Max(0f, Common.Vfx.VisualClock.TicksSince(phaseStartTick)) / 60f;

        /// <summary>Seconds of the re-form within the recovery (she is drawn from the snapshot until then).</summary>
        public float ReformSeconds
        {
            get
            {
                CompProperties_AbilityIllusoryReflection props = Props;
                return ((props?.recoverTicks ?? 30) - (props?.weaponBackTicks ?? 9)) / 60f;
            }
        }

        /// <summary>
        /// The real body is not drawn: from the shatter until the re-form is complete (the snapshot is
        /// drawn instead while she breaks apart and while she takes shape again).
        /// </summary>
        public bool BodyHidden
        {
            get
            {
                switch (phase)
                {
                    case MirrorPhase.Shatter:
                    case MirrorPhase.CutDelay:
                    case MirrorPhase.Cut:
                    case MirrorPhase.ReformDelay:
                        return true;
                    case MirrorPhase.Recover:
                        return PhaseSeconds < ReformSeconds;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Share of the held weapon drawn: fades out over the windup, none from the stance to the end
        /// of the re-form, back over the last part of the recovery; back over the exit.
        /// </summary>
        public float WeaponOpacity
        {
            get
            {
                CompProperties_AbilityIllusoryReflection props = Props;
                float s = PhaseSeconds;
                switch (phase)
                {
                    case MirrorPhase.Windup:
                        return 1f - Smooth(s / Mathf.Max(1e-3f, (props?.windupTicks ?? 30) / 60f));
                    case MirrorPhase.Recover:
                        float back = (props?.weaponBackTicks ?? 9) / 60f;
                        return Mathf.Clamp01((s - ReformSeconds) / Mathf.Max(1e-3f, back));
                    case MirrorPhase.Exit:
                        return Mathf.Clamp01(s / Mathf.Max(1e-3f, (props?.exitTicks ?? 30) / 60f));
                    case MirrorPhase.Done:
                        return 1f;
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>
        /// Factor on her shield's own visibility: fades out over the windup, none from the stance to
        /// the recovery, back over the recovery (or the no-hit exit).
        /// </summary>
        public float ShieldOpacity
        {
            get
            {
                CompProperties_AbilityIllusoryReflection props = Props;
                float s = PhaseSeconds;
                switch (phase)
                {
                    case MirrorPhase.Windup:
                        return 1f - Smooth(s / Mathf.Max(1e-3f, (props?.windupTicks ?? 30) / 60f));
                    case MirrorPhase.Recover:
                        return Smooth(s / Mathf.Max(1e-3f, (props?.recoverTicks ?? 30) / 60f));
                    case MirrorPhase.Exit:
                        return Smooth(s / Mathf.Max(1e-3f, (props?.exitTicks ?? 30) / 60f));
                    case MirrorPhase.Done:
                        return 1f;
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>Smoothstep from 0 to 1.</summary>
        internal static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private int Now => Find.TickManager?.TicksGame ?? 0;

        private int PhaseTicks => Mathf.Max(0, Now - phaseStartTick);

        private CompProperties_AbilityIllusoryReflection Props =>
            job?.ability?.CompOfType<CompAbilityEffect_IllusoryReflection>()?.Props;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            int now = Now;
            stanceStartTick = now;
            SetPhase(MirrorPhase.Windup, now);
            counterQueued = false;
            pressureGiven = false;
            cooldownStarted = false;
            IntVec3 cell = job.GetTarget(TargetIndex.A).Cell;
            if (!cell.IsValid || cell == pawn.Position)
            {
                cell = pawn.Position + pawn.Rotation.FacingCell;
            }
            Vector3 dir = (cell - pawn.Position).ToVector3();
            dir.y = 0f;
            aim = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.right;
            pawn.rotationTracker?.FaceCell(cell);
            stanceFacingInt = pawn.Rotation.AsInt;
            pawn.Rotation = StanceFacing;
        }

        /// <summary>
        /// Damage reaching her: absorbed in every phase but Done. In the stance it is the hit the
        /// counter answers.
        /// </summary>
        public bool TryHandleDamage(ref DamageInfo dinfo, ref bool absorbed)
        {
            if (!Untouchable || dinfo.Amount <= 0f)
            {
                return false;
            }
            if (phase == MirrorPhase.Stance)
            {
                counterQueued = true;
            }
            dinfo.SetAmount(0f);
            absorbed = true;
            return true;
        }

        /// <summary>A hostile attack that missed her (a dodge, a projectile reaching her cell) also triggers the counter.</summary>
        public bool TryHandleAttackAttempt(Thing instigator)
        {
            if (phase != MirrorPhase.Stance || !IsHostileAttackSource(instigator))
            {
                return false;
            }
            counterQueued = true;
            return true;
        }

        /// <summary>A target struck by the cut: the first gives the sword pressure.</summary>
        internal void NotifyCutHit(Thing target)
        {
            if (pressureGiven || !(target is Pawn || target is Building))
            {
                return;
            }
            pressureGiven = true;
            MX_QH_HediffUtility.GetSwordPressure(pawn)?.AddValue(Props?.swordPressureGain ?? 1f);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(_ => StartCooldown());
            Toil toil = ToilMaker.MakeToil("IllusoryReflection");
            toil.initAction = delegate
            {
                Props?.stanceSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            };
            toil.tickAction = delegate
            {
                StateTick();
                // The visuals are not saved: the map component picks the stance up again after a load.
                pawn.Map?.GetComponent<Vfx.MapComponent_MirrorVisuals>()?.Track(pawn);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;
            yield return toil;
        }

        private void StateTick()
        {
            CompProperties_AbilityIllusoryReflection props = Props;
            if (props == null || pawn.Map == null)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            pawn.Rotation = StanceFacing;
            int now = Now;
            int elapsed = PhaseTicks;
            switch (phase)
            {
                case MirrorPhase.Windup:
                    if (ShouldLeave())
                    {
                        SetPhase(MirrorPhase.Exit, now);
                    }
                    else if (elapsed >= props.windupTicks)
                    {
                        SetPhase(MirrorPhase.Stance, now);
                    }
                    break;
                case MirrorPhase.Stance:
                    if (counterQueued)
                    {
                        SetPhase(MirrorPhase.Shatter, now);
                        props.shatterSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    }
                    else if (ShouldLeave() || now - stanceStartTick >= props.durationTicks)
                    {
                        SetPhase(MirrorPhase.Exit, now);
                    }
                    break;
                case MirrorPhase.Shatter:
                    Advance(elapsed, props.shatterTicks, MirrorPhase.CutDelay, now);
                    break;
                case MirrorPhase.CutDelay:
                    if (elapsed >= props.cutDelayTicks)
                    {
                        SetPhase(MirrorPhase.Cut, now);
                        StartCut(props, now);
                    }
                    break;
                case MirrorPhase.Cut:
                    Advance(elapsed, props.cutTicks, MirrorPhase.ReformDelay, now);
                    break;
                case MirrorPhase.ReformDelay:
                    Advance(elapsed, props.reformDelayTicks, MirrorPhase.Recover, now);
                    break;
                case MirrorPhase.Recover:
                    Advance(elapsed, props.recoverTicks, MirrorPhase.Done, now);
                    break;
                case MirrorPhase.Exit:
                    Advance(elapsed, props.exitTicks, MirrorPhase.Done, now);
                    break;
            }
            if (phase == MirrorPhase.Done)
            {
                EndJobWith(JobCondition.Succeeded);
            }
        }

        private void Advance(int elapsed, int duration, MirrorPhase next, int now)
        {
            if (elapsed >= duration)
            {
                SetPhase(next, now);
            }
        }

        private void SetPhase(MirrorPhase next, int now)
        {
            phase = next;
            phaseStartTick = now;
        }

        /// <summary>The stance ends early without a hit: decorative mode, or a player pawn undrafted.</summary>
        private bool ShouldLeave()
        {
            return QinghePowerBalance.Sealed
                || (pawn.Faction == Faction.OfPlayer && pawn.drafter != null && !pawn.Drafted);
        }

        private void StartCut(CompProperties_AbilityIllusoryReflection props, int now)
        {
            Map map = pawn.Map;
            cutStartTick = now;
            props.slashSound?.PlayOneShot(new TargetInfo(pawn.Position, map));
            if (props.sweepDamage == null)
            {
                return;
            }
            float factor = MX_QH_PerkUtility.GetSpellEffectFactor(pawn)
                * pawn.GetStatValue(StatDefOf.MeleeDamageFactor, cacheStaleAfterTicks: -1);
            Vector3 c = pawn.Position.ToVector3Shifted();
            MapComponent_SweepDamage.Add(map, new SweepDamageSource
            {
                def = props.sweepDamage,
                caster = pawn,
                weaponDef = pawn.equipment?.Primary?.def,
                damageDef = props.cutDamageDef,
                posture = props.posture,
                center = new Vector3(c.x, 0f, c.z),
                radius = props.cutRadius,
                damage = props.damage * factor,
                armorPenetration = props.armorPenetration,
                startTick = now,
                headings = MirrorCutGeometry.Headings(aim, props.cutStartYaw, props.cutTicks),
            });
        }

        private void StartCooldown()
        {
            if (cooldownStarted)
            {
                return;
            }
            cooldownStarted = true;
            Ability ability = job?.ability;
            if (ability != null && ability.HasCooldown)
            {
                ability.StartCooldown(ability.def.cooldownTicksRange.RandomInRange);
            }
        }

        private bool IsHostileAttackSource(Thing instigator)
        {
            return instigator != null
                && instigator != pawn
                && instigator.Spawned
                && instigator.MapHeld == pawn.MapHeld
                && GenHostility.HostileTo(pawn, instigator);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref phase, "mx_qh_mirror_phase", MirrorPhase.Windup);
            Scribe_Values.Look(ref phaseStartTick, "mx_qh_mirror_phaseStart", -1);
            Scribe_Values.Look(ref stanceStartTick, "mx_qh_mirror_stanceStart", -1);
            Scribe_Values.Look(ref aim, "mx_qh_mirror_aim", Vector3.right);
            Scribe_Values.Look(ref stanceFacingInt, "mx_qh_mirror_facing", Rot4.South.AsInt);
            Scribe_Values.Look(ref counterQueued, "mx_qh_mirror_counterQueued");
            Scribe_Values.Look(ref pressureGiven, "mx_qh_mirror_pressureGiven");
            Scribe_Values.Look(ref cooldownStarted, "mx_qh_mirror_cooldownStarted");
            Scribe_Values.Look(ref cutStartTick, "mx_qh_mirror_cutStart", -1);
        }
    }

    /// <summary>
    /// The cut's sweep, shared by the damage and the trail. Yaw is relative to the aim, + to the right
    /// (clockwise seen from above facing east); a west-facing stance mirrors it, as the sprite does.
    /// The trail runs from <c>startYaw</c> a full turn with an ease-out: yaw(u) = start + 360·(1 − (1 − u)²).
    /// </summary>
    public static class MirrorCutGeometry
    {
        public static float YawAt(float startYaw, float u)
        {
            u = Mathf.Clamp01(u);
            return startYaw + 360f * (1f - (1f - u) * (1f - u));
        }

        /// <summary>Ground heading (degrees, counter-clockwise from east) of aim-relative <paramref name="yaw"/>.</summary>
        public static float HeadingOf(Vector3 aim, float yaw)
        {
            float aimHeading = SweepDamageSource.HeadingOf(aim);
            float sign = aim.x < 0f ? -1f : 1f;
            return aimHeading - sign * yaw;
        }

        /// <summary>One heading per tick, unwrapped, from the start to the end of the cut.</summary>
        public static List<float> Headings(Vector3 aim, float startYaw, int cutTicks)
        {
            int n = Mathf.Max(1, cutTicks);
            List<float> list = new(n + 1);
            for (int i = 0; i <= n; i++)
            {
                list.Add(HeadingOf(aim, YawAt(startYaw, (float)i / n)));
            }
            return list;
        }
    }

    /// <summary>The cut's contacts: the usual technique damage and posture, and the sword pressure of the first hit.</summary>
    public class SweepDamageWorker_MirrorCut : SweepDamageWorker
    {
        public override void ApplyHit(SweepDamageSource source, Thing target)
        {
            base.ApplyHit(source, target);
            if (source.caster?.jobs?.curDriver is JobDriver_IllusoryReflectionStance driver)
            {
                driver.NotifyCutHit(target);
            }
        }
    }

    /// <summary>Registers the stance's immunity to posture and executions.</summary>
    [StaticConstructorOnStartup]
    public static class IllusoryReflectionInvulnerability
    {
        static IllusoryReflectionInvulnerability()
        {
            CombatPosture.RegisterImmunity(Active);
            ExecutionGuard.Register((victim, _) => Active(victim));
            Common.Vfx.BladeBody.BladeOpacity.Register(pawn =>
                pawn?.jobs?.curDriver is JobDriver_IllusoryReflectionStance driver ? driver.WeaponOpacity : 1f);
        }

        public static bool Active(Pawn pawn)
        {
            return pawn is { Dead: false }
                && pawn.jobs?.curDriver is JobDriver_IllusoryReflectionStance { Untouchable: true };
        }
    }
}
