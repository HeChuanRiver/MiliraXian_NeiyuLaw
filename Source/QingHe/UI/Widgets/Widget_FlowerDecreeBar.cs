using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.UI;
using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_FlowerDecreeBar : Widget_Base
    {
        private const int TipSalt = 910202;
        private const float BarLeftPadding = 10f;
        private const float BarRightPadding = 8f;
        private const float ResourceBarWidth = 150f;
        private const float BarHeight = 12f;
        private const float SegmentGap = 2f;

        private readonly Pawn pawn;

        private static readonly Color SegmentEmptyColor = new Color(0.03f, 0.035f, 0.05f, 1f);
        private static readonly Color FlowerDecreeBaseColor = new Color(0.88f, 0.42f, 0.62f, 1f);
        private static readonly Color FlowerDecreeHighlightColor = new Color(1f, 0.90f, 0.74f, 1f);

        public Widget_FlowerDecreeBar(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_FlowerDecree comp = PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
            Rect barRect = GetResourceBarRect(rect, BarHeight);
            DrawBar(barRect, comp);

            TooltipHandler.TipRegion(barRect, () => BuildTip(comp), GetStableTipId());
            if (Mouse.IsOver(barRect))
            {
                Widgets.DrawHighlight(barRect, 0.45f);
            }
        }

        private static void DrawBar(Rect barRect, HediffComp_FlowerDecree comp)
        {
            float valuePerDecree = Mathf.Max(1f, comp?.ValuePerDecree ?? 100f);
            int max = Mathf.Max(1, Mathf.RoundToInt((comp?.MaxValue ?? 300f) / valuePerDecree));
            float currentValue = Mathf.Clamp(comp?.CurrentValue ?? 0f, 0f, comp?.MaxValue ?? 300f);
            int fullSegments = Mathf.Clamp(Mathf.FloorToInt(currentValue / valuePerDecree), 0, max);
            float partialPercent = Mathf.Clamp01((currentValue - fullSegments * valuePerDecree) / valuePerDecree);
            float segmentWidth = (barRect.width - SegmentGap * (max - 1)) / max;
            float highlight = comp?.HighlightPercent ?? 0f;
            int highlightedSegment = highlight > 0.0001f ? Mathf.Clamp(fullSegments - 1, -1, max - 1) : -1;

            for (int i = 0; i < max; i++)
            {
                Rect segmentRect = new Rect(barRect.x + i * (segmentWidth + SegmentGap), barRect.y, segmentWidth, barRect.height);
                Widgets.DrawBoxSolid(segmentRect, Color.black);
                Rect contentRect = new Rect(segmentRect.x + 1f, segmentRect.y + 1f, segmentRect.width - 2f, segmentRect.height - 2f);
                Widgets.DrawBoxSolid(contentRect, SegmentEmptyColor);

                if (i < fullSegments)
                {
                    bool latestFilledSegment = i == highlightedSegment;
                    Color fill = latestFilledSegment ? Color.Lerp(FlowerDecreeBaseColor, FlowerDecreeHighlightColor, highlight) : FlowerDecreeBaseColor;
                    Widgets.DrawBoxSolid(contentRect, fill);
                }
                else if (i == fullSegments && i < max && partialPercent > 0.0001f)
                {
                    Rect progressRect = new Rect(contentRect.x, contentRect.y, contentRect.width * partialPercent, contentRect.height);
                    Color progress = FlowerDecreeBaseColor;
                    progress.a = 0.65f;
                    Widgets.DrawBoxSolid(progressRect, progress);
                }
            }
        }

        private static Rect GetResourceBarRect(Rect rect, float height)
        {
            float availableWidth = rect.width - BarLeftPadding - BarRightPadding;
            float width = Mathf.Min(ResourceBarWidth, availableWidth);
            return new Rect(rect.x + BarLeftPadding, rect.y + 2f, width, height);
        }

        private string BuildTip(HediffComp_FlowerDecree comp)
        {
            if (comp == null)
            {
                return "花令: 0 / 3";
            }

            int current = Mathf.FloorToInt(comp.CurrentResourceValue);
            int max = Mathf.FloorToInt(comp.MaxResourceValue);
            int recoveryProgress = Mathf.FloorToInt(comp.RecoveryProgress);
            int recoveryProgressMax = Mathf.FloorToInt(comp.RecoveryProgressMax);
            string tip = comp.ResourceLabel + ": " + current.ToString() + " / " + max.ToString()
                         + "\n恢复进度: " + recoveryProgress.ToString() + " / " + recoveryProgressMax.ToString()
                         + "\n恢复速度: " + comp.CurrentRecoveryProgressPerSecond.ToString("F2") + " /s";
            if (!comp.ResourceDescription.NullOrEmpty())
            {
                tip += "\n\n" + comp.ResourceDescription;
            }

            return tip;
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }
}
