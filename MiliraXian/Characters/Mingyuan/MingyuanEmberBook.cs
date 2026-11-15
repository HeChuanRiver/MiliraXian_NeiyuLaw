using System;
using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.Common.PerkSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

public sealed class HediffCompProperties_MingyuanEmberBook : HediffCompProperties
{
    public HediffCompProperties_MingyuanEmberBook() => compClass = typeof(MingyuanEmberBook);
}

/// <summary>Personal wallet, paid-cost receipts and combat history. Node levels belong only to the shared tree.</summary>
public sealed class MingyuanEmberBook : HediffComp, IPerkEventListener
{
    private bool initialized;
    private int insight, ash, experience, casts, rewardedCasts, kills, spreads, combos, overburnTicks;
    private float burnDamage, repairedDamage;
    private int claimedTrials;
    private Dictionary<string, int> paidInsight = new(), paidAsh = new();
    private Dictionary<AbilityDef, int> comboCasts = new();
    private Dictionary<Pawn, int> stackTargets = new(), groundTargets = new();
    private List<Pawn> stackTargetKeys, groundTargetKeys;
    private List<int> stackTargetValues, groundTargetValues;
    private int lastCombatTick = -99999, nextCastRewardTick, nextKillRecoveryTick, nextComboResetTick;
    private int castMoveUntil, comboHintUntil, overburnStarted = -1;
    private int emberWindUntil;
    private int feedbackWindowTick, feedbackLayers;
    private bool echoStepReady;
    private IntVec3 returnOrigin;
    private Map returnMap;
    private int returnUntil;
    private readonly Dictionary<EmberEffect, int> levels = new();
    private int cachedRevision = -1, cachedSealRevision = -1;
    public static MingyuanEmberBookDef Config => MingyuanEmberDefOf.MX_Mingyuan_EmberBook;
    public HediffComp_CharacterPerkTree Tree => parent.TryGetComp<HediffComp_CharacterPerkTree>();
    public HediffComp_PerkEventHandler Handler => parent.TryGetComp<HediffComp_PerkEventHandler>();
    public int Insight => insight;
    public int Ash => ash;
    public int Experience => experience;
    private int Now => Find.TickManager?.TicksGame ?? 0;
    internal bool InCombat => Now - lastCombatTick <= Config.combatMemoryTicks;
    internal bool CastMoveActive => Now < castMoveUntil;
    internal bool ComboHintActive => Now < comboHintUntil && Level(EmberEffect.ComboInsight) > 0;
    public static MingyuanEmberBook For(Pawn pawn) =>
        pawn?.health?.hediffSet?.GetFirstHediffOfDef(MingyuanUtility.BurningBodyDef)?.TryGetComp<MingyuanEmberBook>();

    private void Initialize()
    {
        if (Tree == null || Handler == null || !MingyuanUtility.IsMingyuan(Pawn)) return;
        if (!initialized)
        {
            insight = Config.startingInsight;
            ash = Config.startingAsh;
            initialized = true;
        }
        Handler.TryGrant(Config.Node(EmberEffect.Origin), broadcast: false);
        if (!Pawn.health.hediffSet.HasHediff(MingyuanEmberDefOf.MX_Mingyuan_EmberBoons))
            Pawn.health.AddHediff(MingyuanEmberDefOf.MX_Mingyuan_EmberBoons);
        // Receipts use stable names so a removed Def can still return the exact paid resources.
        var known = new HashSet<string>(Config.Nodes.Select(n => n.defName));
        foreach (string key in paidInsight.Keys.Where(k => !known.Contains(k)).ToArray())
        { insight += paidInsight[key]; paidInsight.Remove(key); }
        foreach (string key in paidAsh.Keys.Where(k => !known.Contains(k)).ToArray())
        { ash += paidAsh[key]; paidAsh.Remove(key); }
        cachedRevision = -1;
        Handler.RestoreGrantedContent();
    }

