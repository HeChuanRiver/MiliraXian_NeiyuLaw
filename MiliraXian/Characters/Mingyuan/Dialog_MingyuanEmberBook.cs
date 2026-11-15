using System;
using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.Common.PerkSystem;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

/// <summary>A native, clipped graph viewport. UI selection never writes gameplay state.</summary>
public sealed class Dialog_MingyuanEmberBook : Window
{
    private static readonly Color Background = new(.09f, .10f, .09f);
    private static readonly Color Panel = new(.12f, .13f, .12f);
    private static readonly Color Ink = new(.93f, .91f, .87f);
    private static readonly Color Muted = new(.68f, .70f, .65f);
    private static readonly Color Rule = new(.28f, .30f, .26f);
    private static readonly Color Copper = new(.82f, .67f, .47f);
    private readonly MingyuanEmberBook book;
    private readonly List<MingyuanEmberNodeDef> nodes;
    private readonly List<CharacterPerkCategoryDef> categories;
    private readonly Dictionary<MingyuanEmberNodeDef, Texture2D> icons = new();
    private readonly Dictionary<MingyuanEmberNodeDef, Rect> hits = new();
    private MingyuanEmberNodeDef selected;
    private CharacterPerkCategoryDef branch;
    private string search = "", lastSearch = "";
    private Vector2 pan, detailsScroll, sidebarScroll;
    private float zoom = .5f, detailHeight = 600f;
    private bool fitted, dragging, trials;
    private Vector2 dragStart, panStart, viewportSize;
    private int searchCursor;
    private string message;
    public override Vector2 InitialSize => new(Mathf.Min(1440f, UI.screenWidth - 24f), Mathf.Min(920f, UI.screenHeight - 24f));
    protected override float Margin => 0f;

    public Dialog_MingyuanEmberBook(MingyuanEmberBook book)
    {
        this.book = book;
        nodes = MingyuanEmberBook.Config.Nodes;
        categories = nodes.Where(n => n.effect != EmberEffect.Origin).Select(n => n.category).Distinct().ToList();
        selected = nodes.FirstOrDefault(n => n.effect == EmberEffect.CombatMove) ?? nodes.FirstOrDefault();
        foreach (var node in nodes) icons[node] = node.ResolveIcon(true, out _);
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
        doCloseX = false;
        draggable = false;
    }

    public override void DoWindowContents(Rect rect)
    {
        var oldColor = GUI.color; var oldFont = Text.Font; var oldAnchor = Text.Anchor; bool oldWrap = Text.WordWrap;
        try
        {
            Widgets.DrawBoxSolid(rect, Background);
            DrawHeader(new Rect(0, 0, rect.width, 68));
            bool compact = rect.width < 1100;
            float sidebar = compact ? 0 : 180;
            float detail = Mathf.Min(316, rect.width * .32f);
            Rect canvas = new(sidebar, 68, rect.width - sidebar - detail, rect.height - 68);
            Rect detailRect = new(rect.width - detail, 68, detail, rect.height - 68);
            if (!compact) DrawSidebar(new Rect(0, 68, sidebar, rect.height - 68));
            DrawCanvas(canvas, compact);
            DrawDetails(detailRect);
        }
        finally { GUI.color = oldColor; Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap; }
    }

