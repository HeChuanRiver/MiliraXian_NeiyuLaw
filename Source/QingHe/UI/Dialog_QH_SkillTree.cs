using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI
{
    public class Dialog_QH_SkillTree : Window
    {
        private const float WindowWidth = 980f;
        private const float WindowHeight = 640f;
        private const float LeftPanelWidth = 272f;
        private const float NodeWidth = 132f;
        private const float NodeHeight = 68f;
        private const float ImportantNodeWidth = 152f;
        private const float ImportantNodeHeight = 78f;
        private const float ColumnSpacing = 190f;
        private const float FirstColumnCenterX = 116f;
        private const float ConnectionAnchorHalfWidth = NodeWidth * 0.5f;
        private const float ConnectionEndYOffset = 18f;
        private const float ConnectionMiddleXOffset = 10f;
        private const float TreeViewWidth = 900f;
        private const float TreeViewHeight = 420f;
        private const float YingyueFlowerDanceY = 70f;
        private const float YingyueTopLinkY = 140f;
        private const float YingyueLinkSpacing = 80f;
        private const float CrossTreeLinkWidth = 118f;
        private const float CrossTreeLinkHeight = NodeHeight;
        private const float ChoiceSummarySize = 64f;
        private const float ChoiceRowHeight = 128f;
        private const float ChoiceIconSize = 46f;

        private readonly Pawn pawn;
        private readonly HediffComp_FlowerResonance state;
        private readonly HediffComp_FlowerChoices choices;
        private QingheSkillTreeDef selectedTree;
        private Vector2 scrollPosition;
        private Vector2 treeViewportSize;

        public delegate bool ChoiceSetter<T>(T def, out string reason) where T : Def;

        private enum ChoicePanelKind
        {
            FlowerMandate,
            TimedFlowerMandate,
            FlowerSigil,
            FlowerWord
        }

        private struct CrossTreePrerequisiteLink
        {
            public QingheSkillNodeDef prerequisite;
            public QingheSkillNodeDef target;
            public float y;

            public CrossTreePrerequisiteLink(QingheSkillNodeDef prerequisite, QingheSkillNodeDef target, float y)
            {
                this.prerequisite = prerequisite;
                this.target = target;
                this.y = y;
            }
        }

        private static readonly Color LearnedNodeColor = new Color(0.46f, 0.68f, 0.54f, 1f);
        private static readonly Color CanLearnNodeColor = new Color(0.34f, 0.42f, 0.48f, 1f);
        private static readonly Color LockedNodeColor = new Color(0.16f, 0.17f, 0.18f, 1f);
        private static readonly Color ImportantNodeColor = new Color(0.54f, 0.42f, 0.27f, 1f);
        private static readonly Color LineLearnedColor = new Color(0.58f, 0.84f, 0.66f, 1f);
        private static readonly Color LineLockedColor = new Color(0.28f, 0.30f, 0.32f, 1f);
        private static readonly Color CrossTreeLinkColor = new Color(0.22f, 0.28f, 0.34f, 1f);
        private static readonly Color CrossTreeLinkLearnedColor = new Color(0.30f, 0.46f, 0.52f, 1f);
        private static readonly Color SelectedChoiceColor = new Color(0.72f, 0.90f, 0.80f, 0.22f);
        private static readonly Color MasteryTextColor = new Color(1f, 0.82f, 0.22f, 1f);
        private static readonly Color ExperienceBarColor = new Color(0.58f, 0.84f, 1f, 1f);
        private static readonly Color MasteryExperienceBarColor = new Color(0.30f, 0.42f, 0.62f, 1f);
        private static readonly Color MasteryCompleteBarColor = new Color(0.22f, 0.24f, 0.30f, 1f);

        public override Vector2 InitialSize => new Vector2(WindowWidth, WindowHeight);

        public Dialog_QH_SkillTree(Pawn pawn, HediffComp_FlowerResonance state, HediffComp_FlowerChoices choices)
        {
            this.pawn = pawn;
            this.state = state;
            this.choices = choices;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseButton = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "花神庭");
            DrawExperienceHeader(new Rect(inRect.x, inRect.y + 36f, inRect.width, 42f));

            Rect contentRect = new Rect(inRect.x, inRect.y + 112f, inRect.width, inRect.height - 126f);
            Rect leftRect = new Rect(contentRect.x, contentRect.y, LeftPanelWidth, contentRect.height);
            Rect rightRect = new Rect(leftRect.xMax + 12f, contentRect.y, contentRect.width - LeftPanelWidth - 12f, contentRect.height);

            DrawChoicePanel(leftRect);
            DrawTreeTabsAndPanel(rightRect);
        }

        private void DrawExperienceHeader(Rect rect)
        {
            Text.Font = GameFont.Small;
            string masteryText = state.MusicMasteryLevel > 0
                ? "    音律精通：" + ToRoman(state.MusicMasteryLevel) + " / XII"
                : "";
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 20f), pawn.LabelShortCap + "    技能点：" + state.SkillPoints + masteryText);

            Rect barRect = new Rect(rect.x, rect.y + 23f, rect.width, 16f);
            DrawExperienceBar(barRect);
            Widgets.DrawBox(barRect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Color oldColor = GUI.color;
            if (state.MusicMasteryLevel > 0)
            {
                GUI.color = MasteryTextColor;
            }
            else
            {
                GUI.color = state.ExperienceProgressPercent > 0.5f ? Color.black : Color.white;
            }
            Widgets.Label(barRect, state.MusicMasteryComplete
                ? "音律精通已满，经验锁定"
                : "经验 " + state.Experience.ToString("F0") + " / " + state.ExperienceToNextPoint.ToString("F0"));
            GUI.color = oldColor;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawExperienceBar(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.06f, 0.07f, 0.08f, 1f));
            if (state.MusicMasteryComplete)
            {
                Widgets.DrawBoxSolid(rect, MasteryCompleteBarColor);
                return;
            }

            float fillPercent = Mathf.Clamp01(state.ExperienceProgressPercent);
            if (fillPercent <= 0f)
            {
                return;
            }

            Color fillColor = state.MusicMasteryLevel > 0 ? MasteryExperienceBarColor : ExperienceBarColor;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width * fillPercent, rect.height), fillColor);
        }

        private void DrawChoicePanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "当前选择");

            float y = inner.y + 30f;
            float cellWidth = (inner.width - 8f) * 0.5f;
            DrawChoiceSummary(new Rect(inner.x, y, cellWidth, ChoiceSummarySize), ChoicePanelKind.FlowerMandate, "飞花令", state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerMandate), choices?.SelectedFlowerMandate, blocked: false, choices?.FlowerMandateCooldownTicksLeft ?? 0);
            DrawChoiceSummary(new Rect(inner.x + cellWidth + 8f, y, cellWidth, ChoiceSummarySize), ChoicePanelKind.TimedFlowerMandate, "寄时飞花令", state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_SishiLiuzhuan), choices?.SelectedTimedFlowerMandate, choices?.SelectedFlowerMandate == null, choices?.TimedFlowerMandateCooldownTicksLeft ?? 0);

            y += ChoiceSummarySize + 8f;
            DrawChoiceSummary(new Rect(inner.x, y, cellWidth, ChoiceSummarySize), ChoicePanelKind.FlowerSigil, "花神签", state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerSigil), choices?.SelectedFlowerSigil, blocked: false, choices?.FlowerSigilCooldownTicksLeft ?? 0);
            DrawChoiceSummary(new Rect(inner.x + cellWidth + 8f, y, cellWidth, ChoiceSummarySize), ChoicePanelKind.FlowerWord, "花语", state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerWord), choices?.SelectedFlowerWord, blocked: false, choices?.FlowerWordCooldownTicksLeft ?? 0);

        }


        private void DrawChoiceSummary(Rect rect, ChoicePanelKind panelKind, string label, bool unlocked, Def selectedDef, bool blocked, int cooldownTicksLeft)
        {
            Widgets.DrawMenuSection(rect);
            if (Mouse.IsOver(rect) && !blocked)
            {
                Widgets.DrawHighlight(rect);
            }

            Rect iconRect = new Rect(rect.x + 8f, rect.y + 8f, 34f, 34f);
            Rect labelRect = new Rect(iconRect.xMax + 6f, rect.y + 6f, rect.width - iconRect.width - 20f, 18f);
            Rect textRect = new Rect(iconRect.xMax + 6f, rect.y + 27f, rect.width - iconRect.width - 20f, 22f);

            Widgets.DrawBox(iconRect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            if (!unlocked)
            {
                Widgets.Label(iconRect, "?");
            }
            else if (selectedDef == null)
            {
                if (!blocked && cooldownTicksLeft <= 0)
                {
                    DrawPulsingPlus(iconRect);
                }
            }
            else
            {
                Texture2D icon = QingheFlowerChoiceUtility.IconForDef(selectedDef);
                if (icon != null)
                {
                    GUI.DrawTexture(iconRect.ContractedBy(3f), icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    Widgets.Label(iconRect, QingheFlowerChoiceUtility.ShortLabelForDef(selectedDef));
                }
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Tiny;
            Widgets.Label(labelRect, label);
            Widgets.Label(textRect, BuildChoiceSummaryStateText(unlocked, selectedDef, blocked, cooldownTicksLeft));

            TooltipHandler.TipRegion(rect, BuildChoiceSummaryTip(label, unlocked, selectedDef, blocked, cooldownTicksLeft));
            if (!blocked && Widgets.ButtonInvisible(rect))
            {
                OpenChoicePicker(panelKind, label, unlocked);
            }
        }

        private void OpenChoicePicker(ChoicePanelKind panelKind, string label, bool unlocked)
        {
            if (!unlocked)
            {
                Messages.Message(label + "尚未习得。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            switch (panelKind)
            {
                case ChoicePanelKind.TimedFlowerMandate:
                    if (choices?.SelectedFlowerMandate == null)
                    {
                        Messages.Message("请先选择主飞花令。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Find.WindowStack.Add(new Dialog_QH_ChoicePicker<AbilityDef>(pawn, label, choices?.SelectedTimedFlowerMandate, QingheFlowerChoiceUtility.FlowerMandates, TrySetTimedFlowerMandate, choices?.SelectedFlowerMandate));
                    break;
                case ChoicePanelKind.FlowerSigil:
                    Find.WindowStack.Add(new Dialog_QH_ChoicePicker<HediffDef>(pawn, label, choices?.SelectedFlowerSigil, QingheFlowerChoiceUtility.FlowerSigils, TrySetFlowerSigil));
                    break;
                case ChoicePanelKind.FlowerWord:
                    Find.WindowStack.Add(new Dialog_QH_ChoicePicker<TraitDef>(pawn, label, choices?.SelectedFlowerWord, QingheFlowerChoiceUtility.FlowerWords, TrySetFlowerWord));
                    break;
                default:
                    Find.WindowStack.Add(new Dialog_QH_ChoicePicker<AbilityDef>(pawn, label, choices?.SelectedFlowerMandate, QingheFlowerChoiceUtility.FlowerMandates, TrySetFlowerMandate));
                    break;
            }
        }

        private static void DrawPulsingPlus(Rect rect)
        {
            float pulse = 0.55f + Mathf.Sin(Time.realtimeSinceStartup * 6f) * 0.35f;
            Color oldColor = GUI.color;
            GUI.color = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.24f, 1f), pulse);
            GUI.DrawTexture(rect.ContractedBy(7f), TexButton.Plus, ScaleMode.ScaleToFit, true);
            GUI.color = oldColor;
        }

        private static string BuildChoiceSummaryStateText(bool unlocked, Def selectedDef, bool blocked, int cooldownTicksLeft)
        {
            if (!unlocked)
            {
                return "?";
            }

            if (blocked)
            {
                return "-";
            }

            if (cooldownTicksLeft > 0)
            {
                return "CD";
            }

            return selectedDef == null ? "+" : QingheFlowerChoiceUtility.ShortLabelForDef(selectedDef);
        }

        private static string BuildChoiceSummaryTip(string label, bool unlocked, Def selectedDef, bool blocked, int cooldownTicksLeft)
        {
            if (!unlocked)
            {
                return label + "\n\n尚未习得。";
            }

            if (blocked)
            {
                return label + "\n\n请先选择主飞花令。";
            }

            string tip = label + "\n\n当前: " + (selectedDef == null ? "未选择" : QingheFlowerChoiceUtility.LabelForDef(selectedDef));
            if (cooldownTicksLeft > 0)
            {
                tip += "\n切换冷却: " + cooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            return tip;
        }

        private void DrawTreeTabsAndPanel(Rect rect)
        {
            List<QingheSkillTreeDef> trees = DefDatabase<QingheSkillTreeDef>.AllDefsListForReading
                .Where(tree => state.IsTreeUnlocked(tree))
                .OrderBy(tree => tree.displayOrder)
                .ToList();
            if (!trees.Contains(selectedTree))
            {
                selectedTree = trees.FirstOrDefault();
                scrollPosition = Vector2.zero;
            }

            List<TabRecord> tabRecords = new List<TabRecord>();
            for (int i = 0; i < trees.Count; i++)
            {
                QingheSkillTreeDef tree = trees[i];
                tabRecords.Add(new TabRecord(tree.LabelCap, delegate
                {
                    selectedTree = tree;
                    scrollPosition = Vector2.zero;
                }, selectedTree == tree));
            }

            TabDrawer.DrawTabs(rect, tabRecords, 200f);
            Widgets.DrawMenuSection(rect);

            if (selectedTree == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "尚未获得曲谱。");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            DrawTreePanel(rect.ContractedBy(8f), selectedTree);
        }

        private void DrawTreePanel(Rect rect, QingheSkillTreeDef tree)
        {
            treeViewportSize = rect.size;
            Rect viewRect = new Rect(0f, 0f, TreeViewWidth, TreeViewHeight);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            List<QingheSkillNodeDef> nodes = DefDatabase<QingheSkillNodeDef>.AllDefsListForReading
                .Where(node => node.tree == tree)
                .ToList();
            if (IsYingyueTree(tree))
            {
                DrawYingyueConnections();
                DrawYingyueCrossTreeLinks();
            }
            else
            {
                DrawConnections(nodes);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                DrawNode(nodes[i]);
            }

            Widgets.EndScrollView();
        }

        private void DrawYingyueConnections()
        {
            QingheSkillNodeDef flowerDance = MX_QHSkillNodeDefOf.MX_QH_Node_FlowerDance;
            QingheSkillNodeDef yingyue = MX_QHSkillNodeDefOf.MX_QH_Node_Yingyue;
            List<CrossTreePrerequisiteLink> links = YingyueCrossTreeLinks();
            for (int i = 0; i < links.Count; i++)
            {
                CrossTreePrerequisiteLink link = links[i];
                if (flowerDance != null && link.target != null)
                {
                    Vector2 flowerDanceCenter = NodeCenter(flowerDance);
                    Vector2 targetCenter = NodeCenter(link.target);
                    Vector2 start = new Vector2(flowerDanceCenter.x + ConnectionAnchorHalfWidth, GetConnectionAnchorY(flowerDanceCenter.y, links.Count, i));
                    Vector2 end = new Vector2(targetCenter.x - ConnectionAnchorHalfWidth, GetConnectionAnchorY(targetCenter.y, 2, 0));
                    DrawBentLine(start, end, state.HasNode(flowerDance) ? LineLearnedColor : LineLockedColor, links.Count, i);
                }

                if (link.prerequisite != null && link.target != null)
                {
                    Rect linkRect = CrossTreeLinkRect(link);
                    Vector2 targetCenter = NodeCenter(link.target);
                    Vector2 start = new Vector2(linkRect.xMax, linkRect.center.y);
                    Vector2 end = new Vector2(targetCenter.x - ConnectionAnchorHalfWidth, GetConnectionAnchorY(targetCenter.y, 2, 1));
                    DrawBentLine(start, end, state.HasNode(link.prerequisite) ? LineLearnedColor : LineLockedColor, 2, 1);
                }

                if (link.target != null && yingyue != null)
                {
                    Vector2 targetCenter = NodeCenter(link.target);
                    Vector2 yingyueCenter = NodeCenter(yingyue);
                    Vector2 start = new Vector2(targetCenter.x + ConnectionAnchorHalfWidth, targetCenter.y);
                    Vector2 end = new Vector2(yingyueCenter.x - ConnectionAnchorHalfWidth, GetConnectionAnchorY(yingyueCenter.y, links.Count, i));
                    DrawBentLine(start, end, state.HasNode(link.target) ? LineLearnedColor : LineLockedColor, links.Count, i);
                }
            }
        }

        private void DrawYingyueCrossTreeLinks()
        {
            List<CrossTreePrerequisiteLink> links = YingyueCrossTreeLinks();
            for (int i = 0; i < links.Count; i++)
            {
                DrawCrossTreeLink(links[i]);
            }
        }

        private void DrawCrossTreeLink(CrossTreePrerequisiteLink link)
        {
            if (link.prerequisite == null)
            {
                return;
            }

            Rect rect = CrossTreeLinkRect(link);
            bool learned = state.HasNode(link.prerequisite);
            bool canLearn = state.CanLearn(link.prerequisite, out string reason);
            bool hidden = ShouldHideNode(link.prerequisite, learned);
            Color fill = learned ? LearnedNodeColor : canLearn ? CanLearnNodeColor : LockedNodeColor;
            if (link.prerequisite.important && !learned)
            {
                fill = Color.Lerp(fill, ImportantNodeColor, 0.55f);
            }

            Widgets.DrawBoxSolid(rect, fill);
            Widgets.DrawBox(rect, link.prerequisite.important ? 2 : 1);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 5f, rect.width - 12f, 20f), hidden ? "?" : link.prerequisite.LabelCap.ToString());
            Text.Font = GameFont.Tiny;
            string stateText = hidden ? "?" : learned ? "已习得" : "消耗 " + link.prerequisite.cost;
            Widgets.Label(new Rect(rect.x + 6f, rect.yMax - 20f, rect.width - 12f, 16f), stateText);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(rect, BuildNodeTip(link.prerequisite, learned, canLearn, reason, hidden) + "\n\n点击跳转到对应曲谱节点。");
            if (Widgets.ButtonInvisible(rect))
            {
                JumpToNode(link.prerequisite);
            }
        }

        private List<CrossTreePrerequisiteLink> YingyueCrossTreeLinks()
        {
            return new List<CrossTreePrerequisiteLink>
            {
                new CrossTreePrerequisiteLink(MX_QHSkillNodeDefOf.MX_QH_Node_Gaoshan, MX_QHSkillNodeDefOf.MX_QH_Node_Yu, YingyueTopLinkY),
                new CrossTreePrerequisiteLink(MX_QHSkillNodeDefOf.MX_QH_Node_Luoyu, MX_QHSkillNodeDefOf.MX_QH_Node_Bianzhi, YingyueTopLinkY + YingyueLinkSpacing),
                new CrossTreePrerequisiteLink(MX_QHSkillNodeDefOf.MX_QH_Node_SishiLiuzhuan, MX_QHSkillNodeDefOf.MX_QH_Node_Run, YingyueTopLinkY + YingyueLinkSpacing * 2f)
            };
        }

        private Rect CrossTreeLinkRect(CrossTreePrerequisiteLink link)
        {
            float x = FirstColumnCenterX - CrossTreeLinkWidth * 0.5f;
            return new Rect(x, link.y - CrossTreeLinkHeight * 0.5f, CrossTreeLinkWidth, CrossTreeLinkHeight);
        }

        private void JumpToNode(QingheSkillNodeDef node)
        {
            if (node?.tree == null)
            {
                return;
            }

            if (!state.IsTreeUnlocked(node.tree))
            {
                Messages.Message("尚未获得对应曲谱。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            selectedTree = node.tree;
            CenterScrollOnNode(node);
        }

        private void CenterScrollOnNode(QingheSkillNodeDef node)
        {
            Vector2 center = NodeCenter(node);
            float viewportWidth = treeViewportSize.x > 0f ? treeViewportSize.x : 640f;
            float viewportHeight = treeViewportSize.y > 0f ? treeViewportSize.y : 400f;
            float x = Mathf.Clamp(center.x - viewportWidth * 0.5f, 0f, Mathf.Max(0f, TreeViewWidth - viewportWidth));
            float y = Mathf.Clamp(center.y - viewportHeight * 0.5f, 0f, Mathf.Max(0f, TreeViewHeight - viewportHeight));
            scrollPosition = new Vector2(x, y);
        }

        private void DrawConnections(List<QingheSkillNodeDef> nodes)
        {
            Dictionary<QingheSkillNodeDef, List<QingheSkillNodeDef>> outgoingByPrerequisite = BuildOutgoingByPrerequisite(nodes);
            for (int i = 0; i < nodes.Count; i++)
            {
                QingheSkillNodeDef node = nodes[i];
                List<QingheSkillNodeDef> prerequisites = SameTreePrerequisites(node);
                for (int j = 0; j < prerequisites.Count; j++)
                {
                    QingheSkillNodeDef prerequisite = prerequisites[j];
                    Vector2 preCenter = NodeCenter(prerequisite);
                    Vector2 nodeCenter = NodeCenter(node);
                    outgoingByPrerequisite.TryGetValue(prerequisite, out List<QingheSkillNodeDef> outgoing);
                    int outgoingIndex = outgoing?.IndexOf(node) ?? 0;
                    int outgoingCount = outgoing?.Count ?? 1;
                    Vector2 start = new Vector2(preCenter.x + ConnectionAnchorHalfWidth, GetConnectionAnchorY(preCenter.y, outgoingCount, outgoingIndex));
                    Vector2 end = new Vector2(nodeCenter.x - ConnectionAnchorHalfWidth, GetConnectionAnchorY(nodeCenter.y, prerequisites.Count, j));
                    Color color = state.HasNode(prerequisite) ? LineLearnedColor : LineLockedColor;
                    int laneCount;
                    int laneIndex;
                    GetConnectionLane(outgoingCount, outgoingIndex, prerequisites.Count, j, out laneCount, out laneIndex);
                    DrawBentLine(start, end, color, laneCount, laneIndex);
                }
            }
        }

        private Dictionary<QingheSkillNodeDef, List<QingheSkillNodeDef>> BuildOutgoingByPrerequisite(List<QingheSkillNodeDef> nodes)
        {
            Dictionary<QingheSkillNodeDef, List<QingheSkillNodeDef>> result = new Dictionary<QingheSkillNodeDef, List<QingheSkillNodeDef>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                QingheSkillNodeDef node = nodes[i];
                List<QingheSkillNodeDef> prerequisites = SameTreePrerequisites(node);
                for (int j = 0; j < prerequisites.Count; j++)
                {
                    QingheSkillNodeDef prerequisite = prerequisites[j];
                    if (!result.TryGetValue(prerequisite, out List<QingheSkillNodeDef> outgoing))
                    {
                        outgoing = new List<QingheSkillNodeDef>();
                        result.Add(prerequisite, outgoing);
                    }

                    outgoing.Add(node);
                }
            }

            foreach (List<QingheSkillNodeDef> outgoing in result.Values)
            {
                outgoing.SortBy(target => NodeCenter(target).y, target => target.column);
            }

            return result;
        }

        private static List<QingheSkillNodeDef> SameTreePrerequisites(QingheSkillNodeDef node)
        {
            if (node?.prerequisites == null)
            {
                return new List<QingheSkillNodeDef>();
            }

            List<QingheSkillNodeDef> result = new List<QingheSkillNodeDef>();
            for (int i = 0; i < node.prerequisites.Count; i++)
            {
                QingheSkillNodeDef prerequisite = node.prerequisites[i];
                if (prerequisite != null && prerequisite.tree == node.tree)
                {
                    result.Add(prerequisite);
                }
            }

            return result;
        }

        private float GetConnectionAnchorY(float nodeCenterY, int connectionCount, int connectionIndex)
        {
            if (connectionCount <= 1)
            {
                return nodeCenterY;
            }

            return nodeCenterY + (connectionIndex - (connectionCount - 1) * 0.5f) * ConnectionEndYOffset;
        }

        private void GetConnectionLane(int outgoingCount, int outgoingIndex, int incomingCount, int incomingIndex, out int laneCount, out int laneIndex)
        {
            if (incomingCount > outgoingCount)
            {
                laneCount = incomingCount;
                laneIndex = incomingIndex;
                return;
            }

            laneCount = outgoingCount;
            laneIndex = outgoingIndex;
        }

        private void DrawBentLine(Vector2 start, Vector2 end, Color color, int laneCount = 1, int laneIndex = 0)
        {
            float middleX = (start.x + end.x) * 0.5f + GetConnectionMiddleXOffset(laneCount, laneIndex);
            Vector2 middleA = new Vector2(middleX, start.y);
            Vector2 middleB = new Vector2(middleX, end.y);
            Widgets.DrawLine(start, middleA, color, 1.2f);
            Widgets.DrawLine(middleA, middleB, color, 1.2f);
            Widgets.DrawLine(middleB, end, color, 1.2f);
        }

        private float GetConnectionMiddleXOffset(int laneCount, int laneIndex)
        {
            if (laneCount <= 1)
            {
                return 0f;
            }

            return (laneIndex - (laneCount - 1) * 0.5f) * ConnectionMiddleXOffset;
        }

        private void DrawNode(QingheSkillNodeDef node)
        {
            Rect rect = NodeRect(node);
            bool learned = state.HasNode(node);
            bool canLearn = state.CanLearn(node, out string reason);
            bool hidden = ShouldHideNode(node, learned);
            Color fill = learned ? LearnedNodeColor : canLearn ? CanLearnNodeColor : LockedNodeColor;
            if (node.important && !learned)
            {
                fill = Color.Lerp(fill, ImportantNodeColor, 0.55f);
            }

            Widgets.DrawBoxSolid(rect, fill);
            Widgets.DrawBox(rect, node.important ? 2 : 1);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 8f, rect.width - 12f, 28f), hidden ? "?" : node.LabelCap.ToString());
            Text.Font = GameFont.Tiny;
            string stateText = hidden ? "?" : learned ? "已习得" : "消耗 " + node.cost;
            Widgets.Label(new Rect(rect.x + 6f, rect.yMax - 24f, rect.width - 12f, 18f), stateText);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(rect, BuildNodeTip(node, learned, canLearn, reason, hidden));
            if (Widgets.ButtonInvisible(rect) && !learned)
            {
                if (hidden)
                {
                    Messages.Message("这个节点尚未显现。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                if (!canLearn)
                {
                    Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                ConfirmLearnNode(node);
            }
        }

        private void ConfirmLearnNode(QingheSkillNodeDef node)
        {
            string text = "确定要习得“" + node.LabelCap + "”吗？\n\n"
                          + node.description + "\n\n"
                          + "将消耗技能点：" + node.cost;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, delegate
            {
                if (!state.TryLearn(node, out string learnReason))
                {
                    Messages.Message(learnReason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                QingheSkillTreeSystem.SyncChoices(pawn);
                Messages.Message("清荷习得了“" + node.LabelCap + "”。", pawn, MessageTypeDefOf.PositiveEvent, historical: false);
            }, title: "习得技能"));
        }

        private bool TrySetFlowerMandate(AbilityDef def, out string reason)
        {
            bool result = QingheSkillTreeSystem.TrySetFlowerMandate(choices, def, out reason);
            return result;
        }

        private bool TrySetTimedFlowerMandate(AbilityDef def, out string reason)
        {
            if (choices == null)
            {
                reason = "清荷尚未建立花神庭。";
                return false;
            }

            return choices.TrySetTimedFlowerMandate(def, out reason);
        }

        private bool TrySetFlowerSigil(HediffDef def, out string reason)
        {
            return QingheSkillTreeSystem.TrySetFlowerSigil(state, choices, def, out reason);
        }

        private bool TrySetFlowerWord(TraitDef def, out string reason)
        {
            return QingheSkillTreeSystem.TrySetFlowerWord(state, choices, def, out reason);
        }

        private bool ShouldHideNode(QingheSkillNodeDef node, bool learned)
        {
            if (learned || node.prerequisites.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < node.prerequisites.Count; i++)
            {
                if (state.HasNode(node.prerequisites[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private string BuildNodeTip(QingheSkillNodeDef node, bool learned, bool canLearn, string reason, bool hidden)
        {
            if (hidden)
            {
                return "未知节点\n\n前置节点尚未习得。";
            }

            string tip = node.LabelCap + "\n\n" + node.description + "\n\n消耗：" + node.cost;
            if (node.important)
            {
                tip += "\n类型：重要节点";
            }

            if (learned)
            {
                return tip + "\n状态：已习得";
            }

            if (canLearn)
            {
                return tip + "\n状态：可习得";
            }

            return tip + "\n状态：" + reason;
        }

        private Rect NodeRect(QingheSkillNodeDef node)
        {
            float width = node.important ? ImportantNodeWidth : NodeWidth;
            float height = node.important ? ImportantNodeHeight : NodeHeight;
            Vector2 center = NodeCenter(node);
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }

        private Vector2 NodeCenter(QingheSkillNodeDef node)
        {
            if (IsYingyueTree(node?.tree))
            {
                if (node == MX_QHSkillNodeDefOf.MX_QH_Node_FlowerDance)
                {
                    return new Vector2(FirstColumnCenterX, YingyueFlowerDanceY);
                }

                if (node == MX_QHSkillNodeDefOf.MX_QH_Node_Yu)
                {
                    return new Vector2(FirstColumnCenterX + ColumnSpacing, YingyueTopLinkY);
                }

                if (node == MX_QHSkillNodeDefOf.MX_QH_Node_Bianzhi)
                {
                    return new Vector2(FirstColumnCenterX + ColumnSpacing, YingyueTopLinkY + YingyueLinkSpacing);
                }

                if (node == MX_QHSkillNodeDefOf.MX_QH_Node_Run)
                {
                    return new Vector2(FirstColumnCenterX + ColumnSpacing, YingyueTopLinkY + YingyueLinkSpacing * 2f);
                }

                if (node == MX_QHSkillNodeDefOf.MX_QH_Node_Yingyue)
                {
                    return new Vector2(FirstColumnCenterX + ColumnSpacing * 2f, YingyueTopLinkY + YingyueLinkSpacing);
                }
            }

            return new Vector2(FirstColumnCenterX + node.column * ColumnSpacing, node.y);
        }

        private static bool IsYingyueTree(QingheSkillTreeDef tree)
        {
            return tree != null && tree == MX_QHSkillNodeDefOf.MX_QH_Node_FlowerDance?.tree;
        }

        private static string ToRoman(int value)
        {
            switch (Mathf.Clamp(value, 1, HediffComp_FlowerResonance.MaxMusicMasteryLevel))
            {
                case 1:
                    return "I";
                case 2:
                    return "II";
                case 3:
                    return "III";
                case 4:
                    return "IV";
                case 5:
                    return "V";
                case 6:
                    return "VI";
                case 7:
                    return "VII";
                case 8:
                    return "VIII";
                case 9:
                    return "IX";
                case 10:
                    return "X";
                case 11:
                    return "XI";
                default:
                    return "XII";
            }
        }
    }

    public class Dialog_QH_ChoicePicker<T> : Window where T : Def
    {
        private const float ChoiceIconSize = 46f;
        private const float WindowWidth = 360f;
        private const float WindowHeight = 150f;

        private readonly Pawn pawn;
        private readonly string choiceTypeLabel;
        private readonly T selectedDef;
        private readonly IReadOnlyList<T> options;
        private readonly Dialog_QH_SkillTree.ChoiceSetter<T> setter;
        private readonly T disabledDef;

        public override Vector2 InitialSize => new Vector2(WindowWidth, WindowHeight);

        public Dialog_QH_ChoicePicker(Pawn pawn, string choiceTypeLabel, T selectedDef, IReadOnlyList<T> options, Dialog_QH_SkillTree.ChoiceSetter<T> setter, T disabledDef = null)
        {
            this.pawn = pawn;
            this.choiceTypeLabel = choiceTypeLabel;
            this.selectedDef = selectedDef;
            this.options = options;
            this.setter = setter;
            this.disabledDef = disabledDef;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 24f), choiceTypeLabel);

            Rect rowRect = new Rect(inRect.x, inRect.y + 34f, inRect.width, 84f);
            float cellWidth = rowRect.width / Mathf.Max(1, options?.Count ?? 1);
            for (int i = 0; i < (options?.Count ?? 0); i++)
            {
                T def = options[i];
                Rect cellRect = new Rect(rowRect.x + i * cellWidth, rowRect.y, cellWidth - 4f, rowRect.height);
                DrawChoiceCell(cellRect, def, selectedDef == def, disabledDef == def);
            }
        }

        private void DrawChoiceCell(Rect rect, T def, bool selected, bool disabled)
        {
            if (selected)
            {
                Widgets.DrawBoxSolid(rect.ExpandedBy(3f), new Color(0.72f, 0.90f, 0.80f, 0.22f));
                Widgets.DrawBox(rect.ExpandedBy(3f), 2);
            }

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            Rect iconRect = new Rect(rect.center.x - ChoiceIconSize * 0.5f, rect.y + 4f, ChoiceIconSize, ChoiceIconSize);
            Rect labelRect = new Rect(rect.x, iconRect.yMax + 3f, rect.width, 32f);

            Widgets.DrawBox(iconRect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;

            Color oldColor = GUI.color;
            if (disabled)
            {
                GUI.color = new Color(0.45f, 0.45f, 0.45f, 0.72f);
            }

            Texture2D icon = QingheFlowerChoiceUtility.IconForDef(def);
            if (icon != null)
            {
                GUI.DrawTexture(iconRect.ContractedBy(3f), icon, ScaleMode.ScaleToFit);
            }
            else
            {
                Widgets.Label(iconRect, QingheFlowerChoiceUtility.ShortLabelForDef(def));
            }

            Text.Font = GameFont.Tiny;
            Widgets.Label(labelRect, QingheFlowerChoiceUtility.ShortLabelForDef(def));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = oldColor;

            TooltipHandler.TipRegion(rect, QingheFlowerChoiceUtility.LabelForDef(def));
            if (Widgets.ButtonInvisible(rect))
            {
                if (disabled)
                {
                    Messages.Message("寄时飞花令不能与主飞花令相同。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                if (selected)
                {
                    Messages.Message(choiceTypeLabel + "已经是“" + QingheFlowerChoiceUtility.LabelForDef(def) + "”。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                if (!setter(def, out string reason))
                {
                    Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                Messages.Message("清荷已将" + choiceTypeLabel + "切换为“" + QingheFlowerChoiceUtility.LabelForDef(def) + "”。", pawn, MessageTypeDefOf.PositiveEvent, historical: false);
                Close();
            }
        }
    }
}
