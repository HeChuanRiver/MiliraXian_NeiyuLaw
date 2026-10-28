using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MiliraXian.Characters.QingHe.Verbs;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Neiyu;

// All balance values live in MX_NeiyuSwordCombat, rather than in character
// patches or a second settings store. Visual preferences use the existing mod.
public sealed class NeiyuSwordCombatDef : Def
{
    public ThingDef sword;
    public HediffDef posture;
    public JobDef executionJob, brokenJob;
    public float successChance = .6f, perfectChance = .15f;
    public float normalPosture = 25f, perfectPosture = 45f;
    public float basePosture = 100f, minimumPostureFraction = .45f;
    public int recoveryDelayTicks = 120;
    public float recoveryPerSecond = 4f, minimumRecoveryFraction = .1f;
    public int perfectStunTicks = 45, vulnerabilityTicks = 360;
    public float vulnerabilityMultiplier = 2f;
    public int breakWindowTicks = 180, barFadeTicks = 90;
    public int executionTicks = 84;
    public float dashFraction = .18f, strikeFraction = .40f, recoverFraction = .61f;
    public float executionRange = 2f, visualIntensity = 1f;
    public int maxVisuals = 96, sparksPerDeflection = 12;
    public SoundDef normalSound, perfectSound, breakSound, prepareSound, strikeSound, endSound;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors()) yield return error;
        if (sword == null || posture == null || executionJob == null || brokenJob == null)
            yield return "剑、架势 Hediff 与处决 Job 引用必须完整。";
        if (!(perfectChance >= 0f && perfectChance <= successChance && successChance <= 1f))
            yield return "弹刀概率必须满足 0 ≤ 完美概率 ≤ 总概率 ≤ 1。";
        if (!(basePosture >= 1f && minimumPostureFraction > 0f && minimumPostureFraction <= 1f
            && recoveryPerSecond >= 0f && minimumRecoveryFraction >= 0f && minimumRecoveryFraction <= 1f
            && normalPosture > 0f && perfectPosture > 0f && vulnerabilityMultiplier >= 1f))
            yield return "架势、恢复和易伤参数超出有效范围。";
        if (!(executionTicks >= 12 && dashFraction > 0f && dashFraction < strikeFraction
            && strikeFraction < recoverFraction && recoverFraction < 1f
            && breakWindowTicks > executionTicks && executionRange >= 1.42f
            && recoveryDelayTicks >= 0 && perfectStunTicks >= 0 && vulnerabilityTicks > 0
            && barFadeTicks > 0 && maxVisuals > 0 && sparksPerDeflection >= 0 && visualIntensity >= 0f))
            yield return "处决时间、阶段、距离或特效参数无效。";
    }
}

[StaticConstructorOnStartup]
internal static class NeiyuSwordCombat
{
    internal static readonly NeiyuSwordCombatDef Config = DefDatabase<NeiyuSwordCombatDef>.GetNamed("MX_NeiyuSwordCombat");
    internal static int Now => Find.TickManager?.TicksGame ?? 0;
    internal static bool SwordEquipped(Pawn pawn) => NeiyuEquipmentUtility.IsNeiyu(pawn)
        && NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(pawn, Config.sword);

    internal static bool CanAct(Pawn pawn) => pawn is { Spawned: true, Dead: false, Destroyed: false, Downed: false }
        && !NeiyuPowerBalance.PassivesDisabled && !pawn.InMentalState && !pawn.Deathresting
        && !pawn.InContainerEnclosed && pawn.Awake() && pawn.health?.capacities.CanBeAwake == true
        && pawn.GetPosture() == PawnPosture.Standing && !pawn.IsBurning()
        && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
        && !pawn.WorkTagIsDisabled(WorkTags.Violent) && pawn.stances?.stunner?.Stunned != true
        && SwordEquipped(pawn);

    internal static HediffComp_NeiyuPosture Posture(Pawn pawn) =>
        pawn?.health?.hediffSet.GetFirstHediffOfDef(Config.posture)?.TryGetComp<HediffComp_NeiyuPosture>();

    internal static bool HostilePair(Pawn defender, Pawn attacker) => attacker is { Spawned: true, Dead: false, Destroyed: false }
        && defender != attacker && defender.Map == attacker.Map && GenHostility.HostileTo(attacker, defender);

