using System;
using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.Common.Combat;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation;

internal static class CultivationTrials
{
    internal static string Label(CultivationTrial trial) => trial switch
    {
        CultivationTrial.Attack => "MX_Cultivation_TrialAttack".Translate().ToString(), CultivationTrial.Switch => "MX_Cultivation_TrialSwitch".Translate().ToString(),
        CultivationTrial.Block => "MX_Cultivation_TrialBlock".Translate().ToString(), CultivationTrial.Dodge => "MX_Cultivation_TrialDodge".Translate().ToString(),
        CultivationTrial.Kills => "MX_Cultivation_TrialKills".Translate().ToString(), CultivationTrial.Skill => "MX_Cultivation_TrialSkill".Translate().ToString(),
        CultivationTrial.Heal => "MX_Cultivation_TrialHeal".Translate().ToString(), _ => "MX_Cultivation_TrialNone".Translate().ToString()
    };
}

// Progress and trigger clocks belong to the existing cultivation carrier, alongside the shared tree.
public sealed partial class Hediff_NeiyuCultivation
{
    private readonly Dictionary<CultivationEffect, float> bonuses = new();
    private Dictionary<CultivationTrial, int> trials = new();
    private Dictionary<CultivationEffect, int> readyAt = new(), activeUntil = new();
    private int combatUntil, combatMap = -1, spellForms, lastAttack = -1, lastDodge = -1, lastBlock = -1, lastHeal = -1;
    private int lastSwitchCount = -1000, lastCastCount = -1, tempoHit = -1, nextQuietRecovery;
    private bool flowerSwordPending, battleBroken;
    private int nextEffectExpiry = int.MaxValue;
    private readonly List<CultivationEffect> expiredEffects = new();
    internal int ProgressRevision { get; private set; }
    internal static int Now => Find.TickManager?.TicksGame ?? 0;
    internal bool InCombat => pawn.Spawned && combatMap == pawn.Map.uniqueID && combatUntil > Now;
    internal float Bonus(CultivationEffect effect) => NeiyuPowerBalance.PassivesDisabled ? 0f : bonuses.GetValueOrDefault(effect);
    internal bool Has(CultivationEffect effect) => !NeiyuPowerBalance.PassivesDisabled && bonuses.ContainsKey(effect);
    internal static NeiyuCultivationNodeDef Definition(CultivationEffect effect) => CultivationService.ExtensionNodes[effect];
    public int Progress(CultivationTrial trial) => trials.GetValueOrDefault(trial);
    private void Count(CultivationTrial trial)
    { trials[trial] = Math.Min(int.MaxValue - 1, Progress(trial)) + 1; ProgressRevision++; }
    private bool ValidEnemy(Thing target) => pawn.Spawned && !pawn.Dead && target is Pawn enemy
        && enemy != pawn && enemy.MapHeld == pawn.Map && enemy.HostileTo(pawn);
    internal void Combat()
    {
        if (!pawn.Spawned || pawn.Dead) return;
        if (!InCombat) { spellForms = 0; flowerSwordPending = false; battleBroken = false; }
        combatMap = pawn.Map.uniqueID;
        combatUntil = Now + 600;
    }
    private bool Active(CultivationEffect effect) => Has(effect) && activeUntil.GetValueOrDefault(effect) > Now;
    private bool Trigger(CultivationEffect effect, bool switchEffect = false)
    {
        if (!Has(effect) || readyAt.GetValueOrDefault(effect) > Now) return false;
        var node = Definition(effect);
        readyAt[effect] = Now + Mathf.RoundToInt(node.cooldownTicks * (switchEffect ? 1f - Bonus(CultivationEffect.SwitchRhythm) : 1f));
        if (node.durationTicks > 0)
        { activeUntil[effect] = Now + node.durationTicks; nextEffectExpiry = Math.Min(nextEffectExpiry, activeUntil[effect]); Events.RefreshEffects(); }
        return true;
    }
    internal void NotifySwitch(ThingDef before, ThingDef after)
    {
        if (!InCombat || before == after || pawn.equipment?.Primary?.def != after) return;
        if (Now - lastSwitchCount >= 120) { Count(CultivationTrial.Switch); lastSwitchCount = Now; }
        if (Has(CultivationEffect.SwitchTempo))
        { activeUntil[CultivationEffect.SwitchTempo] = Now + Definition(CultivationEffect.SwitchTempo).durationTicks;
            nextEffectExpiry = Math.Min(nextEffectExpiry, activeUntil[CultivationEffect.SwitchTempo]); }
        flowerSwordPending = before.defName == "MX_Neiyu_Form_Flower" && after.defName == "MX_Neiyu_Form_Weapon";
        if (spellForms == 7 && Trigger(CultivationEffect.Finale, true)) spellForms = 0;
    }
    internal void NotifyDodge(Pawn attacker)
    {
        if (!ValidEnemy(attacker) || NeiyuPowerBalance.PassivesDisabled || lastDodge == Now) return;
        lastDodge = Now; Combat(); Count(CultivationTrial.Dodge);
        Trigger(CultivationEffect.DodgeStep);
        Trigger(CultivationEffect.DodgeCounter);
    }
    internal bool NotifyShield(DamageInfo damage, bool broke)
    {
        if (!ValidEnemy(damage.Instigator) || NeiyuPowerBalance.PassivesDisabled || damage.Amount <= 0f) return false;
        Combat();
        if (lastBlock != Now) { Count(CultivationTrial.Block); lastBlock = Now; }
        if (broke)
        {
            Trigger(CultivationEffect.BreakStep);
            if (!battleBroken) { battleBroken = true; Trigger(CultivationEffect.LastFeather); }
        }
        return Trigger(CultivationEffect.ShieldMemory);
    }
    internal void NotifyCast(Ability ability, LocalTargetInfo? target)
    {
        if (NeiyuPowerBalance.AbilitiesDisabled || !pawn.Spawned || ability?.def == null) return;
        if (!InCombat && !(target.HasValue && ValidEnemy(target.Value.Thing))
            && !pawn.Map.mapPawns.AllPawnsSpawned.Any(p => p.HostileTo(pawn) && !p.Dead && !p.Downed
                && p.Position.DistanceToSquared(pawn.Position) <= 1600)) return;
        Combat();
        if (lastCastCount != Now) { Count(CultivationTrial.Skill); lastCastCount = Now; }
        string name = ability.def.defName;
        if (name is "MX_Neiyu_Flower_BlessingField" or "MX_Neiyu_Flower_ToxinGarden") spellForms |= 1;
        else if (name is "MX_Neiyu_Sword_Skyfall" or "MX_Neiyu_Sword_ExecuteHead") spellForms |= 2;
        else if (name is "MX_Neiyu_Bow_ArrowBarrage" or "MX_Neiyu_ThunderMarkedStorm") spellForms |= 4;
    }
    internal void NotifyHealing(float healed)
    {
        if (healed > .001f && InCombat && lastHeal != Now)
        { lastHeal = Now; Count(CultivationTrial.Heal); }
    }
    public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
    {
        base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
        if (totalDamageDealt > 0 && ValidEnemy(dinfo.Instigator)) Combat();
    }
    public override void Notify_PawnDamagedThing(Thing thing, DamageInfo dinfo, DamageWorker.DamageResult result)
    {
        base.Notify_PawnDamagedThing(thing, dinfo, result);
        if (!ValidEnemy(thing) || result?.totalDamageDealt is not > 0f) return;
        Combat();
        if (dinfo.Weapon?.defName is not ("MX_Neiyu_Form_Weapon" or "MX_Neiyu_Form_Flower" or "MX_Neiyu_Form_Bow")) return;
        Projectile projectile = Patch_CombatBlockProjectile.Current;
        if (projectile != null && projectile is not (Projectile_HomingShard or Projectile_BigArrowSplit)) return;
        if (lastAttack == Now) return;
        lastAttack = Now; Count(CultivationTrial.Attack);
        if (Active(CultivationEffect.SwitchTempo) && Trigger(CultivationEffect.SwitchTempo, true))
        {
            activeUntil.Remove(CultivationEffect.SwitchTempo);
            // Melee applies damage before its cooldown is installed; arrows arrive after launch.
            if (projectile == null) tempoHit = Now;
            else if (pawn.stances?.curStance is Stance_Cooldown { verb: not Verb_CastAbility } cooldown
                && cooldown.verb?.EquipmentSource?.def == dinfo.Weapon)
                cooldown.ticksLeft = Mathf.Max(1, cooldown.ticksLeft - Mathf.RoundToInt(Bonus(CultivationEffect.SwitchTempo) * 60f));
        }
        if (dinfo.Weapon.defName == "MX_Neiyu_Form_Weapon")
        {
            activeUntil.Remove(CultivationEffect.DodgeCounter);
            if (flowerSwordPending && Trigger(CultivationEffect.FlowerSword, true)
                && MXNeiyuShieldUtility.TryGetShieldComp(pawn, out var shield))
                shield.RestoreCultivationCharges(Mathf.RoundToInt(Bonus(CultivationEffect.FlowerSword)));
            flowerSwordPending = false;
        }
    }
    public override void Notify_KilledPawn(Pawn victim, DamageInfo? dinfo)
    {
        base.Notify_KilledPawn(victim, dinfo);
        if (pawn.Spawned && victim != pawn && victim.HostileTo(pawn) && dinfo?.Instigator == pawn)
        { Combat(); Count(CultivationTrial.Kills); }
    }
    internal float AttackFactor(ThingDef weapon)
    {
        if (weapon == null) return 1f;
        string name = weapon.defName;
        bool sword = name == "MX_Neiyu_Form_Weapon", bow = name == "MX_Neiyu_Form_Bow";
        if (!sword && !bow && name != "MX_Neiyu_Form_Flower") return 1f;
        float factor = (1f + Bonus(CultivationEffect.FormDamage))
            * (1f + (sword ? Bonus(CultivationEffect.SwordDamage) : bow ? Bonus(CultivationEffect.BowDamage) : 0f))
            * (1f + (sword || bow ? Bonus(CultivationEffect.FinalDamage) : 0f));
        factor = Mathf.Min(1f + Definition(CultivationEffect.FinalDamage).secondary, factor);
        return factor * (sword && Active(CultivationEffect.DodgeCounter) ? 1f + Bonus(CultivationEffect.DodgeCounter) : 1f);
    }
    internal float AttackCooldown(Verb verb, float seconds)
    {
        if (verb is Verb_CastAbility || verb?.EquipmentSource?.def is not { } weapon) return seconds;
        if (weapon.defName == "MX_Neiyu_Form_Bow") seconds *= 1f - Bonus(CultivationEffect.BowCooldown);
        if (tempoHit == Now) seconds = Mathf.Max(.05f, seconds - Bonus(CultivationEffect.SwitchTempo));
        return seconds;
    }
    private void AddRhythmStats(Dictionary<StatDef, float> offsets, Dictionary<StatDef, float> factors)
    {
        void Add(StatDef stat, float value) { if (value != 0) offsets[stat] = offsets.GetValueOrDefault(stat) + value; }
        void Multiply(StatDef stat, float value) { if (value != 1) factors[stat] = factors.GetValueOrDefault(stat, 1f) * value; }
        Add(StatDefOf.MoveSpeed, Bonus(CultivationEffect.MoveSpeed));
        float movement = 0;
        foreach (var effect in new[] { CultivationEffect.DodgeStep, CultivationEffect.BreakStep, CultivationEffect.Finale })
            if (Active(effect)) movement = Mathf.Max(movement, Bonus(effect));
        Multiply(StatDefOf.MoveSpeed, 1f + movement);
        Multiply(StatDefOf.IncomingDamageFactor, (1f - Bonus(CultivationEffect.DamageReduction))
            * (Active(CultivationEffect.LastFeather) ? 1f - Bonus(CultivationEffect.LastFeather) : 1f));
    }
    public override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (pawn.Dead || !pawn.Spawned) return;
        if (nextEffectExpiry <= Now)
        {
            expiredEffects.Clear(); nextEffectExpiry = int.MaxValue;
            foreach (var pair in activeUntil)
                if (pair.Value <= Now) expiredEffects.Add(pair.Key);
                else nextEffectExpiry = Math.Min(nextEffectExpiry, pair.Value);
            foreach (var effect in expiredEffects) activeUntil.Remove(effect);
            if (expiredEffects.Count > 0) Events.RefreshEffects();
        }
        if (InCombat || !Has(CultivationEffect.QuietShield)) { nextQuietRecovery = Now + Definition(CultivationEffect.QuietShield).cooldownTicks; return; }
        if (Now < nextQuietRecovery) return;
        nextQuietRecovery = Now + Definition(CultivationEffect.QuietShield).cooldownTicks;
        if (MXNeiyuShieldUtility.TryGetShieldComp(pawn, out var shield))
            shield.RestoreCultivationCharges(1, Mathf.FloorToInt(shield.Phase2MaxCharges * Definition(CultivationEffect.QuietShield).secondary));
    }
    private void ExposeRhythm()
    {
        Scribe_Collections.Look(ref trials, "featherTrials", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref readyAt, "featherReadyAt", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref activeUntil, "featherActiveUntil", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref combatUntil, "featherCombatUntil"); Scribe_Values.Look(ref combatMap, "featherCombatMap", -1);
        Scribe_Values.Look(ref spellForms, "featherSpellForms"); Scribe_Values.Look(ref flowerSwordPending, "featherFlowerSwordPending");
        Scribe_Values.Look(ref battleBroken, "featherBattleBroken"); Scribe_Values.Look(ref lastSwitchCount, "featherLastSwitch", -1000);
        Scribe_Values.Look(ref nextQuietRecovery, "featherQuietRecovery");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        { trials ??= new(); readyAt ??= new(); activeUntil ??= new(); nextEffectExpiry = activeUntil.Values.DefaultIfEmpty(int.MaxValue).Min(); }
    }
}
