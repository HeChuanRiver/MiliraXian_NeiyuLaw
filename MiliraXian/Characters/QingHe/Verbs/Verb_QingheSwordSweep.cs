using System;
using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common.Combat;
using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Verbs
{
    /// <summary>
    /// Qinghe's sword attack. In the sword stance with a weapon that has a <see cref="BladeMotionSetExtension"/>
    /// the attack is a motion: raised over a vanilla warmup, then each strike of the clip is a sweep
    /// whose heading follows the blade; every contact resolves through the usual miss, dodge and
    /// block rules. Without a motion set the old instant cone sweep is used.
    /// </summary>
    /// <summary>Qinghe holds the blade in its idle stance only in the sword stance.</summary>
    [StaticConstructorOnStartup]
    internal static class QingheBladeMotionGate
    {
        static QingheBladeMotionGate()
        {
            // Mirror Flower, Water Moon hides the weapon for most of its course.
            // Decorative mode: the vanilla weapon drawing, no held blade.
            BladeMotionPlayer.RegisterIdleGate(pawn =>
                !QinghePowerBalance.Sealed
                && MX_QHCharacterUtility.IsQinghe(pawn)
                && pawn.jobs?.curDriver is not Jobs.JobDriver_IllusoryReflectionStance { WeaponOpacity: <= 0f }
                && MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance == MX_QHDefOf.MX_QH_Stance_Sword);
        }
    }

    /// <summary>
    /// In the sword stance Qinghe always attacks with the sword motion: vanilla otherwise picks a
    /// body-part verb by weight now and then, and a terrain verb (kicking dirt) 4 % of the time.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryGetMeleeVerb))]
    internal static class Patch_QingheSwordVerbChoice
    {
        private static void Postfix(Pawn_MeleeVerbs __instance, Thing target, ref Verb __result)
        {
            Pawn pawn = __instance.Pawn;
            if (__result is Verb_QingheSwordSweep || pawn?.equipment?.Primary == null
                || MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance != MX_QHDefOf.MX_QH_Stance_Sword)
            {
                return;
            }
            List<Verb> verbs = pawn.equipment.PrimaryEq?.AllVerbs;
            if (verbs == null)
            {
                return;
            }
            for (int i = 0; i < verbs.Count; i++)
            {
                if (verbs[i] is Verb_QingheSwordSweep sword && sword.MotionActive && sword.IsStillUsableBy(pawn)
                    && (target == null || sword.IsUsableOn(target)))
                {
                    __result = sword;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Vanilla <see cref="Pawn_MeleeVerbs.TryMeleeAttack"/> reports an attack whenever it asks a verb
    /// to start, whether or not the verb did. A one-attack job (reacting to a melee threat) then ends
    /// as done at once and the think tree issues the next one, every tick, while the sword refuses to
    /// start (the counter recovery, a broken target left to the execution). A refused start is not an
    /// attack: the job keeps waiting in its follow toil instead.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack))]
    internal static class Patch_QingheSwordRefusedStart
    {
        private static void Prefix() => Verb_QingheSwordSweep.StartRefused = false;

        private static void Postfix(Pawn_MeleeVerbs __instance, ref bool __result)
        {
            if (__result && Verb_QingheSwordSweep.StartRefused)
            {
                __result = false;
            }
            Verb_QingheSwordSweep.StartRefused = false;
            // A started windup is not an attack made yet either. A one-attack job (reacting to a melee
            // threat) would otherwise end as done at once, its cleanup cancel the windup, and the next
            // job start it again, every tick: she stands in the windup's first frame, forever in the
            // counter window, and never cuts. The job keeps following until the cut itself.
            Pawn pawn = __instance.Pawn;
            if (__result && pawn?.stances?.curStance is Stance_Warmup { verb: Verb_QingheSwordSweep })
            {
                __result = false;
                return;
            }
            // The cut counted at its draw used up the job's attacks: end it now, from the job's own
            // attack try. The cooldown and its strikes stay (only a warmup is cancelled by the cleanup).
            if (pawn?.stances?.curStance is not Stance_Warmup && Verb_QingheSwordSweep.JobAttacksDone(pawn))
            {
                __result = false;
                pawn.jobs.curDriver.EndJobWith(JobCondition.Succeeded);
            }
        }
    }

    public class Verb_QingheSwordSweep : Verb_MeleeAttackDamage, IBladeMotionVerb, ISweepHitVerb
    {
        /// <summary>Set when this verb declined a start inside the current TryMeleeAttack call.</summary>
        [ThreadStatic]
        internal static bool StartRefused;

        private static readonly Func<Verb_MeleeAttackDamage, LocalTargetInfo, IEnumerable<DamageInfo>> DamageInfosForTarget =
            (Func<Verb_MeleeAttackDamage, LocalTargetInfo, IEnumerable<DamageInfo>>)Delegate.CreateDelegate(
                typeof(Func<Verb_MeleeAttackDamage, LocalTargetInfo, IEnumerable<DamageInfo>>),
                AccessTools.Method(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply"));
        private static readonly Func<Verb_MeleeAttack, Thing, SoundDef> DodgeSound =
            (Func<Verb_MeleeAttack, Thing, SoundDef>)Delegate.CreateDelegate(
                typeof(Func<Verb_MeleeAttack, Thing, SoundDef>),
                AccessTools.Method(typeof(Verb_MeleeAttack), "SoundDodge"));
        private static readonly Func<Verb_MeleeAttack, SoundDef> HitPawnSound =
            (Func<Verb_MeleeAttack, SoundDef>)Delegate.CreateDelegate(
                typeof(Func<Verb_MeleeAttack, SoundDef>),
                AccessTools.Method(typeof(Verb_MeleeAttack), "SoundHitPawn"));
        private static readonly Func<Verb_MeleeAttack, SoundDef> HitBuildingSound =
            (Func<Verb_MeleeAttack, SoundDef>)Delegate.CreateDelegate(
                typeof(Func<Verb_MeleeAttack, SoundDef>),
                AccessTools.Method(typeof(Verb_MeleeAttack), "SoundHitBuilding"));

        private Dictionary<Thing, List<DamageInfo>> swingDamage;
        private float swingDamageFactor = 1f;
        // Damage type of the motion strike now resolving (the named cut); null = the verb's own.
        private DamageDef swingDamageDef;

        // The attack in progress, saved with the verb: which clip, along which aim, and which
        // warmup started it (the cooldown after it shows the same attack).
        private int attackIndex;
        private int nextAttackIndex;
        private int attackStartTick = -1;
        private Vector3 attackAim = Vector3.right;
        // Clip second the cooldown starts at when the attack skipped part of the clip (a counter); -1 = the windup end.
        private float motionFrom = -1f;
        // Tick the melee cooldown after a counter ends: no windup (so no counter) before it.
        private int counterHoldUntilTick = -1;

        private const float SweepMargin = 0.25f;

        private BladeMotionSetExtension MotionSet => EquipmentSource?.def.GetModExtension<BladeMotionSetExtension>();

        private bool UsesMotion
        {
            get
            {
                Pawn pawn = CasterPawn;
                // Decorative mode: no cross-slash motion (windup, strikes, counter, blade drawing).
                return pawn != null && !QinghePowerBalance.Sealed
                    && MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance == MX_QHDefOf.MX_QH_Stance_Sword
                    && MotionSet?.Attack(0) != null;
            }
        }

        private BladeMotionClip CurrentClip => MotionSet?.Attack(attackIndex);

        public override float WarmupTime => UsesMotion ? CurrentClip.windupSeconds : base.WarmupTime;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref attackIndex, "mxAttackIndex");
            Scribe_Values.Look(ref nextAttackIndex, "mxNextAttackIndex");
            Scribe_Values.Look(ref attackStartTick, "mxAttackStartTick", -1);
            Scribe_Values.Look(ref attackAim, "mxAttackAim", Vector3.right);
            Scribe_Values.Look(ref motionFrom, "mxMotionFrom", -1f);
            Scribe_Values.Look(ref counterHoldUntilTick, "mxCounterHoldUntil", -1);
        }

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false,
            bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            if (UsesMotion && Find.TickManager.TicksGame < counterHoldUntilTick)
            {
                // Recovering from a counter: no new windup yet (she still guards meanwhile).
                StartRefused = true;
                return false;
            }
            if (UsesMotion && castTarg.Thing is Pawn broken && CombatPosture.Get(broken) is { Broken: true } posture
                && (posture.ExecutesNext(CasterPawn)
                    || CombatBlockers.ExecutionerFor(CasterPawn)?.ModeFor(CasterPawn) == ExecutionMode.Manual))
            {
                // Its posture is broken: the execution takes it (at the posture's next settle), not a cut.
                // Manual execution: no cut either, only an attack order (which executes) acts on it.
                StartRefused = true;
                return false;
            }
            if (UsesMotion && caster != null && castTarg.IsValid)
            {
                Vector3 aim = castTarg.CenterVector3 - caster.Position.ToVector3Shifted();
                aim.y = 0f;
                attackAim = aim.sqrMagnitude > 0.001f ? aim.normalized : CasterPawn.Rotation.FacingCell.ToVector3();
                attackStartTick = Find.TickManager.TicksGame;
                attackIndex = nextAttackIndex++;
                motionFrom = -1f;
            }
            bool started = base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
            if (started && UsesMotion && CasterPawn?.stances?.curStance is Stance_Warmup warmup && warmup.verb == this)
            {
                SpawnSheatheFlash(warmup);
            }
            return started;
        }

        /// <summary>
        /// During this verb's own warmup a target that has walked out of touch still counts as in
        /// reach while Qinghe can land next to it within <see cref="QingheSwordGuardDef.chaseRange"/>:
        /// the long windup would otherwise drop the attack against anything that moves. Starting an
        /// attack still needs touch, as in vanilla.
        /// </summary>
        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            if (base.CanHitTargetFrom(root, targ))
            {
                return true;
            }
            return UsesMotion && targ.Thing is { Spawned: true } thing
                && CasterPawn?.stances?.curStance is Stance_Warmup warmup && warmup.verb == this
                && TryFindStepCell(root, thing, out _);
        }

        /// <summary>
        /// A cell next to the target, within <see cref="QingheSwordGuardDef.chaseRange"/> of
        /// <paramref name="root"/>, Qinghe can land on and touch the target from: standable, no door, no
        /// other pawn, in her sight with nothing in between (no wall corner cut on a one-cell diagonal).
        /// The nearest to her wins (the shortest move), then the nearest to the target.
        /// </summary>
        private bool TryFindStepCell(IntVec3 root, Thing target, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            Pawn pawn = CasterPawn;
            Map map = pawn?.Map;
            if (map == null || target.Map != map)
            {
                return false;
            }
            float range = Mathf.Max(1.5f, QingheSwordGuard.Config.chaseRange);
            if (root.DistanceToSquared(target.Position) > (range + 1.5f) * (range + 1.5f))
            {
                return false;
            }
            Vector3 aimAt = target.DrawPos;
            float best = float.MaxValue;
            foreach (IntVec3 c in GenAdj.CellsAdjacent8Way(target))
            {
                float move = root.DistanceToSquared(c);
                if (c == root || move > range * range || !c.InBounds(map) || !c.Standable(map) || c.GetDoor(map) != null)
                {
                    continue;
                }
                if (c.x != root.x && c.z != root.z && Mathf.Abs(c.x - root.x) <= 1 && Mathf.Abs(c.z - root.z) <= 1
                    && (!new IntVec3(c.x, 0, root.z).Walkable(map) || !new IntVec3(root.x, 0, c.z).Walkable(map)))
                {
                    continue;
                }
                Pawn other = c.GetFirstPawn(map);
                if (other != null && other != pawn)
                {
                    continue;
                }
                if (!ReachabilityImmediate.CanReachImmediate(c, target, map, PathEndMode.Touch, pawn)
                    || !GenSight.LineOfSight(root, c, map))
                {
                    continue;
                }
                float d = move * 4f + (c.ToVector3Shifted() - aimAt).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    cell = c;
                }
            }
            return cell.IsValid;
        }

        public bool MotionActive => UsesMotion;

        private QingheBladeTrailExtension TrailExt => EquipmentSource?.def.GetModExtension<QingheBladeTrailExtension>();

        public BladeMotionClip ClipFor(Stance_Busy stance, out Vector3 aim, out int attackId, out float cooldownFrom)
        {
            aim = attackAim;
            attackId = attackStartTick;
            BladeMotionClip clip = attackStartTick >= 0 && UsesMotion ? CurrentClip : null;
            cooldownFrom = clip == null ? 0f : CooldownFrom(clip);
            return clip;
        }

        /// <summary>Clip second the cooldown of the attack in progress starts at (later than the windup for a counter).</summary>
        private float CooldownFrom(BladeMotionClip clip) => motionFrom >= 0f ? motionFrom : clip.windupSeconds;

        /// <summary>
        /// The attack's cooldown stance still plays a strike (from the draw to the end of the last strike):
        /// Qinghe cannot guard then.
        /// </summary>
        internal bool InStrikes(Stance_Busy stance)
        {
            BladeMotionClip clip = ClipFor(stance, out _, out _, out float from);
            if (clip == null || stance is not Stance_Cooldown)
            {
                return false;
            }
            // Game ticks, not the visual clock: this decides combat.
            int elapsed = Find.TickManager.TicksGame - stance.startedTick;
            int total = Mathf.Max(1, elapsed + stance.ticksLeft);
            float seconds = from + elapsed / 60f * BladeMotionUtility.AttackSpeed(clip, total, from);
            return seconds < clip.LastStrikeEnd;
        }

        /// <summary>
        /// This verb's warmup is still sheathing: before the flash at the scabbard mouth (the blade
        /// fully in). A melee hit then is parried and countered; after it Qinghe is committed to the draw.
        /// Game ticks, not the visual clock.
        /// </summary>
        internal bool InCounterWindow(Stance_Warmup warmup)
        {
            BladeMotionClip clip = CurrentClip;
            if (!UsesMotion || clip == null || warmup == null || warmup.verb != this)
            {
                return false;
            }
            if (clip.flashTime < 0f || clip.windupSeconds <= 0f)
            {
                return true;
            }
            int elapsed = Find.TickManager.TicksGame - warmup.startedTick;
            int total = Mathf.Max(1, elapsed + warmup.ticksLeft);
            return elapsed / (float)total * clip.windupSeconds < clip.flashTime;
        }

        /// <summary>
        /// A hit parried while the blade goes into the scabbard: deflect (the parry stance, which plays
        /// the set's parry clip), then <see cref="StartCounter"/> when it ends. Replaces the warmup.
        /// </summary>
        internal bool BeginParry(Pawn attacker)
        {
            Pawn pawn = CasterPawn;
            BladeMotionClip clip = CurrentClip;
            if (!UsesMotion || clip == null || clip.counterFrom < 0f || pawn == null || !pawn.Spawned
                || attacker == null || !attacker.Spawned || attacker.Map != pawn.Map
                || pawn.stances?.curStance is not Stance_Warmup warmup || warmup.verb != this)
            {
                return false;
            }
            Vector3 aim = attacker.DrawPos - pawn.Position.ToVector3Shifted();
            aim.y = 0f;
            attackAim = aim.sqrMagnitude > 0.001f ? aim.normalized : pawn.Rotation.FacingCell.ToVector3();
            pawn.stances.SetStance(new Stance_QingheParry(Mathf.Max(1, QingheSwordGuard.Config.parryTicks), attacker, this, attackAim));
            pawn.rotationTracker.Face(attacker.DrawPos);
            return true;
        }

        /// <summary>
        /// The parry has ended: cut back at the attacker from the clip's
        /// <see cref="BladeMotionClip.counterFrom"/>, skipping the windup and the strikes before it.
        /// Afterwards the attack waits the recovery (guard only) before the next windup.
        /// </summary>
        internal bool StartCounter(Pawn attacker)
        {
            Pawn pawn = CasterPawn;
            BladeMotionClip clip = CurrentClip;
            // A parry already under way when decorative mode is switched on ends without its counter.
            if (QinghePowerBalance.Sealed
                || !UsesMotion || clip == null || clip.counterFrom < 0f || pawn == null || !pawn.Spawned
                || attacker == null || !attacker.Spawned || attacker.Dead || attacker.Map != pawn.Map)
            {
                return false;
            }
            int now = Find.TickManager.TicksGame;
            Vector3 center = pawn.Position.ToVector3Shifted();
            center.y = 0f;
            Vector3 aim = attacker.DrawPos - center;
            aim.y = 0f;
            attackAim = aim.sqrMagnitude > 0.001f ? aim.normalized : pawn.Rotation.FacingCell.ToVector3();
            attackStartTick = now;
            motionFrom = clip.counterFrom;
            currentTarget = attacker;
            float speed = BladeMotionUtility.AttackSpeed(clip, verbProps.AdjustedCooldownTicks(this, pawn));
            int cooldown = Mathf.Max(1, Mathf.RoundToInt((clip.durationSeconds - motionFrom) * 60f / speed));
            // The stance first: the sweeps are bound to the cooldown stance started this tick.
            pawn.stances.SetStance(new Stance_Cooldown(cooldown, attacker, this));
            pawn.rotationTracker.Face(attacker.DrawPos);
            QueueStrikes(pawn, attacker, clip, motionFrom, speed, center, now, clip.counterDamageDef ?? clip.damageDef, sureHit: attacker,
                posture: QingheSwordGuard.Config.counterCutPosture);
            // The counter counts as an attack: a melee cooldown follows before the next windup.
            counterHoldUntilTick = now + cooldown + Mathf.Max(0, QingheSwordGuard.Config.counterRecoveryTicks);
            CountJobAttack(pawn);
            return true;
        }

        protected override bool TryCastShot()
        {
            Pawn pawn = CasterPawn;
            LocalTargetInfo originalTarget = currentTarget;
            Thing primary = originalTarget.Thing;
            // The shot fires from Stance_Warmup.Expire while that warmup is still the current stance.
            bool ownWarmup = pawn?.stances?.curStance is Stance_Warmup warmup && warmup.verb == this;
            if (pawn == null || !pawn.Spawned || (pawn.stances.FullBodyBusy && !ownWarmup)
                || primary == null || !primary.Spawned || primary.MapHeld != pawn.MapHeld)
            {
                return false;
            }

            // Decorative mode: a plain vanilla melee hit on the target, no cone sweep.
            if (QinghePowerBalance.Sealed
                || MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance != MX_QHDefOf.MX_QH_Stance_Sword)
            {
                return base.TryCastShot();
            }

            if (UsesMotion && attackStartTick >= 0)
            {
                return StartMotionStrikes(pawn, primary);
            }

            return InstantSweep(pawn, primary, originalTarget);
        }

        /// <summary>
        /// Queues one sweep per strike of the current clip, bound to the cooldown stance vanilla sets
        /// right after this shot. Damage happens as the blade passes, not now.
        /// </summary>
        private bool StartMotionStrikes(Pawn pawn, Thing primary)
        {
            BladeMotionClip clip = CurrentClip;
            int now = Find.TickManager.TicksGame;
            int cooldownTicks = verbProps.AdjustedCooldownTicks(this, pawn);
            float speed = BladeMotionUtility.AttackSpeed(clip, cooldownTicks);
            // The pawn's place on the ground: the motion only moves the drawn body.
            Vector3 center = pawn.DrawPos - BladeMotionPlayer.BodyOffset(pawn);
            center.y = 0f;

            // The target walked off during the windup: the draw carries her next to it (up to chaseRange).
            // The drawn body slides after (vanilla tweening), so the sweep is centred on the new cell, not DrawPos.
            if (!pawn.CanReachImmediate(primary, PathEndMode.Touch) && TryFindStepCell(pawn.Position, primary, out IntVec3 step))
            {
                pawn.Position = step;
                pawn.pather.Notify_Teleported_Int();
                center = step.ToVector3Shifted();
                center.y = 0f;
            }
            // Cut at where the target is now, not where it stood when the windup began.
            Vector3 aim = primary.DrawPos - center;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.001f)
            {
                attackAim = aim.normalized;
            }

            pawn.rotationTracker.Face(primary.DrawPos);
            if (!IsTargetImmobileFor(primary) && pawn.skills != null && (primary is not Pawn p || !p.IsColonyMech))
            {
                pawn.skills.Learn(SkillDefOf.Melee, 200f * verbProps.AdjustedFullCycleTime(this, pawn));
            }
            QueueStrikes(pawn, primary, clip, clip.windupSeconds, speed, center, now, clip.damageDef,
                posture: QingheSwordGuard.Config.attackPosture);
            CountJobAttack(pawn);
            // Counts as a used attack for vanilla bookkeeping (engagement); the hits come later.
            return true;
        }

        private static readonly AccessTools.FieldRef<JobDriver_AttackMelee, int> MeleeAttacksMade =
            AccessTools.FieldRefAccess<JobDriver_AttackMelee, int>("numMeleeAttacksMade");

        /// <summary>
        /// The melee job's attack count, at the draw instead of the windup start (which
        /// <see cref="Patch_QingheSwordRefusedStart"/> does not report): a one-attack job ends after
        /// its cut. Ending it now leaves the cooldown, and the strikes bound to it, in place.
        /// </summary>
        private static void CountJobAttack(Pawn pawn)
        {
            if (pawn.jobs?.curDriver is not JobDriver_AttackMelee driver || pawn.CurJob == null)
            {
                return;
            }
            // Only counted here: the shot runs inside the verb's burst, before vanilla sets the cooldown.
            // The job ends from its next attack try (Patch_QingheSwordRefusedStart).
            MeleeAttacksMade(driver)++;
        }

        /// <summary>The melee job has made all its attacks (counted at the draws).</summary>
        internal static bool JobAttacksDone(Pawn pawn) =>
            pawn.jobs?.curDriver is JobDriver_AttackMelee driver && pawn.CurJob != null
            && MeleeAttacksMade(driver) >= pawn.CurJob.maxNumMeleeAttacks;

        /// <summary>
        /// One sweep per strike of <paramref name="clip"/> from clip second <paramref name="from"/> on (the
        /// cooldown's start) and the trail, bound to the cooldown stance started at <paramref name="now"/>.
        /// </summary>
        private void QueueStrikes(Pawn pawn, Thing primary, BladeMotionClip clip, float from, float speed, Vector3 center, int now,
            DamageDef damageDef, Thing sureHit = null, float posture = 0f)
        {
            SweepDamageDef def = MX_QHDefOf.MX_QH_SwordSweep;
            Map map = pawn.MapHeld;
            float reach = Mathf.Max(verbProps.range, 1.5f) + SweepMargin;
            int last = clip.strikes.Count - 1;
            while (last >= 0 && clip.strikes[last].start < from)
            {
                last--;
            }
            for (int i = 0; i <= last; i++)
            {
                BladeStrike strike = clip.strikes[i];
                if (strike.start < from)
                {
                    continue;
                }
                SweepDamageSource source = new()
                {
                    def = def,
                    caster = pawn,
                    weaponDef = EquipmentSource?.def,
                    damageDef = damageDef,
                    center = center,
                    radius = reach,
                    damage = strike.damageFactor,
                    resourceFactor = strike.resourceFactor,
                    verb = this,
                    guaranteedTarget = i == last ? primary : null,
                    sureHitTarget = sureHit,
                    posture = posture,
                    primaryTarget = primary,
                    boundStanceTick = now,
                    startTick = now + BladeMotionUtility.TicksAfter(strike.start, from, speed),
                    headings = BladeMotionUtility.StrikeHeadings(clip, strike, attackAim, speed,
                        EquipmentSource?.def.GetModExtension<BladeGraphicExtension>()?.length ?? 1.1f),
                };
                MapComponent_SweepDamage.Add(map, source);
            }
            if (primary is Pawn victim && !victim.Dead)
            {
                victim.mindState.meleeThreat = pawn;
                victim.mindState.lastMeleeThreatHarmTick = now;
            }
            if (clip.HasTrail && TrailExt is { } trail)
            {
                // Trail second 0 = clip second trailFrom; after `from` the clip plays at `speed`.
                float trailFrom = Mathf.Max(clip.trailStart, from);
                float trailTick = now + (trailFrom - from) * 60f / speed;
                QingheBladeTrail.SpawnTrail(pawn, trail, clip, attackAim, Mathf.RoundToInt(trailTick), speed,
                    QingheSwordCombatUtility.ResonanceFor(pawn), trailFrom);
            }
        }

        /// <summary>The flash at the scabbard mouth, timed on the warmup (which plays the clip's windup).</summary>
        private void SpawnSheatheFlash(Stance_Warmup warmup)
        {
            BladeMotionClip clip = CurrentClip;
            QingheBladeTrailExtension ext = TrailExt;
            if (clip == null || ext?.sheatheFlash == null || clip.flashTime < 0f || clip.flashTime >= clip.windupSeconds)
            {
                return;
            }
            float total = Mathf.Max(1f, warmup.ticksLeft);
            float flashTick = warmup.startedTick + clip.flashTime / clip.windupSeconds * total;
            Pawn pawn = CasterPawn;
            int id = attackStartTick;
            QingheBladeTrail.SpawnFlash(pawn, ext, clip, attackAim, flashTick,
                () => pawn.Spawned && pawn.stances?.curStance is Stance_Warmup w && w.verb == this && attackStartTick == id);
        }

        private static bool IsTargetImmobileFor(Thing thing)
        {
            if (thing is Pawn pawn && !pawn.Downed)
            {
                return pawn.GetPosture() != PawnPosture.Standing;
            }
            return true;
        }

        /// <summary>One contact of a motion strike: the vanilla melee hit sequence for this target.</summary>
        public void ResolveSweepHit(SweepDamageSource source, Thing target)
        {
            Pawn pawn = CasterPawn;
            if (pawn == null || !pawn.Spawned || target == null || !target.Spawned || target.MapHeld != pawn.MapHeld)
            {
                return;
            }
            LocalTargetInfo previousTarget = currentTarget;
            float previousFactor = swingDamageFactor;
            DamageDef previousDef = swingDamageDef;
            float previousPressure = QingheSwordCombatUtility.swordPressureScaleOverride;
            currentTarget = target;
            swingDamageFactor = source.damage;
            swingDamageDef = source.damageDef;
            TechniqueDamageExtension technique = TechniqueDamage.For(source.damageDef);
            // A zero share must still mean "none", not "unscaled".
            QingheSwordCombatUtility.swordPressureScaleOverride = Mathf.Max(1e-6f, source.resourceFactor);
            try
            {
                using CombatBlock.Scope defense = CombatBlock.Begin(this, target);
                // The defender's blocker reads the strike's posture (a block costs it half).
                using CombatBlock.PostureScope carried = CombatBlock.WithAttackPosture(source.posture);
                Map map = pawn.MapHeld;
                Pawn victim = target as Pawn;
                if (victim != null && !victim.Dead)
                {
                    victim.mindState.meleeThreat = pawn;
                    victim.mindState.lastMeleeThreatHarmTick = Find.TickManager.TicksGame;
                }
                SoundDef sound;
                ThingDef weapon = EquipmentSource?.def;
                // The counter's own target was just turned aside: no miss or dodge (its blocks still apply).
                bool sure = target != null && target == source.sureHitTarget;
                if (!sure && !CombatBlock.RollHit(this, target))
                {
                    sound = null;
                    if (technique != null)
                    {
                        TechniqueDamage.CreateLog(pawn, target, source.damageDef, weapon, technique.logRulesMiss, alwaysShow: false);
                    }
                    else
                    {
                        CreateCombatLog(m => m.combatLogRulesMiss, alwaysShow: false);
                    }
                }
                else if (!sure && CombatBlock.RollDodge(this, target))
                {
                    sound = DodgeSound(this, target);
                    MoteMaker.ThrowText(target.DrawPos, map, "TextMote_Dodge".Translate(), 1.9f);
                    if (technique != null)
                    {
                        TechniqueDamage.CreateLog(pawn, target, source.damageDef, weapon, technique.logRulesDodge, alwaysShow: false);
                    }
                    else
                    {
                        CreateCombatLog(m => m.combatLogRulesDodge, alwaysShow: false);
                    }
                }
                else
                {
                    sound = target.def.category == ThingCategory.Building ? HitBuildingSound(this) : HitPawnSound(this);
                    DamageWorker.DamageResult result;
                    if (technique != null)
                    {
                        BattleLogEntry_MeleeCombat entry = TechniqueDamage.CreateLog(pawn, target, source.damageDef, weapon);
                        result = ApplyMeleeDamageToTarget(target);
                        TechniqueDamage.FinishLog(entry, result, source.damageDef);
                    }
                    else
                    {
                        BattleLogEntry_MeleeCombat entry = CreateCombatLog(m => m.combatLogRulesHit, alwaysShow: true);
                        result = ApplyMeleeDamageToTarget(target);
                        LogResult(entry, result);
                    }
                    // A blow the target's own guard turned aside: its blocker already settled the posture.
                    if (!CombatBlock.Blocked(this, target))
                    {
                        SweepDamageWorker.AddPosture(source, target, result);
                    }
                }
                sound?.PlayOneShot(new TargetInfo(target.Position, map));
                if (victim != null && !victim.Dead && victim.Spawned)
                {
                    victim.stances.stagger.StaggerFor(95);
                }
            }
            finally
            {
                currentTarget = previousTarget;
                swingDamageFactor = previousFactor;
                swingDamageDef = previousDef;
                QingheSwordCombatUtility.swordPressureScaleOverride = previousPressure;
            }
        }

        private void LogResult(BattleLogEntry_MeleeCombat entry, DamageWorker.DamageResult result)
        {
            if (entry == null)
            {
                return;
            }
            if (result.stunned && result.parts.NullOrEmpty())
            {
                Find.BattleLog.RemoveEntry(entry);
                return;
            }
            result.AssociateWithLog(entry);
            if (result.deflected)
            {
                entry.RuleDef = maneuver.combatLogRulesDeflect;
                entry.alwaysShowInCompact = false;
            }
        }

        /// <summary>Damage share of the motion strike now resolving (1 outside a motion).</summary>
        public float SwingDamageFactor => swingDamageFactor;

        private bool InstantSweep(Pawn pawn, Thing primary, LocalTargetInfo originalTarget)
        {
            Map map = pawn.MapHeld;
            List<Thing> targets = new() { primary };
            HashSet<Thing> seen = new() { primary };
            List<IntVec3> cells = new();
            QingheSwordCombatUtility.FillConeCells(pawn, pawn.Position, primary.Position, verbProps.range, 120f, cells);
            foreach (IntVec3 cell in cells)
            {
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (thing == pawn || !thing.Spawned || thing.Destroyed
                        || thing is not Pawn && thing is not Building
                        || thing is Pawn victim && (victim.Dead || victim.Downed && !NeiyuLawMod.QingheSweepHitsDowned)
                        || !Common.SweepDamage.SweepTargeting.FactionAllows(pawn, thing, Common.SweepDamage.SweepTargetFaction.Hostile)
                        || !seen.Add(thing))
                    {
                        continue;
                    }

                    targets.Add(thing);
                }
            }

            Dictionary<Thing, List<DamageInfo>> previousDamage = swingDamage;
            swingDamage = new Dictionary<Thing, List<DamageInfo>>();
            try
            {
                // Materialize the vanilla damage rolls before any hit changes pressure or buffs.
                foreach (Thing target in targets)
                {
                    swingDamage.Add(target, new List<DamageInfo>(DamageInfosForTarget(this, target)));
                }

                bool hit = base.TryCastShot();
                for (int i = 1; i < targets.Count; i++)
                {
                    Thing target = targets[i];
                    if (!pawn.Spawned || pawn.Dead || pawn.MapHeld != map)
                    {
                        break;
                    }
                    if (target.Destroyed || !target.Spawned || target.MapHeld != map
                        || target is Pawn victim && victim.Dead
                        || !Common.SweepDamage.SweepTargeting.FactionAllows(pawn, target, Common.SweepDamage.SweepTargetFaction.Hostile))
                    {
                        continue;
                    }

                    currentTarget = target;
                    hit |= TryHitSweepTarget(target, map);
                }

                return hit;
            }
            finally
            {
                currentTarget = originalTarget;
                swingDamage = previousDamage;
            }
        }

        private bool TryHitSweepTarget(Thing target, Map map)
        {
            using CombatBlock.Scope defense = CombatBlock.Begin(this, target);
            Pawn victim = target as Pawn;
            if (victim != null)
            {
                victim.mindState.meleeThreat = CasterPawn;
                victim.mindState.lastMeleeThreatHarmTick = Find.TickManager.TicksGame;
                victim.stances.stagger.StaggerFor(95);
            }

            if (!CombatBlock.RollHit(this, target))
            {
                CreateCombatLog(m => m.combatLogRulesMiss, alwaysShow: false);
                return false;
            }
            if (CombatBlock.RollDodge(this, target))
            {
                DodgeSound(this, target)?.PlayOneShot(new TargetInfo(target.Position, map));
                MoteMaker.ThrowText(target.DrawPos, map, "TextMote_Dodge".Translate(), 1.9f);
                CreateCombatLog(m => m.combatLogRulesDodge, alwaysShow: false);
                return false;
            }

            BattleLogEntry_MeleeCombat entry = CreateCombatLog(m => m.combatLogRulesHit, alwaysShow: true);
            DamageWorker.DamageResult result = ApplyMeleeDamageToTarget(target);
            LogResult(entry, result);
            return true;
        }

        protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)
        {
            if (swingDamage != null && swingDamage.TryGetValue(target.Thing, out List<DamageInfo> damageInfos))
            {
                return ApplyAll(target, damageInfos);
            }
            if (swingDamageFactor != 1f || swingDamageDef != null)
            {
                List<DamageInfo> scaled = new();
                Tool techniqueTool = TechniqueDamage.ToolFor(swingDamageDef);
                bool first = true;
                foreach (DamageInfo d in DamageInfosForTarget(this, target))
                {
                    DamageInfo copy = d;
                    copy.SetAmount(d.Amount * swingDamageFactor);
                    // The main packet becomes the named cut; extra packets (other damage types) stay.
                    if (first && swingDamageDef != null)
                    {
                        copy.Def = swingDamageDef;
                        copy.SetTool(techniqueTool);
                    }
                    first = false;
                    scaled.Add(copy);
                }
                return ApplyAll(target, scaled);
            }
            return base.ApplyMeleeDamageToTarget(target);
        }

        private static DamageWorker.DamageResult ApplyAll(LocalTargetInfo target, List<DamageInfo> damageInfos)
        {
            DamageWorker.DamageResult result = new();
            foreach (DamageInfo damage in damageInfos)
            {
                if (target.ThingDestroyed)
                {
                    break;
                }
                result = target.Thing.TakeDamage(damage);
            }
            return result;
        }
    }
}
