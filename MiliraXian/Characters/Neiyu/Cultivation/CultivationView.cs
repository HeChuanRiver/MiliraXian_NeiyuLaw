using System;
using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.Common.PerkSystem;
using MiliraXian.Characters.Common.UI;
using UnityEngine;
using Verse;
using static MiliraXian.Characters.Neiyu.Cultivation.CultivationArt;

namespace MiliraXian.Characters.Neiyu.Cultivation;

internal sealed class CultivationView
{
    private CharacterPerkCanvas<NeiyuCultivationNodeDef> canvas;
    private Vector2 detailsScroll, lastSize;
    private bool fitted, detailOnly;
    private string search = "", lastSelected;
    private float contentHeight = 600;
    private static string CompleteContents => "MX_Cultivation_CompleteContents".Translate().ToString();

    internal bool Draw(Rect rect, CultivationViewModel model)
    {
        Color color = GUI.color; GameFont font = Text.Font; TextAnchor anchor = Text.Anchor; bool wrap = Text.WordWrap;
        try
        {
            GUI.color = Color.white; Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
            if (model.State == null) { Theme.Label(rect.ContractedBy(30), "修行状态尚未就绪。", Ink); return false; }
            canvas ??= new(model.Nodes, n => model.Positions[n],
                n => new PerkCanvasState(model.States[n].Rank, model.State.Tree.EffectiveNodeLevel(n) > 0,
                    n.LabelCap + "  " + model.States[n].Rank + "/" + n.maxLevel + " · " + model.States[n].Status + "\n" + n.EffectText(Math.Max(1, model.States[n].Rank))),
                (r, n, chosen, dim) => Theme.Node(r, Icon(n), model.States[n].Visual, chosen, dim), Ink, Muted, Rule, Accent, horizontal: true);
            if (lastSelected != model.Selected?.defName)
            { lastSelected = model.Selected?.defName; detailsScroll = Vector2.zero; canvas.Select(model.Selected); }
            GUI.DrawTexture(rect, TreeBackdrop, ScaleMode.ScaleAndCrop);
            Widgets.DrawBoxSolid(rect.ContractedBy(4), new Color(.04f, .08f, .14f, .36f));
            if (Header(new Rect(rect.x, rect.y, rect.width, 76), model)) return true;
            Rect body = new(rect.x + 18, rect.y + 86, rect.width - 36, rect.height - 104);
            bool narrow = body.width < 740;
            float side = body.width < 1060 ? 0 : 160;
            float detail = Mathf.Clamp(body.width * .26f, 280, 350);
            if (side > 0) Sidebar(new Rect(body.x, body.y, side, body.height), model);
            Rect graph = new(body.x + side, body.y, body.width - side - (narrow ? 0 : detail + 16), body.height);
            if (narrow && detailOnly)
            {
                if (Theme.Button(new Rect(body.x, body.y, 100, 30), "返回图谱", quiet: true)) detailOnly = false;
                Details(new Rect(body.x, body.y + 38, body.width, body.height - 38), model);
            }
            else
            {
                Graph(graph, model, side == 0, narrow);
                if (!narrow) Details(new Rect(body.xMax - detail, body.y, detail, body.height), model);
            }
            return false;
        }
        finally { GUI.color = color; Text.Font = font; Text.Anchor = anchor; Text.WordWrap = wrap; }
    }
    private bool Header(Rect rect, CultivationViewModel model)
    {
        Theme.Texture(new Rect(rect.x + 22, rect.y + 15, 42, 42), Emblem, Color.white);
        Theme.Label(new Rect(rect.x + 76, rect.y + 13, 220, 32), "羽律之书", Ink, GameFont.Medium);
        Theme.Label(new Rect(rect.x + 78, rect.y + 46, 220, 22), model.Pawn.LabelShortCap, Muted, GameFont.Tiny);
        if (rect.width > 840)
        {
            string[] names = { "Milira_Feather", "Jade", "ComponentSpacer" };
            for (int i = 0; i < names.Length; i++)
            {
                var def = DefDatabase<ThingDef>.GetNamed(names[i]);
                Theme.Label(new Rect(rect.xMax - 500 + i * 118, rect.y + 25, 116, 25), def.LabelCap + "  " + model.InventoryText(model.Available(def)), i == 0 ? Accent : Muted, GameFont.Tiny);
            }
        }
        if (Theme.Button(new Rect(rect.xMax - 127, rect.y + 22, 68, 32), "试炼", quiet: true))
            Find.WindowStack.Add(new Dialog_NeiyuCultivationTrials(model.State));
        bool close = Theme.Button(new Rect(rect.xMax - 47, rect.y + 22, 28, 32), "×", quiet: true);
        Widgets.DrawLine(new(rect.x + 20, rect.yMax), new(rect.xMax - 20, rect.yMax), Rule, .8f);
        return close;
    }
    private void Choose(CultivationViewModel model, CharacterPerkCategoryDef category, bool foundation = false)
    { model.Choose(category, foundation); search = ""; fitted = false; }
    private void FoundationMenu(CultivationViewModel model)
    {
        Find.WindowStack.Add(new FloatMenu(model.Foundations.Select(c => new FloatMenuOption(c.LabelCap, () => Choose(model, c, true))).ToList()));
    }
    private void Sidebar(Rect rect, CultivationViewModel model)
    {
        Theme.Label(new Rect(rect.x + 10, rect.y + 10, 140, 24), "修行之路", Muted, GameFont.Tiny);
        float y = rect.y + 45;
        if (Theme.Button(new Rect(rect.x, y, 144, 42), CompleteContents, !model.Foundation && model.Category == null)) Choose(model, null);
        y += 52;
        foreach (var category in model.Categories)
        {
            if (Theme.Button(new Rect(rect.x, y, 144, 42), category.LabelCap, model.Category == category, quiet: true)) Choose(model, category);
            y += 50;
        }
        y += 15;
        Widgets.DrawLine(new(rect.x + 12, y), new(rect.x + 134, y), Rule, .6f); y += 16;
        if (Theme.Button(new Rect(rect.x, y, 144, 42), model.Foundation ? "基础 · " + model.Category.LabelCap : "基础修行", model.Foundation, quiet: true)) FoundationMenu(model);
        y += 80;
        Theme.Label(new Rect(rect.x + 12, y, 132, 24), "已投入", Muted, GameFont.Tiny);
        Theme.Label(new Rect(rect.x + 12, y + 28, 132, 32), model.Investment, Ink, GameFont.Medium);
        Widgets.DrawLine(new(rect.xMax - 5, rect.y + 5), new(rect.xMax - 5, rect.yMax - 5), Rule, .5f);
    }
    private bool Matches(NeiyuCultivationNodeDef node) => string.IsNullOrWhiteSpace(search)
        || (node.label + " " + node.description).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    private void Graph(Rect rect, CultivationViewModel model, bool compact, bool narrow)
    {
        GUI.BeginGroup(rect);
        try
        {
            float toolbar = compact ? 40 : 0;
            if (compact && Theme.Button(new Rect(8, 0, 150, 32), model.Foundation ? "基础 · " + model.Category.LabelCap : model.Category?.LabelCap ?? CompleteContents, quiet: true))
            {
                List<FloatMenuOption> options = new() { new(CompleteContents, () => Choose(model, null)) };
                foreach (var c in model.Categories) options.Add(new(c.LabelCap, () => Choose(model, c)));
                foreach (var c in model.Foundations) options.Add(new("基础 · " + c.LabelCap, () => Choose(model, c, true)));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (compact) Theme.Label(new Rect(175, 6, rect.width - 190, 24), "已投入  " + model.Investment, Muted, GameFont.Tiny);
            Theme.Label(new Rect(12, toolbar + 3, 46, 25), "查找", Muted, GameFont.Tiny);
            string entered = Widgets.TextField(new Rect(58, toolbar, Mathf.Max(70, rect.width - 70), 27), search);
            if (entered != search)
            {
                search = entered;
                var match = model.Nodes.FirstOrDefault(n => model.Shown(n) && Matches(n));
                if (!string.IsNullOrWhiteSpace(search) && match != null) { model.Select(match); canvas.Focus(match); }
            }
            Rect viewport = new(0, toolbar + 40, rect.width, Mathf.Max(40, rect.height - toolbar - 103));
            canvas.Size = viewport.size;
            if (lastSize != viewport.size) { lastSize = viewport.size; fitted = false; }
            if (!fitted) { canvas.Fit(model.Nodes.Where(model.InBranch)); fitted = true; }
            GUI.BeginGroup(viewport);
            try
            {
                var clicked = canvas.Draw(model.Selected, n => !model.InBranch(n) || !Matches(n) && !canvas.SelectedPath.Contains(n), model.Shown);
                if (clicked != null) { model.Select(clicked); if (narrow) detailOnly = true; }
            }
            finally { GUI.EndGroup(); }
            string[] labels = { "未解锁", "可学习", "已领悟", "已满级" };
            float slot = Mathf.Min(92, (rect.width - 20) / 4);
            for (int i = 0; i < 4; i++)
            {
                Theme.Symbol(new Rect(10 + i * slot, rect.height - 55, 14, 14), (PerkNodeVisual)i, i < 2 ? Accent : Pink);
                Theme.Label(new Rect(32 + i * slot, rect.height - 59, slot - 23, 23), labels[i], Muted, GameFont.Tiny);
            }
            Theme.Label(new Rect(10, rect.height - 28, rect.width - 155, 23), "MX_Cultivation_PanZoomHint".Translate().ToString(), Muted, GameFont.Tiny);
            if (Theme.Button(new Rect(rect.width - 139, rect.height - 30, 32, 27), "−", quiet: true)) canvas.ZoomAt(.83f, canvas.Size * .5f);
            if (Theme.Button(new Rect(rect.width - 103, rect.height - 30, 62, 27), "适应", quiet: true)) canvas.Fit(model.Nodes.Where(model.InBranch));
            if (Theme.Button(new Rect(rect.width - 37, rect.height - 30, 29, 27), "+", quiet: true)) canvas.ZoomAt(1.2f, canvas.Size * .5f);
        }
        finally { GUI.EndGroup(); }
    }
    private void Details(Rect rect, CultivationViewModel model)
    {
        Theme.Frame(rect, Color.white, 24);
        var node = model.Selected;
        if (node == null || !model.States.TryGetValue(node, out var state)) return;
        Rect scroll = new(rect.x + 20, rect.y + 24, rect.width - 40, Mathf.Max(20, rect.height - 112));
        Rect content = new(0, 0, scroll.width - 16, Mathf.Max(scroll.height, contentHeight));
        Widgets.BeginScrollView(scroll, ref detailsScroll, content);
        try
        {
            float y = 0, width = content.width;
            Theme.TextBlock(width, ref y, (node.IsExtension ? "" : "基础修行 · ") + node.category.LabelCap, Muted, GameFont.Tiny);
            y += 5;
            Theme.Texture(new Rect(0, y, 48, 48), Icon(node), Color.white);
            float titleHeight = Theme.Height(node.LabelCap, width - 60, GameFont.Medium);
            Theme.Label(new Rect(60, y, width - 60, titleHeight), node.LabelCap, Ink, GameFont.Medium);
            Theme.Label(new Rect(60, y + titleHeight + 3, width - 60, 24), "等级  " + state.Rank + " / " + node.maxLevel, Muted, GameFont.Tiny);
            y += Mathf.Max(58, titleHeight + 33) + 14;
            Theme.Symbol(new Rect(0, y + 5, 14, 14), state.Visual, state.CanLearn ? Accent : Pink);
            Theme.Label(new Rect(23, y, width - 23, 26), state.Status, state.CanLearn ? Accent : Ink); y += 32;
            if (!string.IsNullOrEmpty(model.Result)) Theme.TextBlock(width, ref y, model.Result, Accent, GameFont.Tiny);
            else if (!state.CanLearn && state.Rank < node.maxLevel && !string.IsNullOrEmpty(state.Reason)) Theme.TextBlock(width, ref y, state.Reason, Muted, GameFont.Tiny);
            Divider(width, ref y);
            Theme.TextBlock(width, ref y, "节点效果", Muted, GameFont.Tiny);
            if (node.IsExtension)
            {
                if (node.maxLevel > 1 && state.Rank > 0 && state.Rank < node.maxLevel)
                    Theme.TextBlock(width, ref y, "当前  " + node.EffectText(state.Rank), Ink);
                Theme.TextBlock(width, ref y, (node.maxLevel > 1 && state.Rank < node.maxLevel ? "下一级  " : "")
                    + node.EffectText(Math.Min(node.maxLevel, state.Rank + 1)), Ink);
            }
            else
            {
                Theme.TextBlock(width, ref y, node.description, Ink);
                foreach (var effect in model.FoundationEffects)
                    Theme.TextBlock(width, ref y, effect.Heading || effect.Before == null ? effect.Label : effect.Label + "  " + effect.Before + " → " + effect.After,
                        effect.Heading ? Muted : Accent, GameFont.Tiny);
            }
            if (node.trial != CultivationTrial.None)
            {
                Divider(width, ref y);
                Theme.TextBlock(width, ref y, CultivationTrials.Label(node.trial) + "  " + model.State.Progress(node.trial) + " / " + node.trialTarget, Muted, GameFont.Tiny);
            }
            if (state.Rank < node.maxLevel && node.CostsAt(state.Rank) is { Count: > 0 } costs)
            {
                Divider(width, ref y);
                foreach (var cost in costs)
                {
                    int? count = model.Available(cost.thingDef);
                    Color color = count.HasValue ? count.Value >= cost.count ? Accent : Pink : Muted;
                    Theme.TextBlock(width, ref y, cost.thingDef.LabelCap + "  " + model.InventoryText(count) + " / " + cost.count,
                        color, GameFont.Tiny);
                }
            }
            if (!node.story.NullOrEmpty())
            {
                Divider(width, ref y);
                Theme.TextBlock(width, ref y, node.story, Muted, GameFont.Tiny);
            }
            contentHeight = y + 12;
        }
        finally { Widgets.EndScrollView(); }
        Rect button = new(rect.x + 22, rect.yMax - 64, rect.width - 44, 42);
        if (Theme.Button(button, state.Rank >= node.maxLevel ? "已掌握" : state.Rank > 0 ? "升级节点" : "领悟节点", state.CanLearn, state.CanLearn)) model.ConfirmLearn();
        if (!state.CanLearn && !string.IsNullOrEmpty(state.Reason)) TooltipHandler.TipRegion(button, state.Reason);
    }
    private static void Divider(float width, ref float y)
    { y += 8; Widgets.DrawLine(new(0, y), new(width, y), Rule, .6f); y += 18; }
}