    public override void CompPostPostAdd(DamageInfo? dinfo) { base.CompPostPostAdd(dinfo); Initialize(); }
    public override void CompExposeData()
    {
        Scribe_Values.Look(ref initialized, "emberInitialized");
        Scribe_Values.Look(ref insight, "emberInsight"); Scribe_Values.Look(ref ash, "emberAsh");
        Scribe_Values.Look(ref experience, "emberExperience"); Scribe_Values.Look(ref casts, "emberCasts");
        Scribe_Values.Look(ref rewardedCasts, "emberRewardedCasts");
        Scribe_Values.Look(ref kills, "emberKills"); Scribe_Values.Look(ref spreads, "emberSpreads");
        Scribe_Values.Look(ref combos, "emberCombos"); Scribe_Values.Look(ref overburnTicks, "emberOverburnTicks");
        Scribe_Values.Look(ref burnDamage, "emberBurnDamage"); Scribe_Values.Look(ref repairedDamage, "emberRepairedDamage");
        Scribe_Values.Look(ref claimedTrials, "emberClaimedTrials");
        Scribe_Collections.Look(ref paidInsight, "emberPaidInsight", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref paidAsh, "emberPaidAsh", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref comboCasts, "emberComboCasts", LookMode.Def, LookMode.Value);
        Scribe_Collections.Look(ref stackTargets, "emberStackTargets", LookMode.Reference, LookMode.Value,
            ref stackTargetKeys, ref stackTargetValues, logNullErrors: false);
        Scribe_Collections.Look(ref groundTargets, "emberGroundTargets", LookMode.Reference, LookMode.Value,
            ref groundTargetKeys, ref groundTargetValues, logNullErrors: false);
        Scribe_Values.Look(ref lastCombatTick, "emberLastCombatTick", -99999);
        Scribe_Values.Look(ref nextCastRewardTick, "emberNextCastRewardTick");
        Scribe_Values.Look(ref nextKillRecoveryTick, "emberNextKillRecoveryTick");
        Scribe_Values.Look(ref nextComboResetTick, "emberNextComboResetTick");
        Scribe_Values.Look(ref castMoveUntil, "emberCastMoveUntil");
        Scribe_Values.Look(ref emberWindUntil, "emberWindUntil");
        Scribe_Values.Look(ref comboHintUntil, "emberComboHintUntil");
        Scribe_Values.Look(ref overburnStarted, "emberOverburnStarted", -1);
        Scribe_Values.Look(ref feedbackWindowTick, "emberFeedbackWindowTick");
        Scribe_Values.Look(ref feedbackLayers, "emberFeedbackLayers");
        Scribe_Values.Look(ref echoStepReady, "emberEchoStepReady");
        Scribe_Values.Look(ref returnOrigin, "emberReturnOrigin");
        Scribe_References.Look(ref returnMap, "emberReturnMap");
        Scribe_Values.Look(ref returnUntil, "emberReturnUntil");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            paidInsight ??= new(); paidAsh ??= new(); comboCasts ??= new();
            stackTargets ??= new(); groundTargets ??= new();
            insight = Math.Max(0, insight); ash = Math.Max(0, ash);
            Initialize();
        }
    }

    public int Level(EmberEffect effect)
    {
        if (cachedRevision != (Tree?.Revision ?? -1) || cachedSealRevision != MingyuanPowerBalance.Profile.Revision)
        {
            levels.Clear();
            foreach (var node in Config.Nodes) levels[node.effect] = Tree?.EffectiveNodeLevel(node) ?? 0;
            cachedRevision = Tree?.Revision ?? -1;
            cachedSealRevision = MingyuanPowerBalance.Profile.Revision;
        }
        return levels.GetValueOrDefault(effect);
    }
    internal float Value(EmberEffect effect) => Level(effect) * Config.Node(effect).value;
    internal static float Value(Pawn pawn, EmberEffect effect) => For(pawn)?.Value(effect) ?? 0f;
    internal static bool Has(Pawn pawn, EmberEffect effect) => For(pawn)?.Level(effect) > 0;
    public void Notify_PerkTreeChanged(Pawn pawn, HediffComp_CharacterPerkTree state) { if (state == Tree) cachedRevision = -1; }
    public int Spent(CharacterPerkCategoryDef category, Func<CharacterPerkNodeDef, int> level = null)
    {
        int total = 0;
        foreach (var node in Config.Nodes)
            if (node.category == category && (level?.Invoke(node) ?? Tree.GetNodeLevel(node)) > 0)
                total += paidInsight.GetValueOrDefault(node.defName);
        return total;
    }
    internal IPerkCostTransaction StagePayment(MingyuanEmberNodeDef node, int level) => new Payment(this, node, level);
    private sealed class Payment(MingyuanEmberBook owner, MingyuanEmberNodeDef node, int level) : IPerkCostTransaction
    {
        private bool complete;
        public void Commit()
        {
            if (complete) return;
            complete = true;
            int i = node.InsightCost(level), a = node.AshCost(level);
            owner.insight -= i; owner.ash -= a;
            owner.paidInsight[node.defName] = owner.paidInsight.GetValueOrDefault(node.defName) + i;
            owner.paidAsh[node.defName] = owner.paidAsh.GetValueOrDefault(node.defName) + a;
        }
        public void Dispose() { complete = true; }
    }
    internal void Refund(MingyuanEmberNodeDef node)
    {
        if (paidInsight.Remove(node.defName, out int i)) insight += i;
        if (paidAsh.Remove(node.defName, out int a)) ash += a;
    }
    public (int insight, int ash) RefundAmount(IEnumerable<CharacterPerkNodeDef> nodes)
    {
        int i = 0, a = 0;
        foreach (var node in nodes) { i += paidInsight.GetValueOrDefault(node.defName); a += paidAsh.GetValueOrDefault(node.defName); }
        return (i, a);
    }

    private bool CanEarn => !MingyuanPowerBalance.Sealed && Pawn is { Dead: false } && Pawn.IsColonistPlayerControlled;
    private void EarnExperience(int amount)
    {
        int before = experience / Math.Max(1, Config.experiencePerInsight);
        experience = Math.Min(100000000, experience + amount);
        insight += experience / Math.Max(1, Config.experiencePerInsight) - before;
    }
    internal void NotifyCombat()
    {
        bool wasInCombat = InCombat;
        lastCombatTick = Now;
        if (!wasInCombat) RefreshBoons();
    }
    internal void NotifyCast(Ability ability)
    {
        if (!CanEarn || ability == null) return;
        if (Level(EmberEffect.CastMove) > 0) castMoveUntil = Now + Config.Node(EmberEffect.CastMove).durationTicks;
        RefreshBoons();
        if (!InCombat) return;
        comboHintUntil = Now + Config.Node(EmberEffect.ComboInsight).durationTicks;
        casts++;
        if (Now >= nextCastRewardTick)
        {
            int before = rewardedCasts / Math.Max(1, Config.castsPerAsh);
            rewardedCasts++;
            ash += rewardedCasts / Math.Max(1, Config.castsPerAsh) - before;
            EarnExperience(1);
            nextCastRewardTick = Now + Config.resourceEventCooldownTicks;
        }
        comboCasts[ability.def] = Now;
        int window = Config.Node(EmberEffect.ComboReset).durationTicks;
        foreach (var key in comboCasts.Where(p => Now - p.Value > window).Select(p => p.Key).ToArray()) comboCasts.Remove(key);
        if (comboCasts.Count >= 3)
        {
            combos++;
            comboCasts.Clear();
            if (Level(EmberEffect.ComboReset) > 0 && Now >= nextComboResetTick)
            {
                echoStepReady = true;
                nextComboResetTick = Now + Config.Node(EmberEffect.ComboReset).cooldownTicks;
            }
        }
        ClaimTrials();
    }
    internal void NotifyBurnDamage(float actual)
    {
        if (!CanEarn || actual <= 0f) return;
        NotifyCombat();
        int before = Mathf.FloorToInt(burnDamage / Math.Max(1f, Config.burnDamagePerAsh));
        burnDamage += actual;
        int gained = Mathf.FloorToInt(burnDamage / Math.Max(1f, Config.burnDamagePerAsh)) - before;
        ash += gained; EarnExperience(gained);
    }
    internal float ExtraStack(Pawn target, float layers)
    {
        var node = Config.Node(EmberEffect.ExtraStack);
        if (Level(node.effect) <= 0 || target == null || !target.HostileTo(Pawn) || MingyuanUtility.GetLifeBurnLayers(target) <= 0f
            || !UseTargetCooldown(stackTargets, target, node.cooldownTicks)) return layers;
        return layers * (1f + node.value);
    }
    internal void NotifyBurst(Pawn target)
    {
        var node = Config.Node(EmberEffect.EmberGround);
        if (Level(node.effect) <= 0 || target?.MapHeld == null
            || !UseTargetCooldown(groundTargets, target, node.cooldownTicks)) return;
        MingyuanEmberEffects.SpawnGround(Pawn, target.MapHeld, target.PositionHeld, node);
    }
    private bool UseTargetCooldown(Dictionary<Pawn, int> cooldowns, Pawn target, int duration)
    {
        if (cooldowns.TryGetValue(target, out int until) && Now < until) return false;
        foreach (var key in cooldowns.Where(p => p.Key == null || p.Key.Destroyed || p.Value <= Now).Select(p => p.Key).ToArray())
            cooldowns.Remove(key);
        cooldowns[target] = Now + duration;
        return true;
    }
    internal void NotifyBurnKill(bool directlyKilled)
    {
        if (!CanEarn) return;
        NotifyCombat(); kills++; EarnExperience(1);
        if (directlyKilled && Level(EmberEffect.KillRecovery) > 0 && Now >= nextKillRecoveryTick)
        {
            nextKillRecoveryTick = Now + Config.Node(EmberEffect.KillRecovery).cooldownTicks;
            MingyuanUtility.AddSelfBurn(Pawn, Value(EmberEffect.KillRecovery));
        }
        ClaimTrials();
    }
    internal void NotifySpread() { if (!CanEarn) return; spreads++; ClaimTrials(); }
    internal void NotifyShieldRepair(float actualHealed)
    {
        if (!CanEarn || actualHealed <= 0f || !InCombat) return;
        int before = Mathf.FloorToInt(repairedDamage / Math.Max(1f, Config.shieldRepairPerAsh));
        repairedDamage += actualHealed;
        int gained = Mathf.FloorToInt(repairedDamage / Math.Max(1f, Config.shieldRepairPerAsh)) - before;
        ash += gained; EarnExperience(gained);
        var node = Config.Node(EmberEffect.ShieldFeedback);
        if (Level(EmberEffect.ShieldFeedback) > 0 && actualHealed >= node.threshold)
        {
            if (Now - feedbackWindowTick >= node.cooldownTicks) { feedbackWindowTick = Now; feedbackLayers = 0; }
            float grant = Mathf.Min(node.value, node.penalty - feedbackLayers);
            if (grant > 0f) { feedbackLayers += Mathf.RoundToInt(grant); MingyuanUtility.AddSelfBurn(Pawn, grant); }
        }
        ClaimTrials();
    }
    internal void ObserveOverburn(bool active)
    {
        if (!CanEarn || !InCombat || !active) { overburnStarted = -1; return; }
        if (overburnStarted < 0) overburnStarted = Now;
        int survived = Now - overburnStarted + 1;
        if (survived > overburnTicks) overburnTicks = survived;
        if (survived == Config.overburnTrialTicks) ClaimTrials();
    }
    public float TrialProgress(EmberTrial trial) => trial switch
    {
        EmberTrial.Cast => casts, EmberTrial.Burn => kills, EmberTrial.Shield => repairedDamage,
        EmberTrial.Spread => spreads, EmberTrial.Overburn => overburnTicks / 60f, EmberTrial.Combo => combos, _ => 1f
    };
    public float TrialTarget(EmberTrial trial) => trial switch
    {
        EmberTrial.Cast => Config.castTrialCount, EmberTrial.Burn => Config.killTrialCount,
        EmberTrial.Shield => Config.shieldTrialAmount, EmberTrial.Spread => Config.spreadTrialCount,
        EmberTrial.Overburn => Config.overburnTrialTicks / 60f, EmberTrial.Combo => Config.comboTrialCount, _ => 1f
    };
    public bool TrialComplete(EmberTrial trial) => TrialProgress(trial) >= TrialTarget(trial);
    public string TrialDescription(EmberTrial trial) => trial switch
    {
        EmberTrial.Cast => EmberText.Get("TrialCast", "焰仪熟练：实战施放主动技能"),
        EmberTrial.Burn => EmberText.Get("TrialBurn", "烬火回收：带有生命燃烧的敌人死亡"),
        EmberTrial.Shield => EmberText.Get("TrialShield", "守焰试炼：护焰在战斗中修复伤势"),
        EmberTrial.Spread => EmberText.Get("TrialSpread", "扩焰试炼：生命燃烧成功传播"),
        EmberTrial.Overburn => EmberText.Get("TrialOverburn", "自限之战：连续过燃并存活（秒）"),
        EmberTrial.Combo => EmberText.Get("TrialCombo", "连章演武：20 秒内施放三种主动技能"),
        _ => EmberText.Get("NoTrial", "无需试炼")
    };
    private void ClaimTrials()
    {
        foreach (EmberTrial trial in Enum.GetValues<EmberTrial>())
        {
            int mask = 1 << (int)trial;
            if (trial == EmberTrial.None || (claimedTrials & mask) != 0 || !TrialComplete(trial)) continue;
            claimedTrials |= mask;
            insight += Config.trialInsightReward; ash += Config.trialAshReward;
        }
    }
    internal void OfferReturn(IntVec3 origin, Map map)
    {
        if (Level(EmberEffect.DashReturn) <= 0) return;
        returnOrigin = origin; returnMap = map; returnUntil = Now + Config.Node(EmberEffect.DashReturn).durationTicks;
    }

    public override IEnumerable<Gizmo> CompGetGizmos()
    {
        if (!MingyuanUtility.IsMingyuan(Pawn) || !Pawn.IsColonistPlayerControlled || Pawn.Dead) yield break;
        yield return new Command_Action
        {
            defaultLabel = Config.LabelCap, defaultDesc = Config.description,
            icon = ContentFinder<Texture2D>.Get(Config.iconPath),
            action = () => Find.WindowStack.Add(new Dialog_MingyuanEmberBook(this))
        };
        if (Level(EmberEffect.DashReturn) > 0 && returnMap == Pawn.Map && Now < returnUntil && Pawn.Spawned)
            yield return MingyuanEmberEffects.ReturnCommand(this, returnOrigin, returnMap);
        if (Level(EmberEffect.ComboReset) > 0 && echoStepReady && Pawn.Spawned)
            yield return MingyuanEmberEffects.EchoStepCommand(this);
        if (ComboHintActive)
            yield return new Command_Action
            {
                defaultLabel = EmberText.Get("ComboWindow", "连章窗口"),
                defaultDesc = EmberText.Get("ComboTip", "20 秒内施放三种不同主动技能，记录一次连章。"),
                icon = ContentFinder<Texture2D>.Get(Config.iconPath),
                action = () => Find.WindowStack.Add(new Dialog_MingyuanEmberBook(this))
            };
    }
    internal bool ConsumeReturn()
    {
        if (Level(EmberEffect.DashReturn) <= 0 || Now >= returnUntil || Pawn.Map != returnMap || Pawn.Dead) return false;
        returnUntil = 0; return true;
    }
    internal bool ConsumeEchoStep()
    {
        if (Level(EmberEffect.ComboReset) <= 0 || !echoStepReady || Pawn.Dead) return false;
        echoStepReady = false; return true;
    }
    internal bool EmberWindActive => Now < emberWindUntil;
    internal void GrantEmberWind()
    { emberWindUntil = Now + Config.Node(EmberEffect.PillarTurn).durationTicks; RefreshBoons(); }
    internal void RefreshBoons()
    {
        if (Pawn?.health?.hediffSet?.GetFirstHediffOfDef(MingyuanEmberDefOf.MX_Mingyuan_EmberBoons) is Hediff_MingyuanEmberBoons boons)
        {
            boons.Notify_PerkTreeChanged(Pawn, Tree);
            Pawn.health.Notify_HediffChanged(boons);
        }
    }
    public override void CompPostTick(ref float severityAdjustment)
    {
        // Only local expiry boundaries; no pawn or map scan and no progress repair polling.
        if (Now == castMoveUntil || Now == emberWindUntil || Now == lastCombatTick + Config.combatMemoryTicks + 1)
            RefreshBoons();
    }
}

public sealed class Hediff_MingyuanEmberBoons : Hediff_CharacterPerkTree
{
    protected override HediffComp_CharacterPerkTree SourceTree => MingyuanEmberBook.For(pawn)?.Tree;
    protected override void AddAdditionalStats(Dictionary<StatDef, float> offsets, Dictionary<StatDef, float> factors)
    {
        var book = MingyuanEmberBook.For(pawn);
        if (book == null || MingyuanPowerBalance.Sealed) return;
        float speed = book.InCombat ? book.Value(EmberEffect.CombatMove) : 0f;
        if (book.CastMoveActive) speed += book.Value(EmberEffect.CastMove);
        if (book.EmberWindActive) speed += book.Value(EmberEffect.PillarTurn);
        if (speed > 0f) factors[StatDefOf.MoveSpeed] = 1f + speed;
    }
}
