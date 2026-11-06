using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common.Combat;

/// <summary>
/// Marks a DamageDef as one named technique (a sword form, a skill cut, an execution). Its hits
/// carry the def's label as the tool (shown in the wound tooltip as "weapon technique") and are
/// written to the combat log with the technique's own rules.
/// </summary>
public class TechniqueDamageExtension : DefModExtension
{
    /// <summary>Combat log rules of a hit; null = no log entry.</summary>
    public RulePackDef logRulesHit;
    /// <summary>Rules when armour turned the whole blow aside; null = keep the hit rules.</summary>
    public RulePackDef logRulesDeflect;
    /// <summary>Rules of a miss and a dodge, for techniques that roll them (melee verb cuts).</summary>
    public RulePackDef logRulesMiss;
    public RulePackDef logRulesDodge;
}

public static class TechniqueDamage
{
    private static readonly Dictionary<DamageDef, Tool> Tools = new();

    public static TechniqueDamageExtension For(DamageDef def) => def?.GetModExtension<TechniqueDamageExtension>();

    /// <summary>
    /// A tool standing for the technique: its label is the (translated) DamageDef label. Built on
    /// first use, after translations are loaded.
    /// </summary>
    public static Tool ToolFor(DamageDef def)
    {
        if (def == null)
        {
            return null;
        }
        if (!Tools.TryGetValue(def, out Tool tool))
        {
            tool = new Tool
            {
                id = def.defName,
                label = def.label,
                labelNoLocation = def.label,
                labelUsedInLogging = true,
            };
            Tools[def] = tool;
        }
        return tool;
    }

    /// <summary>The combat log entry of one hit of the technique, before its damage; null without hit rules.</summary>
    public static BattleLogEntry_MeleeCombat CreateLog(Pawn caster, Thing target, DamageDef def, ThingDef weapon)
    {
        return CreateLog(caster, target, def, weapon, For(def)?.logRulesHit, alwaysShow: true);
    }

    /// <summary>A combat log entry of the technique with the given rules (hit, miss, dodge); null without them.</summary>
    public static BattleLogEntry_MeleeCombat CreateLog(Pawn caster, Thing target, DamageDef def, ThingDef weapon,
        RulePackDef rules, bool alwaysShow)
    {
        if (rules == null || def == null || caster == null || target == null)
        {
            return null;
        }
        BattleLogEntry_MeleeCombat entry = new(rules, alwaysShow, caster, target,
            ImplementOwnerTypeDefOf.Weapon, def.label, weapon, null, LogEntryDefOf.MeleeAttack);
        Find.BattleLog.Add(entry);
        return entry;
    }

    /// <summary>Links the damage to its log entry (the wounded parts), as vanilla melee does.</summary>
    public static void FinishLog(BattleLogEntry_MeleeCombat entry, DamageWorker.DamageResult result, DamageDef def)
    {
        if (entry == null || result == null)
        {
            return;
        }
        if (result.stunned && result.parts.NullOrEmpty())
        {
            Find.BattleLog.RemoveEntry(entry);
            return;
        }
        result.AssociateWithLog(entry);
        if (result.deflected && For(def)?.logRulesDeflect is { } deflect)
        {
            entry.RuleDef = deflect;
            entry.alwaysShowInCompact = false;
        }
    }

    /// <summary>
    /// Deals <paramref name="dinfo"/> as the technique of its def: the technique tool, then the log
    /// entry, the damage and the link between them. A def without the extension is plain damage.
    /// </summary>
    public static DamageWorker.DamageResult Apply(Thing target, DamageInfo dinfo)
    {
        if (For(dinfo.Def) == null)
        {
            return target.TakeDamage(dinfo);
        }
        dinfo.SetTool(ToolFor(dinfo.Def));
        BattleLogEntry_MeleeCombat entry = CreateLog(dinfo.Instigator as Pawn, target, dinfo.Def, dinfo.Weapon);
        DamageWorker.DamageResult result = target.TakeDamage(dinfo);
        FinishLog(entry, result, dinfo.Def);
        return result;
    }
}
