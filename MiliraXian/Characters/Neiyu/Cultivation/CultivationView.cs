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
        private static readonly Color Accent = new(.72f, .87f, .84f);
        private static readonly Color Gold = new(.92f, .82f, .60f);
        private static readonly Color Ink = new(.95f, .94f, .88f);
        private static readonly Color Muted = new(.78f, .83f, .80f);
        private static readonly Color Warning = new(.94f, .71f, .61f);
        private readonly Dictionary<(string, int, GameFont), float> textHeights = new();
        private Vector2 listScroll, detailScroll;
        private bool narrowDetails;
        private bool compact;
        private string selectedId;

        internal static bool SingleColumn(float width) => width < 720f;
        internal static float ListWidth(float width) => Mathf.Clamp(width * .34f, 225f, 360f);
        internal static bool StackEffectValues(float width) => width < 340f;

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
                compact = rect.height < 520f;
                // Keep the generated alpha silhouette clear of the content. The same
                // inset scales both the nine-slice corners and the safe reading area.
                float rim = Mathf.Min(40f, Mathf.Min(rect.width, rect.height) * .065f);
                CultivationArt.DrawFrame(rect, CultivationArt.SoftBackdrop, Color.white, rim * 2.4f);
                Rect inner = rect.ContractedBy(rim);
                float headerTextWidth = inner.width - (!compact && inner.width > 480f ? 104f : 0f) - 44f;
                float headerHeight = (compact ? 0f : 23f) + Height(CultivationText.Title, headerTextWidth, GameFont.Medium) + 4f
                    + Height(model.Pawn.LabelShortCap + "   ·   " + model.Progress, headerTextWidth, GameFont.Tiny);
                Rect header = new(inner.x, inner.y, inner.width, headerHeight);
                CultivationArt.DrawFrame(header.ExpandedBy(rim * .2f), CultivationArt.Panel, new Color(1f, 1f, 1f, .88f), 18f);
                DrawHeader(header, model);
                Rect close = new(header.xMax - 32f, header.y, 32f, 32f);
                if (Button(close, "×", CultivationText.Get("Close", "关闭 · Esc"), false, true)) return true;

                float tabsY = header.yMax + Gap;
                float tabWidth = (inner.width - Gap * 3f) / 4f;
                bool stackedTabs = tabWidth < 150f && !compact;
                float tabHeight = 48f;
                for (int i = 0; i < 4; i++)
                    tabHeight = Mathf.Max(tabHeight, Height(CultivationText.Branch((CultivationBranch)i), tabWidth - (stackedTabs ? 20f : compact ? 50f : 58f))
                        + (stackedTabs ? 38f : 0f) + 16f);
                for (int i = 0; i < 4; i++)
                {
                    var branch = (CultivationBranch)i;
                    Rect tab = new(inner.x + i * (tabWidth + Gap), tabsY, tabWidth, tabHeight);
                    if (Button(tab, CultivationText.Branch(branch), CultivationText.Branch(branch) + " · " + model.Ranks[i] + " / 3", model.Branch == branch, true, CultivationArt.ForBranch(branch), compact))
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
            if (!compact && rect.width > 480f) DrawIcon(new Rect(rect.x, rect.y, 86f, rect.height), CultivationArt.Emblem);
            float offset = !compact && rect.width > 480f ? 104f : 0f;
            float width = rect.width - offset - 44f;
            float y = rect.y;
            if (!compact)
            {
                Label(new Rect(rect.x + offset, y, width, 22f), CultivationText.Get("Header", "米莉拉 · 羽律"), Gold, GameFont.Tiny);
                y += 23f;
            }
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
            CultivationArt.DrawFrame(rect, CultivationArt.Panel, Color.white);
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
                    CultivationArt.DrawFrame(row, selected ? CultivationArt.ButtonActive : CultivationArt.Button,
                        Mouse.IsOver(row) && !selected ? new Color(1.10f, 1.10f, 1.10f) : Color.white);
                    Rect text = row.ContractedBy(14f);
                    string status = learned ? CultivationText.Get("Learned", "已领悟") : next ? CultivationText.Get("Next", "待领悟") : CultivationText.Get("Locked", "未解锁");
                    string badge = "0" + node.rank + "   /   " + status;
                    float badgeHeight = Height(badge, text.width, GameFont.Tiny);
                    Label(new Rect(text.x, text.y, text.width, badgeHeight), badge, learned || next ? Gold : Muted, GameFont.Tiny);
                    float titleHeight = Height(node.LabelCap, text.width);
                    Label(new Rect(text.x, text.y + badgeHeight + 6f, text.width, titleHeight), node.LabelCap, selected ? Gold : Ink);
                    float descY = text.y + badgeHeight + 6f + titleHeight + 8f;
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

        private float NodeHeight(NeiyuCultivationNodeDef node, float width)
        {
            // Reserve enough height for the longest translated state label, not a fixed line.
            float badge = Mathf.Max(Height("0" + node.rank + "   /   " + CultivationText.Get("Learned", "已领悟"), width - 28f, GameFont.Tiny),
                Mathf.Max(Height("0" + node.rank + "   /   " + CultivationText.Get("Next", "待领悟"), width - 28f, GameFont.Tiny),
                    Height("0" + node.rank + "   /   " + CultivationText.Get("Locked", "未解锁"), width - 28f, GameFont.Tiny)));
            return 28f + badge + 6f + Height(node.LabelCap, width - 28f) + 8f + Height(node.description, width - 28f);
        }

        private void DrawDetails(Rect rect, CultivationViewModel model, bool narrow)
        {
            CultivationArt.DrawFrame(rect, CultivationArt.Panel, Color.white);
            Rect inner = rect.ContractedBy(18f);
            var node = model.Selected;
            if (node == null)
            {
                Label(inner, CultivationText.Get("SelectNode", "选择一项修行。"), Muted);
                return;
            }
            if (narrow)
            {
                string backLabel = CultivationText.Get("Back", "‹ 返回修行分支");
                Rect back = new(inner.x, inner.y, inner.width, Mathf.Max(32f, Height(backLabel, inner.width - 20f) + 16f));
                if (Button(back, backLabel, null, false, true)) narrowDetails = false;
                inner.yMin += back.height + Gap;
            }
            string button = model.Ranks[(int)node.branch] >= node.rank ? CultivationText.Get("Learned", "已领悟") :
                !model.Ready ? CultivationText.Get("Checking", "核验材料…") : CultivationText.Get("Learn", "消耗材料 · 领悟");
            float buttonHeight = Mathf.Max(42f, Height(button, inner.width - 24f) + 16f);
            string feedback = model.Result ?? model.DisabledReason ?? (!model.Ready ? CultivationText.Get("Checking", "核验材料…") : CultivationText.Get("Ready", "材料齐备，可以领悟。"));
            bool active = model.Ready && model.DisabledReason == null;
            Color feedbackColor = model.Result != null || active ? Gold : Warning;
            // Short windows keep every control reachable in one scroll instead of
            // letting a fixed footer cover the entire reading area.
            if (inner.height < 260f || Height(feedback, inner.width) > 54f || buttonHeight > inner.height * .3f)
            {
                float scrollWidth = Mathf.Max(1f, inner.width - 18f);
                float readingHeight = DetailsFlow(scrollWidth, model, false);
                float messageHeight = Height(feedback, scrollWidth);
                float actionHeight = Mathf.Max(44f, Height(button, scrollWidth - 20f) + 16f);
                Widgets.BeginScrollView(inner, ref detailScroll, new Rect(0, 0, scrollWidth, readingHeight + messageHeight + actionHeight + 24f));
                try
                {
                    DetailsFlow(scrollWidth, model, true);
                    Label(new Rect(0, readingHeight + 4f, scrollWidth, messageHeight), feedback, feedbackColor);
                    if (Button(new Rect(0, readingHeight + messageHeight + 16f, scrollWidth, actionHeight), button, model.DisabledReason, true, active)) model.Learn();
                }
                finally { Widgets.EndScrollView(); }
                return;
            }
            // Anchor concise feedback and the action when there is enough reading space.
            float feedbackHeight = Height(feedback, inner.width);
            Rect action = new(inner.x, inner.yMax - buttonHeight, inner.width, buttonHeight);
            Rect reason = new(inner.x, action.y - feedbackHeight - 8f, inner.width, feedbackHeight);
            Label(reason, feedback, feedbackColor);
            TooltipHandler.TipRegion(reason, feedback);
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
            float titleWidth = Mathf.Max(1f, width - 60f);
            float titleHeight = Mathf.Max(48f, Height(node.LabelCap, titleWidth, GameFont.Medium));
            if (draw)
            {
                DrawIcon(new Rect(0f, (titleHeight - 48f) / 2f, 48f, 48f), CultivationArt.ForBranch(node.branch));
                Label(new Rect(60f, 0f, titleWidth, titleHeight), node.LabelCap, Ink, GameFont.Medium);
            }
            y += titleHeight + 12f;
            Flow(ref y, width, node.description, Muted, draw, gap: 22f);
            Section(ref y, width, CultivationText.Get("EffectsTitle", "力量变化"), draw);
            DrawEffects(ref y, width, model.Effects, draw);
            Section(ref y, width, CultivationText.Get("Requirements", "修行所需"), draw);
            if (node.prerequisite != null)
                Flow(ref y, width, CultivationText.Get("Previous", "前置") + " · " + node.prerequisite.LabelCap, Ink, draw, gap: 8f);
            Flow(ref y, width, model.ConditionText, Muted, draw, gap: 12f);
            foreach (var cost in model.Costs) Cost(ref y, width, cost, draw);
            Flow(ref y, width, CultivationText.Get("MaterialScope", "使用本地图储存区中未禁用、可安全到达且可预留的材料。领悟时立即消耗。"), Muted, draw, GameFont.Tiny, 24f);
            Section(ref y, width, CultivationText.Get("Memory", "羽间拾忆"), draw);
            float storyHeight = Height(node.story, width - 24f);
            if (draw)
            {
                Widgets.DrawBoxSolid(new Rect(0f, y, width, storyHeight + 24f), new Color(.025f, .04f, .04f, .35f));
                Label(new Rect(12f, y + 12f, width - 24f, storyHeight), node.story, Muted);
            }
            y += storyHeight + 36f;
            return y;
        }

        private void Section(ref float y, float width, string title, bool draw)
        {
            y += 12f;
            if (draw) Widgets.DrawBoxSolid(new Rect(0f, y, width, 1f), new Color(Gold.r, Gold.g, Gold.b, .25f));
            y += 10f;
            Flow(ref y, width, title, Gold, draw, gap: 12f);
        }

        private void DrawEffects(ref float y, float width, List<CultivationEffectRow> rows, bool draw)
        {
            bool stacked = StackEffectValues(width);
            float nameWidth = stacked ? 0f : width * .5f;
            float valueWidth = (width - nameWidth - 32f) * .5f;
            float beforeX = nameWidth + 8f, afterX = beforeX + valueWidth + 24f;
            string beforeTitle = CultivationText.Get("Current", "当前");
            string afterTitle = CultivationText.Get("After", "领悟后");
            float headerHeight = Mathf.Max(Height(beforeTitle, valueWidth, GameFont.Tiny), Height(afterTitle, valueWidth, GameFont.Tiny));
            if (draw)
            {
                Label(new Rect(beforeX, y, valueWidth, headerHeight), beforeTitle, Muted, GameFont.Tiny);
                Label(new Rect(afterX, y, valueWidth, headerHeight), afterTitle, Gold, GameFont.Tiny);
            }
            y += headerHeight + 6f;
            foreach (var row in rows)
            {
                if (!row.HasValues)
                {
                    y += row.Heading ? 10f : 4f;
                    Flow(ref y, width, row.Label, row.Heading ? Accent : Muted, draw, gap: 8f);
                    continue;
                }
                float labelHeight = Height(row.Label, stacked ? width - 16f : nameWidth - 8f);
                float valuesHeight = Mathf.Max(Height(row.Before, valueWidth), Height(row.After, valueWidth));
                float rowHeight = (stacked ? labelHeight + 6f + valuesHeight : Mathf.Max(labelHeight, valuesHeight)) + 16f;
                if (draw)
                {
                    Widgets.DrawBoxSolid(new Rect(0f, y, width, rowHeight), new Color(.02f, .035f, .04f, .28f));
                    Label(new Rect(8f, y + 8f, stacked ? width - 16f : nameWidth - 8f, labelHeight), row.Label, Ink);
                    float valuesY = y + 8f + (stacked ? labelHeight + 6f : 0f);
                    Label(new Rect(beforeX, valuesY, valueWidth, valuesHeight), row.Before, Muted);
                    Label(new Rect(beforeX + valueWidth, valuesY, 24f, valuesHeight), "→", Muted);
                    Label(new Rect(afterX, valuesY, valueWidth, valuesHeight), row.After, row.Before == row.After ? Ink : Gold);
                }
                y += rowHeight + 3f;
            }
            y += 8f;
        }

        private void Cost(ref float y, float width, CultivationViewModel.CostRow cost, bool draw)
        {
            float numberWidth = Mathf.Min(115f, width * .43f);
            float labelWidth = Mathf.Max(1f, width - numberWidth - 40f);
            float height = Mathf.Max(28f, Mathf.Max(Height(cost.Cost.thingDef.LabelCap, labelWidth), Height(cost.Quantity, numberWidth))) + 12f;
            if (draw)
            {
                Widgets.ThingIcon(new Rect(0f, y + 6f, 28f, 28f), cost.Cost.thingDef);
                Label(new Rect(36f, y + 6f, labelWidth, height - 12f), cost.Cost.thingDef.LabelCap, Ink);
                Color color = !cost.Complete ? Muted : cost.Available >= cost.Cost.count ? Gold : Warning;
                Label(new Rect(width - numberWidth, y + 6f, numberWidth, height - 12f), cost.Quantity, color);
                TooltipHandler.TipRegion(new Rect(0f, y, width, height), cost.Text);
            }
            y += height;
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

        private static bool Button(Rect rect, string text, string tip, bool selected, bool active, Texture2D icon = null, bool compact = false)
        {
            bool hovered = active && Mouse.IsOver(rect);
            Color tint = !active ? new Color(.65f, .69f, .68f) : hovered && !selected ? new Color(1.12f, 1.12f, 1.12f) : Color.white;
            CultivationArt.DrawFrame(rect, selected && active ? CultivationArt.ButtonActive : CultivationArt.Button, tint);
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect label = rect.ContractedBy(rect.width <= 40f ? 4f : 10f, rect.height <= 36f ? 4f : 8f);
            if (icon != null)
            {
                bool stacked = rect.width < 150f && !compact;
                float iconSize = compact ? 24f : 32f;
                DrawIcon(new Rect(stacked ? label.center.x - iconSize / 2f : label.x,
                    stacked ? label.y : label.center.y - iconSize / 2f, iconSize, iconSize), icon);
                if (stacked) label.yMin += iconSize + 6f;
                else label.xMin += iconSize + 6f;
            }
            Label(label, text, active ? selected ? Gold : Ink : Muted);
            Text.Anchor = TextAnchor.UpperLeft;
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(rect, tip);
            return active && Widgets.ButtonInvisible(rect);
        }

        private static void DrawIcon(Rect rect, Texture2D icon)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }
    }
}
