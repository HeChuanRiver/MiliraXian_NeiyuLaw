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
        internal sealed class CostRow
        {
            public ThingDefCountClass Cost;
            public int Available, Index;
            public bool Complete;
            public string Text, Quantity;
        }
        public readonly Pawn Pawn;
        public readonly List<NeiyuCultivationNodeDef> Nodes;
        public readonly List<CharacterPerkCategoryDef> Categories;
        public readonly List<NeiyuCultivationNodeDef> VisibleNodes = new();
        public readonly List<CostRow> Costs = new();
        public NeiyuCultivationNodeDef Selected { get; private set; }
        public CharacterPerkCategoryDef Category { get; private set; }
        public List<CultivationEffectRow> Effects { get; private set; } = new();
        public string ConditionText { get; private set; }
        public string DisabledReason { get; private set; }
        public string Result { get; private set; }
        public string Progress { get; private set; }
        public bool Ready { get; private set; }
        private float nextRefresh;
        private int observedRevision = -1, observedPowerRevision = -1, lastFrame = -1;

        public CultivationViewModel(Pawn pawn)
        {
            Pawn = pawn;
            var tree = CultivationService.State(pawn)?.Tree;
            Nodes = CultivationService.Nodes.Where(n => tree?.IsRelevantNode(n) == true).OrderBy(n => n.displayOrder).ThenBy(n => n.rank).ToList();
            Categories = tree?.Props.categories?.OrderBy(c => c.displayOrder).ToList() ?? new();
            SelectCategory(Categories.FirstOrDefault());
        }
        public bool Learned(NeiyuCultivationNodeDef node) => CultivationService.Learned(Pawn, node);
        public int Rank(NeiyuCultivationNodeDef node) => CultivationService.Rank(Pawn, node.category, node.ability);
        public string CategoryProgress(CharacterPerkCategoryDef category)
        {
            var nodes = Nodes.Where(n => n.category == category).ToList();
            if (nodes.Count == 0) return "0 / 0";
            if (nodes[0].ability != null)
                return nodes.Where(n => n.rank == 1 && Learned(n)).Count() + " / " + nodes.Count(n => n.rank == 1);
            return CultivationService.Rank(Pawn, category) + " / " + nodes.Max(n => n.rank);
        }
        public void SelectCategory(CharacterPerkCategoryDef category)
        {
            Category = category;
            RebuildCards();
            Select(VisibleNodes.FirstOrDefault(n => CultivationService.Check(Pawn, n) == null)
                ?? VisibleNodes.FirstOrDefault(n => !Learned(n)) ?? VisibleNodes.FirstOrDefault());
        }
        private void RebuildCards()
        {
            VisibleNodes.Clear();
            foreach (var group in Nodes.Where(n => n.category == Category).GroupBy(n => n.ability))
            {
                var ordered = group.OrderBy(n => n.rank).ToList();
                if (group.Key == null) VisibleNodes.AddRange(ordered);
                else VisibleNodes.Add(ordered.FirstOrDefault(n => !Learned(n)) ?? ordered[^1]);
            }
        }
        public void Select(NeiyuCultivationNodeDef node)
        {
            Selected = node;
            Result = null;
            Refresh();
        }
        public void Update()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            int revision = CultivationService.State(Pawn)?.Tree.Revision ?? 0;
            if (revision != observedRevision || observedPowerRevision != NeiyuPowerBalance.Revision
                || (Ready && Time.realtimeSinceStartup >= nextRefresh)) Refresh();
            int budget = 12;
            long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 500;
            foreach (CostRow row in Costs)
            {
                if (row.Complete || Pawn.Map == null) { row.Complete = true; continue; }
                var things = Pawn.Map.listerThings.ThingsOfDef(row.Cost.thingDef);
                while (budget > 0 && (budget == 12 || Stopwatch.GetTimestamp() < deadline)
                    && row.Index < things.Count && row.Available < row.Cost.count)
                {
                    Thing thing = things[row.Index++];
                    budget--;
                    if (CultivationService.UsableMaterial(Pawn, thing)) row.Available += thing.stackCount;
                }
                row.Complete = row.Index >= things.Count || row.Available >= row.Cost.count;
                row.Quantity = (row.Available >= row.Cost.count ? "≥ " : "") + Math.Min(row.Available, row.Cost.count) + " / " + row.Cost.count;
                row.Text = row.Cost.thingDef.LabelCap + "   " + row.Quantity;
                if (budget == 0 || Stopwatch.GetTimestamp() >= deadline) break;
            }
            Ready = Costs.All(r => r.Complete);
            if (Ready && DisabledReason == null && Costs.Any(r => r.Available < r.Cost.count)) DisabledReason = "可用材料不足。";
        }
        private void Refresh()
        {
            observedRevision = CultivationService.State(Pawn)?.Tree.Revision ?? 0;
            observedPowerRevision = NeiyuPowerBalance.Revision;
            nextRefresh = Time.realtimeSinceStartup + 1f;
            RebuildCards();
            Progress = "已领悟 " + Nodes.Count(n => !n.initiallyLearned && Learned(n)) + " / " + Nodes.Count(n => !n.initiallyLearned);
            DisabledReason = CultivationService.Check(Pawn, Selected);
            Effects = CultivationEffectText.Build(Pawn, Selected);
            ConditionText = Selected?.requiresAll.NullOrEmpty() != false ? "无额外经历要求。"
                : string.Join("\n", Selected.requiresAll.Select(c => c.GetProgressText(Pawn, null)));
            Costs.Clear();
            if (Selected?.costs != null)
                foreach (var cost in Selected.costs)
                    Costs.Add(new CostRow { Cost = cost, Text = cost.thingDef.LabelCap + "   … / " + cost.count, Quantity = "… / " + cost.count });
            Ready = Costs.Count == 0;
        }
        public void Learn()
        {
            bool success = CultivationService.TryLearn(Pawn, Selected, out string message);
            if (success)
                Selected = Nodes.Where(n => n.category == Selected.category && n.ability == Selected.ability)
                    .OrderBy(n => n.rank).FirstOrDefault(n => !Learned(n)) ?? Selected;
            Refresh();
            Result = message;
            if (success) SoundDefOf.Tick_High.PlayOneShotOnCamera();
            else SoundDefOf.ClickReject.PlayOneShotOnCamera();
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
