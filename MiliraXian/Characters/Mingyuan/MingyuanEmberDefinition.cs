using System;
using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.Common.PerkSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

public enum EmberEffect
{
    Origin, CombatMove, MeleeDamage, CastGuard, ShieldRecovery, MeleeSpeed, CastMove,
    BowAccuracy, LowBurnDefense, ShieldCapacity, BurnInsight, DecayDelay, SpreadRadius,
    KillRecovery, EmberGround, ShieldFeedback, SafeOverburn, ExtraStack, OverburnDamage,
    CastKindling, DashGuard, CombustionKindling, ScorchDuration, CombustionFocus, AshesRadius,
    PillarCapture, TimeEmberHold, PillarCore, ComboShield
}

public enum EmberTrial { None, Cast, Burn, Shield, Spread, Overburn, Combo }
public enum EmberNodeShape { Origin, Minor, Notable, Keystone, Capstone }

public sealed class MingyuanEmberNodeDef : CharacterPerkNodeDef
{
    public string nodeId;
    public Vector2 position;
    public EmberEffect effect;
    public EmberNodeShape shape;
    public EmberTrial trial;
    public int insightCost;
    public int ashCost;
    public int branchInvestment;
    public float value;
    public float penalty;
    public float threshold;
    public float radius;
    public int durationTicks;
    public int cooldownTicks;
    [MustTranslate] public string tradeoff;
    [MustTranslate] public string role;
    [MustTranslate] public string flavorText;

    public int InsightCost(int level) => insightCost + (maxLevel > 1 ? level / 2 : 0);
    public int AshCost(int level) => ashCost + (maxLevel > 1 ? level : 0);
    public override int EffectiveLevel(Pawn pawn, int storedLevel) => MingyuanPowerBalance.Sealed ? 0 : storedLevel;
    public override bool AllowsReset => effect != EmberEffect.Origin;

    public override string PurchaseBlockedReason(Pawn pawn)
    {
        if (!MingyuanUtility.IsMingyuan(pawn) || pawn.Dead || !pawn.IsColonistPlayerControlled)
            return EmberText.Get("Unavailable", "当前无法学习。");
        if (MingyuanPowerBalance.Sealed) return EmberText.Get("Sealed", "力量已封印，已学进度仍会保留。");
        var book = MingyuanEmberBook.For(pawn);
        if (book == null || effect == EmberEffect.Origin) return EmberText.Get("Origin", "起点已学会。");
        if (book.Spent(category) < branchInvestment)
            return EmberText.Get("Investment", "本路线需累计投入 {0} 焰识。").Formatted(branchInvestment);
        if (!book.TrialComplete(trial)) return book.TrialDescription(trial);
        return null;
    }

    public override bool HasCosts(Pawn pawn, int level) => MingyuanEmberBook.For(pawn) is { } book
        && book.Insight >= InsightCost(level) && book.Ash >= AshCost(level);

    public override bool TryStageCosts(Pawn pawn, int level, out IPerkCostTransaction payment)
    {
        payment = null;
        if (!HasCosts(pawn, level)) return false;
        payment = MingyuanEmberBook.For(pawn).StagePayment(this, level);
        return true;
    }

    public override bool CanRetain(Pawn pawn, Func<CharacterPerkNodeDef, int> levels) =>
        MingyuanEmberBook.For(pawn)?.Spent(category, levels) >= branchInvestment;

    public override void Refund(Pawn pawn) => MingyuanEmberBook.For(pawn)?.Refund(this);

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors()) yield return error;
        if (category == null || nodeId.NullOrEmpty() || maxLevel < 1 || insightCost < 0 || ashCost < 0)
            yield return defName + ": invalid ember node identity, category, level or cost.";
        if (grantedHediffs?.Count > 0)
            yield return defName + ": ember nodes do not remove or recreate state containers.";
        if (grantedAbilities?.Count > 0)
            yield return defName + ": ember nodes modify existing skills; they must not grant additional active abilities.";
        if (effect != EmberEffect.Origin && prerequisites.NullOrEmpty()) yield return defName + ": missing prerequisite.";
        HashSet<CharacterPerkNodeDef> visiting = new();
        bool Cycle(CharacterPerkNodeDef node)
        {
            if (!visiting.Add(node)) return true;
            if (node.prerequisites != null)
                foreach (var parent in node.prerequisites)
                    if (parent?.node != null && Cycle(parent.node)) return true;
            visiting.Remove(node);
            return false;
        }
        if (Cycle(this)) yield return defName + ": cyclic prerequisite graph.";
    }
}

public sealed class MingyuanEmberBookDef : Def
{
    public int startingInsight = 3;
    public int startingAsh = 6;
    public int experiencePerInsight = 20;
    public int trialInsightReward = 2;
    public int trialAshReward = 5;
    public float burnDamagePerAsh = 100f;
    public float shieldRepairPerAsh = 30f;
    public int castsPerAsh = 3;
    public int castTrialCount = 100;
    public int killTrialCount = 30;
    public float shieldTrialAmount = 3000f;
    public int spreadTrialCount = 50;
    public int overburnTrialTicks = 600;
    public int comboTrialCount = 10;
    public int combatMemoryTicks = 600;
    public int resourceEventCooldownTicks = 120;
    public string iconPath;
    public List<AbilityDef> retiredAbilities = new();
    public List<AbilityDef> mainSkills = new();
    private List<MingyuanEmberNodeDef> nodes;
    private Dictionary<EmberEffect, MingyuanEmberNodeDef> byEffect;
    public List<MingyuanEmberNodeDef> Nodes => nodes ??= DefDatabase<CharacterPerkNodeDef>.AllDefs
        .OfType<MingyuanEmberNodeDef>().OrderBy(n => n.displayOrder).ToList();
    public MingyuanEmberNodeDef Node(EmberEffect effect) =>
        (byEffect ??= Nodes.ToDictionary(n => n.effect)).GetValueOrDefault(effect);
}

[DefOf]
public static class MingyuanEmberDefOf
{
    public static MingyuanEmberBookDef MX_Mingyuan_EmberBook;
    public static HediffDef MX_Mingyuan_EmberBoons;
    public static ThingDef MX_Mingyuan_EmberGround;
    static MingyuanEmberDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(MingyuanEmberDefOf));
}

internal static class EmberText
{
    internal static string Get(string key, string source)
    {
        string id = "MX_Ember_" + key;
        return id.CanTranslate() ? id.Translate().ToString() : source;
    }
}
