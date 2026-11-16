using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MiliraXian.Characters.Common.PerkSystem;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    internal sealed class CultivationViewModel
    {
        internal readonly record struct NodeState(int Rank, bool CanLearn, string Status, string Reason, Common.UI.PerkNodeVisual Visual);
        private sealed class InventoryScan
        {
            internal ThingDef Def;
            internal int Index, Count;
            internal bool Complete;
        }
        internal readonly Pawn Pawn;
        internal readonly List<NeiyuCultivationNodeDef> Nodes;
        internal readonly List<CharacterPerkCategoryDef> Categories, Foundations;
        internal readonly Dictionary<NeiyuCultivationNodeDef, NodeState> States = new();
        internal readonly Dictionary<NeiyuCultivationNodeDef, Vector2> Positions = new();
        private readonly List<InventoryScan> inventory = new();
        private readonly Dictionary<ThingDef, InventoryScan> byMaterial = new();
        internal NeiyuCultivationNodeDef Selected { get; private set; }
        internal CharacterPerkCategoryDef Category { get; private set; }
        internal bool Foundation { get; private set; }
        internal readonly Hediff_NeiyuCultivation State;
        internal List<CultivationEffectRow> FoundationEffects { get; private set; } = new();
        internal string Result { get; private set; }
        private float nextRefresh;
        private int revision = -1, powerRevision = -1, trialRevision = -1;
        internal CultivationViewModel(Pawn pawn)
        {
            Pawn = pawn;
            State = CultivationService.State(pawn);
            Nodes = CultivationService.Nodes.Where(n => State?.Tree.IsRelevantNode(n) == true).ToList();
            Categories = Nodes.Where(n => n.IsExtension && n.effect != CultivationEffect.Origin).Select(n => n.category).Distinct().OrderBy(c => c.displayOrder).ToList();
            Foundations = Nodes.Where(n => !n.IsExtension).Select(n => n.category).Distinct().OrderBy(c => c.displayOrder).ToList();
            foreach (var node in Nodes.Where(n => n.IsExtension)) Positions[node] = node.position;
            foreach (var category in Foundations)
            {
                int row = 0;
                foreach (var group in Nodes.Where(n => !n.IsExtension && n.category == category).GroupBy(n => n.ability))
                {
                    foreach (var node in group) Positions[node] = new Vector2(180 + (node.rank - 1) * 240, 150 + row * 200);
                    row++;
                }
            }
            foreach (var def in Nodes.Where(n => n.costs != null).SelectMany(n => n.costs).Select(c => c.thingDef).Distinct())
            { InventoryScan scan = new() { Def = def }; inventory.Add(scan); byMaterial[def] = scan; }
            Selected = Nodes.FirstOrDefault(n => n.effect == CultivationEffect.SwordDamage) ?? Nodes.FirstOrDefault();
        }
        internal bool Shown(NeiyuCultivationNodeDef node) => Foundation ? !node.IsExtension && node.category == Category : node.IsExtension;
        internal bool InBranch(NeiyuCultivationNodeDef node) => Shown(node) && (Foundation || Category == null || node.category == Category || node.effect == CultivationEffect.Origin);
        internal void Choose(CharacterPerkCategoryDef category, bool foundation = false)
        {
            Category = category; Foundation = foundation;
            Select(Nodes.FirstOrDefault(n => InBranch(n) && !n.initiallyLearned) ?? Nodes.FirstOrDefault(InBranch));
        }
        internal void Select(NeiyuCultivationNodeDef node)
        { Selected = node; Result = null; RefreshFoundationEffects(); }
        private void RefreshFoundationEffects() => FoundationEffects = Selected?.IsExtension == false
            ? CultivationEffectText.Build(Pawn, Selected) : new();
        internal string Investment => Nodes.Where(n => Shown(n) && !n.initiallyLearned).Sum(n => State?.Tree.GetNodeLevel(n) ?? 0)
            + " / " + Nodes.Where(n => Shown(n) && !n.initiallyLearned).Sum(n => n.maxLevel);
        internal int Available(ThingDef def) => byMaterial.TryGetValue(def, out var row) ? row.Count : 0;
        internal string InventoryText(ThingDef def) => byMaterial.TryGetValue(def, out var row) && row.Complete ? row.Count.ToString() : "…";
        internal void Invalidate() { revision = -1; }
        internal void Update()
        {
            if (State == null) return;
            bool refresh = revision != State.Tree.Revision || powerRevision != NeiyuPowerBalance.Revision
                || trialRevision != State.ProgressRevision || Time.realtimeSinceStartup >= nextRefresh;
            if (refresh)
            {
                revision = State.Tree.Revision; powerRevision = NeiyuPowerBalance.Revision; trialRevision = State.ProgressRevision;
                RefreshFoundationEffects();
                nextRefresh = Time.realtimeSinceStartup + 2;
                foreach (var row in inventory) { row.Index = row.Count = 0; row.Complete = false; }
            }
            int budget = 24;
            long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 500;
            foreach (var row in inventory)
            {
                if (row.Complete) continue;
                if (Pawn.Map == null) { row.Complete = true; continue; }
                var things = Pawn.Map.listerThings.ThingsOfDef(row.Def);
                while (row.Index < things.Count && budget > 0 && (budget == 24 || Stopwatch.GetTimestamp() < deadline))
                {
                    var thing = things[row.Index++]; budget--;
                    if (CultivationService.UsableMaterial(Pawn, thing)) row.Count += thing.stackCount;
                }
                row.Complete = row.Index >= things.Count;
                if (budget == 0 || Stopwatch.GetTimestamp() >= deadline) break;
            }
            foreach (var node in Nodes)
            {
                int rank = State.Tree.GetNodeLevel(node);
                bool eligible = State.Events.CanUnlock(node, out string reason, checkCosts: false);
                bool enough = node.CostsAt(rank)?.All(c => Available(c.thingDef) >= c.count) != false;
                bool can = eligible && enough;
                string status = rank >= node.maxLevel ? "已满级" : can ? rank > 0 ? "可升级" : "可学习" : eligible ? "材料不足" : "条件未满足";
                if (!eligible && rank < node.maxLevel && node.IsExtension)
                    status = node.trial != CultivationTrial.None && State.Progress(node.trial) < node.trialTarget ? "待试炼"
                        : node.prerequisites?.Any(p => State.Tree.GetNodeLevel(p.node) < p.minLevel) == true ? "前置不足" : "暂不可学习";
                if (eligible && !enough) reason = "所需材料尚未备齐。";
                if (NeiyuPowerBalance.PassivesDisabled) { status = "已封印"; reason = "已学等级保留，节点效果暂停。"; }
                var visual = NeiyuPowerBalance.PassivesDisabled ? Common.UI.PerkNodeVisual.Locked
                    : rank >= node.maxLevel ? Common.UI.PerkNodeVisual.Mastered : rank > 0 ? Common.UI.PerkNodeVisual.Learned
                    : can ? Common.UI.PerkNodeVisual.Available : Common.UI.PerkNodeVisual.Locked;
                States[node] = new(rank, can, status, reason, visual);
            }
        }
        internal void ConfirmLearn()
        {
            var node = Selected;
            if (node == null || !States[node].CanLearn) return;
            int level = State.Tree.GetNodeLevel(node);
            string costs = string.Join(" · ", node.CostsAt(level)?.Select(c => c.thingDef.LabelCap + " × " + c.count) ?? System.Array.Empty<string>());
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(node.LabelCap + "  " + level + " → " + (level + 1) + "\n\n" + costs, () =>
            {
                if (State.Tree.GetNodeLevel(node) != level) { Result = "节点状态已变化，请重新确认。"; return; }
                bool success = CultivationService.TryLearn(Pawn, node, out string message);
                Result = message; Invalidate(); Update();
                if (success) SoundDefOf.Tick_High.PlayOneShotOnCamera(); else SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }));
        }
    }

    internal sealed class CultivationEffectRow
    {
        public string Label, Before, After;
        public bool Heading;
        public bool HasValues => Before != null && After != null;
    }

    internal static class CultivationEffectText
    {
        private static readonly (string field, string label, string suffix, float scale)[] SkillFields =
        {
            ("radius", "作用半径", "格", 1f), ("range", "射程", "格", 1f),
            ("damage", "落地伤害", "", 1f), ("durationTicks", "持续时间", "秒", 1f / 60f),
            ("shotCount", "箭幕箭数", "支", 1f), ("strikeBudget", "雷击预算", "", 1f),
            ("cooldownTicks", "冷却", "秒", 1f / 60f)
        };
        private static float SkillValue(CultivationAbilityValues v, string field) => v == null ? -1f : field switch
        {
            "radius" => v.radius, "range" => v.range, "damage" => v.damage, "durationTicks" => v.durationTicks,
            "shotCount" => v.shotCount, "strikeBudget" => v.strikeBudget, "cooldownTicks" => v.cooldownTicks, _ => -1f
        };
        internal static string SkillSummary(CultivationAbilityValues values)
        {
            if (values == null) return "尚未领悟。";
            return string.Join("\n", SkillFields.Where(f => SkillValue(values, f.field) >= 0)
                .Select(f => f.label + "：" + (SkillValue(values, f.field) * f.scale).ToString("0.##") + f.suffix));
        }
        public static List<CultivationEffectRow> Build(Pawn pawn, NeiyuCultivationNodeDef selected)
        {
            List<CultivationEffectRow> rows = new();
            if (selected == null) return rows;
            if (NeiyuPowerBalance.PassivesDisabled)
            { rows.Add(new() { Label = "力量已封印，已领悟的修行仍会保留。" }); return rows; }
            int rank = CultivationService.Rank(pawn, selected.category, selected.ability);
            int target = Math.Max(rank, selected.rank);
            if (selected.ability != null)
            {
                var before = CultivationService.Nodes.FirstOrDefault(n => n.ability == selected.ability && n.rank == rank)?.abilityValues;
                var after = CultivationService.Nodes.FirstOrDefault(n => n.ability == selected.ability && n.rank == target)?.abilityValues;
                foreach (var field in SkillFields)
                {
                    float b = SkillValue(after, field.field), a = SkillValue(before, field.field);
                    if (b >= 0) rows.Add(new() { Label = field.label, Before = a < 0 ? "未领悟" : (a * field.scale).ToString("0.##") + field.suffix, After = (b * field.scale).ToString("0.##") + field.suffix });
                }
                return rows;
            }
            if (selected.category == CultivationDefOf.MX_Neiyu_Cultivation_Halo)
            {
                float Capacity(int r) => 24 + CultivationService.Nodes.Where(n => n.category == selected.category && n.rank <= r).Sum(n => n.shieldCharges);
                Line(rows, "常态盾层上限", Capacity(rank), Capacity(target));
                rows.Add(new() { Label = "虚弱期上限24层。领悟只增加容量，不补充当前盾层或刷新循环。" });
                return rows;
            }
            Dictionary<ThingDef, CultivationEquipmentBonus> Projection(int r)
            {
                Dictionary<ThingDef, CultivationEquipmentBonus> result = new();
                foreach (var node in CultivationService.Nodes.Where(n => n.category == selected.category && n.rank <= r))
                    if (node.equipmentEffects != null)
                        foreach (var effect in node.equipmentEffects)
                        {
                            if (!result.TryGetValue(effect.equipment, out var bonus)) result[effect.equipment] = bonus = new();
                            bonus.Add(effect);
                        }
                return result;
            }
            var current = Projection(rank); var next = Projection(target);
            foreach (var pair in next)
            {
                ThingDef def = pair.Key;
                var a = current.GetValueOrDefault(def) ?? new CultivationEquipmentBonus();
                var b = pair.Value;
                rows.Add(new() { Label = def.LabelCap, Heading = true });
                if (def.tools?.Count > 0)
                {
                    Line(rows, "基础伤害", def.tools[0].power + a.MeleeDamage, def.tools[0].power + b.MeleeDamage);
                    Line(rows, "穿透", (def.tools[0].armorPenetration + a.MeleePenetration) * 100f, (def.tools[0].armorPenetration + b.MeleePenetration) * 100f, "%");
                }
                foreach (var stat in b.Stats.Keys)
                    StatLine(rows, stat, def.statBases.GetStatOffsetFromList(stat) + a.Stats.GetValueOrDefault(stat), def.statBases.GetStatOffsetFromList(stat) + b.Stats[stat], false);
                foreach (var stat in b.Equipped.Keys)
                    StatLine(rows, stat, def.equippedStatOffsets.GetStatOffsetFromList(stat) + a.Equipped.GetValueOrDefault(stat), def.equippedStatOffsets.GetStatOffsetFromList(stat) + b.Equipped[stat], true);
                if (def.defName == "MX_Neiyu_Form_Bow")
                {
                    int baselineCount = DefDatabase<ThingDef>.GetNamed("MX_Bullet_BigSplitArrow").GetModExtension<SplitArrowExtension>().splitCount;
                    var shard = DefDatabase<ThingDef>.GetNamed("MX_Bullet_HomingShard").projectile;
                    Line(rows, "普攻碎矢", baselineCount + a.SplitCount, baselineCount + b.SplitCount, "支");
                    Line(rows, "每支碎矢伤害", shard.GetDamageAmount(1f, null) + a.ProjectileDamage, shard.GetDamageAmount(1f, null) + b.ProjectileDamage);
                    Line(rows, "碎矢穿透", (shard.GetArmorPenetration(null) + a.ProjectilePenetration) * 100f, (shard.GetArmorPenetration(null) + b.ProjectilePenetration) * 100f, "%");
                }
            }
            return rows;
        }
        private static void StatLine(List<CultivationEffectRow> rows, StatDef stat, float a, float b, bool equipped)
        {
            string label = stat.LabelCap + (stat == StatDefOf.MeleeDodgeChance ? "（原始分值）" : "");
            var sense = equipped ? ToStringNumberSense.Offset : ToStringNumberSense.Absolute;
            rows.Add(new() { Label = label, Before = a.ToStringByStyle(stat.ToStringStyleUnfinalized, sense), After = b.ToStringByStyle(stat.ToStringStyleUnfinalized, sense) });
        }
        private static void Line(List<CultivationEffectRow> rows, string label, float a, float b, string suffix = "") =>
            rows.Add(new() { Label = label, Before = a.ToString("0.##") + suffix, After = b.ToString("0.##") + suffix });
    }
}
