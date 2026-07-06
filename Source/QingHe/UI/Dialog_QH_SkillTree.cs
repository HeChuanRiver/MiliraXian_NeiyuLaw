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
        private const float NodeWidth = 132f;
        private const float NodeHeight = 68f;
        private const float ImportantNodeWidth = 152f;
        private const float ImportantNodeHeight = 78f;
        private const float ColumnSpacing = 190f;
        private const float FirstColumnCenterX = 116f;
        private const float DetailPanelWidth = 270f;
        private const float PanelGap = 10f;
        private const float TabAreaHeight = 36f;
        private const float CloseButtonReserveWidth = 30f;

        private readonly Pawn pawn;
        private readonly HediffComp_FlowerResonance state;
        private QingheSkillTreeDef selectedTree;
        private MX_QHSkillNodeDef selectedNode;
        private Vector2 scrollPosition;
        private Vector2 detailScrollPosition;

        private static readonly Color LearnedNodeColor = new Color(0.46f, 0.68f, 0.54f, 1f);
        private static readonly Color CanLearnNodeColor = new Color(0.34f, 0.42f, 0.48f, 1f);
        private static readonly Color LockedNodeColor = new Color(0.16f, 0.17f, 0.18f, 1f);
        private static readonly Color ImportantNodeColor = new Color(0.54f, 0.42f, 0.27f, 1f);
        private static readonly Color ProgressFillColor = new Color(0.62f, 0.78f, 0.66f, 0.72f);

        public override Vector2 InitialSize => new Vector2(WindowWidth, WindowHeight);

        public Dialog_QH_SkillTree(Pawn pawn, HediffComp_FlowerResonance state)
        {
            this.pawn = pawn;
            this.state = state;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseButton = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Widgets.CloseButtonFor(inRect))
            {
                Close();
            }

            Rect panelRect = new Rect(inRect.x, inRect.y + TabAreaHeight, inRect.width, inRect.height - TabAreaHeight);
            DrawTreeTabsAndPanel(panelRect);
        }

        private void DrawTreeTabsAndPanel(Rect rect)
        {
            List<QingheSkillTreeDef> trees = DefDatabase<QingheSkillTreeDef>.AllDefsListForReading
                .OrderBy(tree => tree.displayOrder)
                .ToList();
            if (!trees.Contains(selectedTree))
            {
                selectedTree = trees.FirstOrDefault();
                selectedNode = null;
                scrollPosition = Vector2.zero;
            }

            List<TabRecord> tabRecords = new List<TabRecord>();
            for (int i = 0; i < trees.Count; i++)
            {
                QingheSkillTreeDef tree = trees[i];
                tabRecords.Add(new TabRecord(tree.LabelCap, delegate
                {
                    selectedTree = tree;
                    selectedNode = null;
                    scrollPosition = Vector2.zero;
                    detailScrollPosition = Vector2.zero;
                }, selectedTree == tree));
            }

            Rect tabRect = new Rect(rect.x, rect.y, rect.width - CloseButtonReserveWidth, rect.height);
            TabDrawer.DrawTabs(tabRect, tabRecords, 200f);
            Widgets.DrawMenuSection(rect);

            if (selectedTree == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Small;
                Widgets.Label(rect, "MX_QH_SkillTreeNoScores".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            DrawTreePanel(rect.ContractedBy(8f), selectedTree);
        }

        private void DrawTreePanel(Rect rect, QingheSkillTreeDef tree)
        {
            List<MX_QHSkillNodeDef> nodes = DefDatabase<MX_QHSkillNodeDef>.AllDefsListForReading
                .Where(node => node.tree == tree)
                .OrderBy(node => node.displayOrder)
                .ToList();
            if (!nodes.Contains(selectedNode))
            {
                selectedNode = nodes.FirstOrDefault();
                detailScrollPosition = Vector2.zero;
            }

            Rect treeRect = new Rect(rect.x, rect.y, rect.width - DetailPanelWidth - PanelGap, rect.height);
            Rect detailRect = new Rect(treeRect.xMax + PanelGap, rect.y, DetailPanelWidth, rect.height);
            Rect viewRect = BuildTreeViewRect(treeRect, nodes);

            Widgets.BeginScrollView(treeRect, ref scrollPosition, viewRect);
            for (int i = 0; i < nodes.Count; i++)
            {
                DrawNode(nodes[i], i);
            }

            Widgets.EndScrollView();
            DrawNodeDetails(detailRect, selectedNode);
        }

        private void DrawNode(MX_QHSkillNodeDef node, int fallbackIndex)
        {
            bool learned = state?.HasNode(node) == true;
            bool selected = selectedNode == node;
            string reason = null;
            bool canLearn = state != null && state.CanLearn(node, out reason);
            float progress = learned ? 1f : state?.GetNodeReadingProgressPercent(node) ?? 0f;
            Rect rect = NodeRect(node, fallbackIndex);
            Color fill = learned ? LearnedNodeColor : canLearn ? CanLearnNodeColor : LockedNodeColor;
            if (node.important && !learned)
            {
                fill = Color.Lerp(fill, ImportantNodeColor, 0.55f);
            }

            Widgets.DrawBoxSolid(rect, fill);
            if (!learned && progress > 0f)
            {
                Rect progressRect = rect;
                progressRect.width *= Mathf.Clamp01(progress);
                Widgets.DrawBoxSolid(progressRect, ProgressFillColor);
            }
            Widgets.DrawBox(rect, node.important ? 2 : 1);
            if (selected)
            {
                Widgets.DrawBox(rect, 2);
            }
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            if (Widgets.ButtonInvisible(rect))
            {
                selectedNode = node;
                detailScrollPosition = Vector2.zero;
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 8f, rect.width - 12f, 28f), node.LabelCap.ToString());
            Text.Font = GameFont.Tiny;
            string stateText = learned
                ? "MX_QH_SkillTreeStateLearned".Translate().ToString()
                : progress > 0f
                    ? "MX_QH_SkillTreeStateReadingWithProgress".Translate(progress.ToStringPercent("F0")).ToString()
                    : canLearn
                        ? "MX_QH_SkillTreeStatePending".Translate().ToString()
                        : reason;
            Widgets.Label(new Rect(rect.x + 6f, rect.yMax - 24f, rect.width - 12f, 18f), stateText);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(rect, BuildNodeTip(node, learned, canLearn, reason, progress));
        }

        private void DrawNodeDetails(Rect rect, MX_QHSkillNodeDef node)
        {
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(10f);
            if (node == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Small;
                Widgets.Label(innerRect, "MX_QH_SkillTreeNoNodeSelected".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, Mathf.Max(innerRect.height, 360f));
            float y = 0f;
            Widgets.BeginScrollView(innerRect, ref detailScrollPosition, viewRect);

            bool learned = state?.HasNode(node) == true;
            string reason = null;
            bool canLearn = state != null && state.CanLearn(node, out reason);
            float progress = learned ? 1f : state?.GetNodeReadingProgressPercent(node) ?? 0f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, viewRect.width, 32f), node.LabelCap);
            y += 38f;

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), "MX_QH_SkillTreeUnlockProgress".Translate());
            y += 24f;
            Rect progressRect = new Rect(0f, y, viewRect.width, 22f);
            Widgets.DrawBoxSolid(progressRect, LockedNodeColor);
            Rect fillRect = progressRect;
            fillRect.width *= Mathf.Clamp01(progress);
            Widgets.DrawBoxSolid(fillRect, learned ? LearnedNodeColor : ProgressFillColor);
            Widgets.DrawBox(progressRect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(progressRect, learned ? "MX_QH_SkillTreeStateLearned".Translate().ToString() : progress.ToStringPercent("F0"));
            Text.Anchor = TextAnchor.UpperLeft;
            y += 34f;

            string stateText = learned
                ? "MX_QH_SkillTreeStateLearned".Translate().ToString()
                : progress > 0f
                    ? "MX_QH_SkillTreeStateReading".Translate().ToString()
                    : canLearn
                        ? "MX_QH_SkillTreeStatePending".Translate().ToString()
                        : reason;
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), "MX_QH_SkillTreeStatusLine".Translate(stateText));
            y += 32f;

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), "MX_QH_SkillTreeEffects".Translate());
            y += 26f;
            Text.Font = GameFont.Tiny;
            float descHeight = Text.CalcHeight(node.description, viewRect.width);
            Widgets.Label(new Rect(0f, y, viewRect.width, descHeight), node.description);
            y += descHeight + 18f;

            string sourceText = BuildSourceScoreText(node);
            if (!sourceText.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0f, y, viewRect.width, 24f), "MX_QH_SkillTreeSourceScores".Translate());
                y += 26f;
                Text.Font = GameFont.Tiny;
                float sourceHeight = Text.CalcHeight(sourceText, viewRect.width);
                Widgets.Label(new Rect(0f, y, viewRect.width, sourceHeight), sourceText);
            }

            Text.Font = GameFont.Small;
            Widgets.EndScrollView();
        }

        private static string BuildNodeTip(MX_QHSkillNodeDef node, bool learned, bool canLearn, string reason, float progress)
        {
            string tip = node.LabelCap + "\n\n" + node.description;
            if (node.important)
            {
                tip += "\n" + "MX_QH_SkillTreeImportantNode".Translate();
            }

            if (learned)
            {
                return tip + "\n" + "MX_QH_SkillTreeStatusLine".Translate("MX_QH_SkillTreeStateLearned".Translate());
            }

            if (canLearn)
            {
                string stateText = progress > 0f
                    ? "MX_QH_SkillTreeStateReadingWithProgress".Translate(progress.ToStringPercent("F0")).ToString()
                    : "MX_QH_SkillTreeStatePending".Translate().ToString();
                return tip + "\n" + "MX_QH_SkillTreeStatusLine".Translate(stateText);
            }

            return tip + "\n" + "MX_QH_SkillTreeStatusLine".Translate(reason);
        }

        private Rect NodeRect(MX_QHSkillNodeDef node, int fallbackIndex)
        {
            float width = node.important ? ImportantNodeWidth : NodeWidth;
            float height = node.important ? ImportantNodeHeight : NodeHeight;
            Vector2 center = NodeCenter(node, fallbackIndex);
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }

        private Rect BuildTreeViewRect(Rect visibleRect, List<MX_QHSkillNodeDef> nodes)
        {
            float width = Mathf.Max(visibleRect.width - 16f, 1f);
            float height = Mathf.Max(visibleRect.height - 16f, 1f);
            for (int i = 0; i < nodes.Count; i++)
            {
                Rect nodeRect = NodeRect(nodes[i], i);
                width = Mathf.Max(width, nodeRect.xMax + 24f);
                height = Mathf.Max(height, nodeRect.yMax + 24f);
            }

            return new Rect(0f, 0f, width, height);
        }

        private static string BuildSourceScoreText(MX_QHSkillNodeDef node)
        {
            List<QingheMusicScoreDef> scores = DefDatabase<QingheMusicScoreDef>.AllDefsListForReading
                .Where(score => score.unlocksNodes != null && score.unlocksNodes.Contains(node))
                .OrderBy(score => score.label)
                .ToList();
            if (scores.Count == 0)
            {
                return null;
            }

            return string.Join("\n", scores.Select(score => "MX_QH_SkillTreeSourceScoreEntry".Translate(score.LabelCap)).ToArray());
        }

        private static Vector2 NodeCenter(MX_QHSkillNodeDef node, int fallbackIndex)
        {
            int column = node.column;
            float y = node.y;
            if (y < 0f)
            {
                column = fallbackIndex;
                y = 160f;
            }

            return new Vector2(FirstColumnCenterX + column * ColumnSpacing, y);
        }
    }
}
