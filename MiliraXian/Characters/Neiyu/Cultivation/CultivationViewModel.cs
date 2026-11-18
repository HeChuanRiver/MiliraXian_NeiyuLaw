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
        private const int InventoryStacksPerFrame = 24;
        private const float InventoryRefreshInterval = 2f;
        private const string InventoryPendingText = "正在统计";

        internal readonly record struct NodeState(int Rank, bool CanLearn, string Status, string Reason, Common.UI.PerkNodeVisual Visual);
        private enum MaterialStatus { Ready, Pending, Missing }

        private sealed class InventoryScan(ThingDef def)
        {
            internal readonly ThingDef Def = def;
            internal int Index, Accumulated;
            internal int? Count;
        }
        internal readonly Pawn Pawn;
        internal readonly List<NeiyuCultivationNodeDef> Nodes;
        internal readonly List<CharacterPerkCategoryDef> Categories, Foundations;
        internal readonly Dictionary<NeiyuCultivationNodeDef, NodeState> States = new();
        internal readonly Dictionary<NeiyuCultivationNodeDef, Vector2> Positions = new();
        private readonly Dictionary<ThingDef, InventoryScan> byMaterial = new();
        private readonly Queue<InventoryScan> pendingInventory = new();
        internal NeiyuCultivationNodeDef Selected { get; private set; }
        internal CharacterPerkCategoryDef Category { get; private set; }
        internal bool Foundation { get; private set; }
        internal readonly Hediff_NeiyuCultivation State;
        internal List<CultivationEffectRow> FoundationEffects { get; private set; } = new();
        internal string Result { get; private set; }
        private Map inventoryMap;
        private float nextInventoryScan;
        private bool inventoryInvalidated;
        private int revision = -1, powerRevision = -1, trialRevision = -1, conditionTick = -1;
        private string lastEligibility;
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
                byMaterial.Add(def, new InventoryScan(def));
            Selected = Nodes.FirstOrDefault(n => n.effect == CultivationEffect.SwordDamage) ?? Nodes.FirstOrDefault();
            inventoryMap = pawn?.Map;
            RefreshStates(inventoryChanged: true);
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
        internal int? Available(ThingDef def) => byMaterial.TryGetValue(def, out var row) ? row.Count : null;
        internal string InventoryText(int? count) => count?.ToString() ?? (inventoryMap == null ? "—" : InventoryPendingText);

        private void InvalidateInventory()
        {
            pendingInventory.Clear();
            foreach (var row in byMaterial.Values) row.Count = null;
            nextInventoryScan = 0f;
            inventoryInvalidated = true;
        }

        internal void Update()
        {
            if (State == null) return;
            Map map = Pawn.Map;
            if (map != inventoryMap)
            {
                inventoryMap = map;
                InvalidateInventory();
            }

            bool inventoryChanged = UpdateInventory();
            RefreshStates(inventoryChanged || inventoryInvalidated);
            inventoryInvalidated = false;
        }

        private bool UpdateInventory()
        {
            if (inventoryMap == null || byMaterial.Count == 0) return false;
            if (pendingInventory.Count == 0)
            {
                if (Time.realtimeSinceStartup < nextInventoryScan) return false;
                // Keep the last completed counts visible while the next pass is in progress.
                foreach (var row in byMaterial.Values)
                {
                    row.Index = row.Accumulated = 0;
                    pendingInventory.Enqueue(row);
                }
            }

            bool changed = false;
            int checkedStacks = 0;
            long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 500;
            do
            {
                var row = pendingInventory.Dequeue();
                var things = inventoryMap.listerThings.ThingsOfDef(row.Def);
                if (row.Index < things.Count)
                {
                    var thing = things[row.Index++];
                    checkedStacks++;
                    if (CultivationService.UsableMaterial(Pawn, thing)) row.Accumulated += thing.stackCount;
                }
                if (row.Index < things.Count)
                {
                    pendingInventory.Enqueue(row);
                }
                else if (row.Count != row.Accumulated)
                {
                    row.Count = row.Accumulated;
                    changed = true;
                }
            }
            while (pendingInventory.Count > 0 && checkedStacks < InventoryStacksPerFrame && Stopwatch.GetTimestamp() < deadline);

            if (pendingInventory.Count == 0) nextInventoryScan = Time.realtimeSinceStartup + InventoryRefreshInterval;
            return changed;
        }

        private void RefreshStates(bool inventoryChanged)
        {
            if (State == null) return;
            int treeRevision = State.Tree.Revision;
            int currentPowerRevision = NeiyuPowerBalance.Revision;
            int progressRevision = State.ProgressRevision;
            int tick = Find.TickManager?.TicksGame ?? 0;
            string eligibility = CultivationService.Eligibility(Pawn);
            bool effectsChanged = revision != treeRevision || powerRevision != currentPowerRevision;
            if (!effectsChanged && !inventoryChanged && trialRevision == progressRevision
                && conditionTick == tick && lastEligibility == eligibility) return;

            revision = treeRevision;
            powerRevision = currentPowerRevision;
            trialRevision = progressRevision;
            conditionTick = tick;
            lastEligibility = eligibility;
            if (effectsChanged) RefreshFoundationEffects();

            foreach (var node in Nodes)
            {
                int rank = State.Tree.GetNodeLevel(node);
                bool eligible = State.Events.CanUnlock(node, out string reason, checkCosts: false);
                MaterialStatus materials = eligible ? CheckMaterials(node, rank) : MaterialStatus.Ready;
                bool can = eligible && materials == MaterialStatus.Ready;
                string status = rank >= node.maxLevel ? "已满级" : can ? rank > 0 ? "可升级" : "可学习" : "条件未满足";
                if (!eligible && rank < node.maxLevel && node.IsExtension)
                    status = node.trial != CultivationTrial.None && State.Progress(node.trial) < node.trialTarget ? "待试炼"
                        : node.prerequisites?.Any(p => State.Tree.GetNodeLevel(p.node) < p.minLevel) == true ? "前置不足" : "暂不可学习";
                if (materials == MaterialStatus.Pending) { status = InventoryPendingText; reason = "正在统计可用材料。"; }
                if (materials == MaterialStatus.Missing) { status = "材料不足"; reason = "所需材料尚未备齐。"; }
                if (NeiyuPowerBalance.PassivesDisabled) { status = "已封印"; reason = "已学等级保留，节点效果暂停。"; }
                var visual = NeiyuPowerBalance.PassivesDisabled ? Common.UI.PerkNodeVisual.Locked
                    : rank >= node.maxLevel ? Common.UI.PerkNodeVisual.Mastered : rank > 0 ? Common.UI.PerkNodeVisual.Learned
                    : can ? Common.UI.PerkNodeVisual.Available : Common.UI.PerkNodeVisual.Locked;
                States[node] = new(rank, can, status, reason, visual);
            }
        }

        private MaterialStatus CheckMaterials(NeiyuCultivationNodeDef node, int rank)
        {
            var costs = node.CostsAt(rank);
            if (costs.NullOrEmpty()) return MaterialStatus.Ready;

            MaterialStatus status = MaterialStatus.Ready;
            foreach (var cost in costs)
            {
                int? count = Available(cost.thingDef);
                if (!count.HasValue) status = MaterialStatus.Pending;
                else if (count.Value < cost.count) return MaterialStatus.Missing;
            }
            return status;
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
                Result = message;
                InvalidateInventory();
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
