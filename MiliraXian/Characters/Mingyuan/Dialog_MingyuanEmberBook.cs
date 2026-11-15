using System;
using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.Common.PerkSystem;
using UnityEngine;
using Verse;
using static MiliraXian.Characters.Mingyuan.MingyuanEmberArt;

namespace MiliraXian.Characters.Mingyuan;

/// <summary>Presentation only: the shared perk handler remains the sole purchase/reset writer.</summary>
public sealed class Dialog_MingyuanEmberBook : Window
{
    private readonly MingyuanEmberBook book;
    private readonly List<MingyuanEmberNodeDef> nodes;
    private readonly List<CharacterPerkCategoryDef> categories;
    private readonly Dictionary<MingyuanEmberNodeDef, Rect> hits = new();
    private readonly Dictionary<MingyuanEmberNodeDef, NodeView> views = new();
    private readonly HashSet<MingyuanEmberNodeDef> selectedPath = new();
    private MingyuanEmberNodeDef selected;
    private CharacterPerkCategoryDef branch;
    private Vector2 pan, viewportSize, detailsScroll, dragStart, panStart;
    private float zoom = .5f, detailHeight = 440;
    private bool fitted, dragging, searchOpen;
    private string search = "", lastSearch = "", message;
    private int searchCursor;
    private (int tree, int seal, int insight, int ash, int tick) stateKey = (-1, -1, -1, -1, -1);
    private readonly record struct NodeView(int Rank, bool CanLearn, string State, string Reason, EmberNodeVisual Visual);

    public override Vector2 InitialSize => new(Mathf.Min(1680, UI.screenWidth - 32), Mathf.Min(1000, UI.screenHeight - 32));
    protected override float Margin => 0;

    public Dialog_MingyuanEmberBook(MingyuanEmberBook book)
    {
        this.book = book;
        nodes = MingyuanEmberBook.Config.Nodes;
        categories = nodes.Where(n => n.effect != EmberEffect.Origin).Select(n => n.category).Distinct().ToList();
        Select(nodes.FirstOrDefault(n => n.effect == EmberEffect.CombatMove) ?? nodes.FirstOrDefault());
        forcePause = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
        closeOnAccept = false;
        doCloseX = false;
        doWindowBackground = false;
        draggable = false;
    }

    public override void DoWindowContents(Rect rect)
    {
        if (book.Pawn == null || book.Pawn.Dead || book.Pawn.Destroyed) { Close(); return; }
        Vector2 fit = InitialSize;
        if (Mathf.Abs(windowRect.width - fit.x) > 1 || Mathf.Abs(windowRect.height - fit.y) > 1)
        {
            windowRect = new Rect((UI.screenWidth - fit.x) * .5f, (UI.screenHeight - fit.y) * .5f, fit.x, fit.y);
            fitted = false;
            return;
        }
        Color oldColor = GUI.color; GameFont oldFont = Text.Font; TextAnchor oldAnchor = Text.Anchor; bool oldWrap = Text.WordWrap;
        try
        {
            GUI.color = Color.white;
            RefreshStates();
            GUI.DrawTexture(rect, Backdrop, ScaleMode.StretchToFill);
            Widgets.DrawBoxSolid(new Rect(0, 0, rect.width, 73), new Color(.09f, .07f, .05f, .65f));
            DrawHeader(new Rect(0, 0, rect.width, 73));
            bool compact = rect.width < 1050 || rect.height < 660;
            float side = compact ? 0 : Mathf.Clamp(rect.width * .125f, 146, 190);
            float detail = Mathf.Clamp(rect.width * .245f, 274, 356);
            Rect body = new(10, 82, rect.width - 20, rect.height - 92);
            if (!compact) DrawSidebar(new Rect(body.x, body.y, side, body.height));
            Rect canvas = new(body.x + side, body.y, body.width - side - detail - 12, body.height);
            DrawCanvas(canvas, compact);
            DrawDetails(new Rect(body.xMax - detail, body.y, detail, body.height));
            GUI.color = Rule; Widgets.DrawBox(rect.ContractedBy(2)); GUI.color = Color.white;
        }
        finally { GUI.color = oldColor; Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap; }
    }

