using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu;

// One decision belongs to one invocation/target, not to an attacker or a tick.
// The native cast and the sweep's secondary targets enter the same scope; their
// damage loops only consume its result. Nested virtual/base calls never reroll.
internal static class NeiyuMeleeDefense
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
    [ThreadStatic] private static byte decision; // 0 undecided, 1 failed/ineligible, 2 deflected

    internal readonly struct Scope(Verb_MeleeAttack previousVerb, Thing previousTarget, byte previousDecision, bool owns) : IDisposable
    {
        public void Dispose()
        {
            if (!owns) return;
            activeVerb = previousVerb; activeTarget = previousTarget; decision = previousDecision;
        }
    }

    internal static Scope Begin(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        if (target.Pawn == null || !NeiyuEquipmentUtility.IsNeiyu(target.Pawn)
            || activeVerb == verb && activeTarget == target.Thing) return default;
        Scope scope = new(activeVerb, activeTarget, decision, true);
        activeVerb = verb; activeTarget = target.Thing; decision = 0;
        return scope;
    }

    internal static bool Deflected(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        if (target.Pawn == null || !NeiyuEquipmentUtility.IsNeiyu(target.Pawn)) return false;
        if (activeVerb != verb || activeTarget != target.Thing)
            return NeiyuSwordCombat.TryDeflect(verb, target.Pawn);
        if (decision == 0) decision = NeiyuSwordCombat.TryDeflect(verb, target.Pawn) ? (byte)2 : (byte)1;
        return decision == 2;
    }

    internal static bool RollDodge(Verb_MeleeAttack verb, LocalTargetInfo target)
    {
        // A successful parry never rolls dodge. A failed parry reaches the exact
        // native chance, including other mods' patches on GetDodgeChance.
        if (Deflected(verb, target)) return false;
        return Rand.Chance(NativeDodgeCall.Chance(verb, target));
    }
}

[HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
internal static class Patch_NeiyuMeleeDefenseOrder
{
    private static void Prefix(Verb_MeleeAttack __instance, out NeiyuMeleeDefense.Scope __state) =>
        __state = NeiyuMeleeDefense.Begin(__instance, __instance.CurrentTarget);

    private static void Finalizer(NeiyuMeleeDefense.Scope __state) => __state.Dispose();

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = new(instructions);
        MethodInfo chance = AccessTools.Method(typeof(Rand), nameof(Rand.Chance), [typeof(float)]);
        MethodInfo resolve = AccessTools.Method(typeof(NeiyuMeleeDefense), nameof(NeiyuMeleeDefense.RollDodge));
        int replacements = 0;
        for (int i = 0; i < code.Count - 1; i++)
        {
            if (!code[i].Calls(NeiyuMeleeDefense.NativeDodge) || !code[i + 1].Calls(chance)) continue;
            // The stack already contains (verb, LocalTargetInfo). Keep labels and
            // exception blocks in place; only replace the two calls, not the shot.
            code[i].opcode = OpCodes.Call; code[i].operand = resolve;
            code[i + 1].opcode = OpCodes.Nop; code[i + 1].operand = null;
            replacements++;
        }
        if (replacements != 1)
            throw new InvalidOperationException($"霓羽近战防御：预期一个闪避入口，实际匹配 {replacements} 个。请核对当前游戏与其他转译补丁。");
        return code;
    }
}

// This second boundary prevents the entire main/extra/surprise damage group.
// Declared overrides are discovered once because some melee verbs replace the
// loop entirely (including the local sweep and innate mechanoid attacks).
[HarmonyPatch]
internal static class Patch_NeiyuSwordDeflection
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
        ref DamageWorker.DamageResult __result, out NeiyuMeleeDefense.Scope __state)
    {
        __state = NeiyuMeleeDefense.Begin(__instance, __0);
        if (!NeiyuMeleeDefense.Deflected(__instance, __0)) return true;
        __result = new DamageWorker.DamageResult { deflected = true };
        return false;
    }

    private static void Finalizer(NeiyuMeleeDefense.Scope __state) => __state.Dispose();
}
