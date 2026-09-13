using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Biography
{
    public sealed class ITab_Biography : ITab
    {
        private const float LeftPanelWidth = 220f;
        private const float PanelGap = 10f;
        private const float StoryRowHeight = 34f;
        private const int OpenTabEvaluationInterval = 60;

        private Vector2 leftScrollPosition;
        private Vector2 rightScrollPosition;
        private float rightContentHeight;
        private string selectedStoryName;
        private int lastPawnThingId = -1;
        private int lastEvaluationTick = -OpenTabEvaluationInterval;

        public ITab_Biography()
        {
            size = new Vector2(780f, 520f);
            labelKey = "MX_Biography_ITab_Label";
        }

        public override bool IsVisible
        {
            get
            {
                Pawn pawn = SelPawn;
                return pawn != null
                    && BiographyDatabase.TryGet(pawn.kindDef, out BiographyExtension extension)
                    && !extension.stories.NullOrEmpty();
            }
        }

        public override void OnOpen()
        {
            ResetForPawn(SelPawn);
            PreparePawn(SelPawn, forceEvaluation: true);
        }

        public override void TabUpdate()
        {
            PreparePawn(SelPawn, forceEvaluation: false);
        }

        protected override void UpdateSize()
        {
            size.y = Mathf.Min(520f, (float)(Verse.UI.screenHeight - 35) - 165f - 30f);
        }

        protected override void FillTab()
        {
            Pawn pawn = SelPawn;
            if (pawn == null || !BiographyDatabase.TryGet(pawn.kindDef, out BiographyExtension extension)
                || extension.stories.NullOrEmpty())
            {
                return;
            }

            if (pawn.thingIDNumber != lastPawnThingId)
            {
                ResetForPawn(pawn);
            }

            Hediff_BiographyTracker tracker = PreparePawn(pawn, forceEvaluation: false);
            BiographyStory selectedStory = EnsureSelectedStory(extension);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            Rect contentRect = new Rect(0f, 20f, size.x, size.y - 20f).ContractedBy(10f);
            Rect leftPanel = new(contentRect.x, contentRect.y, LeftPanelWidth, contentRect.height);
            Rect rightPanel = new(
                leftPanel.xMax + PanelGap,
                contentRect.y,
                contentRect.width - LeftPanelWidth - PanelGap,
                contentRect.height);

            DrawStoryList(leftPanel, extension, tracker);
            DrawStoryDetails(rightPanel, pawn, selectedStory, tracker);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private Hediff_BiographyTracker PreparePawn(Pawn pawn, bool forceEvaluation)
        {
            if (pawn == null || !BiographyDatabase.TryGet(pawn.kindDef, out BiographyExtension extension))
            {
                return null;
            }

            if (pawn.thingIDNumber != lastPawnThingId)
            {
                ResetForPawn(pawn);
                forceEvaluation = true;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            Hediff_BiographyTracker tracker = BiographyFrameworkUtility.GetOrCreateTracker(pawn);
            if (tracker != null && (forceEvaluation || currentTick - lastEvaluationTick >= OpenTabEvaluationInterval))
            {
                tracker.EvaluateUnlocks(extension, sendNotifications: true);
                lastEvaluationTick = currentTick;
            }

            return tracker;
        }

        private void ResetForPawn(Pawn pawn)
        {
            lastPawnThingId = pawn?.thingIDNumber ?? -1;
            lastEvaluationTick = -OpenTabEvaluationInterval;
            selectedStoryName = null;
            leftScrollPosition = Vector2.zero;
            rightScrollPosition = Vector2.zero;
            rightContentHeight = 0f;
        }

        private BiographyStory EnsureSelectedStory(BiographyExtension extension)
        {
            BiographyStory selected = extension.GetStory(selectedStoryName);
            if (selected != null)
            {
                return selected;
            }

            for (int i = 0; i < extension.stories.Count; i++)
            {
                if (extension.stories[i] != null)
                {
                    selectedStoryName = extension.stories[i].storyName;
                    rightScrollPosition = Vector2.zero;
                    return extension.stories[i];
                }
            }

            return null;
        }

        private void DrawStoryList(Rect panelRect, BiographyExtension extension, Hediff_BiographyTracker tracker)
        {
            Widgets.DrawMenuSection(panelRect);
            Rect outRect = panelRect.ContractedBy(6f);
            float viewHeight = Mathf.Max(outRect.height, extension.stories.Count * StoryRowHeight);
            Rect viewRect = new(0f, 0f, outRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(outRect, ref leftScrollPosition, viewRect);
            for (int i = 0; i < extension.stories.Count; i++)
            {
                BiographyStory story = extension.stories[i];
                if (story == null)
                {
                    continue;
                }

                Rect rowRect = new(0f, i * StoryRowHeight, viewRect.width, StoryRowHeight - 2f);
                bool selected = story.storyName == selectedStoryName;
                bool unlocked = tracker != null && tracker.IsStoryUnlocked(story.storyName);
                if (selected)
                {
                    Widgets.DrawHighlightSelected(rowRect);
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(rowRect);
                }

                Color previousColor = GUI.color;
                if (!unlocked)
                {
                    GUI.color = ColoredText.SubtleGrayColor;
                }

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rowRect.ContractedBy(8f, 0f), unlocked ? story.label : "???");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = previousColor;

                if (Widgets.ButtonInvisible(rowRect))
                {
                    selectedStoryName = story.storyName;
                    rightScrollPosition = Vector2.zero;
                }
            }

            Widgets.EndScrollView();
        }

        private void DrawStoryDetails(
            Rect panelRect,
            Pawn pawn,
            BiographyStory story,
            Hediff_BiographyTracker tracker)
        {
            Widgets.DrawMenuSection(panelRect);
            Rect outRect = panelRect.ContractedBy(10f);
            float viewWidth = outRect.width - 16f;
            Rect viewRect = new(0f, 0f, viewWidth, Mathf.Max(outRect.height, rightContentHeight));
            Widgets.BeginScrollView(outRect, ref rightScrollPosition, viewRect);

            float curY = 0f;
            if (story == null)
            {
                DrawWrappedText(ref curY, viewWidth, "MX_Biography_NoStorySelected".Translate(), 0f);
            }
            else if (tracker == null)
            {
                DrawHeading(ref curY, viewWidth, "MX_Biography_DataUnavailable".Translate());
            }
            else if (!tracker.IsStoryUnlocked(story.storyName))
            {
                DrawHeading(ref curY, viewWidth, "???");
                DrawSectionHeading(ref curY, viewWidth, "MX_Biography_UnlockCondition".Translate());
                string progress = story.unlockCondition != null
                    ? story.unlockCondition.GetProgressText(pawn, tracker)
                    : "MX_Biography_ConditionMissing".Translate().ToString();
                DrawWrappedText(ref curY, viewWidth, progress, 0f);
            }
            else
            {
                DrawHeading(ref curY, viewWidth, story.label);
                DrawWrappedText(ref curY, viewWidth, story.storyText, 12f);
                DrawRewards(ref curY, viewWidth, pawn, story, tracker);
            }

            if (Event.current.type == EventType.Layout)
            {
                rightContentHeight = Mathf.Max(outRect.height, curY + 4f);
            }

            Widgets.EndScrollView();
        }

        private static void DrawRewards(
            ref float curY,
            float width,
            Pawn pawn,
            BiographyStory story,
            Hediff_BiographyTracker tracker)
        {
            DrawSectionHeading(ref curY, width, "MX_Biography_Rewards".Translate());
            if (story.rewards.NullOrEmpty())
            {
                DrawWrappedText(ref curY, width, "MX_Biography_NoRewards".Translate(), 0f);
                return;
            }

            for (int i = 0; i < story.rewards.Count; i++)
            {
                BiographyReward reward = story.rewards[i];
                if (reward == null)
                {
                    continue;
                }

                bool claimed = tracker.IsRewardClaimed(story.storyName, reward.rewardName);
                string status = claimed
                    ? "MX_Biography_RewardClaimed".Translate()
                    : "MX_Biography_RewardUnclaimed".Translate();
                DrawWrappedText(ref curY, width, "- " + reward.GetDescription() + " - " + status, 2f);
            }

            curY += 6f;
            bool canClaim = tracker.CanClaimRewards(story, out string disabledReason);
            Rect buttonRect = new(0f, curY, Mathf.Min(220f, width), 32f);
            if (Widgets.ButtonText(
                    buttonRect,
                    "MX_Biography_ClaimRewards".Translate(),
                    drawBackground: true,
                    doMouseoverSound: true,
                    active: canClaim))
            {
                bool claimed = tracker.TryClaimRewards(story, out string resultMessage);
                Messages.Message(
                    resultMessage,
                    pawn,
                    claimed ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                    historical: false);
            }

            if (!canClaim && !disabledReason.NullOrEmpty())
            {
                TooltipHandler.TipRegion(buttonRect, disabledReason);
            }

            curY += buttonRect.height + 4f;
        }

        private static void DrawHeading(ref float curY, float width, string text)
        {
            Text.Font = GameFont.Medium;
            DrawWrappedText(ref curY, width, text, 10f);
            Text.Font = GameFont.Small;
        }

        private static void DrawSectionHeading(ref float curY, float width, string text)
        {
            curY += 8f;
            GUI.color = ColoredText.TipSectionTitleColor;
            DrawWrappedText(ref curY, width, text, 4f);
            GUI.color = Color.white;
        }

        private static void DrawWrappedText(ref float curY, float width, string text, float bottomGap)
        {
            string safeText = text ?? string.Empty;
            float height = Mathf.Max(Text.LineHeight, Text.CalcHeight(safeText, width));
            Widgets.Label(new Rect(0f, curY, width, height), safeText);
            curY += height + bottomGap;
        }
    }
}