    private void RefreshStates()
    {
        var key = (book.Tree.Revision, MingyuanPowerBalance.Profile.Revision, book.Insight, book.Ash, Find.TickManager.TicksGame);
        if (key == stateKey) return;
        stateKey = key;
        foreach (var node in nodes)
        {
            int rank = book.Tree.GetNodeLevel(node);
            bool can = book.Handler.CanUnlock(node, out string reason);
            string state;
            if (MingyuanPowerBalance.Sealed)
            { state = T("SealedState", "已封印"); reason = T("SealedCard", "节点效果暂停，已学等级保留。"); }
            else if (rank >= node.maxLevel) { state = T("Full", "已满级"); reason = null; }
            else if (can) state = rank > 0 ? T("Upgradeable", "可升级") : T("Learnable", "可学习");
            else if (book.Handler.CanUnlock(node, out _, checkCosts: false))
            { state = T("ResourcesMissing", "资源不足"); reason = T("ResourcesMissingDetail", "焰识或灰烬不足。"); }
            else if (book.Spent(node.category) < node.branchInvestment) state = T("InvestmentMissing", "投入不足");
            else if (!book.TrialComplete(node.trial))
            {
                state = T("TrialPending", "待试炼");
                reason = book.TrialDescription(node.trial) + "  " + book.TrialProgress(node.trial).ToString("0")
                    + " / " + book.TrialTarget(node.trial).ToString("0");
            }
            else state = T("PrerequisiteMissing", "前置不足");
            EmberNodeVisual visual = MingyuanPowerBalance.Sealed ? EmberNodeVisual.Locked
                : rank >= node.maxLevel ? EmberNodeVisual.Mastered : rank > 0 ? EmberNodeVisual.Learned
                : can ? EmberNodeVisual.Available : EmberNodeVisual.Locked;
            views[node] = new NodeView(rank, can, state, reason, visual);
        }
    }

    private void DrawHeader(Rect rect)
    {
        Label(new Rect(25, 13, 230, 36), MingyuanEmberBook.Config.LabelCap, Ink, GameFont.Medium);
        Label(new Rect(27, 48, 200, 21), book.Pawn.LabelShortCap, Muted, GameFont.Tiny);
        float x = rect.xMax - 366;
        Label(new Rect(x, 21, 115, 30), T("Insight", "焰识") + "  " + book.Insight, Gold, anchor: TextAnchor.MiddleLeft);
        Label(new Rect(x + 118, 21, 112, 30), T("Ash", "灰烬") + "  " + book.Ash, Ink, anchor: TextAnchor.MiddleLeft);
        if (Button(new Rect(rect.xMax - 126, 19, 67, 34), T("TrialEntry", "试炼"), quiet: true))
            Find.WindowStack.Add(new Dialog_MingyuanEmberProgress(book));
        if (Button(new Rect(rect.xMax - 47, 19, 29, 34), "×", quiet: true)) Close();
        Widgets.DrawLine(new(20, rect.yMax), new(rect.xMax - 20, rect.yMax), Rule, 1);
    }