    internal static bool TryDeflect(Verb_MeleeAttackDamage verb, Pawn defender)
    {
        Pawn attacker = verb.CasterPawn;
        // EquipmentSource excludes fists, bites and implanted attacks. A rifle used
        // as a club is still a melee weapon attack; IsMeleeAttack identifies the verb.
        if (!CanAct(defender) || !HostilePair(defender, attacker)
            || verb.EquipmentSource?.def.IsWeapon != true || !verb.verbProps.IsMeleeAttack
            || verb.verbProps.meleeDamageDef?.harmsHealth != true
            || !DebugSettings.enableDamage) return false;
        int outcome = NeiyuSwordCombatRules.Deflection(Rand.Value, Config.successChance, Config.perfectChance);
        if (outcome == 0) return false;
        HediffComp_NeiyuPosture posture = Posture(attacker);
        if (posture == null)
        {
            posture = Common.PawnSpecialResourceUtility.EnsureSpecialResourceComp(attacker, Config.posture)
                as HediffComp_NeiyuPosture;
        }
        if (posture == null) return false;
        bool perfect = outcome == 2;
        posture.ReceiveDeflection(defender, perfect);
        if (perfect && Config.perfectStunTicks > 0)
            attacker.stances?.stunner?.StunFor(Config.perfectStunTicks, defender);
        NeiyuSwordCombatVfx.Deflect(defender, attacker, perfect);
        return true;
    }

    internal static bool ExecutionTarget(Pawn target)
    {
        if (target is not { Spawned: true, Dead: false, Destroyed: false } || target.health == null
            || target.health.isBeingKilled || target.IsQuestLodger()) return false;
        // Native deathless and hediff protection must not be bypassed by direct Kill.
        if (ModsConfig.BiotechActive && target.genes?.HasActiveGene(GeneDefOf.Deathless) == true) return false;
        foreach (Hediff hediff in target.health.hediffSet.hediffs)
            if (hediff.def.preventsDeath) return false;
        return true;
    }

    internal static bool ExecutionPair(Pawn executor, Pawn target) => CanAct(executor) && ExecutionTarget(target)
        && HostilePair(executor, target)
        && executor.Position.DistanceToSquared(target.Position) <= Config.executionRange * Config.executionRange
        && GenSight.LineOfSight(executor.Position, target.Position, executor.Map);
}

// The native loop is after hit/dodge and before every main/extra/surprise packet.
// The repository's sweep overrides that loop, so it shares this same patch. No
// fallback based on attacker+tick: distinct attacks in one tick remain distinct.
[HarmonyPatch]
internal static class Patch_NeiyuSwordDeflection
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget");
        yield return AccessTools.DeclaredMethod(typeof(Verb_QingheSwordSweep), "ApplyMeleeDamageToTarget");
    }

    [HarmonyPriority(Priority.First)]
    internal readonly record struct AttackFrame(Verb_MeleeAttackDamage Verb, Thing Target, bool Owns);

    private static bool Prefix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target,
        ref DamageWorker.DamageResult __result, out AttackFrame __state)
    {
        __state = default;
        // An overriding loop may delegate to base. The outer invocation owns the roll.
        if (activeVerb == __instance && activeTarget == target.Thing) return true;
        if (target.Pawn == null || !NeiyuSwordCombat.SwordEquipped(target.Pawn)) return true;
        __state = new AttackFrame(activeVerb, activeTarget, true);
        activeVerb = __instance;
        activeTarget = target.Thing;
        if (!NeiyuSwordCombat.TryDeflect(__instance, target.Pawn)) return true;
        __result = new DamageWorker.DamageResult { deflected = true };
        return false;
    }

    // Game combat runs on the main thread; the finalizer also clears on exceptions.
    [System.ThreadStatic] private static Verb_MeleeAttackDamage activeVerb;
    [System.ThreadStatic] private static Thing activeTarget;
    private static void Finalizer(AttackFrame __state)
    {
        if (__state.Owns) { activeVerb = __state.Verb; activeTarget = __state.Target; }
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
internal static class Patch_NeiyuSwordOpening
{
    private static void Prefix(Thing __instance, ref DamageInfo dinfo, out HediffComp_NeiyuPosture __state)
    {
        __state = null;
        if (__instance is not Pawn pawn || !float.IsFinite(dinfo.Amount) || dinfo.Amount <= 0f
            || dinfo.Instigator is not Pawn || dinfo.Def?.harmsHealth != true
            || dinfo.Def.Worker is not DamageWorker_AddInjury || !dinfo.Def.ExternalViolenceFor(pawn)
            || dinfo.Def.isExplosive || (dinfo.Tool == null && dinfo.Weapon?.IsWeapon != true
                && dinfo.Weapon?.projectile == null)) return;
        HediffComp_NeiyuPosture posture = NeiyuSwordCombat.Posture(pawn);
        if (posture?.TryReserveOpening() != true) return;
        __state = posture;
        dinfo.SetAmount(dinfo.Amount * NeiyuSwordCombat.Config.vulnerabilityMultiplier);
    }

    private static void Postfix(HediffComp_NeiyuPosture __state, DamageWorker.DamageResult __result) =>
        __state?.CompleteOpening(__result?.totalDamageDealt > 0f);

    private static void Finalizer(HediffComp_NeiyuPosture __state) => __state?.ReleaseOpening();
}
