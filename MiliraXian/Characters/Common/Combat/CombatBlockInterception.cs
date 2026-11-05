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
    // Bind at first combat use, after startup patch registration has finished.
    private static class NativeDodgeCall
    {
        internal static readonly Func<Verb_MeleeAttack, LocalTargetInfo, float> Chance =
            NativeDodge.CreateDelegate<Func<Verb_MeleeAttack, LocalTargetInfo, float>>();
    }
    [ThreadStatic] private static Verb_MeleeAttack activeVerb;
    [ThreadStatic] private static Thing activeTarget;
    [ThreadStatic] private static byte decision; // 0 undecided, 1 failed/ineligible, 2 blocked

    public readonly struct Scope(Verb_MeleeAttack previousVerb, Thing previousTarget, byte previousDecision, bool owns) : IDisposable
    {
        public void Dispose()
        {
            if (!owns) return;
            activeVerb = previousVerb; activeTarget = previousTarget; decision = previousDecision;
        }
    }

    public static Scope Begin(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        if (!CombatBlockers.Covers(target.Pawn)
            || activeVerb == verb && activeTarget == target.Thing) return default;
        Scope scope = new(activeVerb, activeTarget, decision, true);
        activeVerb = verb; activeTarget = target.Thing; decision = 0;
        return scope;
    }

    public static bool Blocked(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        if (!CombatBlockers.Covers(target.Pawn)) return false;
        if (activeVerb != verb || activeTarget != target.Thing)
            return CombatBlockers.TryBlockMelee(verb, target.Pawn);
        if (decision == 0) decision = CombatBlockers.TryBlockMelee(verb, target.Pawn) ? (byte)2 : (byte)1;
        return decision == 2;
    }

    public static bool RollDodge(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        // A successful block never rolls dodge. A failed block reaches the exact
        // native chance, including other mods' patches on GetDodgeChance.
        if (Blocked(verb, target)) return false;
        return Rand.Chance(NativeDodgeCall.Chance(verb, target));
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
        MethodInfo resolve = AccessTools.Method(typeof(CombatBlock), nameof(CombatBlock.RollDodge));
        int replacements = 0;
        for (int i = 0; i < code.Count - 1; i++)
        {
            if (!code[i].Calls(CombatBlock.NativeDodge) || !code[i + 1].Calls(chance)) continue;
            // The stack already contains (verb, LocalTargetInfo). Keep labels and
            // exception blocks in place; only replace the two calls, not the shot.
            code[i].opcode = OpCodes.Call; code[i].operand = resolve;
            code[i + 1].opcode = OpCodes.Nop; code[i + 1].operand = null;
            replacements++;
        }
        if (replacements != 1)
            throw new InvalidOperationException($"通用格挡：预期一个闪避入口，实际匹配 {replacements} 个。请核对当前游戏与其他转译补丁。");
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
        if (!CombatBlock.Blocked(__instance, __0)) return true;
        __result = new DamageWorker.DamageResult { deflected = true };
        return false;
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