    private void DrawSidebar(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, new Color(.075f, .06f, .045f, .43f));
        Widgets.DrawLine(new(rect.xMax - 4, rect.y + 12), new(rect.xMax - 4, rect.yMax - 12), Rule, .6f);
        float x = rect.x + 8, width = rect.width - 24, y = rect.y + 18;
        Label(new Rect(x + 8, y, width - 16, 25), T("Routes", "成长路线"), Muted, GameFont.Tiny); y += 39;
        if (Button(new Rect(x, y, width, 42), T("FullTree", "完整技能树"), branch == null)) ChooseBranch(null);
        y += 52;
        foreach (var category in categories)
        {
            if (Button(new Rect(x, y, width, 42), category.LabelCap, branch == category, quiet: true)) ChooseBranch(category);
            y += 49;
        }
        y += 30;
        Label(new Rect(x + 9, y, width - 18, 24), T("InvestedShort", "已投入"), Muted, GameFont.Tiny); y += 27;
        Label(new Rect(x + 9, y, width - 18, 32), Investment(), Ink, GameFont.Medium); y += 44;
        int ranks = nodes.Where(n => n.AllowsReset).Sum(n => book.Tree.GetNodeLevel(n));
        int max = nodes.Where(n => n.AllowsReset).Sum(n => n.maxLevel);
        Widgets.DrawBoxSolid(new Rect(x + 9, y, width - 18, 3), Rule);
        Widgets.DrawBoxSolid(new Rect(x + 9, y, (width - 18) * ranks / Mathf.Max(1, max), 3), Gold);
        if (Button(new Rect(x, rect.yMax - 61, width, 38), T("ResetAll", "重置构筑"), quiet: true)) ResetMenu();
    }

    private string Investment() => nodes.Where(n => n.AllowsReset).Sum(n => book.Tree.GetNodeLevel(n))
        + " / " + nodes.Where(n => n.AllowsReset).Sum(n => n.maxLevel);

    private void ChooseBranch(CharacterPerkCategoryDef category)
    {
        branch = category;
        search = lastSearch = "";
        if (category != null && selected?.category != category) Select(nodes.First(n => n.category == category));
        fitted = false;
    }

    private void DrawCanvas(Rect rect, bool compact)
    {
        GUI.BeginGroup(rect);
        try
        {
            float top = compact ? 78 : 42;
            if (compact)
            {
                if (Button(new Rect(8, 0, 125, 32), branch?.LabelCap ?? T("FullTree", "完整技能树"), quiet: true))
                {
                    List<FloatMenuOption> options = new() { new(T("FullTree", "完整技能树"), () => ChooseBranch(null)) };
                    foreach (var category in categories) options.Add(new FloatMenuOption(category.LabelCap, () => ChooseBranch(category)));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                Label(new Rect(140, 5, Mathf.Max(60, rect.width - 250), 23), T("InvestedShort", "已投入") + "  " + Investment(), Muted, GameFont.Tiny);
                if (Button(new Rect(rect.width - 94, 0, 86, 32), T("ResetShort", "重置"), quiet: true)) ResetMenu();
            }
            float toolbarY = compact ? 39 : 1;
            if (searchOpen)
            {
                GUI.SetNextControlName("EmberSearch");
                Text.Font = GameFont.Small;
                search = Widgets.TextField(new Rect(12, toolbarY, Mathf.Max(50, rect.width - 86), 28), search);
                if (search != lastSearch) { lastSearch = search; searchCursor = 0; LocateSearch(); }
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
                    && GUI.GetNameOfFocusedControl() == "EmberSearch")
                { searchCursor++; LocateSearch(); Event.current.Use(); }
            }
            else Label(new Rect(14, toolbarY + 3, rect.width - 90, 25), branch?.LabelCap ?? "", Muted, GameFont.Tiny);
            if (Button(new Rect(rect.width - 66, toolbarY, 54, 28), searchOpen ? "×" : T("SearchShort", "查找"), quiet: true))
            {
                searchOpen = !searchOpen;
                if (!searchOpen) search = lastSearch = "";
                GUI.FocusControl(searchOpen ? "EmberSearch" : null);
            }
            Rect viewport = new(0, top, rect.width, Mathf.Max(80, rect.height - top - 62));
            if (viewportSize != viewport.size) { viewportSize = viewport.size; fitted = false; }
            if (!fitted) { Fit(); fitted = true; }
            GUI.BeginGroup(viewport);
            try { DrawGraph(viewport.size); }
            finally { GUI.EndGroup(); }

            float legendY = rect.height - 54;
            string[] labels = { T("Locked", "未解锁"), T("Learnable", "可学习"), T("LearnedState", "已解锁"), T("Full", "已满级") };
            float legendWidth = Mathf.Min(80, (rect.width - 24) / 4);
            for (int i = 0; i < labels.Length; i++)
            {
                float x = 12 + i * legendWidth;
                Symbol(new Rect(x, legendY + 4, 10, 10), (EmberNodeVisual)i, i == 0 ? Muted : Gold);
                Label(new Rect(x + 17, legendY, legendWidth - 18, 23), labels[i], Muted, GameFont.Tiny);
            }
            Label(new Rect(12, rect.height - 26, Mathf.Max(70, rect.width - 146), 22),
                T("MapHintShort", "拖动平移 · 滚轮缩放"), Muted, GameFont.Tiny);
            if (Button(new Rect(rect.width - 127, rect.height - 30, 29, 25), "−", quiet: true)) ZoomAt(.83f, viewportSize * .5f);
            if (Button(new Rect(rect.width - 96, rect.height - 30, 53, 25), T("FitView", "适应"), quiet: true)) Fit();
            if (Button(new Rect(rect.width - 41, rect.height - 30, 29, 25), "+", quiet: true)) ZoomAt(1.2f, viewportSize * .5f);
        }
        finally { GUI.EndGroup(); }
    }

    private void Fit()
    {
        var visible = branch == null ? nodes : nodes.Where(n => n.category == branch).ToList();
        if (visible.Count == 0) return;
        Vector2 min = new(visible.Min(n => n.position.x), visible.Min(n => n.position.y));
        Vector2 max = new(visible.Max(n => n.position.x), visible.Max(n => n.position.y));
        zoom = Mathf.Clamp(Mathf.Min((viewportSize.x - 126) / Mathf.Max(230, max.x - min.x),
            (viewportSize.y - 112) / Mathf.Max(200, max.y - min.y)), .18f, .84f);
        pan = viewportSize * .5f - (min + max) * .5f * zoom - new Vector2(0, 12);
    }
    private void ZoomAt(float factor, Vector2 point)
    {
        float previous = zoom; zoom = Mathf.Clamp(zoom * factor, .18f, 1.5f);
        pan = point - (point - pan) * (zoom / previous);
    }
    private bool Matches(MingyuanEmberNodeDef node) => search.NullOrEmpty()
        || (node.label + " " + node.description).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    private void LocateSearch()
    {
        if (search.Trim().Length == 0) return;
        var matches = nodes.Where(Matches).ToList();
        if (matches.Count == 0) { message = T("NoMatch", "没有匹配的节点。"); return; }
        Select(matches[searchCursor % matches.Count]);
        branch = null;
        zoom = .70f; pan = viewportSize * .5f - selected.position * zoom;
    }
    private void Select(MingyuanEmberNodeDef node)
    {
        selected = node; detailsScroll = Vector2.zero; message = null;
        selectedPath.Clear();
        void Visit(MingyuanEmberNodeDef current)
        {
            if (current == null || !selectedPath.Add(current) || current.prerequisites == null) return;
            foreach (var parent in current.prerequisites) Visit(parent.node as MingyuanEmberNodeDef);
        }
        Visit(node);
    }
    private bool Dim(MingyuanEmberNodeDef node) =>
        (branch != null && node.category != branch && node.effect != EmberEffect.Origin)
        || (!Matches(node) && !selectedPath.Contains(node));

    private void DrawGraph(Vector2 size)
    {
        Rect viewport = new(0, 0, size.x, size.y);
        bool captions = zoom >= .29f;
        float diameter = Mathf.Clamp(zoom * 92, 26, 62);
        float labelWidth = Mathf.Min(116, Mathf.Max(70, zoom * 220));
        hits.Clear();
        foreach (var node in nodes)
        {
            Vector2 c = pan + node.position * zoom;
            float width = captions ? labelWidth : Mathf.Max(32, diameter);
            hits[node] = new Rect(c.x - width * .5f, c.y - diameter * .5f, width, diameter + (captions ? 32 : 5));
        }
        HandleGraphInput(viewport);
        foreach (var node in nodes)
        {
            if (node.prerequisites == null) continue;
            foreach (var requirement in node.prerequisites)
            {
                if (requirement.node is not MingyuanEmberNodeDef parent) continue;
                Vector2 a = pan + parent.position * zoom, b = pan + node.position * zoom;
                a.y += diameter * .5f + (captions ? 31 : 4);
                b.y -= diameter * .5f + 5;
                bool learned = views[node].Rank > 0 && views[parent].Rank > 0 && !MingyuanPowerBalance.Sealed;
                bool path = selectedPath.Contains(node) && selectedPath.Contains(parent);
                Color color = path ? Gold : learned ? new Color(.66f, .40f, .18f) : Rule;
                if (Dim(node)) color.a = .18f;
                Vector2 bendA = new(a.x, Mathf.Lerp(a.y, b.y, .45f)), bendB = new(b.x, bendA.y);
                DrawConnection(a, bendA, bendB, b, color, path || learned);
            }
        }
        foreach (var node in nodes)
        {
            Rect hit = hits[node];
            if (!hit.Overlaps(viewport)) continue;
            NodeView view = views[node];
            bool dim = Dim(node);
            Rect icon = new(hit.center.x - diameter * .5f, hit.y, diameter, diameter);
            Node(icon, Icon(node), view.Visual, node == selected, dim);
            if (captions)
            {
                Rect caption = new(hit.x, icon.yMax + 6, hit.width, 20);
                Label(caption, node.LabelCap, dim ? Rule : view.Visual == EmberNodeVisual.Locked ? Muted : Ink,
                    GameFont.Tiny, TextAnchor.MiddleCenter);
                if (node.maxLevel > 1) Ranks(new(hit.center.x, caption.yMax + 5), view.Rank, node.maxLevel, dim ? Rule : Gold);
            }
            if (!dim)
            {
                if (!dragging && Widgets.ButtonInvisible(hit)) Select(node);
                TooltipHandler.TipRegion(hit, node.LabelCap + "  " + view.Rank + "/" + node.maxLevel + " · " + view.State
                    + "\n" + node.description + (node.tradeoff.NullOrEmpty() ? "" : "\n" + node.tradeoff));
            }
        }
    }
    private static void DrawConnection(Vector2 a, Vector2 bendA, Vector2 bendB, Vector2 b, Color color, bool lit)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (lit)
        {
            Color glow = new(color.r, color.g, color.b, color.a * .12f);
            Widgets.DrawLine(a, bendA, glow, 3); Widgets.DrawLine(bendA, bendB, glow, 3); Widgets.DrawLine(bendB, b, glow, 3);
        }
        Widgets.DrawLine(a, bendA, color, lit ? 1.2f : .65f);
        Widgets.DrawLine(bendA, bendB, color, lit ? 1.2f : .65f);
        Widgets.DrawLine(bendB, b, color, lit ? 1.2f : .65f);
    }
    private void HandleGraphInput(Rect viewport)
    {
        Event e = Event.current;
        bool overNode = hits.Any(p => !Dim(p.Key) && p.Value.Contains(e.mousePosition));
        if (e.type == EventType.ScrollWheel && viewport.Contains(e.mousePosition))
        { ZoomAt(e.delta.y < 0 ? 1.12f : 1 / 1.12f, e.mousePosition); e.Use(); }
        if (e.type == EventType.MouseDown && viewport.Contains(e.mousePosition) && (e.button == 2 || e.button == 0 && !overNode))
        { dragging = true; dragStart = e.mousePosition; panStart = pan; GUI.FocusControl(null); e.Use(); }
        if (dragging && e.type == EventType.MouseDrag) { pan = panStart + e.mousePosition - dragStart; e.Use(); }
        if (dragging && e.rawType == EventType.MouseUp) { dragging = false; e.Use(); }
        if (e.type == EventType.Ignore) dragging = false;
    }

    private void DrawDetails(Rect rect)
    {
        Frame(rect, Color.white, 32);
        if (selected == null) return;
        NodeView state = views[selected];
        const float footerHeight = 100;
        Rect scroll = new(rect.x + 22, rect.y + 24, rect.width - 44, Mathf.Max(60, rect.height - footerHeight - 42));
        Rect content = new(0, 0, scroll.width - 16, Mathf.Max(scroll.height, detailHeight));
        Widgets.BeginScrollView(scroll, ref detailsScroll, content);
        try
        {
            float y = 0;
            TextBlock(content.width, ref y, selected.category.LabelCap, Muted, GameFont.Tiny);
            y += 10;
            float iconSize = content.width < 250 ? 48 : 58;
            Texture(new Rect(0, y, iconSize, iconSize), Icon(selected), Color.white);
            Ring(new(iconSize * .5f, y + iconSize * .5f), iconSize * .5f + 1, Rule);
            float titleWidth = content.width - iconSize - 14;
            float titleHeight = Height(selected.LabelCap, titleWidth, GameFont.Medium);
            Label(new Rect(iconSize + 14, y, titleWidth, titleHeight), selected.LabelCap, Ink, GameFont.Medium);
            Label(new Rect(iconSize + 14, y + titleHeight + 3, titleWidth, 25),
                T("LevelShort", "等级") + "  " + state.Rank + " / " + selected.maxLevel, Muted);
            y += Mathf.Max(iconSize, titleHeight + 30) + 17;
            Rect statusRect = new(0, y, content.width, 26);
            Symbol(new Rect(1, y + 7, 12, 12), state.Visual, state.CanLearn ? Gold : Muted);
            Label(new Rect(23, y, content.width - 23, 26), state.State, state.CanLearn ? Gold : Ink);
            y += 34;
            string reason = message ?? (!state.CanLearn && state.Rank < selected.maxLevel || MingyuanPowerBalance.Sealed ? state.Reason : null);
            if (!reason.NullOrEmpty()) TextBlock(content.width, ref y, reason, Muted, GameFont.Tiny);
            if (!state.Reason.NullOrEmpty()) TooltipHandler.TipRegion(statusRect, state.Reason);
            Divider(content.width, ref y);
            TextBlock(content.width, ref y, T("Effect", "节点效果"), Muted, GameFont.Tiny);
            if (selected.maxLevel > 1 && selected.value != 0)
            {
                string value = ValueLabel(selected.value * state.Rank, selected);
                if (state.Rank < selected.maxLevel) value += "  →  " + ValueLabel(selected.value * (state.Rank + 1), selected);
                TextBlock(content.width, ref y, value, Gold, GameFont.Medium);
            }
            TextBlock(content.width, ref y, selected.description, Ink);
            // Detailed rule qualifiers remain available on demand without crowding the card.
            if (!selected.tradeoff.NullOrEmpty())
                TooltipHandler.TipRegion(new Rect(0, y - 40, content.width, 40), selected.tradeoff);
            Divider(content.width, ref y);
            TextBlock(content.width, ref y, selected.flavorText ?? T("FlavorFallback", "灰烬之下，仍有余温。"), Muted, GameFont.Tiny);
            detailHeight = y + 12;
            float blank = scroll.height - y;
            if (blank > 105)
            {
                float emblemSize = Mathf.Min(132, blank - 32);
                Texture(new Rect((content.width - emblemSize) * .5f, y + (blank - emblemSize) * .5f, emblemSize, emblemSize),
                    Icon(selected), new Color(.76f, .48f, .24f, .12f));
            }
        }
        finally { Widgets.EndScrollView(); }
        Rect footer = new(rect.x + 24, rect.yMax - footerHeight, rect.width - 48, footerHeight - 22);
        Widgets.DrawLine(new(footer.x, footer.y), new(footer.xMax, footer.y), Rule, .7f);
        if (state.Rank < selected.maxLevel)
            Label(new Rect(footer.x, footer.y + 8, footer.width, 24),
                selected.InsightCost(state.Rank) + " " + T("Insight", "焰识") + "  ·  "
                + selected.AshCost(state.Rank) + " " + T("Ash", "灰烬"), Gold, GameFont.Tiny, TextAnchor.MiddleCenter);
        string action = state.Rank >= selected.maxLevel ? T("Mastered", "已掌握")
            : state.Rank == 0 ? T("LearnNode", "学习节点")
            : T("UpgradeRank", "升级至 {0} 级").Formatted(state.Rank + 1).ToString();
        Rect button = new(footer.x, footer.y + 35, footer.width, 43);
        if (Button(button, action, state.CanLearn, state.CanLearn)) ConfirmPurchase(selected);
        if (!state.CanLearn && !state.Reason.NullOrEmpty()) TooltipHandler.TipRegion(button, state.Reason);
    }
    private static void Divider(float width, ref float y)
    {
        y += 9; Widgets.DrawLine(new(0, y), new(width, y), Rule, .7f); y += 20;
    }
    private static string ValueLabel(float value, MingyuanEmberNodeDef node)
    {
        if (node.effect == EmberEffect.DecayDelay) return "+" + value.ToString("0.#") + " " + T("Seconds", "秒");
        return (node.effect is EmberEffect.MeleeSpeed or EmberEffect.CastGuard ? "−" : "+") + (value * 100).ToString("0.#") + "%";
    }
    private void ResetMenu()
    {
        MingyuanEmberNodeDef node = selected;
        bool canResetSelected = node != null && node.AllowsReset && book.Tree.GetNodeLevel(node) > 0;
        bool canResetAll = nodes.Any(n => n.AllowsReset && book.Tree.GetNodeLevel(n) > 0);
        Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
        {
            new(T("RefundSelected", "返还选中节点"), canResetSelected ? () => ConfirmReset(node) : null),
            new(T("ResetEntireTree", "重置全部节点"), canResetAll ? () => ConfirmReset(null) : null)
        }));
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
            message = null;
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

