using System;
using System.Collections.Generic;
using System.Diagnostics;
using MiliraXian.Characters.Biography;
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
            public string Text;
            public string Quantity;
        }

        public readonly Pawn Pawn;
        public readonly List<NeiyuCultivationNodeDef> Nodes;
        public readonly List<CostRow> Costs = new();
        public readonly int[] Ranks = new int[4];
        public NeiyuCultivationNodeDef Selected { get; private set; }
        public CultivationBranch Branch { get; private set; }
        public List<CultivationEffectRow> Effects { get; private set; }
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
            Nodes = new List<NeiyuCultivationNodeDef>(DefDatabase<NeiyuCultivationNodeDef>.AllDefsListForReading);
            Nodes.Sort((a, b) => a.branch == b.branch ? a.rank.CompareTo(b.rank) : a.branch.CompareTo(b.branch));
            SelectBranch(CultivationBranch.Wing);
        }

        public void SelectBranch(CultivationBranch branch)
        {
            Branch = branch;
            NeiyuCultivationNodeDef first = null;
            int rank = CultivationService.Rank(Pawn, branch);
            foreach (var node in Nodes)
                if (node.branch == branch)
                {
                    first ??= node;
                    if (node.rank == rank + 1) { first = node; break; }
                }
            Select(first);
        }

        public void Select(NeiyuCultivationNodeDef node)
        {
            Selected = node;
            Result = null;
            Refresh();
        }

        public void Update()
        {
            // IMGUI runs Layout/Repaint/input events for the same frame. Material work
            // is budgeted across frames and never repeated for each GUI event.
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            int revision = BiographyFrameworkUtility.GetTracker(Pawn)?.Revision ?? 0;
            if (revision != observedRevision || observedPowerRevision != NeiyuPowerBalance.Revision
                || (Ready && Time.realtimeSinceStartup >= nextRefresh)) Refresh();
            int budget = 12;
            long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 500; // 2 ms between candidate checks.
            foreach (CostRow row in Costs)
            {
                if (row.Complete || Pawn?.Map == null) { row.Complete = true; continue; }
                List<Thing> things = Pawn.Map.listerThings.ThingsOfDef(row.Cost.thingDef);
                while (budget > 0 && (budget == 12 || Stopwatch.GetTimestamp() < deadline)
                    && row.Index < things.Count && row.Available < row.Cost.count)
                {
                    Thing thing = things[row.Index++];
                    budget--;
                    if (CultivationService.UsableMaterial(Pawn, thing)) row.Available += thing.stackCount;
                }
                row.Complete = row.Index >= things.Count || row.Available >= row.Cost.count;
                row.Quantity = (row.Available >= row.Cost.count ? "≥ " : "")
                    + Math.Min(row.Available, row.Cost.count) + " / " + row.Cost.count;
                row.Text = row.Cost.thingDef.LabelCap + "   " + row.Quantity;
                if (budget == 0 || Stopwatch.GetTimestamp() >= deadline) break;
            }
            Ready = true;
            foreach (CostRow row in Costs) if (!row.Complete) Ready = false;
            if (Ready && DisabledReason == null)
                foreach (CostRow row in Costs)
                    if (row.Available < row.Cost.count) { DisabledReason = CultivationText.Get("MaterialsMissing", "可用材料不足。"); break; }
        }

        private void Refresh()
        {
            observedRevision = BiographyFrameworkUtility.GetTracker(Pawn)?.Revision ?? 0;
            observedPowerRevision = NeiyuPowerBalance.Revision;
            nextRefresh = Time.realtimeSinceStartup + 1f;
            int total = 0;
            for (int i = 0; i < Ranks.Length; i++) { Ranks[i] = CultivationService.Rank(Pawn, (CultivationBranch)i); total += Ranks[i]; }
            Progress = CultivationText.Get("Progress", "已领悟") + "   " + total + " / " + Nodes.Count;
            DisabledReason = CultivationService.Check(Pawn, Selected);
            Effects = CultivationEffectText.Build(Selected, Ranks[(int)Branch]);
            ConditionText = Selected?.condition?.GetProgressText(Pawn, BiographyFrameworkUtility.GetTracker(Pawn))
                ?? CultivationText.Get("NoCondition", "无额外经历要求。");
            Costs.Clear();
            if (Selected != null)
                foreach (var cost in Selected.costs)
                    Costs.Add(new CostRow { Cost = cost, Text = cost.thingDef.LabelCap + "   … / " + cost.count, Quantity = "… / " + cost.count });
            Ready = Costs.Count == 0;
        }

        public void Learn()
        {
            bool success = CultivationService.TryLearn(Pawn, Selected, out string message);
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
        // Keep labels and values separate; layout must never parse translated prose.
        public static List<CultivationEffectRow> Build(NeiyuCultivationNodeDef node, int currentRank)
        {
            var text = new List<CultivationEffectRow>();
            if (node == null) return text;
            int targetRank = Math.Max(currentRank, node.rank);
            float before = CultivationService.Fraction(currentRank), after = CultivationService.Fraction(targetRank);
            if (node.branch == CultivationBranch.Wing)
            {
                Weapon(text, "MX_Neiyu_Form_Flower", 3f, .045f, before, after);
                Weapon(text, "MX_Neiyu_Form_Weapon", 10f, .12f, before, after);
                Projectile(text, "MX_Bullet_BigSplitArrow", 12f, .18f, before, after);
                Projectile(text, "MX_Bullet_HomingShard", 6f, .09f, before, after);
                Projectile(text, "MX_Bullet_BarrageArrow", 6f, .09f, before, after);
                AppendGear(text, true, before, after);
            }
            else if (node.branch == CultivationBranch.Arrow)
            {
                int splits = DefDatabase<ThingDef>.GetNamedSilentFail("MX_Bullet_BigSplitArrow")?.GetModExtension<SplitArrowExtension>()?.splitCount ?? 16;
                var barrage = DefDatabase<AbilityDef>.GetNamedSilentFail("MX_Neiyu_Bow_ArrowBarrage");
                int shots = 108;
                if (barrage?.comps != null) foreach (var comp in barrage.comps) if (comp is CompProperties_AbilityNeiyuArrowBarrage p) shots = p.shotCount;
                Line(text, CultivationText.Get("SplitCount", "普攻分裂箭数"), CultivationPower.ArrowCount(currentRank, splits, 1, 4, 8), CultivationPower.ArrowCount(targetRank, splits, 1, 4, 8));
                Line(text, CultivationText.Get("BarrageCount", "箭幕总箭数"), CultivationPower.ArrowCount(currentRank, shots, 4, 24, 54), CultivationPower.ArrowCount(targetRank, shots, 4, 24, 54));
            }
            else if (node.branch == CultivationBranch.Halo)
            {
                var def = DefDatabase<HediffDef>.GetNamedSilentFail("MXNL_NeiyuShield");
                HediffCompProperties_MXNeiyuCountShield props = null;
                if (def?.comps != null) foreach (var comp in def.comps) if (comp is HediffCompProperties_MXNeiyuCountShield p) props = p;
                int ceiling = props?.phase2MaxChargesNormal ?? 108;
                Line(text, CultivationText.Get("ShieldCapacity", "常态盾层上限"), CultivationPower.ShieldCapacity(currentRank, ceiling), CultivationPower.ShieldCapacity(targetRank, ceiling));
                Note(text, CultivationText.Get("ShieldOpening", "首击被完全阻挡，随后展开计数护盾。"));
                Note(text, CountShieldUtility.RuleDescription(props?.phase2Threshold ?? 36f, NeiyuPowerBalance.IsOriginal));
                Note(text, CultivationText.Get("Recovery", "盾层停止变化后的恢复时间") + "  " + ((props?.phase2RecoverTicksNoChange ?? 3600) / 60f).ToString("0.#") + " s");
                if (targetRank >= 3)
                {
                    Note(text, CultivationText.Get("WeakShieldCapacity", "虚弱时盾层上限") + "  " + (props?.phase2MaxChargesWeak ?? 24));
                    Note(text, CultivationText.Get("StageThree", "解锁三阶蓄伤与增益循环。"));
                    Note(text, CultivationText.Get("AbsorbDuration", "蓄伤时间") + "  " + ((props?.stage3AbsorbTicks ?? 7500) / 60f).ToString("0.#") + " s");
                    Note(text, CultivationText.Get("BuffDuration", "增益时间") + "  " + ((props?.stage3BuffTicks ?? 30000) / 60f).ToString("0.#") + " s");
                    Note(text, CultivationText.Get("WeakDuration", "结束后虚弱时间") + "  " + ((props?.weakDurationTicks ?? 9000) / 60f).ToString("0.#") + " s");
                }
                else Note(text, CultivationText.Get("EarlyShield", "盾层耗尽后不进入三阶。"));
            }
            else AppendGear(text, false, before, after);
            return text;
        }

        private static void Weapon(List<CultivationEffectRow> text, string name, float damage, float ap, float before, float after)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (def?.tools.NullOrEmpty() != false) return;
            text.Add(new CultivationEffectRow { Label = def.LabelCap, Heading = true });
            Line(text, "  " + CultivationText.Get("Damage", "伤害"), CultivationPower.Effective(def.tools[0].power, damage, before), CultivationPower.Effective(def.tools[0].power, damage, after));
            Line(text, "  " + CultivationText.Get("Penetration", "护甲穿透"), CultivationPower.Effective(def.tools[0].armorPenetration, ap, before) * 100, CultivationPower.Effective(def.tools[0].armorPenetration, ap, after) * 100, "%");
        }

        private static void Projectile(List<CultivationEffectRow> text, string name, float damage, float ap, float before, float after)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (def?.projectile == null) return;
            float ceiling = def.projectile.GetDamageAmount(1f, null);
            Line(text, def.LabelCap + " · " + CultivationText.Get("Damage", "伤害"), Mathf.RoundToInt(CultivationPower.Effective(ceiling, damage, before)), Mathf.RoundToInt(CultivationPower.Effective(ceiling, damage, after)));
            float penetration = CultivationPower.ProjectilePenetrationCeiling(def.projectile);
            Line(text, "  " + CultivationText.Get("Penetration", "护甲穿透"), CultivationPower.Effective(penetration, ap, before) * 100, CultivationPower.Effective(penetration, ap, after) * 100, "%");
        }

        private static void AppendGear(List<CultivationEffectRow> text, bool weapons, float before, float after)
        {
            foreach (var gear in CultivationPower.StatFloors)
            {
                if (gear.Key.IsWeapon != weapons) continue;
                text.Add(new CultivationEffectRow { Label = gear.Key.LabelCap, Heading = true });
                foreach (var stat in gear.Value)
                {
                    bool offset = CultivationPower.OffsetStats.TryGetValue(gear.Key, out var offsets) && offsets.Contains(stat.Key);
                    float ceiling = (offset ? gear.Key.equippedStatOffsets : gear.Key.statBases).GetStatOffsetFromList(stat.Key);
                    ToStringNumberSense sense = offset ? ToStringNumberSense.Offset : ToStringNumberSense.Absolute;
                    string a = CultivationPower.Effective(ceiling, stat.Value, before).ToStringByStyle(stat.Key.ToStringStyleUnfinalized, sense);
                    string b = CultivationPower.Effective(ceiling, stat.Value, after).ToStringByStyle(stat.Key.ToStringStyleUnfinalized, sense);
                    text.Add(new CultivationEffectRow { Label = stat.Key.LabelCap, Before = a, After = b });
                }
            }
        }

        private static void Note(List<CultivationEffectRow> text, string label) =>
            text.Add(new CultivationEffectRow { Label = label });

        private static void Line(List<CultivationEffectRow> text, string label, float before, float after, string suffix = "") =>
            text.Add(new CultivationEffectRow { Label = label.Trim(), Before = before.ToString("0.##") + suffix, After = after.ToString("0.##") + suffix });
    }
}
