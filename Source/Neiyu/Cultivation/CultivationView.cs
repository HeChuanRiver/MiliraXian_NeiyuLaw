using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    // A bounded, immediate-mode view. Geometry is derived from available space and text
    // measurements; no business state, material searches, or textures are created here.
    internal sealed class CultivationView
    {
        private const float Gap = 12f;
        private static readonly Color Background = new(.045f, .074f, .102f);
        private static readonly Color Panel = new(.071f, .111f, .145f);
        private static readonly Color Accent = new(.64f, .88f, .92f);
        private static readonly Color Gold = new(.85f, .75f, .52f);
        private static readonly Color Ink = new(.91f, .94f, .92f);
        private static readonly Color Muted = new(.59f, .69f, .72f);
        private static readonly Vector2[] Circle = MakeCircle();
        private readonly Dictionary<(string, int, GameFont), float> textHeights = new();
        private Vector2 listScroll, detailScroll;
        private bool narrowDetails;
        private string selectedId;

        internal static bool SingleColumn(float width) => width < 720f;
        internal static float ListWidth(float width) => Mathf.Clamp(width * .34f, 225f, 360f);

        public bool Draw(Rect rect, CultivationViewModel model)
        {
            Color oldColor = GUI.color;
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            try
            {
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                Widgets.DrawBoxSolid(rect, Background);
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 2f), Accent);
                Rect inner = rect.ContractedBy(rect.width < 500 ? 14f : 24f);
                float headerTextWidth = inner.width - (inner.width > 480f ? 104f : 0f) - 44f;
                float headerHeight = 23f + Height(CultivationText.Title, headerTextWidth, GameFont.Medium) + 4f
                    + Height(model.Pawn.LabelShortCap + "   ·   " + model.Progress, headerTextWidth, GameFont.Tiny);
                Rect header = new(inner.x, inner.y, inner.width, headerHeight);
                DrawHeader(header, model);
                Rect close = new(header.xMax - 32f, header.y, 32f, 32f);
                if (Button(close, "×", CultivationText.Get("Close", "关闭 · Esc"), false, true)) return true;

                float tabsY = header.yMax + Gap;
                float tabWidth = (inner.width - Gap * 3f) / 4f;
                float tabHeight = 40f;
                for (int i = 0; i < 4; i++) tabHeight = Mathf.Max(tabHeight, Height(CultivationText.Branch((CultivationBranch)i), tabWidth - 14f) + 16f);
                for (int i = 0; i < 4; i++)
                {
                    var branch = (CultivationBranch)i;
                    Rect tab = new(inner.x + i * (tabWidth + Gap), tabsY, tabWidth, tabHeight);
                    if (Button(tab, CultivationText.Branch(branch), CultivationText.Branch(branch) + " · " + model.Ranks[i] + " / 3", model.Branch == branch, true))
                    {
                        model.SelectBranch(branch);
                        listScroll = detailScroll = Vector2.zero;
                        narrowDetails = false;
                    }
                }
                float bodyY = tabsY + tabHeight + Gap;
                Rect body = new(inner.x, bodyY, inner.width, Mathf.Max(0f, inner.yMax - bodyY));
                if (model.Selected?.defName != selectedId)
                {
                    selectedId = model.Selected?.defName;
                    detailScroll = Vector2.zero;
                }
                if (SingleColumn(body.width))
                {
                    if (narrowDetails) DrawDetails(body, model, true);
                    else DrawNodes(body, model, true);
                }
                else
                {
                    float width = ListWidth(body.width);
                    DrawNodes(new Rect(body.x, body.y, width, body.height), model, false);
                    DrawDetails(new Rect(body.x + width + Gap, body.y, body.width - width - Gap, body.height), model, false);
                }
                return false;
            }
            finally
            {
                GUI.color = oldColor;
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWrap;
            }
        }

        private void DrawHeader(Rect rect, CultivationViewModel model)
        {
            if (rect.width > 480f) DrawEmblem(new Rect(rect.x, rect.y, 86f, rect.height), .9f);
            float offset = rect.width > 480f ? 104f : 0f;
            float width = rect.width - offset - 44f;
            float y = rect.y;
            Label(new Rect(rect.x + offset, y, width, 22f), "MILIRA  /  NEIYU", Gold, GameFont.Tiny);
            y += 23f;
            float titleHeight = Height(CultivationText.Title, width, GameFont.Medium);
            Label(new Rect(rect.x + offset, y, width, titleHeight), CultivationText.Title, Ink, GameFont.Medium);
            y += titleHeight + 4f;
            string sub = model.Pawn.LabelShortCap + "   ·   " + model.Progress;
            float subHeight = Height(sub, width, GameFont.Tiny);
            Label(new Rect(rect.x + offset, y, width, subHeight), sub, Muted, GameFont.Tiny);
            TooltipHandler.TipRegion(new Rect(rect.x + offset, y, width, subHeight), sub);
        }

        private void DrawNodes(Rect rect, CultivationViewModel model, bool narrow)
        {
            Widgets.DrawBoxSolid(rect, Panel);
            Rect viewport = rect.ContractedBy(14f);
            float width = Mathf.Max(1f, viewport.width - 18f);
            float contentHeight = 0f;
            foreach (var node in model.Nodes)
                if (node.branch == model.Branch) contentHeight += NodeHeight(node, width) + Gap;
            string hint = CultivationText.Get("BranchHint", "以羽为引，循光而行。\n选择一项修行，查看所需材料与力量变化。");
            contentHeight += Height(hint, width) + 26f;
            Widgets.BeginScrollView(viewport, ref listScroll, new Rect(0, 0, width, Mathf.Max(viewport.height, contentHeight)));
            try
            {
                float y = 0f;
                foreach (var node in model.Nodes)
                {
                    if (node.branch != model.Branch) continue;
                    float height = NodeHeight(node, width);
                    Rect row = new(0f, y, width, height);
                    y += height + Gap;
                    if (row.yMax < listScroll.y || row.y > listScroll.y + viewport.height) continue;
                    bool selected = model.Selected == node;
                    bool learned = model.Ranks[(int)node.branch] >= node.rank;
                    bool next = model.Ranks[(int)node.branch] + 1 == node.rank;
                    Widgets.DrawBoxSolid(row, selected ? new Color(.13f, .23f, .28f) : new Color(.06f, .09f, .12f));
                    Widgets.DrawBoxSolid(new Rect(row.x, row.y, 2f, row.height), selected ? Accent : new Color(.21f, .29f, .32f));
                    if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                    Rect text = row.ContractedBy(14f);
                    string status = learned ? CultivationText.Get("Learned", "已领悟") : next ? CultivationText.Get("Next", "待领悟") : CultivationText.Get("Locked", "未解锁");
                    string badge = "0" + node.rank + "   /   " + status;
                    Label(new Rect(text.x, text.y, text.width, 20f), badge, learned ? Gold : Muted, GameFont.Tiny);
                    float titleHeight = Height(node.LabelCap, text.width);
                    Label(new Rect(text.x, text.y + 25f, text.width, titleHeight), node.LabelCap, selected ? Accent : Ink);
                    float descY = text.y + 25f + titleHeight + 6f;
                    Label(new Rect(text.x, descY, text.width, Height(node.description, text.width)), node.description, Muted);
                    TooltipHandler.TipRegion(row, node.description + "\n" + (node.prerequisite == null ? "" : CultivationText.Get("Previous", "前置") + " · " + node.prerequisite.LabelCap));
                    if (Widgets.ButtonInvisible(row))
                    {
                        model.Select(node);
                        detailScroll = Vector2.zero;
                        narrowDetails = narrow;
                    }
                }
                Label(new Rect(0, y + 12f, width, Height(hint, width)), hint, Muted);
            }
            finally { Widgets.EndScrollView(); }
        }

        private float NodeHeight(NeiyuCultivationNodeDef node, float width) =>
            28f + 25f + Height(node.LabelCap, width - 28f) + 6f + Height(node.description, width - 28f);

        private void DrawDetails(Rect rect, CultivationViewModel model, bool narrow)
        {
            Widgets.DrawBoxSolid(rect, new Color(.062f, .095f, .124f));
            Rect inner = rect.ContractedBy(18f);
            var node = model.Selected;
            if (node == null)
            {
                Label(inner, CultivationText.Get("SelectNode", "选择一项修行。"), Muted);
                return;
            }
            if (narrow)
            {
                Rect back = new(inner.x, inner.y, inner.width, 32f);
                if (Button(back, CultivationText.Get("Back", "‹ 返回修行分支"), null, false, true)) narrowDetails = false;
                inner.yMin += 32f + Gap;
            }
            string button = model.Ranks[(int)node.branch] >= node.rank ? CultivationText.Get("Learned", "已领悟") :
                !model.Ready ? CultivationText.Get("Checking", "核验材料…") : CultivationText.Get("Learn", "消耗材料 · 领悟");
            float buttonHeight = Mathf.Max(42f, Height(button, inner.width - 24f) + 16f);
            string feedback = model.Result ?? model.DisabledReason ?? CultivationText.Get("Ready", "材料齐备，可以领悟。");
            // Keep the action and its reason visible. Very long reasons have a tooltip;
            // the complete condition and resource list remain in the scrollable detail.
            float feedbackHeight = Mathf.Min(54f, Height(feedback, inner.width));
            Rect action = new(inner.x, inner.yMax - buttonHeight, inner.width, buttonHeight);
            Rect reason = new(inner.x, action.y - feedbackHeight - 8f, inner.width, feedbackHeight);
            Label(reason, feedback, model.Result != null ? Gold : Muted);
            TooltipHandler.TipRegion(reason, feedback);
            bool active = model.Ready && model.DisabledReason == null;
            if (Button(action, button, model.DisabledReason, true, active)) model.Learn();

            Rect viewport = new(inner.x, inner.y, inner.width, Mathf.Max(1f, reason.y - inner.y - Gap));
            float width = Mathf.Max(1f, viewport.width - 18f);
            float height = DetailsFlow(width, model, false);
            Widgets.BeginScrollView(viewport, ref detailScroll, new Rect(0, 0, width, Mathf.Max(viewport.height, height)));
            try { DetailsFlow(width, model, true); }
            finally { Widgets.EndScrollView(); }
        }

        private float DetailsFlow(float width, CultivationViewModel model, bool draw)
        {
            float y = 0f;
            var node = model.Selected;
            Flow(ref y, width, node.LabelCap, Ink, draw, GameFont.Medium, 12f);
            Flow(ref y, width, node.description, Muted, draw, gap: 22f);
            Flow(ref y, width, CultivationText.Get("Effects", "力量变化 · 当前 → 领悟后"), Accent, draw, gap: 10f);
            Flow(ref y, width, model.EffectText, Ink, draw, gap: 22f);
            Flow(ref y, width, CultivationText.Get("Requirements", "修行所需"), Accent, draw, gap: 10f);
            if (node.prerequisite != null)
                Flow(ref y, width, CultivationText.Get("Previous", "前置") + " · " + node.prerequisite.LabelCap, Ink, draw, gap: 8f);
            Flow(ref y, width, model.ConditionText, Muted, draw, gap: 12f);
            foreach (var cost in model.Costs) Flow(ref y, width, cost.Text, cost.Available >= cost.Cost.count ? Gold : Ink, draw, gap: 8f);
            Flow(ref y, width, CultivationText.Get("MaterialScope", "使用本地图储存区中未禁用、可安全到达且可预留的材料。领悟时立即消耗。"), Muted, draw, GameFont.Tiny, 24f);
            Flow(ref y, width, CultivationText.Get("Memory", "羽间拾忆"), Gold, draw, gap: 10f);
            Flow(ref y, width, node.story, Muted, draw, gap: 12f);
            return y;
        }

        private void Flow(ref float y, float width, string text, Color color, bool draw, GameFont font = GameFont.Small, float gap = 6f)
        {
            float height = Height(text, width, font);
            if (draw) Label(new Rect(0, y, width, height), text, color, font);
            y += height + gap;
        }

        private float Height(string text, float width, GameFont font = GameFont.Small)
        {
            var key = (text ?? "", Mathf.Max(1, Mathf.FloorToInt(width)), font);
            if (textHeights.TryGetValue(key, out float value)) return value;
            if (textHeights.Count > 768) textHeights.Clear();
            GameFont old = Text.Font;
            Text.Font = font;
            value = Mathf.Max(Text.LineHeight, Text.CalcHeight(key.Item1, key.Item2));
            Text.Font = old;
            textHeights[key] = value;
            return value;
        }

        private static void Label(Rect rect, string text, Color color, GameFont font = GameFont.Small)
        {
            GUI.color = color;
            Text.Font = font;
            Widgets.Label(rect, text ?? string.Empty);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private static bool Button(Rect rect, string text, string tip, bool selected, bool active)
        {
            Widgets.DrawBoxSolid(rect, selected ? new Color(.15f, .27f, .31f) : new Color(.09f, .14f, .18f));
            if (selected) Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), active ? Accent : Muted);
            if (active && Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Label(rect.ContractedBy(7f, 4f), text, active ? Ink : Muted);
            Text.Anchor = TextAnchor.UpperLeft;
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(rect, tip);
            return active && Widgets.ButtonInvisible(rect);
        }

        private static Vector2[] MakeCircle()
        {
            var points = new Vector2[49];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / 48f;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return points;
        }

        private static void DrawEmblem(Rect rect, float alpha)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .27f;
            var color = new Color(Accent.r, Accent.g, Accent.b, alpha);
            for (int i = 1; i < Circle.Length; i++)
                Widgets.DrawLine(center + Circle[i - 1] * radius, center + Circle[i] * radius, color, 1f);
            for (int side = -1; side <= 1; side += 2)
                for (int feather = 0; feather < 5; feather++)
                {
                    Vector2 start = center + new Vector2(side * (9f + feather * 3f), 16f - feather * 4f);
                    Vector2 end = center + new Vector2(side * (31f + feather * 2f), -15f + feather * 5f);
                    Widgets.DrawLine(start, end, color, 1.5f);
                }
            Widgets.DrawLine(center + new Vector2(0, -radius - 8f), center + new Vector2(0, radius + 10f), Gold, 1f);
            Widgets.DrawLine(center + new Vector2(-6, 0), center + new Vector2(0, -8), Gold, 1.5f);
            Widgets.DrawLine(center + new Vector2(0, -8), center + new Vector2(6, 0), Gold, 1.5f);
            Widgets.DrawLine(center + new Vector2(6, 0), center + new Vector2(0, 8), Gold, 1.5f);
            Widgets.DrawLine(center + new Vector2(0, 8), center + new Vector2(-6, 0), Gold, 1.5f);
        }
    }
}
