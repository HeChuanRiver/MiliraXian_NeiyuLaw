using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common.Combat;

// One decision belongs to one invocation/target, not to an attacker or a tick.
// The native cast and the sweep's secondary targets enter the same scope; their
// damage loops only consume its result. Nested virtual/base calls never reroll.
public static class CombatBlock
{
    internal static readonly MethodInfo NativeDodge = AccessTools.DeclaredMethod(typeof(Verb_MeleeAttack), "GetDodgeChance");
    internal static readonly MethodInfo NativeHit = AccessTools.DeclaredMethod(typeof(Verb_MeleeAttack), "GetNonMissChance");
    // Bind at first combat use, after startup patch registration has finished.
    private static class NativeCall
    {
        internal static readonly Func<Verb_MeleeAttack, LocalTargetInfo, float> Dodge =
            NativeDodge.CreateDelegate<Func<Verb_MeleeAttack, LocalTargetInfo, float>>();
        internal static readonly Func<Verb_MeleeAttack, LocalTargetInfo, float> Hit =
            NativeHit.CreateDelegate<Func<Verb_MeleeAttack, LocalTargetInfo, float>>();
    }
    [ThreadStatic] private static Verb_MeleeAttack activeVerb;
    [ThreadStatic] private static Thing activeTarget;
    // 0 undecided, 1 before-hit blockers failed, 2 blocked, 3 every blocker failed
    [ThreadStatic] private static byte decision;
    [ThreadStatic] private static bool hitNotified;
    // Posture the attack now resolving carries (a technique strike); 0 = none, the defender's defaults apply.
    [ThreadStatic] private static float attackPosture;

    /// <summary>
    /// Posture of the melee attack now resolving; 0 when it carries none. A blocker reads it to
    /// scale what a block costs its defender; an unblocked hit's posture is applied by the attacker.
    /// </summary>
    public static float AttackPosture => attackPosture;

    public readonly struct PostureScope(float previous) : IDisposable
    {
        public void Dispose() => attackPosture = previous;
    }

    /// <summary>Marks the attack resolved inside the scope as carrying <paramref name="posture"/>.</summary>
    public static PostureScope WithAttackPosture(float posture)
    {
        PostureScope scope = new(attackPosture);
        attackPosture = Math.Max(0f, posture);
        return scope;
    }

    public readonly struct Scope(Verb_MeleeAttack previousVerb, Thing previousTarget, byte previousDecision, bool previousNotified, bool owns) : IDisposable
    {
        /// <summary>This call opened the decision scope (the outermost one for its verb and target).</summary>
        public bool Owns => owns;

        public void Dispose()
        {
            if (!owns) return;
            activeVerb = previousVerb; activeTarget = previousTarget; decision = previousDecision; hitNotified = previousNotified;
        }
    }

    public static Scope Begin(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        if (!CombatBlockers.Covers(target.Pawn)
            || activeVerb == verb && activeTarget == target.Thing) return default;
        Scope scope = new(activeVerb, activeTarget, decision, hitNotified, true);
        activeVerb = verb; activeTarget = target.Thing; decision = 0; hitNotified = false;
        return scope;
    }

    private static bool InScope(Verb_MeleeAttack verb, LocalTargetInfo target) =>
        activeVerb == verb && activeTarget == target.Thing;

    public static bool Blocked(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        if (!CombatBlockers.Covers(target.Pawn)) return false;
        if (!InScope(verb, target))
            return CombatBlockers.TryBlockMelee(verb, target.Pawn);
        if (decision == 0) decision = CombatBlockers.TryBlockMelee(verb, target.Pawn, MeleeBlockStage.BeforeHit) ? (byte)2 : (byte)1;
        if (decision == 1) decision = CombatBlockers.TryBlockMelee(verb, target.Pawn, MeleeBlockStage.AfterHit) ? (byte)2 : (byte)3;
        return decision == 2;
    }

    // Replaces the native hit roll. Blockers that decide before the hit see
    // every swing; a block counts as a hit so the swing reaches the block path
    // (no dodge, deflected damage result) instead of being logged as a miss.
    public static bool RollHit(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        if (CombatBlockers.Covers(target.Pawn) && InScope(verb, target))
        {
            if (decision == 0) decision = CombatBlockers.TryBlockMelee(verb, target.Pawn, MeleeBlockStage.BeforeHit) ? (byte)2 : (byte)1;
            if (decision == 2) return true;
        }
        return Rand.Chance(NativeCall.Hit(verb, target));
    }

    public static bool RollDodge(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        // A successful block never rolls dodge. A failed block reaches the exact
        // native chance, including other mods' patches on GetDodgeChance.
        if (Blocked(verb, target)) return false;
        return Rand.Chance(NativeCall.Dodge(verb, target));
    }

    // Damage is about to be applied and no blocker stopped it.
    internal static void NotifyUnblockedHit(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        if (target.Pawn == null || !InScope(verb, target) || hitNotified) return;
        hitNotified = true;
        resultNotified = false;
        CombatBlockers.NotifyMeleeHit(verb, target.Pawn);
    }

    [ThreadStatic] private static bool resultNotified;

