using System.Collections.Generic;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.Common.Combat;
using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Jobs;
using MiliraXian.Characters.QingHe.Verbs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>Values of Qinghe's sword guard (MX_QH_SwordGuard).</summary>
    public sealed class QingheSwordGuardDef : Def
    {
        /// <summary>Guard stance length, and the extensions a further hit adds (one per hit, the last repeats as 0).</summary>
        public int guardTicks = 30;
        public List<int> extendTicks = new() { 12, 8, 5, 3 };
        /// <summary>Plain block: posture to Qinghe and to the melee attacker.</summary>
        public float blockSelfPosture = 5f, blockAttackerPosture = 5f;
        /// <summary>Share of an attack's own posture a plain block still costs Qinghe.</summary>
        public float blockCarriedPostureShare = 0.5f;
        /// <summary>Parry: posture to the melee attacker (none to Qinghe).</summary>
        public float parryAttackerPosture = 20f;
        /// <summary>Parry while sheathing (化解): posture to the melee attacker.</summary>
        public float counterAttackerPosture = 30f;
        /// <summary>Posture each unblocked hit of her attack strikes, and of the counter cut, adds to the enemy.</summary>
        public float attackPosture = 5f, counterCutPosture = 10f;
        /// <summary>A parried melee attacker is stunned this long (its own cooldown pauses meanwhile).</summary>
        public int parryStaggerTicks = 120;
        /// <summary>Stun of the attacker when the parry happens while sheathing (the counter, 化解).</summary>
        public int counterStaggerTicks = 210;
        /// <summary>The deflect before a counter (parry clip), ticks.</summary>
        public int parryTicks = 24;
        /// <summary>Melee cooldown after the counter's cut, before the next windup (guard and parry only).</summary>
        public int counterRecoveryTicks = 60;
        /// <summary>
        /// Cross-slash windup chase, cells: a target that walks out of touch during the windup is still
        /// pursued while Qinghe can land next to it within this distance (in sight); the draw moves her there.
        /// </summary>
        public float chaseRange = 4f;
        /// <summary>Plain block of a projectile: posture to Qinghe.</summary>
        public float projectileBlockSelfPosture = 8f;
        /// <summary>A melee hit she did not turn aside, when the attack carries no posture of its own.</summary>
        public float struckSelfPosture = 10f;

        /// <summary>Impact effects and sounds: plain block, parry, the parry before a counter (化解), posture break, execution.</summary>
        public QingheImpactVfxProps blockVfx, parryVfx, counterVfx, breakVfx, executionVfx;
        /// <summary>The draw cut of the execution dash, laid along the dash through the enemy.</summary>
        public QingheImpactVfxProps executionDashVfx;
        /// <summary>Afterimages left along the execution dash.</summary>
        public int executionAfterimages = 4;
        /// <summary>Share of the blade (from the grip) the block effects sit on.</summary>
        public float contactShare = 0.7f;
        public string vfxTexPath = "MiliraXianQinghe/Effect/SakuraDance/Glow";
        public ScreenBloomProperties bloom;

        /// <summary>
        /// Execution of an enemy whose posture Qinghe broke: an iaido played on the attack clip
        /// (<see cref="executionAttack"/>) through <see cref="executionMotion"/> (execution second → clip second).
        /// The blow lands at <see cref="executionHitSeconds"/> (the blade clicks home), as one hit on the core part.
        /// </summary>
        public JobDef executionJob;
        public int executionAttack;
        public List<QingheExecutionSegment> executionMotion = new();
        public float executionSeconds = 3f;
        /// <summary>Execution seconds the draw cut starts at (the trail) and the blade clicks home (the hit).</summary>
        public float executionCutSeconds = 0.95f, executionHitSeconds = 2.4f;
        /// <summary>Execution reach, cells (in sight). The execution's dash closes the distance.</summary>
        public float executionRange = 12f;
        public DamageDef executionDamageDef;
        public float executionDamage = 600f, executionArmorPenetration = 2f;
        /// <summary>Sword pressure the blow grants, fixed (no resonance bonus or strike share).</summary>
        public float executionSwordPressure = 1f;
        public SoundDef executionDrawSound;
        /// <summary>
        /// Share of its maximum posture an enemy she broke keeps when its break ends without her
        /// execution (she was busy with others): it breaks again soon (as Sekiro's posture).
        /// </summary>
        public float unexecutedPostureKept = 0.8f;

        /// <summary>
        /// The sword stance's guard motion, the same with any melee weapon: the projection, the guard
        /// and the guard recoil (guard, guardHit). The deflect before a counter belongs to the cross
        /// slash and stays on the weapon that has it.
        /// </summary>
        public BladeMotionSetExtension guardMotion;
        /// <summary>
        /// The execution is always this weapon's iaido, drawn with this weapon (its attack clip
        /// <see cref="executionAttack"/>, its trail and its scabbard), whatever Qinghe holds.
        /// </summary>
        public ThingDef executionWeapon;

        /// <summary>
        /// The killing thrust after a blow that landed (no guard negated it) and left the enemy alive
        /// and mortal: played from execution second <see cref="killStartSeconds"/> instead of the
        /// return, toward the enemy; at clip second <see cref="killAtSeconds"/> the blade is drawn out
        /// and the enemy dies. Null = no thrust.
        /// </summary>
        public BladeMotionClip killMotion;
        /// <summary>
        /// Petals burst from the wound as the blade is drawn out (the kill): at <see cref="killPetalShare"/>
        /// of the blade from the grip, where it runs through the enemy, thrown the way the blade is drawn. Null = none.
        /// </summary>
        public QingheImpactVfxProps killPetalVfx;
        public float killPetalShare = 0.6f;
        public float killStartSeconds = 2.1f;
        public float killAtSeconds = 0.9f;
        public float killBlendSeconds = 0.1f;
        /// <summary>Damage of the thrust on a target that cannot die (deathless, prevents death), instead of the kill.</summary>
        public float killWoundDamage = 200f;
        /// <summary>The thrust runs the enemy through at clip second <see cref="killThrustSeconds"/> (this sound); the draw (the kill) plays its own.</summary>
        public float killThrustSeconds = 0.8f;
        public SoundDef killThrustSound, killDrawSound;
        /// <summary>The flash on the blade with the thrust, at <see cref="killThrustFlashShare"/> of the blade from the grip. Null = none.</summary>
        public QingheSheatheFlashProps killThrustFlash;
        public float killThrustFlashShare = 0.9f;
        /// <summary>The flash at the scabbard mouth of the sheathe the blow lands with, instead of the weapon's. Null = the weapon's.</summary>
        public QingheSheatheFlashProps executionHitFlash;
        /// <summary>The word over the killed enemy and the darkening around it.</summary>
        public QingheKillVfxProps killVfx;

        public int ExecutionTicks => Mathf.Max(12, Mathf.RoundToInt(executionSeconds * 60f));

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors())
            {
                yield return e;
            }
            if (executionJob == null || executionDamageDef == null || executionMotion.NullOrEmpty()
                || executionMotion.Exists(s => s == null || s.end <= s.start))
            {
                yield return "处决 Job、伤害类型和动作分段必须完整，每段 end > start。";
            }
            if (!(executionCutSeconds >= 0f && executionCutSeconds < executionHitSeconds && executionHitSeconds <= executionSeconds
                && executionRange >= 1.42f && executionDamage > 0f))
            {
                yield return "处决时间须满足 0 ≤ 出刀 < 收刀 ≤ 总时长，距离至少 1.42，伤害为正。";
            }
            if (killMotion != null && (killMotion.blade == null || killMotion.durationSeconds <= 0f
                || killAtSeconds < 0f || killAtSeconds > killMotion.durationSeconds || killStartSeconds < executionHitSeconds))
            {
                yield return "killMotion 需要 blade 关键帧；0 ≤ killAtSeconds ≤ 片段时长，killStartSeconds 不早于 executionHitSeconds。";
            }
            if (guardMotion?.guard?.blade == null)
            {
                yield return "guardMotion 需要带 blade 关键帧的 guard 片段。";
            }
            if (executionWeapon?.GetModExtension<BladeMotionSetExtension>()?.Attack(executionAttack) == null
                || executionWeapon.GetModExtension<QingheBladeTrailExtension>() == null)
            {
                yield return "executionWeapon 必须带动作集（含处决用的攻击片段）和刀光。";
            }
            if (guardTicks < 1 || extendTicks == null || extendTicks.Exists(t => t < 0))
            {
                yield return "防御时长必须为正，延长量不能为负。";
            }
            if (blockSelfPosture < 0f || blockAttackerPosture < 0f || parryAttackerPosture < 0f
                || projectileBlockSelfPosture < 0f || struckSelfPosture < 0f)
            {
                yield return "架势伤害不能为负。";
            }
        }
    }

    /// <summary>
    /// One piece of the execution motion: execution seconds <see cref="start"/>–<see cref="end"/> play
    /// the attack clip from <see cref="clipFrom"/> to <see cref="clipTo"/> (backwards when lower).
    /// Entering a piece cross-fades over <see cref="blend"/> seconds, so pieces need not join.
    /// </summary>
    public class QingheExecutionSegment
    {
        public float start;
        public float end;
        public float clipFrom;
        public float clipTo;
        public float blend;
        /// <summary>Played along the way back (towards the enemy after the dash past it).</summary>
        public bool aimBack;
        /// <summary>Qinghe dashes through the enemy to the cell behind it over this piece.</summary>
        public bool dash;

        public float ClipAt(float seconds) => Mathf.Lerp(clipFrom, clipTo, Mathf.InverseLerp(start, end, seconds));
    }

    /// <summary>
    /// Qinghe holds the sword up and takes the hits on it. A <see cref="Stance_Busy"/>: vanilla stops
    /// her moving and attacking meanwhile and saves it. A hit while it lasts is always parried and
    /// lengthens it, by less each time. When it ends the melee cooldown is already over.
    /// </summary>
    public class Stance_QingheGuard : Stance_Busy, IGuardMotionStance
    {
        private int hits;
        private int lastHitTick = -1;
        private Vector3 aim = Vector3.right;

        public Stance_QingheGuard()
        {
        }

        public Stance_QingheGuard(int ticks, Thing attacker, Vector3 aim) : base(ticks, attacker, null)
        {
            this.aim = aim;
            neverAimWeapon = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hits, "mxGuardHits");
            Scribe_Values.Look(ref lastHitTick, "mxGuardLastHit", -1);
            Scribe_Values.Look(ref aim, "mxGuardAim", Vector3.right);
        }

        public void Notify_Hit(Thing attacker, List<int> extendTicks)
        {
            int extra = extendTicks.NullOrEmpty() ? 0 : hits < extendTicks.Count ? extendTicks[hits] : 0;
            hits++;
            ticksLeft += extra;
            lastHitTick = Find.TickManager.TicksGame;
            if (attacker != null && attacker.Spawned)
            {
                Vector3 d = attacker.DrawPos - Pawn.DrawPos;
                d.y = 0f;
                if (d.sqrMagnitude > 0.001f)
                {
                    aim = d.normalized;
                }
                focusTarg = attacker;
            }
        }

        public bool TryGetGuardPlayback(BladeMotionSetExtension weaponSet, out MotionPlayback playback)
        {
            playback = default;
            // The guard is the sword stance's own motion: the same with any weapon.
            BladeMotionSetExtension set = QingheSwordGuard.Config.guardMotion;
            BladeMotionClip guard = set?.guard;
            if (guard?.blade == null)
            {
                return false;
            }
            float now = VisualClock.Ticks;
            BladeMotionClip clip = guard;
            float seconds = Mathf.Min((now - startedTick) / 60f, guard.durationSeconds);
            int id = startedTick;
            BladeMotionClip recoil = set.guardHit;
            if (recoil?.blade != null && lastHitTick >= 0)
            {
                float since = (now - lastHitTick) / 60f;
                if (since >= 0f && since < recoil.durationSeconds)
                {
                    clip = recoil;
                    seconds = since;
                    id = lastHitTick;
                }
            }
            playback = new MotionPlayback
            {
                clip = clip,
                seconds = seconds,
                aim = aim,
                projection = set.Projection,
                scabbard = true,
                blendSeconds = clip == recoil ? 0.03f : set.blendSeconds,
                source = clip,
                sourceId = id,
            };
            return true;
        }
    }

    /// <summary>
    /// The deflect before a counter: the blade knocks the attack aside and winds up (the set's
    /// <see cref="BladeMotionSetExtension.parry"/> clip, stretched over the stance), then the counter
    /// cut starts. Hits meanwhile are parried without a further counter.
    /// </summary>
    public class Stance_QingheParry : Stance_Busy, IGuardMotionStance
    {
        private Vector3 aim = Vector3.right;
        private int total;

        public Stance_QingheParry()
        {
        }

        public Stance_QingheParry(int ticks, Thing attacker, Verb verb, Vector3 aim) : base(ticks, attacker, verb)
        {
            this.aim = aim;
            total = ticks;
            neverAimWeapon = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref aim, "mxParryAim", Vector3.right);
            Scribe_Values.Look(ref total, "mxParryTotal");
        }

        protected override void Expire()
        {
            if (stanceTracker.curStance != this)
            {
                return;
            }
            Pawn attacker = focusTarg.Thing as Pawn;
            if (verb is Verb_QingheSwordSweep sword && sword.StartCounter(attacker))
            {
                return;
            }
            stanceTracker.SetStance(new Stance_Mobile());
        }

        public bool TryGetGuardPlayback(BladeMotionSetExtension set, out MotionPlayback playback)
        {
            playback = default;
            // The deflect leads into the cross slash's counter: the weapon's own clip, drawn in the weapon's projection.
            BladeMotionClip clip = set?.parry;
            if (clip?.blade == null)
            {
                return false;
            }
            float t = Mathf.Clamp01((VisualClock.Ticks - startedTick) / Mathf.Max(1f, total));
            playback = new MotionPlayback
            {
                clip = clip,
                seconds = t * clip.durationSeconds,
                aim = aim,
                projection = set.Projection,
                scabbard = true,
                blendSeconds = 0.04f,
                source = clip,
                sourceId = startedTick,
            };
            return true;
        }
    }

    /// <summary>
    /// Qinghe's sword guard in the shared block framework. Decided before the attacker's hit roll,
    /// so every swing that comes out is met. In the sword stance only:
    /// idle or after the last strike: guard (stop, raise the blade; a parry by chance);
    /// while sheathing and gathering (the warmup): always parry, then cut back at once;
    /// while the strikes play: no guard, a landing hit adds posture to her.
    /// </summary>
    internal sealed class QingheSwordBlocker : CombatBlocker
    {
        public override int Priority => 10;
        public override bool BeforeHit => true;
        public override bool Executes => true;
        // Qinghe may be cutting or countering when the posture breaks: the victim waits for her.
        public override bool WaitForExecutor => true;
        public override int MinimumBreakWindowTicks => QingheSwordGuard.Config.ExecutionTicks + 30;
        public override float UnexecutedPostureKept => QingheSwordGuard.Config.unexecutedPostureKept;

        // Decorative mode seals the whole sword defence: no guard, parry, counter, posture or execution.
        // Her ordinary attacks (the motion strikes) stay.
        public override bool Covers(Pawn pawn) => !QinghePowerBalance.Sealed && MX_QHCharacterUtility.IsQinghe(pawn);

        public override bool CanExecute(Pawn executor, Pawn victim) => QingheSwordGuard.CanExecute(executor, victim);

        public override Job MakeExecutionJob(Pawn executor, Pawn victim) => JobMaker.MakeJob(QingheSwordGuard.Config.executionJob, victim);

        public override void NotifyPostureBroken(Pawn source, Pawn victim)
        {
            QingheSwordGuardDef c = QingheSwordGuard.Config;
            if (c.breakVfx == null || victim?.Map == null)
            {
                return;
            }
            Vector3 toward = source != null && source.Spawned && source.Map == victim.Map ? victim.DrawPos - source.DrawPos : Vector3.forward;
            QingheImpactVfx.Spawn(victim.Map, c.breakVfx, victim.DrawPos, toward, victim.Position);
        }

        public override bool TryBlockMelee(Verb_MeleeAttack verb, Pawn defender)
        {
            Pawn attacker = verb.CasterPawn;
            // A swing aimed at her is an attack whoever makes it: an enemy, or one not hostile yet (a
            // neutral animal answering her blow, a social fight).
            if (!verb.verbProps.IsMeleeAttack || attacker == null || attacker == defender
                || !QingheSwordGuard.CanGuard(defender, out Verb_QingheSwordSweep sword))
            {
                return false;
            }
            QingheSwordGuardDef c = QingheSwordGuard.Config;
            object stance = defender.stances.curStance;
            if (defender.jobs?.curDriver is JobDriver_QingheSwordExecution)
            {
                // Executing: invulnerable (SakuraDanceInvulnerability), the blow simply does nothing.
                return false;
            }
            if (sword != null && stance is Stance_Warmup warmup && warmup.verb == sword)
            {
                // Past the flash the blade is in and the draw is coming: committed, the hit lands.
                if (!sword.InCounterWindow(warmup))
                {
                    return false;
                }
                QingheSwordGuard.Parry(defender, attacker, c.counterAttackerPosture, c.counterStaggerTicks, counter: true);
                if (CombatPosture.Get(attacker)?.Broken == true)
                {
                    // The parry broke its posture: no counter cut, the execution comes at once.
                    // The guard stance stops the windup and leaves her free to execute.
                    QingheSwordGuard.StartGuard(defender, attacker);
                }
                else
                {
                    sword.BeginParry(attacker);
                }
                return true;
            }
            if (stance is Stance_QingheParry)
            {
                QingheSwordGuard.Parry(defender, attacker, c.parryAttackerPosture);
                return true;
            }
            if (stance is Stance_QingheGuard guard)
            {
                guard.Notify_Hit(attacker, c.extendTicks);
                QingheSwordGuard.Parry(defender, attacker, c.parryAttackerPosture);
                return true;
            }
            if (!QingheSwordGuard.Free(defender, sword))
            {
                return false;
            }
            bool parry = Rand.Chance(defender.GetStatValue(MX_QHDefOf.MX_QH_ParryChance));
            QingheSwordGuard.StartGuard(defender, attacker);
            if (parry)
            {
                QingheSwordGuard.Parry(defender, attacker, c.parryAttackerPosture);
            }
            else
            {
                QingheSwordGuard.Block(defender, attacker, QingheSwordGuard.BlockSelfPosture(), c.blockAttackerPosture);
            }
            return true;
        }

        public override bool TryBlockProjectile(Projectile projectile, Pawn defender)
        {
            Thing shooter = projectile.Launcher;
            if (projectile is not { Spawned: true, Destroyed: false } || projectile.Map != defender.Map
                || shooter == null || shooter == defender
                // An enemy's shot, or one aimed at her by anyone not hostile yet.
                || !GenHostility.HostileTo(shooter, defender) && projectile.intendedTarget.Thing != defender
                || !QingheSwordGuard.CanGuard(defender, out Verb_QingheSwordSweep sword))
            {
                return false;
            }
            QingheSwordGuardDef c = QingheSwordGuard.Config;
            object stance = defender.stances.curStance;
            bool parry;
            if (defender.jobs?.curDriver is JobDriver_QingheSwordExecution)
            {
                // Executing: invulnerable, the shot lands and does nothing.
                return false;
            }
            if (sword != null && stance is Stance_Warmup warmup && warmup.verb == sword)
            {
                // Taken on the sheathing blade without breaking the attack.
                parry = true;
            }
            else if (stance is Stance_QingheGuard guard)
            {
                guard.Notify_Hit(shooter, c.extendTicks);
                parry = true;
            }
            else if (stance is Stance_QingheParry)
            {
                parry = true;
            }
            else if (QingheSwordGuard.Free(defender, sword))
            {
                parry = Rand.Chance(defender.GetStatValue(MX_QHDefOf.MX_QH_ParryChance));
                QingheSwordGuard.StartGuard(defender, shooter);
            }
            else
            {
                return false;
            }
            if (!parry && c.projectileBlockSelfPosture > 0f)
            {
                CombatPosture.Ensure(defender)?.AddPressure(shooter as Pawn, c.projectileBlockSelfPosture);
            }
            QingheSwordGuard.Effects(defender, shooter, parry ? c.parryVfx : c.blockVfx);
            projectile.Destroy();
            return true;
        }

        public override void NotifyMeleeHitResult(Verb_MeleeAttack verb, Pawn defender, DamageWorker.DamageResult result)
        {
            // Any melee hit she did not turn aside in the sword stance (committed to a cut, stunned, ...).
            // An attack carrying its own posture applies that itself, with nothing added here.
            if (verb.CasterPawn == null || CombatBlock.AttackPosture > 0f || defender.Dead || !defender.Spawned
                || MX_QH_HediffUtility.GetCombatStance(defender)?.CurrentStance != MX_QHDefOf.MX_QH_Stance_Sword)
            {
                return;
            }
            float amount = QingheSwordGuard.Config.struckSelfPosture;
            if (SweepDamageWorker.Absorbed(result))
            {
                amount *= SweepDamageWorker.AbsorbedPostureShare;
            }
            CombatPosture.Ensure(defender)?.AddPressure(verb.CasterPawn, amount);
        }
    }

    [StaticConstructorOnStartup]
    internal static class QingheSwordGuard
    {
        internal static readonly QingheSwordGuardDef Config = DefDatabase<QingheSwordGuardDef>.GetNamed("MX_QH_SwordGuard");

        static QingheSwordGuard()
        {
            CombatBlockers.Register(new QingheSwordBlocker());
            WeaponQuickSwap.RegisterBlocker(QuickSwapBlocked);
        }

        /// <summary>
        /// No weapon swap while the sword is committed: guarding, recoiling from a parry, or a cut still
        /// playing its strikes (the windup is refused by the swap itself).
        /// </summary>
        private static string QuickSwapBlocked(Pawn pawn)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn))
            {
                return null;
            }
            Stance stance = pawn.stances?.curStance;
            bool busy = stance is Stance_QingheGuard or Stance_QingheParry
                || (stance is Stance_Busy b && b.verb is Verb_QingheSwordSweep sword && sword.InStrikes(b));
            return busy ? "MX_WeaponQuickSwap_SwordBusy".Translate().ToString() : null;
        }

        /// <summary>Qinghe in the sword stance with a melee weapon, able to act, outside her own defensive skills.</summary>
        internal static bool CanGuard(Pawn pawn, out Verb_QingheSwordSweep sword)
        {
            sword = null;
            if (QinghePowerBalance.Sealed
                || pawn is not { Spawned: true, Dead: false, Downed: false } || pawn.InMentalState || !pawn.Awake()
                || pawn.IsBurning() || pawn.stances?.stunner?.Stunned != false
                || pawn.WorkTagIsDisabled(WorkTags.Violent)
                || MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance != MX_QHDefOf.MX_QH_Stance_Sword
                || pawn.jobs?.curDriver is JobDriver_IllusoryReflectionStance
                || pawn.CurJobDef == MX_QHDefOf.MX_QH_SakuraDance
                || CombatPosture.Get(pawn)?.Broken == true)
            {
                return false;
            }
            // Any melee weapon guards (the stance's own motion). Only one with the cross slash also
            // sheathes, and so has the counter (化解) and its chase; sword is null otherwise.
            if (pawn.equipment?.Primary?.def.IsMeleeWeapon != true)
            {
                return false;
            }
            List<Verb> verbs = pawn.equipment.PrimaryEq?.AllVerbs;
            if (verbs != null)
            {
                for (int i = 0; i < verbs.Count; i++)
                {
                    if (verbs[i] is Verb_QingheSwordSweep s && s.MotionActive)
                    {
                        sword = s;
                        break;
                    }
                }
            }
            return true;
        }

        /// <summary>A living, spawned target whose death or wounds are not handled elsewhere (quest lodgers).</summary>
        internal static bool ExecutionTarget(Pawn target) =>
            target is { Spawned: true, Dead: false, Destroyed: false } && target.health != null
            && !target.health.isBeingKilled && !target.IsQuestLodger();

        /// <summary>
        /// An enemy, or a factionless one (a wild animal she broke while it was answering her blow).
        /// Members of a faction that is not hostile are never executed: their break just runs out.
        /// </summary>
        internal static bool Executable(Pawn executor, Pawn victim) =>
            GenHostility.HostileTo(executor, victim) || victim.Faction == null;

        internal static bool ExecutionPair(Pawn executor, Pawn victim) =>
            ExecutionTarget(victim) && executor.Map == victim.Map && Executable(executor, victim)
            && executor.Position.DistanceToSquared(victim.Position) <= Config.executionRange * Config.executionRange
            && GenSight.LineOfSight(executor.Position, victim.Position, executor.Map);

        /// <summary>
        /// Qinghe can start the execution now: able to act in the sword stance, and not committed to a cut
        /// (idle, guarding, after the last strike or still sheathing). Otherwise the victim waits.
        /// </summary>
        internal static bool CanExecute(Pawn executor, Pawn victim)
        {
            if (!CanGuard(executor, out Verb_QingheSwordSweep sword) || !ExecutionPair(executor, victim))
            {
                return false;
            }
            return executor.stances.curStance switch
            {
                Stance_QingheGuard => true,
                // The execution replaces the counter the parry was winding up.
                Stance_QingheParry => true,
                Stance_Warmup w => sword != null && w.verb == sword && sword.InCounterWindow(w),
                _ => Free(executor, sword),
            };
        }

        /// <summary>
        /// Not busy with anything but a melee cooldown: the cross slash's return to idle after the last
        /// strike, or a plain melee attack's cooldown (another weapon's).
        /// </summary>
        internal static bool Free(Pawn pawn, Verb_QingheSwordSweep sword)
        {
            Stance stance = pawn.stances.curStance;
            if (stance is not Stance_Busy busy)
            {
                return true;
            }
            if (busy is not Stance_Cooldown)
            {
                return false;
            }
            if (busy.verb is Verb_QingheSwordSweep cut)
            {
                return cut == sword && !cut.InStrikes(busy);
            }
            return busy.verb?.verbProps?.IsMeleeAttack == true;
        }

        /// <summary>
        /// Posture a plain block costs Qinghe: half the attack's own posture when it carries one,
        /// otherwise <see cref="QingheSwordGuardDef.blockSelfPosture"/>. A parry costs her none.
        /// </summary>
        internal static float BlockSelfPosture()
        {
            float carried = CombatBlock.AttackPosture;
            return carried > 0f ? carried * Config.blockCarriedPostureShare : Config.blockSelfPosture;
        }

        internal static void StartGuard(Pawn pawn, Thing attacker)
        {
            Vector3 aim = attacker.DrawPos - pawn.DrawPos;
            aim.y = 0f;
            aim = aim.sqrMagnitude > 0.001f ? aim.normalized : pawn.Rotation.FacingCell.ToVector3();
            // A plain melee cooldown the guard replaces still runs out under it: guarding never
            // shortens another weapon's attack interval.
            int ticks = Config.guardTicks;
            if (pawn.stances.curStance is Stance_Cooldown cooldown && cooldown.verb is not Verb_QingheSwordSweep)
            {
                ticks = Mathf.Max(ticks, cooldown.ticksLeft);
            }
            // A busy stance holds the pather in place (like a stagger) without dropping the path.
            pawn.stances.SetStance(new Stance_QingheGuard(ticks, attacker, aim));
            pawn.rotationTracker.Face(attacker.DrawPos);
        }

        internal static void Block(Pawn defender, Pawn attacker, float selfPosture, float attackerPosture)
        {
            // Effects first: a block that fills a posture bar then shows the break after it.
            Effects(defender, attacker, Config.blockVfx);
            if (selfPosture > 0f)
            {
                CombatPosture.Ensure(defender)?.AddPressure(attacker, selfPosture);
            }
            if (attackerPosture > 0f)
            {
                CombatPosture.Ensure(attacker)?.ReceiveBlock(defender, attackerPosture, opening: false);
            }
        }

        /// <param name="counter">The parry while sheathing that Qinghe answers with the counter (化解).</param>
        internal static void Parry(Pawn defender, Pawn attacker, float attackerPosture, int staggerTicks = -1, bool counter = false)
        {
            Effects(defender, attacker, counter ? Config.counterVfx : Config.parryVfx);
            if (attackerPosture > 0f)
            {
                CombatPosture.Ensure(attacker)?.ReceiveBlock(defender, attackerPosture, opening: false);
            }
            // The deflected attacker recoils: its next swing (its cooldown pauses while stunned) comes
            // after Qinghe's answer, as in Sekiro where a deflect hands the next move to the defender.
            // A posture break above has already put it on hold; the stun does not disturb that.
            int stagger = staggerTicks >= 0 ? staggerTicks : Config.parryStaggerTicks;
            if (stagger > 0 && attacker.Spawned && !attacker.Dead)
            {
                attacker.stances?.stunner?.StunFor(stagger, defender, addBattleLog: false, showMote: false);
            }
        }

        /// <summary>The tier's effect and sound on Qinghe's blade, sparks flying towards the attacker.</summary>
        internal static void Effects(Pawn defender, Thing attacker, QingheImpactVfxProps vfx)
        {
            if (vfx == null || defender?.Map == null)
            {
                return;
            }
            try
            {
                Vector3 at = QingheImpactVfx.BladeContact(defender, attacker, Config.contactShare);
                Vector3 toward = attacker != null && attacker.Spawned && attacker.Map == defender.Map
                    ? attacker.DrawPos - defender.DrawPos
                    : defender.Rotation.FacingCell.ToVector3();
                QingheImpactVfx.Spawn(defender.Map, vfx, at, toward, defender.Position);
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce("[MiliraXian] Qinghe guard effects failed: " + ex, 0x51c7a3);
            }
        }
    }
}
