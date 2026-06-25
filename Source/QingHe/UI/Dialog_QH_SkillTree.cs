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
        private const float ChoiceRowHeight = 128f;
        private const float ChoiceIconSize = 46f;

        private readonly Pawn pawn;
        private readonly HediffComp_FlowerResonance state;
        private QingheSkillTreeDef selectedTree;
        private Vector2 scrollPosition;

        private delegate bool ChoiceSetter(string defName, out string reason);

        private static readonly Color LearnedNodeColor = new Color(0.46f, 0.68f, 0.54f, 1f);
        private static readonly Color CanLearnNodeColor = new Color(0.34f, 0.42f, 0.48f, 1f);
        private static readonly Color LockedNodeColor = new Color(0.16f, 0.17f, 0.18f, 1f);
        private static readonly Color ImportantNodeColor = new Color(0.54f, 0.42f, 0.27f, 1f);
        private static readonly Color LineLearnedColor = new Color(0.58f, 0.84f, 0.66f, 1f);
        private static readonly Color LineLockedColor = new Color(0.28f, 0.30f, 0.32f, 1f);
        private static readonly Color SelectedChoiceColor = new Color(0.72f, 0.90f, 0.80f, 0.22f);
        private static readonly Color MasteryTextColor = new Color(1f, 0.82f, 0.22f, 1f);
        private static readonly Color ExperienceBarColor = new Color(0.58f, 0.84f, 1f, 1f);
        private static readonly Color MasteryExperienceBarColor = new Color(0.30f, 0.42f, 0.62f, 1f);
        private static readonly Color MasteryCompleteBarColor = new Color(0.22f, 0.24f, 0.30f, 1f);

        public override Vector2 InitialSize => new Vector2(WindowWidth, WindowHeight);

        public Dialog_QH_SkillTree(Pawn pawn, HediffComp_FlowerResonance state)
        {
            this.pawn = pawn;
            this.state = state;
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
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "当前特性");

            float y = inner.y + 30f;
            DrawChoiceRow(new Rect(inner.x, y, inner.width, ChoiceRowHeight), "飞花令", state.HasNode(QingheSkillTreeSystem.NodeFlowerMandate), state.SelectedFlowerMandateDefName, QingheFlowerChoiceUtility.FlowerMandates, TrySetFlowerMandate);
            y += ChoiceRowHeight + 8f;
            DrawChoiceRow(new Rect(inner.x, y, inner.width, ChoiceRowHeight), "花神签", state.HasNode(QingheSkillTreeSystem.NodeFlowerSigil), state.SelectedFlowerSigilDefName, QingheFlowerChoiceUtility.FlowerSigils, TrySetFlowerSigil);
            y += ChoiceRowHeight + 8f;
            DrawChoiceRow(new Rect(inner.x, y, inner.width, ChoiceRowHeight), "花语", state.HasNode(QingheSkillTreeSystem.NodeFlowerWord), state.SelectedFlowerWordDefName, QingheFlowerChoiceUtility.FlowerWords, TrySetFlowerWord);
        }

        private void DrawChoiceRow(Rect rect, string label, bool unlocked, string selectedDefName, IReadOnlyList<string> options, ChoiceSetter setter)
        {
            Widgets.DrawMenuSection(rect);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), label);

            float cellWidth = (rect.width - 16f) / 4f;
            for (int i = 0; i < options.Count; i++)
            {
                string defName = options[i];
                Rect cellRect = new Rect(rect.x + 8f + i * cellWidth, rect.y + 30f, cellWidth - 4f, rect.height - 38f);
                DrawChoiceCell(cellRect, label, unlocked, selectedDefName == defName, defName, setter);
            }
        }

        private void DrawChoiceCell(Rect rect, string choiceTypeLabel, bool unlocked, bool selected, string defName, ChoiceSetter setter)
        {
            bool applied = QingheFlowerChoiceUtility.HasAppliedChoice(pawn, defName);
            if (selected || applied)
            {
                Widgets.DrawBoxSolid(rect.ExpandedBy(3f), SelectedChoiceColor);
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
            if (!unlocked)
            {
                Widgets.Label(iconRect, "?");
                Text.Font = GameFont.Tiny;
                Widgets.Label(labelRect, "?");
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(rect, "需要先习得对应节点。");
                return;
            }

            Texture2D icon = QingheFlowerChoiceUtility.IconForDefName(defName);
            if (icon != null)
            {
                GUI.DrawTexture(iconRect.ContractedBy(3f), icon, ScaleMode.ScaleToFit);
            }
            else
            {
                Widgets.Label(iconRect, QingheFlowerChoiceUtility.ShortLabelForDefName(defName));
            }

            Text.Font = GameFont.Tiny;
            Widgets.Label(labelRect, QingheFlowerChoiceUtility.ShortLabelForDefName(defName));
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(rect, QingheFlowerChoiceUtility.LabelForDefName(defName));

            if (Widgets.ButtonInvisible(rect))
            {
                if (selected && applied)
                {
                    Messages.Message(choiceTypeLabel + "已经是“" + QingheFlowerChoiceUtility.LabelForDefName(defName) + "”。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                ConfirmChoice(choiceTypeLabel, defName, setter);
            }
        }

        private void ConfirmChoice(string choiceTypeLabel, string defName, ChoiceSetter setter)
        {
            string choiceLabel = QingheFlowerChoiceUtility.LabelForDefName(defName);
            string text = "确定要将" + choiceTypeLabel + "切换为“" + choiceLabel + "”吗？";
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, delegate
            {
                if (!setter(defName, out string reason))
                {
                    Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                Messages.Message("清荷已将" + choiceTypeLabel + "切换为“" + choiceLabel + "”。", pawn, MessageTypeDefOf.PositiveEvent, historical: false);
            }, title: "切换" + choiceTypeLabel));
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
            Rect viewRect = new Rect(0f, 0f, 900f, 400f);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            List<QingheSkillNodeDef> nodes = DefDatabase<QingheSkillNodeDef>.AllDefsListForReading
                .Where(node => node.tree == tree)
                .ToList();
            DrawConnections(nodes);
            for (int i = 0; i < nodes.Count; i++)
            {
                DrawNode(nodes[i]);
            }

            Widgets.EndScrollView();
        }

        private void DrawConnections(List<QingheSkillNodeDef> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                QingheSkillNodeDef node = nodes[i];
                Rect nodeRect = NodeRect(node);
                List<QingheSkillNodeDef> prerequisites = node.prerequisites;
                for (int j = 0; j < prerequisites.Count; j++)
                {
                    QingheSkillNodeDef prerequisite = prerequisites[j];
                    if (prerequisite == null || prerequisite.tree != node.tree)
                    {
                        continue;
                    }

                    Rect preRect = NodeRect(prerequisite);
                    Vector2 start = new Vector2(preRect.xMax, preRect.center.y);
                    Vector2 end = new Vector2(nodeRect.x, GetConnectionEndY(nodeRect, prerequisites.Count, j));
                    Color color = state.HasNode(prerequisite.defName) ? LineLearnedColor : LineLockedColor;
                    DrawBentLine(start, end, color);
                }
            }
        }

        private float GetConnectionEndY(Rect nodeRect, int prerequisiteCount, int prerequisiteIndex)
        {
            if (prerequisiteCount <= 1)
            {
                return nodeRect.center.y;
            }

            float step = Mathf.Min(18f, nodeRect.height / (prerequisiteCount + 1));
            return nodeRect.center.y + (prerequisiteIndex - (prerequisiteCount - 1) * 0.5f) * step;
        }

        private void DrawBentLine(Vector2 start, Vector2 end, Color color)
        {
            float middleX = (start.x + end.x) * 0.5f;
            Vector2 middleA = new Vector2(middleX, start.y);
            Vector2 middleB = new Vector2(middleX, end.y);
            Widgets.DrawLine(start, middleA, color, 1.2f);
            Widgets.DrawLine(middleA, middleB, color, 1.2f);
            Widgets.DrawLine(middleB, end, color, 1.2f);
        }

        private void DrawNode(QingheSkillNodeDef node)
        {
            Rect rect = NodeRect(node);
            bool learned = state.HasNode(node.defName);
            bool canLearn = state.CanLearn(node.defName, out string reason);
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
                if (!state.TryLearn(node.defName, out string learnReason))
                {
                    Messages.Message(learnReason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                QingheSkillTreeSystem.SyncChoices(pawn);
                Messages.Message("清荷习得了“" + node.LabelCap + "”。", pawn, MessageTypeDefOf.PositiveEvent, historical: false);
            }, title: "习得技能"));
        }

        private bool TrySetFlowerMandate(string defName, out string reason)
        {
            return QingheSkillTreeSystem.TrySetFlowerMandate(state, defName, out reason);
        }

        private bool TrySetFlowerSigil(string defName, out string reason)
        {
            return QingheSkillTreeSystem.TrySetFlowerSigil(state, defName, out reason);
        }

        private bool TrySetFlowerWord(string defName, out string reason)
        {
            return QingheSkillTreeSystem.TrySetFlowerWord(state, defName, out reason);
        }

        private bool ShouldHideNode(QingheSkillNodeDef node, bool learned)
        {
            if (learned || node.prerequisites.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < node.prerequisites.Count; i++)
            {
                if (state.HasNode(node.prerequisites[i].defName))
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
            return new Rect(40f + node.column * ColumnSpacing, node.y, width, height);
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
}