    // The unblocked hit's damage has been applied (once per invocation and target, outermost call).
    internal static void NotifyUnblockedResult(Verb_MeleeAttack verb, LocalTargetInfo target, DamageWorker.DamageResult result)
    {
        if (target.Pawn == null || !InScope(verb, target) || !hitNotified || resultNotified) return;
        resultNotified = true;
        CombatBlockers.NotifyMeleeHitResult(verb, target.Pawn, result);
    }
}

[HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
internal static class Patch_CombatBlockOrder
{
    private static void Prefix(Verb_MeleeAttack __instance, out CombatBlock.Scope __state) =>
        __state = CombatBlock.Begin(__instance, __instance.CurrentTarget);

    private static void Finalizer(CombatBlock.Scope __state) => __state.Dispose();

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = new(instructions);
        MethodInfo chance = AccessTools.Method(typeof(Rand), nameof(Rand.Chance), [typeof(float)]);
        MethodInfo resolveDodge = AccessTools.Method(typeof(CombatBlock), nameof(CombatBlock.RollDodge));
        MethodInfo resolveHit = AccessTools.Method(typeof(CombatBlock), nameof(CombatBlock.RollHit));
        int dodges = 0, hits = 0;
        for (int i = 0; i < code.Count - 1; i++)
        {
            MethodInfo resolve;
            if (code[i].Calls(CombatBlock.NativeDodge) && code[i + 1].Calls(chance)) { resolve = resolveDodge; dodges++; }
            else if (code[i].Calls(CombatBlock.NativeHit) && code[i + 1].Calls(chance)) { resolve = resolveHit; hits++; }
            else continue;
            // The stack already contains (verb, LocalTargetInfo). Keep labels and
            // exception blocks in place; only replace the two calls, not the shot.
            code[i].opcode = OpCodes.Call; code[i].operand = resolve;
            code[i + 1].opcode = OpCodes.Nop; code[i + 1].operand = null;
        }
        if (dodges != 1 || hits != 1)
            throw new InvalidOperationException($"通用格挡：预期命中和闪避入口各一个，实际匹配命中 {hits} 个、闪避 {dodges} 个。请核对当前游戏与其他转译补丁。");
        return code;
    }
}

// This second boundary prevents the entire main/extra/surprise damage group.
// Declared overrides are discovered once because some melee verbs replace the
// loop entirely (including the local sweep and innate mechanoid attacks).
[HarmonyPatch]
internal static class Patch_CombatBlockMeleeDamage
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type type in GenTypes.AllSubclasses(typeof(Verb_MeleeAttack)))
        {
            if (type.ContainsGenericParameters) continue;
            MethodInfo method = AccessTools.DeclaredMethod(type, "ApplyMeleeDamageToTarget", [typeof(LocalTargetInfo)]);
            if (method is { IsAbstract: false } && method.GetBaseDefinition().DeclaringType == typeof(Verb_MeleeAttack))
                yield return method;
        }
    }

    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Verb_MeleeAttack __instance, LocalTargetInfo __0,
        ref DamageWorker.DamageResult __result, out CombatBlock.Scope __state)
    {
        __state = CombatBlock.Begin(__instance, __0);
        if (!CombatBlock.Blocked(__instance, __0))
        {
            CombatBlock.NotifyUnblockedHit(__instance, __0);
            return true;
        }
        __result = new DamageWorker.DamageResult { deflected = true };
        return false;
    }

    // After the damage of an unblocked hit (the outermost scope owner reports it once).
    private static void Postfix(Verb_MeleeAttack __instance, LocalTargetInfo __0, DamageWorker.DamageResult __result,
        CombatBlock.Scope __state)
    {
        if (__state.Owns) CombatBlock.NotifyUnblockedResult(__instance, __0, __result);
    }

    private static void Finalizer(CombatBlock.Scope __state) => __state.Dispose();
}

// Bullet destroys itself in base.Impact BEFORE applying damage. Each declared
// override must therefore be intercepted before its own body, including special
// arrows/explosives. The invocation scope avoids rerolling through base calls.
[HarmonyPatch]
internal static class Patch_CombatBlockProjectile
{
    [ThreadStatic] private static Projectile activeProjectile;
    internal readonly struct Scope(Projectile previous, bool owns)
    {
        internal void Restore() { if (owns) activeProjectile = previous; }
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type[] parameters = [typeof(Thing), typeof(bool)];
        yield return AccessTools.DeclaredMethod(typeof(Projectile), "Impact", parameters);
        foreach (Type type in GenTypes.AllSubclasses(typeof(Projectile)))
        {
            if (type.ContainsGenericParameters) continue;
            MethodInfo method = AccessTools.DeclaredMethod(type, "Impact", parameters);
            if (method is { IsAbstract: false } && method.GetBaseDefinition().DeclaringType == typeof(Projectile))
                yield return method;
        }
    }

    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Projectile __instance, Thing __0, bool __1, out Scope __state)
    {
        __state = default;
        if (activeProjectile == __instance || __1 || __0 is not Pawn defender
            || !CombatBlockers.Covers(defender)) return true;
        __state = new Scope(activeProjectile, true);
        activeProjectile = __instance;
        return !CombatBlockers.TryBlockProjectile(__instance, defender);
    }

    private static void Finalizer(Scope __state) => __state.Restore();
}