    private static string T(string id, string source) => EmberText.Get(id, source);
    private static void Label(Rect rect, string text, Color color, GameFont font = GameFont.Small,
        TextAnchor anchor = TextAnchor.UpperLeft)
    {
        GUI.color = color; Text.Font = font; Text.Anchor = anchor; Text.WordWrap = true;
        Widgets.Label(rect, text); GUI.color = Color.white; Text.Anchor = TextAnchor.UpperLeft;
    }
    private static bool Button(Rect rect, string label, bool active = false, bool enabled = true)
    {
        Widgets.DrawBoxSolid(rect, active ? new Color(.25f, .24f, .19f) : Panel);
        GUI.color = active ? Copper : Rule; Widgets.DrawBox(rect); GUI.color = Color.white;
        Label(rect, label, enabled ? Ink : Muted, GameFont.Tiny, TextAnchor.MiddleCenter);
        if (enabled && Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
        return enabled && Widgets.ButtonInvisible(rect);
    }
    private Color BranchColor(CharacterPerkCategoryDef category)
    {
        int i = categories.IndexOf(category);
        return i switch { 1 => new(.60f, .73f, .68f), 2 => new(.74f, .69f, .79f), _ => Copper };
    }
    private void DrawHeader(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, Panel);
        Label(new Rect(22, 10, 235, 30), MingyuanEmberBook.Config.LabelCap, Ink, GameFont.Medium);
        Label(new Rect(24, 41, 220, 21), book.Pawn.LabelShortCap, Muted, GameFont.Tiny);
        float x = rect.width - 425;
        Label(new Rect(x, 13, 120, 40), T("Insight", "焰识") + "  " + book.Insight, Copper, GameFont.Small, TextAnchor.MiddleLeft);
        Label(new Rect(x + 120, 13, 120, 40), T("Ash", "灰烬") + "  " + book.Ash, Ink, GameFont.Small, TextAnchor.MiddleLeft);
        if (Button(new Rect(rect.width - 171, 19, 116, 30), T("Trials", "试炼与资源"), trials)) trials = !trials;
        if (Button(new Rect(rect.width - 43, 19, 28, 30), "×")) Close();
        Widgets.DrawLine(new(0, rect.yMax - 1), new(rect.xMax, rect.yMax - 1), Rule, 1);
    }
    private void DrawSidebar(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, Panel);
        Rect view = new(0, 0, rect.width - 18, Mathf.Max(rect.height - 4, 570));
        Widgets.BeginScrollView(rect, ref sidebarScroll, view);
        float y = 20;
        Label(new Rect(16, y, 140, 20), T("Routes", "成长路线"), Muted, GameFont.Tiny); y += 31;
        if (Button(new Rect(12, y, view.width - 24, 35), T("FullTree", "完整技能树"), branch == null)) { branch = null; fitted = false; } y += 42;
        foreach (var category in categories)
        {
            int count = nodes.Count(n => n.category == category && book.Tree.HasNode(n));
            int total = nodes.Count(n => n.category == category);
            if (Button(new Rect(12, y, view.width - 24, 38), category.LabelCap + "  " + count + "/" + total, branch == category))
            { branch = category; FocusBranch(); }
            y += 44;
        }
        y += 18;
        Label(new Rect(16, y, view.width - 30, 20), T("Invested", "已投入等级"), Muted, GameFont.Tiny); y += 26;
        int ranks = nodes.Where(n => n.AllowsReset).Sum(n => book.Tree.GetNodeLevel(n));
        int max = nodes.Where(n => n.AllowsReset).Sum(n => n.maxLevel);
        Label(new Rect(16, y, view.width - 30, 26), ranks + " / " + max, Ink); y += 36;
        Widgets.DrawBoxSolid(new Rect(16, y, view.width - 32, 3), Rule);
        Widgets.DrawBoxSolid(new Rect(16, y, (view.width - 32) * ranks / Mathf.Max(1, max), 3), Copper); y += 23;
        foreach (var category in categories)
        {
            Label(new Rect(16, y, view.width - 30, 42), category.LabelCap + "\n" + book.Spent(category) + " " + T("Insight", "焰识"), Muted, GameFont.Tiny); y += 49;
        }
        if (Button(new Rect(12, y + 12, view.width - 24, 32), T("ResetAll", "重置构筑"))) ConfirmReset(null);
        Widgets.EndScrollView();
    }

    private void DrawCanvas(Rect rect, bool compact)
    {
        GUI.BeginGroup(rect);
        try
        {
            float top = compact ? 102 : 62;
            Rect viewport = new(0, top, rect.width, Mathf.Max(60, rect.height - top - 30));
            if (viewportSize != viewport.size) { viewportSize = viewport.size; fitted = false; }
            if (!fitted) { Fit(); fitted = true; }
            Rect graph = new(viewport.x, viewport.y, viewport.width, viewport.height);
            GUI.BeginGroup(graph);
            try { DrawGraph(graph.size); }
            finally { GUI.EndGroup(); }
            Widgets.DrawBoxSolid(new Rect(0, 0, rect.width, top), Background);
            if (Button(new Rect(14, 15, 28, 30), "−")) ZoomAt(.83f, viewportSize * .5f);
            Label(new Rect(43, 15, 48, 30), Mathf.RoundToInt(zoom * 100) + "%", Muted, GameFont.Tiny, TextAnchor.MiddleCenter);
            if (Button(new Rect(92, 15, 28, 30), "+")) ZoomAt(1.2f, viewportSize * .5f);
            if (Button(new Rect(128, 15, 54, 30), T("Fit", "全图"))) { branch = null; Fit(); }
            Rect searchRect = new(191, 15, Mathf.Max(60, rect.width - 205), 30);
            GUI.SetNextControlName("EmberSearch");
            search = Widgets.TextField(searchRect, search);
            TooltipHandler.TipRegion(searchRect, T("SearchHint", "搜索节点名称或效果；回车定位下一个结果。"));
            if (search != lastSearch) { lastSearch = search; searchCursor = 0; LocateSearch(); }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
                && GUI.GetNameOfFocusedControl() == "EmberSearch") { searchCursor++; LocateSearch(); Event.current.Use(); }
            if (compact)
            {
                float width = (rect.width - 28) / 4;
                if (Button(new Rect(14, 58, width - 4, 29), T("All", "全部"), branch == null)) { branch = null; Fit(); }
                for (int i = 0; i < categories.Count; i++)
                    if (Button(new Rect(14 + (i + 1) * width, 58, width - 4, 29), categories[i].LabelCap, branch == categories[i]))
                    { branch = categories[i]; FocusBranch(); }
            }
            Label(new Rect(14, rect.height - 25, rect.width - 28, 23), T("MapHint", "拖动平移 · 滚轮缩放 · 点击节点查看"), Muted, GameFont.Tiny);
        }
        finally { GUI.EndGroup(); }
    }
    private bool Matches(MingyuanEmberNodeDef node) => search.NullOrEmpty()
        || (node.label + " " + node.description + " " + node.nodeId).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    private void LocateSearch()
    {
        if (search.Trim().Length == 0) return;
        var matches = nodes.Where(Matches).ToList();
        if (matches.Count == 0) { message = T("NoMatch", "没有匹配的节点。"); return; }
        selected = matches[searchCursor % matches.Count]; branch = null; trials = false; Focus(selected.position, .7f); message = null;
    }
    private void Fit()
    {
        zoom = Mathf.Clamp(Mathf.Min((viewportSize.x - 90) / 1600, (viewportSize.y - 70) / 1280), .20f, .8f);
        pan = viewportSize * .5f - new Vector2(950, 750) * zoom;
    }
    private void FocusBranch()
    {
        var node = selected?.category == branch ? selected : nodes.FirstOrDefault(n => n.category == branch);
        if (node == null) return;
        selected = node;
        // A branch is a reading view; keep the labels spaced and pan to reach its later nodes.
        float centerX = nodes.Where(n => n.category == branch).Average(n => n.position.x);
        Focus(new Vector2(centerX, Mathf.Clamp(node.position.y, 560, 1120)), .72f);
    }
    private void Focus(Vector2 point, float scale) { zoom = Mathf.Clamp(scale, .25f, 1.6f); pan = viewportSize * .5f - point * zoom; }
    private void ZoomAt(float factor, Vector2 point)
    {
        float previous = zoom; zoom = Mathf.Clamp(zoom * factor, .20f, 1.6f);
        pan = point - (point - pan) * (zoom / previous);
    }
    private void DrawGraph(Vector2 size)
    {
        Event e = Event.current;
        Rect viewport = new(0, 0, size.x, size.y);
        bool showLabels = zoom >= .35f;
        float iconSize = zoom < .45f ? 22 : 34;
        hits.Clear();
        foreach (var node in nodes)
        {
            Vector2 center = pan + node.position * zoom;
            float width = showLabels ? 88 : iconSize + 8;
            hits[node] = new Rect(center.x - width * .5f, center.y - iconSize * .5f,
                width, showLabels ? iconSize + 37 : iconSize + 4);
        }
        foreach (var node in nodes)
        {
            if (node.prerequisites == null) continue;
            bool dim = (branch != null && node.category != branch) || !Matches(node);
            foreach (var prerequisite in node.prerequisites)
            {
                if (prerequisite.node is not MingyuanEmberNodeDef parent) continue;
                Vector2 a = pan + parent.position * zoom, b = pan + node.position * zoom;
                a.y += iconSize * .5f; b.y -= iconSize * .5f;
                bool learned = book.Tree.HasNode(node) && book.Tree.HasNode(parent);
                Color color = learned ? BranchColor(node.category) : Rule;
                if (node == selected) color = Muted;
                if (dim) color *= new Color(1, 1, 1, .25f);
                Vector2 bendA = new(a.x, a.y + (b.y - a.y) * .45f), bendB = new(b.x, bendA.y);
                Widgets.DrawLine(a, bendA, color, learned ? 2 : 1);
                Widgets.DrawLine(bendA, bendB, color, learned ? 2 : 1);
                Widgets.DrawLine(bendB, b, color, learned ? 2 : 1);
            }
        }
        MingyuanEmberNodeDef hovered = null;
        foreach (var node in nodes)
        {
            Rect hit = hits[node];
            if (!hit.Overlaps(viewport)) continue;
            bool dim = (branch != null && node.category != branch && node.effect != EmberEffect.Origin) || !Matches(node);
            int rank = book.Tree.GetNodeLevel(node);
            bool available = book.Handler.CanUnlock(node, out _);
            Color accent = BranchColor(node.category);
            if (dim) accent *= new Color(1, 1, 1, .3f);
            Rect iconRect = new(hit.center.x - iconSize * .5f, hit.y, iconSize, iconSize);
            Widgets.DrawBoxSolid(iconRect, rank > 0 ? new Color(accent.r * .3f, accent.g * .3f, accent.b * .3f) : Panel);
            GUI.color = rank > 0 || available ? accent : Rule; Widgets.DrawBox(iconRect, node.shape >= EmberNodeShape.Keystone ? 2 : 1);
            GUI.color = dim ? new Color(.4f, .4f, .4f) : rank > 0 || available ? Color.white : Muted;
            GUI.DrawTexture(iconRect.ContractedBy(iconSize < 30 ? 2 : 4), icons[node]); GUI.color = Color.white;
            if (node == selected) { GUI.color = Ink; Widgets.DrawBox(iconRect.ExpandedBy(4)); GUI.color = Color.white; }
            string state = NodeState(node, rank, available);
            if (showLabels)
            {
                Rect caption = new(hit.center.x - 44, iconRect.yMax + 1, 88, 36);
                Widgets.DrawBoxSolid(caption, Background);
                Label(new Rect(caption.x, caption.y, caption.width, 18), node.LabelCap,
                    dim ? Rule : Ink, GameFont.Tiny, TextAnchor.MiddleCenter);
                Label(new Rect(caption.x, caption.y + 18, caption.width, 18), rank + "/" + node.maxLevel + " · " + state,
                    dim ? Rule : available ? accent : Muted, GameFont.Tiny, TextAnchor.MiddleCenter);
            }
            if (!dim && hit.Contains(e.mousePosition)) hovered = node;
            if (!dragging && !dim && Widgets.ButtonInvisible(hit)) { selected = node; trials = false; detailsScroll = Vector2.zero; message = null; }
            if (!dim) TooltipHandler.TipRegion(hit, node.LabelCap + "  " + rank + "/" + node.maxLevel + " · " + state + "\n" + node.description);
        }
        if (e.type == EventType.ScrollWheel && viewport.Contains(e.mousePosition)) { ZoomAt(e.delta.y < 0 ? 1.12f : 1 / 1.12f, e.mousePosition); e.Use(); }
        if (e.type == EventType.MouseDown && (e.button == 2 || (e.button == 0 && hovered == null)) && viewport.Contains(e.mousePosition))
        { dragging = true; dragStart = e.mousePosition; panStart = pan; GUI.FocusControl(null); e.Use(); }
        if (dragging && e.type == EventType.MouseDrag) { pan = panStart + e.mousePosition - dragStart; e.Use(); }
        if (e.rawType == EventType.MouseUp || e.type == EventType.Ignore) dragging = false;
    }

    private string NodeState(MingyuanEmberNodeDef node, int rank, bool available)
    {
        if (MingyuanPowerBalance.Sealed) return T("SealedState", "已封印");
        if (rank >= node.maxLevel) return T("Full", "满级");
        if (available) return rank > 0 ? T("Upgradeable", "可升级") : T("Learnable", "可学习");
        if (!book.TrialComplete(node.trial)) return T("TrialPending", "待试炼");
        if (book.Handler.CanUnlock(node, out _, checkCosts: false)) return T("ResourcesMissing", "资源不足");
        return rank > 0 ? T("Learned", "已学") : T("Locked", "未解锁");
    }

    private void DrawDetails(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, Panel);
        Widgets.DrawLine(new(rect.x, rect.y), new(rect.x, rect.yMax), Rule, 1);
        float footer = trials ? 54 : 132;
        Rect scroll = new(rect.x + 16, rect.y + 18, rect.width - 30, Mathf.Max(40, rect.height - footer - 28));
        Rect view = new(0, 0, scroll.width - 18, detailHeight);
        Widgets.BeginScrollView(scroll, ref detailsScroll, view);
        float y = 0;
        if (trials) DrawTrials(view.width, ref y);
        else if (selected != null)
        {
            var node = selected;
            TextBlock(view.width, ref y, node.category.LabelCap + " / " + node.role, Muted, GameFont.Tiny);
            y += 12; TextBlock(view.width, ref y, node.LabelCap, Ink, GameFont.Medium);
            int rank = book.Tree.GetNodeLevel(node);
            y += 10; TextBlock(view.width, ref y, T("Level", "已学等级") + "  " + rank + " / " + node.maxLevel, Copper);
            TextBlock(view.width, ref y, NodeState(node, rank, book.Handler.CanUnlock(node, out _)), Muted, GameFont.Tiny);
            if (MingyuanPowerBalance.Sealed) TextBlock(view.width, ref y, T("Sealed", "力量已封印，已学进度仍会保留。"), Muted);
            y += 18; Section(view.width, ref y, T("Effect", "节点效果"), node.description);
            if (node.maxLevel > 1 && node.value != 0)
                TextBlock(view.width, ref y, T("ValuePreview", "当前 → 下一级") + "  " + ValueLabel(node.value * rank, node)
                    + " → " + ValueLabel(node.value * Math.Min(node.maxLevel, rank + 1), node), Copper, GameFont.Tiny);
            y += 18; Section(view.width, ref y, T("Tradeoff", "代价与限制"), node.tradeoff);
            y += 18; TextBlock(view.width, ref y, T("Requirements", "解锁条件"), Muted, GameFont.Tiny);
            if (!node.prerequisites.NullOrEmpty())
            {
                TextBlock(view.width, ref y, node.anyPrerequisite ? T("AnyParent", "学会任一前置：") : T("AllParents", "学会全部前置："), Muted, GameFont.Tiny);
                foreach (var p in node.prerequisites)
                {
                    if (Button(new Rect(0, y, view.width, 28), (book.Tree.HasNode(p.node) ? "✓ " : "· ") + p.node.LabelCap))
                    { selected = p.node as MingyuanEmberNodeDef; Focus(selected.position, .7f); detailsScroll = Vector2.zero; }
                    y += 34;
                }
            }
            if (node.branchInvestment > 0) TextBlock(view.width, ref y, T("BranchSpent", "本路线投入") + "  " + book.Spent(node.category) + " / " + node.branchInvestment + " " + T("Insight", "焰识"), Muted, GameFont.Tiny);
            if (node.trial != EmberTrial.None)
            {
                TextBlock(view.width, ref y, book.TrialDescription(node.trial), Muted, GameFont.Tiny);
                TextBlock(view.width, ref y, book.TrialProgress(node.trial).ToString("0") + " / " + book.TrialTarget(node.trial).ToString("0"), Copper);
            }
            if (rank > 0 && node.AllowsReset)
            { y += 20; if (Button(new Rect(0, y, view.width, 30), T("RefundNode", "返还此节点…"))) ConfirmReset(node); y += 36; }
        }
        detailHeight = y + 16;
        Widgets.EndScrollView();
        Rect bottom = new(rect.x + 18, rect.yMax - footer + 8, rect.width - 36, footer - 16);
        if (trials)
        { if (Button(new Rect(bottom.x, bottom.y, bottom.width, 32), T("ResetAll", "重置构筑"))) ConfirmReset(null); return; }
        if (selected == null) return;
        int current = book.Tree.GetNodeLevel(selected);
        bool can = book.Handler.CanUnlock(selected, out string reason);
        if (!can && book.Handler.CanUnlock(selected, out _, checkCosts: false)) reason = T("ResourcesMissing", "资源不足");
        Label(new Rect(bottom.x, bottom.y, bottom.width, 22), current >= selected.maxLevel ? T("Full", "满级")
            : selected.InsightCost(current) + " " + T("Insight", "焰识") + "  /  " + selected.AshCost(current) + " " + T("Ash", "灰烬"), Copper, GameFont.Tiny);
        if (Button(new Rect(bottom.x, bottom.y + 28, bottom.width, 35), current >= selected.maxLevel ? T("Mastered", "已掌握")
            : T("LearnNext", "学习下一等级") + "  " + (current + 1) + "/" + selected.maxLevel, can, can)) ConfirmPurchase(selected);
        Label(new Rect(bottom.x, bottom.y + 70, bottom.width, 44), message ?? (can ? T("ConfirmCost", "确认后扣除资源。") : reason), Muted, GameFont.Tiny);
    }
    private static string ValueLabel(float value, MingyuanEmberNodeDef node) =>
        node.effect == EmberEffect.DecayDelay ? value.ToString("0.#") + " s" : (value * 100).ToString("0.#") + "%";
    private static void TextBlock(float width, ref float y, string text, Color color, GameFont font = GameFont.Small)
    {
        Text.Font = font; Text.WordWrap = true;
        float height = Mathf.Max(22, Text.CalcHeight(text ?? "", width));
        Label(new Rect(0, y, width, height), text ?? "", color, font); y += height + 5;
    }
    private static void Section(float width, ref float y, string title, string body)
    { TextBlock(width, ref y, title, Muted, GameFont.Tiny); TextBlock(width, ref y, body, Ink); }
    private void DrawTrials(float width, ref float y)
    {
        TextBlock(width, ref y, T("Trials", "试炼与资源"), Ink, GameFont.Medium); y += 12;
        var c = MingyuanEmberBook.Config;
        TextBlock(width, ref y, T("ResourceRules", "实战施法每 {0} 次、生命燃烧实际伤害每 {1} 点、护焰战斗修复每 {2} 点，各获得 1 灰烬。\n每 {3} 点战斗经历获得 1 焰识。完成每项试炼额外获得 {4} 焰识、{5} 灰烬。")
            .Formatted(c.castsPerAsh, c.burnDamagePerAsh, c.shieldRepairPerAsh, c.experiencePerInsight, c.trialInsightReward, c.trialAshReward), Muted, GameFont.Tiny);
        TextBlock(width, ref y, T("ExperienceRules", "每次计入奖励的施法、每次由伤害或修复获得灰烬，以及每名带有生命燃烧的敌人死亡，均增加 1 点战斗经历。"), Muted, GameFont.Tiny);
        TextBlock(width, ref y, T("CastRewardInterval", "施法奖励每 {0} 秒最多计入一次，试炼次数照常累计。")
            .Formatted((c.resourceEventCooldownTicks / 60f).ToString("0.#")), Muted, GameFont.Tiny);
        TextBlock(width, ref y, T("Experience", "累计战斗经历") + "  " + book.Experience, Copper);
        foreach (EmberTrial trial in Enum.GetValues<EmberTrial>())
        {
            if (trial == EmberTrial.None) continue;
            y += 16; TextBlock(width, ref y, book.TrialDescription(trial), Ink);
            TextBlock(width, ref y, book.TrialProgress(trial).ToString("0") + " / " + book.TrialTarget(trial).ToString("0")
                + (book.TrialComplete(trial) ? "  ✓" : ""), Copper);
        }
    }
    private void ConfirmPurchase(MingyuanEmberNodeDef node)
    {
        int rank = book.Tree.GetNodeLevel(node);
        string text = node.LabelCap + "  " + rank + " → " + (rank + 1) + "\n\n"
            + node.InsightCost(rank) + " " + T("Insight", "焰识") + " / " + node.AshCost(rank) + " " + T("Ash", "灰烬");
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () =>
        {
            if (book.Tree.GetNodeLevel(node) != rank) { message = T("Changed", "节点状态已变化，请重新确认。"); return; }
            if (!book.Handler.TryUnlock(node, out message)) return;
            message = T("Learned", "已学");
        }));
    }
    private void ConfirmReset(MingyuanEmberNodeDef node)
    {
        var plan = book.Handler.ResetPlan(node);
        if (plan.Count == 0) { message = T("NoRefund", "没有可以返还的节点。"); return; }
        var refund = book.RefundAmount(plan);
        string text = T("ResetConfirm", "返还以下节点及失去前置或投入门槛的后续节点：") + "\n\n"
            + string.Join("、", plan.Select(n => n.LabelCap.ToString())) + "\n\n"
            + refund.insight + " " + T("Insight", "焰识") + " / " + refund.ash + " " + T("Ash", "灰烬");
        int revision = book.Tree.Revision;
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () =>
        {
            if (book.Tree.Revision != revision) { message = T("Changed", "节点状态已变化，请重新确认。"); return; }
            book.Handler.TryReset(node, out message);
        }));
    }
}
