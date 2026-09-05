using System.Collections.Generic;
using MiliraXian.Characters;
using System.Linq;
using MiliraXian.Characters.QingHe;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things;
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
        private readonly HediffComp_SkillTreeState state;
        private readonly List<SkillNodeCollectionDef> collections = new();
        private readonly Dictionary<SkillNodeCollectionDef, List<SkillNodeDef>> nodesByCollection = new();
        private readonly Dictionary<SkillNodeCollectionDef, bool> collectionLearned = new();
        private readonly Dictionary<SkillNodeCollectionDef, bool> collectionHasUnrevealed = new();
        private readonly Dictionary<SkillNodeCollectionDef, bool> collectionCompletionActive = new();
        private readonly Dictionary<SkillNodeDef, SkillNodeUiState> nodeStates = new();
        private SkillNodeCollectionDef selectedCollection;
        private SkillNodeDef selectedNode;
        private Vector2 scrollPosition;
        private Vector2 detailScrollPosition;

        private static readonly Color LearnedNodeColor = new(0.46f, 0.68f, 0.54f, 1f);
        private static readonly Color CanLearnNodeColor = new(0.34f, 0.42f, 0.48f, 1f);
        private static readonly Color LockedNodeColor = new(0.16f, 0.17f, 0.18f, 1f);
        private static readonly Color ImportantNodeColor = new(0.54f, 0.42f, 0.27f, 1f);
        private static readonly Color ProgressFillColor = new(0.62f, 0.78f, 0.66f, 0.72f);
        private static readonly Color ThinNodeBorderColor = new(0f, 0f, 0f, 0.82f);

        public override Vector2 InitialSize => new(WindowWidth, WindowHeight);

        public Dialog_QH_SkillTree(Pawn pawn, HediffComp_SkillTreeState state)
        {
            this.pawn = pawn;
            this.state = state;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseButton = false;
            BuildSkillTreeSnapshot();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Widgets.CloseButtonFor(inRect))
            {
                Close();
            }

            Rect panelRect = new(inRect.x, inRect.y + TabAreaHeight, inRect.width, inRect.height - TabAreaHeight);
            DrawCollectionTabsAndPanel(panelRect);
        }

        private void DrawCollectionTabsAndPanel(Rect rect)
        {
            if (!collections.Contains(selectedCollection))
            {
                selectedCollection = collections.FirstOrDefault();
                selectedNode = null;
                scrollPosition = Vector2.zero;
            }

            List<TabRecord> tabRecords = new();
            for (int i = 0; i < collections.Count; i++)
            {
                SkillNodeCollectionDef collection = collections[i];
                string tabLabel = IsCollectionLearned(collection) ? collection.LabelCap.ToString() : "?";
                tabRecords.Add(new TabRecord(tabLabel, delegate
                {
                    selectedCollection = collection;
                    selectedNode = null;
                    scrollPosition = Vector2.zero;
                    detailScrollPosition = Vector2.zero;
                }, selectedCollection == collection));
            }

            Rect tabRect = new(rect.x, rect.y, rect.width - CloseButtonReserveWidth, rect.height);
            TabDrawer.DrawTabs(tabRect, tabRecords, 200f);
            Widgets.DrawMenuSection(rect);

            if (selectedCollection == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Small;
                Widgets.Label(rect, "MX_QH_SkillTreeNoScores".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            DrawCollectionPanel(rect.ContractedBy(8f), selectedCollection);
        }

        private void DrawCollectionPanel(Rect rect, SkillNodeCollectionDef collection)
        {
            List<SkillNodeDef> nodes = NodesForCollection(collection);
            bool collectionLearned = IsCollectionLearned(collection);
            if (!collectionLearned)
            {
                selectedNode = null;
                detailScrollPosition = Vector2.zero;
            }
            else if (!nodes.Contains(selectedNode) || !IsNodeSelectable(selectedNode, collectionLearned))
            {
                selectedNode = nodes.FirstOrDefault(node => IsNodeSelectable(node, collectionLearned));
                detailScrollPosition = Vector2.zero;
            }

            Rect treeRect = collectionLearned
                ? new Rect(rect.x, rect.y, rect.width - DetailPanelWidth - PanelGap, rect.height)
                : rect;
            Rect detailRect = new(treeRect.xMax + PanelGap, rect.y, DetailPanelWidth, rect.height);
            Rect viewRect = BuildTreeViewRect(treeRect, nodes);

            Widgets.BeginScrollView(treeRect, ref scrollPosition, viewRect);
            for (int i = 0; i < nodes.Count; i++)
            {
                DrawNode(nodes[i], i);
            }

            Widgets.EndScrollView();
            if (collectionLearned)
            {
                DrawNodeDetails(detailRect, selectedNode);
            }
        }

        private void DrawNode(SkillNodeDef node, int fallbackIndex)
        {
            SkillNodeUiState nodeState = StateForNode(node);
            int level = nodeState.level;
            bool learned = nodeState.learned;
            bool maxed = nodeState.maxed;
            bool selected = selectedNode == node;
            string reason = nodeState.reason;
            bool canLearn = nodeState.canLearn;
            float readProgress = nodeState.readProgress;
            float progress = nodeState.progress;
            bool known = nodeState.known;
            bool selectable = nodeState.selectable;
            Rect rect = NodeRect(node, fallbackIndex);
            Color fill = learned ? LearnedNodeColor : canLearn ? CanLearnNodeColor : LockedNodeColor;
            if (node.important && !learned)
            {
                fill = Color.Lerp(fill, ImportantNodeColor, 0.55f);
            }

            Widgets.DrawBoxSolid(rect, fill);
            if (!maxed && progress > 0f)
            {
                Rect progressRect = rect;
                progressRect.width *= Mathf.Clamp01(progress);
                Widgets.DrawBoxSolid(progressRect, ProgressFillColor);
            }
            Color oldColor = GUI.color;
            GUI.color = ThinNodeBorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = oldColor;
            if (selected)
            {
                Widgets.DrawBox(rect, 2);
            }
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            if (selectable && Widgets.ButtonInvisible(rect))
            {
                selectedNode = node;
                detailScrollPosition = Vector2.zero;
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 8f, rect.width - 12f, 28f), known ? node.LabelCap.ToString() : "?");
            Text.Font = GameFont.Tiny;
            string stateText = !known
                ? "?"
                : node.MaxLevel > 1
                ? "MX_QH_SkillTreeStateLevel".Translate(level, node.MaxLevel).ToString()
                : learned
                    ? "MX_QH_SkillTreeStateLearned".Translate().ToString()
                    : readProgress > 0f
                    ? "MX_QH_SkillTreeStateReadingWithProgress".Translate(progress.ToStringPercent("F0")).ToString()
                    : canLearn
                        ? "MX_QH_SkillTreeStatePending".Translate().ToString()
                        : reason;
            Widgets.Label(new Rect(rect.x + 6f, rect.yMax - 24f, rect.width - 12f, 18f), stateText);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(rect, known ? BuildNodeTip(node, learned, canLearn, reason, progress) : "?");
        }

        private void DrawNodeDetails(Rect rect, SkillNodeDef node)
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

            Rect viewRect = new(0f, 0f, innerRect.width - 16f, Mathf.Max(innerRect.height, 360f));
            float y = 0f;
            Widgets.BeginScrollView(innerRect, ref detailScrollPosition, viewRect);

            SkillNodeUiState nodeState = StateForNode(node);
            int level = nodeState.level;
            bool learned = nodeState.learned;
            bool maxed = nodeState.maxed;
            string reason = nodeState.reason;
            bool canLearn = nodeState.canLearn;
            float readProgress = nodeState.readProgress;
            float progress = nodeState.progress;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, viewRect.width, 32f), node.LabelCap);
            y += 38f;

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), "MX_QH_SkillTreeUnlockProgress".Translate());
            y += 24f;
            Rect progressRect = new(0f, y, viewRect.width, 22f);
            Widgets.DrawBoxSolid(progressRect, LockedNodeColor);
            Rect fillRect = progressRect;
            fillRect.width *= Mathf.Clamp01(progress);
            Widgets.DrawBoxSolid(fillRect, learned ? LearnedNodeColor : ProgressFillColor);
            Widgets.DrawBox(progressRect);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(progressRect, node.MaxLevel > 1 ? "MX_QH_SkillTreeStateLevel".Translate(level, node.MaxLevel).ToString() : learned ? "MX_QH_SkillTreeStateLearned".Translate().ToString() : progress.ToStringPercent("F0"));
            Text.Anchor = TextAnchor.UpperLeft;
            y += 34f;

            string stateText = node.MaxLevel > 1
                ? "MX_QH_SkillTreeStateLevel".Translate(level, node.MaxLevel).ToString()
                : learned
                    ? "MX_QH_SkillTreeStateLearned".Translate().ToString()
                    : readProgress > 0f
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

            string completionEffectText = BuildCollectionCompletionEffectText(node.collection);
            if (!completionEffectText.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                float completionHeight = Text.CalcHeight(completionEffectText, viewRect.width);
                Widgets.Label(new Rect(0f, y, viewRect.width, completionHeight), completionEffectText);
                y += completionHeight + 18f;
            }

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

        private static string BuildNodeTip(SkillNodeDef node, bool learned, bool canLearn, string reason, float progress)
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

        private Rect NodeRect(SkillNodeDef node, int fallbackIndex)
        {
            float width = node.important ? ImportantNodeWidth : NodeWidth;
            float height = node.important ? ImportantNodeHeight : NodeHeight;
            Vector2 center = NodeCenter(node, fallbackIndex);
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }

        private Rect BuildTreeViewRect(Rect visibleRect, List<SkillNodeDef> nodes)
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

        private static string BuildSourceScoreText(SkillNodeDef node)
        {
            if (node == null || node.bookName.NullOrEmpty())
            {
                return null;
            }

            string label = MX_QHCharacterUtility.TranslateIfKey(node.bookName);
            return "MX_QH_SkillTreeSourceScoreEntry".Translate(label).ToString();
        }

        private string BuildCollectionCompletionEffectText(SkillNodeCollectionDef collection)
        {
            if (collection == null || !collection.HasCompletionEffect)
            {
                return null;
            }

            if (collectionHasUnrevealed.TryGetValue(collection, out bool hasUnrevealed) && hasUnrevealed)
            {
                return null;
            }

            string label = MX_QHCharacterUtility.TranslateIfKey(collection.completionEffectLabel);
            string description = MX_QHCharacterUtility.TranslateIfKey(collection.completionEffectDescription);
            bool active = collectionCompletionActive.TryGetValue(collection, out bool value) && value;
            string title = active
                ? "MX_QH_SkillTreeCollectionCompletionActive".Translate(label).ToString()
                : "MX_QH_SkillTreeCollectionCompletionLocked".Translate(label).ToString();

            return description.NullOrEmpty() ? title : title + "\n" + description;
        }

        private void BuildSkillTreeSnapshot()
        {
            HashSet<SkillNodeDef> extractedNodes = GameComponent_MX_SkillTreeKnowledge.ExtractedNodesSnapshot();
            List<SkillNodeDef> relevantNodes = DefDatabase<SkillNodeDef>.AllDefsListForReading
                .Where(node => state == null || state.IsRelevantNode(node))
                .ToList();

            collections.AddRange(DefDatabase<SkillNodeCollectionDef>.AllDefsListForReading
                .Where(collection => relevantNodes.Any(node => node.collection == collection))
                .OrderBy(collection => collection.displayOrder));

            for (int i = 0; i < relevantNodes.Count; i++)
            {
                SkillNodeDef node = relevantNodes[i];
                int level = state?.GetNodeLevel(node) ?? 0;
                bool maxed = state != null && level >= node.MaxLevel;
                string reason = null;
                bool canLearn = state != null && state.CanLearn(node, out reason);
                float readProgress = maxed ? 0f : state?.GetNodeReadingProgressPercent(node) ?? 0f;
                bool known = level > 0 || readProgress > 0f || extractedNodes.Contains(node);

                nodeStates[node] = new SkillNodeUiState
                {
                    level = level,
                    learned = level > 0,
                    maxed = maxed,
                    canLearn = canLearn,
                    reason = reason,
                    readProgress = readProgress,
                    progress = BuildNodeProgress(node, level, readProgress),
                    known = known
                };
            }

            for (int i = 0; i < collections.Count; i++)
            {
                SkillNodeCollectionDef collection = collections[i];
                List<SkillNodeDef> nodes = relevantNodes
                    .Where(node => node.collection == collection)
                    .OrderBy(node => node.displayOrder)
                    .ToList();
                nodesByCollection[collection] = nodes;

                bool learned = nodes.Any(node => StateForNode(node).learned);
                collectionLearned[collection] = learned;
                collectionHasUnrevealed[collection] = nodes.Any(node => !StateForNode(node).known);
                collectionCompletionActive[collection] = state != null && state.IsCollectionCompletionEffectActive(collection);
            }

            foreach (KeyValuePair<SkillNodeDef, SkillNodeUiState> pair in nodeStates)
            {
                pair.Value.selectable = pair.Value.known && IsCollectionLearned(pair.Key.collection);
            }
        }

        private List<SkillNodeDef> NodesForCollection(SkillNodeCollectionDef collection)
        {
            if (collection != null && nodesByCollection.TryGetValue(collection, out List<SkillNodeDef> nodes))
            {
                return nodes;
            }

            return new List<SkillNodeDef>();
        }

        private SkillNodeUiState StateForNode(SkillNodeDef node)
        {
            if (node != null && nodeStates.TryGetValue(node, out SkillNodeUiState nodeState))
            {
                return nodeState;
            }

            return SkillNodeUiState.Empty;
        }

        private bool IsCollectionLearned(SkillNodeCollectionDef collection)
        {
            return collection != null && collectionLearned.TryGetValue(collection, out bool learned) && learned;
        }

        private bool IsNodeSelectable(SkillNodeDef node, bool collectionLearned)
        {
            if (!collectionLearned || node == null)
            {
                return false;
            }

            return StateForNode(node).selectable;
        }

        private class SkillNodeUiState
        {
            public static readonly SkillNodeUiState Empty = new();

            public int level;
            public bool learned;
            public bool maxed;
            public bool canLearn;
            public string reason;
            public float readProgress;
            public float progress;
            public bool known;
            public bool selectable;
        }

        private static float BuildNodeProgress(SkillNodeDef node, int level, float readProgress)
        {
            if (node == null)
            {
                return 0f;
            }

            if (node.MaxLevel <= 1)
            {
                return level > 0 ? 1f : Mathf.Clamp01(readProgress);
            }

            return Mathf.Clamp01((Mathf.Clamp(level, 0, node.MaxLevel) + Mathf.Clamp01(readProgress)) / node.MaxLevel);
        }

        private static Vector2 NodeCenter(SkillNodeDef node, int fallbackIndex)
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


