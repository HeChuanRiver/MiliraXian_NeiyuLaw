using System;
using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI
{
    public class Dialog_QH_SkillTree : Window
    {
        private enum FlowerCourtTab
        {
            Resonance,
            SkillTree
        }

        private const float WindowWidth = 980f;
        private const float WindowHeight = 700f;
        private const float CloseButtonReserveWidth = 30f;
        private const float TabBarHeight = 30f;
        private const float TabWidth = 150f;
        private const float ResonanceCardWidth = 190f;
        private const float ResonanceCardHeight = 142f;
        private const float ResonanceIconSize = 56f;
        private const float CenterOrnamentSize = 78f;
        private const float TopBarHeight = 58f;
        private const float SpecialNodeIconSize = 42f;
        private const float SpecialNodeGap = 8f;
        private const float SkillTreeBottomPadding = 18f;
        private const float LevelRailWidth = 96f;
        private const float LevelRowHeight = 64f;
        private const float LevelNodeWidth = 156f;
        private const float LevelNodeHeight = 52f;
        private const float NodeIconSize = 34f;


        private static readonly Color CardBackColor = new(0.10f, 0.11f, 0.12f, 0.92f);
        private static readonly Color NodeBackColor = new(0.13f, 0.14f, 0.15f, 0.92f);
        private static readonly Color LearnedBorderColor = new(1f, 0.80f, 0.88f, 1f);
        private static readonly Color LockedBorderColor = new(0.40f, 0.40f, 0.42f, 1f);
        private static readonly Color LockedIconColor = new(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color LockedLabelColor = new(0.58f, 0.58f, 0.60f, 1f);
        private static readonly Color RailBackColor = new(0.10f, 0.11f, 0.12f, 1f);
        private static readonly Color RailFillColor = new(1f, 0.80f, 0.88f, 0.90f);
        private static readonly Color TabBackColor = new(0.09f, 0.10f, 0.11f, 1f);
        private static readonly Color TabSelectedColor = new(0.22f, 0.16f, 0.19f, 1f);

        private static Texture2D placeholderIcon;

        private readonly Pawn pawn;
        private readonly HediffComp_SkillTreeState state;
        private readonly Dictionary<int, List<SkillNodeDef>> nodesByLevel = new();
        private List<SkillNodeDef> levelNodes = new();
        private List<SkillNodeDef> specialNodes = new();
        private FlowerCourtTab currentTab = FlowerCourtTab.SkillTree;
        private Vector2 levelScrollPosition;
        private Vector2 specialScrollPosition;

        public override Vector2 InitialSize => new(WindowWidth, WindowHeight);

        private static Texture2D PlaceholderIcon
        {
            get
            {
                if (placeholderIcon == null)
                {
                    placeholderIcon = ContentFinder<Texture2D>.Get("MiliraXianNeiyu/UI/MX_Neiyu_ThunderMarkedStorm", false) ?? BaseContent.BadTex;
                }

                return placeholderIcon;
            }
        }

        public Dialog_QH_SkillTree(Pawn pawn, HediffComp_SkillTreeState state)
        {
            this.pawn = pawn;
            this.state = state ?? MX_QH_HediffUtility.EnsureFlowerResonance(pawn);
            MX_QH_HediffUtility.EnsureDivineGraceComp(pawn);
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseButton = false;
            EnsureNodeCache();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Widgets.CloseButtonFor(inRect))
            {
                Close();
            }

            Rect contentRect = new(inRect.x, inRect.y, inRect.width - CloseButtonReserveWidth, inRect.height);
            Rect tabBar = new(contentRect.x, contentRect.y, contentRect.width, TabBarHeight);
            bool resonanceUnlocked = MX_QHSkillUtility.HasSeasonalResonance(pawn);
            if (!resonanceUnlocked)
            {
                currentTab = FlowerCourtTab.SkillTree;
            }
            if (resonanceUnlocked)
            {
                DrawTabButton(new Rect(tabBar.x, tabBar.y, TabWidth, TabBarHeight), FlowerCourtTab.Resonance, "MX_QH_ResonanceTab_Label".Translate());
            }
            float skillTabX = tabBar.x + (resonanceUnlocked ? TabWidth + 4f : 0f);
            DrawTabButton(new Rect(skillTabX, tabBar.y, TabWidth, TabBarHeight), FlowerCourtTab.SkillTree, "MX_QH_SkillTreeTab_Label".Translate());

            Rect pageRect = new(contentRect.x, tabBar.yMax + 6f, contentRect.width, contentRect.height - TabBarHeight - 6f);
            if (currentTab == FlowerCourtTab.Resonance)
            {
                DrawResonancePage(pageRect);
            }
            else
            {
                DrawSkillTreePage(pageRect);
            }

            ResetGui();
        }

        private void DrawTabButton(Rect rect, FlowerCourtTab tab, string label)
        {
            bool selected = currentTab == tab;
            Widgets.DrawBoxSolid(rect, selected ? TabSelectedColor : TabBackColor);
            GUI.color = selected ? LearnedBorderColor : LockedBorderColor;
            Widgets.DrawBox(rect, selected ? 2 : 1);
            GUI.color = Color.white;
            if (!selected && Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = selected ? Color.white : LockedLabelColor;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            if (!selected && Widgets.ButtonInvisible(rect))
            {
                currentTab = tab;
            }
        }

        private void DrawResonancePage(Rect rect)
        {
            HediffComp_QingheCombatState combatState = MX_QH_HediffUtility.GetCombatState(pawn);
            FlowerBellResonance current = MX_QH_HediffUtility.GetSeasonalResonance(pawn)?.Resonance ?? FlowerBellResonance.None;
            bool hasSelection = current != FlowerBellResonance.None;
            int cooldownRemaining = combatState?.TuneCooldownRemainingTicks ?? 0;

            float cardSize = Mathf.Min(ResonanceCardWidth, Mathf.Max(136f, Mathf.Min(rect.width, rect.height) * 0.24f));
            Vector2 center = rect.center;
            float offset = Mathf.Min(Mathf.Min(rect.width, rect.height) * 0.25f, cardSize * 1.05f);

            Rect springRect = CenteredRect(center + new Vector2(-offset, -offset), cardSize, cardSize);
            Rect summerRect = CenteredRect(center + new Vector2(offset, -offset), cardSize, cardSize);
            Rect autumnRect = CenteredRect(center + new Vector2(-offset, offset), cardSize, cardSize);
            Rect winterRect = CenteredRect(center + new Vector2(offset, offset), cardSize, cardSize);

            DrawResonanceCenter(rect, hasSelection, current);
            DrawSeasonCard(springRect, FlowerBellResonance.Spring, hasSelection, current, cooldownRemaining);
            DrawSeasonCard(summerRect, FlowerBellResonance.Summer, hasSelection, current, cooldownRemaining);
            DrawSeasonCard(autumnRect, FlowerBellResonance.Autumn, hasSelection, current, cooldownRemaining);
            DrawSeasonCard(winterRect, FlowerBellResonance.Winter, hasSelection, current, cooldownRemaining);
        }

        private static Rect CenteredRect(Vector2 center, float width, float height)
        {
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }

        private void DrawResonanceCenter(Rect rect, bool hasSelection, FlowerBellResonance current)
        {
            Vector2 center = rect.center;
            Rect haloRect = new(center.x - 118f, center.y - 118f, 236f, 236f);
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(haloRect, MX_QHRenderStatics.DiamondSolidTex, ScaleMode.ScaleToFit, true);

            Rect ornament = new(center.x - CenterOrnamentSize * 0.5f, center.y - CenterOrnamentSize * 0.5f, CenterOrnamentSize, CenterOrnamentSize);
            GUI.color = new Color(1f, 1f, 1f, 0.20f);
            GUI.DrawTexture(ornament.ExpandedBy(10f), MX_QHRenderStatics.DiamondSolidTex, ScaleMode.ScaleToFit, true);
            GUI.color = hasSelection ? ColorFor(current) : LockedBorderColor;
            GUI.DrawTexture(ornament, MX_QHRenderStatics.DiamondSolidTex, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;

            if (hasSelection)
            {
                DrawResonancePointer(center, current);
            }
        }

        private static void DrawResonancePointer(Vector2 center, FlowerBellResonance resonance)
        {
            Vector2 direction = resonance switch
            {
                FlowerBellResonance.Spring => new Vector2(-1f, -1f),
                FlowerBellResonance.Summer => new Vector2(1f, -1f),
                FlowerBellResonance.Autumn => new Vector2(-1f, 1f),
                FlowerBellResonance.Winter => new Vector2(1f, 1f),
                _ => Vector2.zero,
            };
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            Vector2 unit = direction.normalized;
            Vector2 start = center + unit * 46f;
            Vector2 end = center + unit * 112f;
            Color color = ColorFor(resonance);
            Widgets.DrawLine(start, end, color * new Color(1f, 1f, 1f, 0.88f), 4f);

            Rect tip = CenteredRect(end, 18f, 18f);
            GUI.color = color;
            GUI.DrawTexture(tip, MX_QHRenderStatics.DiamondSolidTex, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;
        }

        private void DrawSeasonCard(Rect rect, FlowerBellResonance resonance, bool hasSelection, FlowerBellResonance current, int cooldownRemaining)
        {
            bool selected = hasSelection && resonance == current;
            bool canSwitch = cooldownRemaining <= 0;
            bool hovered = Mouse.IsOver(rect);
            bool selectableHighlight = hovered && (canSwitch || selected);
            Color seasonColor = ColorFor(resonance);

            if (selected)
            {
                float pulse = 0.68f + 0.32f * Mathf.Sin(Time.realtimeSinceStartup * 4.2f);
                Widgets.DrawBoxSolid(rect.ExpandedBy(5f), seasonColor * new Color(1f, 1f, 1f, 0.22f * pulse));
                GUI.color = seasonColor * new Color(1f, 1f, 1f, 0.88f);
                Widgets.DrawBox(rect.ExpandedBy(2f), 3);
                GUI.color = Color.white;
            }

            Widgets.DrawBoxSolid(rect, selected ? Color.Lerp(CardBackColor, seasonColor, 0.16f) : CardBackColor);
            GUI.color = selected || canSwitch ? seasonColor : LockedBorderColor;
            Widgets.DrawBox(rect, selected ? 3 : 1);
            GUI.color = Color.white;
            DrawCornerOrnaments(rect, selected || canSwitch ? seasonColor : LockedBorderColor);

            if (selectableHighlight)
            {
                Widgets.DrawHighlight(rect);
            }

            Rect iconRect = new(rect.x + (rect.width - ResonanceIconSize) * 0.5f, rect.y + 16f, ResonanceIconSize, ResonanceIconSize);
            Color iconColor = selected ? Color.white : canSwitch ? seasonColor : LockedIconColor;
            if (selectableHighlight)
            {
                iconColor = Color.Lerp(iconColor, Color.white, 0.35f);
            }

            GUI.color = iconColor;
            GUI.DrawTexture(iconRect, PlaceholderIcon, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;

            string label = CompFlowerBellResonance.LabelFor(resonance);
            if (selected)
            {
                label += "MX_QH_CurrentSuffix".Translate();
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = selected || canSwitch ? Color.white : LockedLabelColor;
            Widgets.Label(new Rect(rect.x + 6f, iconRect.yMax + 7f, rect.width - 12f, 26f), label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            TooltipHandler.TipRegion(rect, TooltipForResonance(resonance, cooldownRemaining));
            if (canSwitch && !selected && Widgets.ButtonInvisible(rect))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "MX_QH_TuneConfirmText".Translate(CompFlowerBellResonance.LabelFor(resonance)),
                    delegate { StartTuning(resonance); }));
            }
        }

        private static void DrawCornerOrnaments(Rect rect, Color color)
        {
            const float ornamentSize = 12f;
            GUI.color = color * new Color(1f, 1f, 1f, 0.72f);
            GUI.DrawTexture(new Rect(rect.x + 5f, rect.y + 5f, ornamentSize, ornamentSize), MX_QHRenderStatics.DiamondSolidTex, ScaleMode.ScaleToFit, true);
            GUI.DrawTexture(new Rect(rect.xMax - 5f - ornamentSize, rect.y + 5f, ornamentSize, ornamentSize), MX_QHRenderStatics.DiamondSolidTex, ScaleMode.ScaleToFit, true);
            GUI.DrawTexture(new Rect(rect.x + 5f, rect.yMax - 5f - ornamentSize, ornamentSize, ornamentSize), MX_QHRenderStatics.DiamondSolidTex, ScaleMode.ScaleToFit, true);
            GUI.DrawTexture(new Rect(rect.xMax - 5f - ornamentSize, rect.yMax - 5f - ornamentSize, ornamentSize, ornamentSize), MX_QHRenderStatics.DiamondSolidTex, ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;
        }

        private void StartTuning(FlowerBellResonance resonance)
        {
            if (!MX_QHSkillUtility.HasSeasonalResonance(pawn))
            {
                return;
            }
            HediffComp_QingheCombatState combatState = MX_QH_HediffUtility.EnsureCombatState(pawn);
            if (combatState == null || combatState.TuneCooldownRemainingTicks > 0)
            {
                return;
            }

            combatState.BeginTuning(resonance);
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(MX_QHDefOf.MX_QH_TuneResonance), JobTag.Misc);
            Close();
        }

        private void DrawSkillTreePage(Rect rect)
        {
            HediffComp_QingheGraceSync grace = MX_QH_HediffUtility.EnsureDivineGraceComp(pawn);
            Rect topBar = new(rect.x, rect.y, rect.width, TopBarHeight);
            float specialAreaHeight = GetSpecialAreaHeight(rect.width);
            Rect specialArea = new(rect.x, rect.yMax - specialAreaHeight - SkillTreeBottomPadding, rect.width, specialAreaHeight);
            Rect mainArea = new(rect.x, topBar.yMax + 6f, rect.width, specialArea.y - topBar.yMax - 12f);

            DrawGraceTopBar(topBar, grace);
            DrawLevelArea(mainArea, grace);
            DrawSpecialArea(specialArea);
        }

        private void EnsureNodeCache()
        {
            List<SkillNodeDef> all = DefDatabase<SkillNodeDef>.AllDefsListForReading
                .Where(node => state == null || state.IsRelevantNode(node))
                .OrderBy(node => node.requiredGraceLevel)
                .ThenBy(node => node.displayOrder)
                .ToList();
            levelNodes = all.Where(node => !node.traitNode).ToList();
            specialNodes = all.Where(node => node.traitNode).OrderBy(node => node.displayOrder).ToList();
            nodesByLevel.Clear();
            foreach (SkillNodeDef node in levelNodes)
            {
                int level = Mathf.Clamp(node.requiredGraceLevel, 0, HediffComp_QingheGraceSync.MaxGraceLevel);
                if (!nodesByLevel.TryGetValue(level, out List<SkillNodeDef> list))
                {
                    list = new List<SkillNodeDef>();
                    nodesByLevel[level] = list;
                }

                list.Add(node);
            }
        }

        private void DrawGraceTopBar(Rect rect, HediffComp_QingheGraceSync grace)
        {
            int actualLevel = grace?.CurrentLevel ?? MX_QH_HediffUtility.GetDivineGraceLevel(pawn);
            int maxLevel = QinghePowerBalance.MaxEffectiveLevel;
            int level = Mathf.Min(actualLevel, maxLevel);
            float required = grace?.RequiredProgressForCurrentLevel ?? 0f;
            float progress = grace?.Progress ?? 0f;
            float percent = grace?.ProgressPercent ?? 0f;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            Widgets.Label(new Rect(rect.x, rect.y, 260f, 24f), "MX_QH_FlowerCourtGraceLine".Translate(level, maxLevel));
            GUI.color = Color.white;

            Rect barRect = new(rect.x, rect.y + 28f, rect.width, 16f);
            Widgets.DrawBoxSolid(barRect, RailBackColor);
            Rect fillRect = barRect.ContractedBy(1f);
            fillRect.width *= Mathf.Clamp01(percent);
            if (fillRect.width > 0.5f)
            {
                Widgets.DrawBoxSolid(fillRect, RailFillColor);
            }
            Widgets.DrawBox(barRect, 1);

            Text.Anchor = TextAnchor.MiddleCenter;
            bool reachedCurrentMax = actualLevel >= maxLevel;
            string progressText = reachedCurrentMax || (grace != null && grace.IsMaxLevel)
                ? "已达当前最大等级"
                : "MX_QH_GraceProgressLine".Translate(progress.ToString("0"), required.ToString("0"), percent.ToStringPercent());
            GUI.color = percent < 0.4f ? Color.white : Color.black;
            Widgets.Label(barRect, progressText);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawLevelArea(Rect rect, HediffComp_QingheGraceSync grace)
        {
            Rect outRect = rect;
            List<int> unlockLevels = nodesByLevel.Keys.OrderBy(level => level).ToList();
            float viewWidth = outRect.width - 16f;
            float viewHeight = (HediffComp_QingheGraceSync.MaxGraceLevel + 1) * LevelRowHeight;
            Rect viewRect = new(0f, 0f, viewWidth, viewHeight);
            Widgets.BeginScrollView(outRect, ref levelScrollPosition, viewRect);

            int currentLevel = grace?.CurrentLevel ?? MX_QH_HediffUtility.GetDivineGraceLevel(pawn);
            int effectiveLevel = Mathf.Min(currentLevel, QinghePowerBalance.MaxEffectiveLevel);
            float fillPercent = Mathf.Clamp01((currentLevel + (grace?.ProgressPercent ?? 0f)) / HediffComp_QingheGraceSync.MaxGraceLevel);
            for (int i = 0; i < unlockLevels.Count; i++)
            {
                int level = unlockLevels[i];
                float y = level * LevelRowHeight;
                bool reached = level <= currentLevel;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = reached ? Color.white : LockedLabelColor;
                Widgets.Label(new Rect(8f, y + 6f, LevelRailWidth - 12f, 18f), "MX_QH_FlowerCourtNodeLevel".Translate(level));
                GUI.color = Color.white;

                List<SkillNodeDef> rowNodes = nodesByLevel[level];
                float nodeX = LevelRailWidth + 8f;
                for (int nodeIndex = 0; nodeIndex < rowNodes.Count; nodeIndex++)
                {
                    Rect nodeRect = new(nodeX, y + (LevelRowHeight - LevelNodeHeight) * 0.5f, LevelNodeWidth, LevelNodeHeight);
                    DrawNodeCard(rowNodes[nodeIndex], nodeRect, effectiveLevel, compact: false);
                    nodeX += LevelNodeWidth + 8f;
                }
            }

            Widgets.EndScrollView();
        }

        private float GetSpecialAreaHeight(float width)
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + SpecialNodeGap) / (SpecialNodeIconSize + SpecialNodeGap)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(specialNodes.Count / (float)columns));
            float needed = 14f + rows * (SpecialNodeIconSize + SpecialNodeGap) - SpecialNodeGap;
            return Mathf.Min(needed, 148f);
        }

        private void DrawSpecialArea(Rect rect)
        {
            float step = SpecialNodeIconSize + SpecialNodeGap;
            int columns = Mathf.Max(1, Mathf.FloorToInt((rect.width + SpecialNodeGap) / step));
            int rows = Mathf.Max(1, Mathf.CeilToInt(specialNodes.Count / (float)columns));
            float contentHeight = rows * step - SpecialNodeGap;
            Rect contentRect = new(rect.x, rect.y + 7f, rect.width, rect.height - 7f);
            Rect viewRect = new(0f, 0f, contentRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(contentRect, ref specialScrollPosition, viewRect);

            for (int i = 0; i < specialNodes.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                int rowCount = Mathf.Min(columns, specialNodes.Count - row * columns);
                float rowWidth = rowCount * step - SpecialNodeGap;
                float x = (viewRect.width - rowWidth) * 0.5f + column * step;
                float y = row * step;
                Rect nodeRect = new(x, y, SpecialNodeIconSize, SpecialNodeIconSize);
                int effectiveLevel = Mathf.Min(
                    MX_QH_HediffUtility.GetDivineGraceLevel(pawn),
                    QinghePowerBalance.MaxEffectiveLevel);
                DrawNodeCard(specialNodes[i], nodeRect, effectiveLevel, compact: true);
            }

            Widgets.EndScrollView();
        }

        private void DrawNodeCard(SkillNodeDef node, Rect rect, int currentLevel, bool compact)
        {
            bool locked = node.requiredGraceLevel > currentLevel;
            bool learned = state != null
                && !locked
                && state.HasNode(node);
            Rect iconRect = compact
                ? new Rect(rect.x + (rect.width - NodeIconSize) * 0.5f, rect.y + 6f, NodeIconSize, NodeIconSize)
                : new Rect(rect.x + 6f, rect.y + (rect.height - NodeIconSize) * 0.5f, NodeIconSize, NodeIconSize);

            Widgets.DrawBoxSolid(rect, NodeBackColor);
            GUI.color = learned ? LearnedBorderColor : LockedBorderColor;
            Widgets.DrawBox(rect, learned ? 2 : 1);
            GUI.color = Color.white;

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            GUI.color = learned ? Color.white : LockedIconColor;
            GUI.DrawTexture(iconRect, node.ResolveIcon(), ScaleMode.ScaleToFit, true);
            GUI.color = Color.white;

            if (!compact)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = learned ? Color.white : LockedLabelColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new(iconRect.xMax + 6f, rect.y + 5f, rect.width - iconRect.width - 18f, rect.height - 10f);
                string label = node.LabelCap.ToString();
                if (locked)
                {
                    label += "\n已锁定";
                }
                else if (!learned)
                {
                    label += "\n未习得";
                }
                Widgets.Label(labelRect, label);
            }

            TooltipHandler.TipRegion(rect, BuildNodeTip(node, learned, locked));
            ResetGui();
        }

        private static string BuildNodeTip(SkillNodeDef node, bool learned, bool locked)
        {
            string tip = node.LabelCap.ToString() + "\n\n" + node.description;
            string stateText = learned
                ? "MX_QH_FlowerCourtNodeLearned".Translate()
                : locked
                    ? "MX_QH_FlowerCourtNodeLocked".Translate(node.requiredGraceLevel)
                    : "未习得";
            return tip + "\n\n" + stateText;
        }

        private static string TooltipForResonance(FlowerBellResonance resonance, int cooldownRemainingTicks)
        {
            string tip = resonance switch
            {
                FlowerBellResonance.Spring => "MX_QH_FlowerBellResonanceDescriptionSpring".Translate(),
                FlowerBellResonance.Summer => "MX_QH_FlowerBellResonanceDescriptionSummer".Translate(),
                FlowerBellResonance.Autumn => "MX_QH_FlowerBellResonanceDescriptionAutumn".Translate(),
                FlowerBellResonance.Winter => "MX_QH_FlowerBellResonanceDescriptionWinter".Translate(),
                _ => null,
            };
            if (cooldownRemainingTicks > 0)
            {
                tip += "\n\n" + "MX_QH_TuneCooldownRemaining".Translate((cooldownRemainingTicks / 2500f).ToString("F1"));
            }

            return tip;
        }

        private static Color ColorFor(FlowerBellResonance resonance)
        {
            return resonance switch
            {
                FlowerBellResonance.Spring => new Color(1f, 0.58f, 0.74f, 1f),
                FlowerBellResonance.Summer => new Color(1f, 0.78f, 0.34f, 1f),
                FlowerBellResonance.Autumn => new Color(1f, 0.55f, 0.30f, 1f),
                FlowerBellResonance.Winter => new Color(0.48f, 0.78f, 1f, 1f),
                _ => Color.white,
            };
        }

        private static void ResetGui()
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}
