using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.Common.UI;
using UnityEngine;
using Verse;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.QingHe;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_FlowerDecreeBar : Widget_Base
    {
        private const int TipSalt = 910202;
        private const float BarLeftPadding = 10f;
        private const float BarRightPadding = 7f;
        private const float ResourceBarWidth = 150f;
        private const float BarHeight = 9f;
        private const float SegmentGap = 2f;
        private static readonly RectOffset BarMargin = new((int)BarLeftPadding, (int)BarRightPadding, 0, 0);

        private readonly Pawn pawn;
        private HediffComp_FlowerDecree cachedComp;

        private static readonly Color SegmentEmptyColor = new(0.16f, 0.17f, 0.18f, 1f);
        private static readonly Color OuterBorderColor = new(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color FlowerDecreeBaseColor = new(0.88f, 0.42f, 0.62f, 1f);
        private static readonly Color FlowerDecreeHighlightColor = new(1f, 0.90f, 0.74f, 1f);

        public Widget_FlowerDecreeBar(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_FlowerDecree comp = GetFlowerDecreeComp();
            float width = Mathf.Min(ResourceBarWidth, rect.width - BarLeftPadding - BarRightPadding);
            Rect barRect = GetAlignedRect(rect, new Vector2(width, BarHeight), BarMargin);
            DrawBar(barRect, comp, comp?.SegmentHighlightPercent ?? 0f);

            TooltipHandler.TipRegion(barRect, () => BuildTip(comp), Gen.HashCombineInt(pawn?.thingIDNumber ?? 0, TipSalt));
            if (Mouse.IsOver(barRect))
            {
                Widgets.DrawHighlight(barRect, 0.45f);
            }
        }

        private static void DrawBar(Rect barRect, HediffComp_FlowerDecree comp, float highlight)
        {
            int max = Mathf.Max(1, Mathf.RoundToInt(comp?.MaxValue ?? 3f));
            float currentValue = Mathf.Clamp(comp?.CurrentValue ?? 0f, 0f, comp?.MaxValue ?? 3f);
            int fullSegments = Mathf.Clamp(Mathf.FloorToInt(currentValue), 0, max);
            float partialPercent = Mathf.Clamp01(currentValue - fullSegments);
            float segmentWidth = (barRect.width - SegmentGap * (max - 1)) / max;
            int highlightedSegment = highlight > 0.0001f ? Mathf.Clamp(fullSegments - 1, -1, max - 1) : -1;

            for (int i = 0; i < max; i++)
            {
                Rect segmentRect = new(barRect.x + i * (segmentWidth + SegmentGap), barRect.y, segmentWidth, barRect.height);
                Widgets.DrawBoxSolid(segmentRect, OuterBorderColor);
                Rect contentRect = segmentRect.ContractedBy(1f);
                Widgets.DrawBoxSolid(contentRect, SegmentEmptyColor);

                if (i < fullSegments)
                {
                    bool latestFilledSegment = i == highlightedSegment;
                    Color fill = latestFilledSegment ? Color.Lerp(FlowerDecreeBaseColor, FlowerDecreeHighlightColor, highlight) : FlowerDecreeBaseColor;
                    Widgets.DrawBoxSolid(contentRect, fill);
                }
                else if (i == fullSegments && i < max && partialPercent > 0.0001f)
                {
                    Rect progressRect = new(contentRect.x, contentRect.y, contentRect.width * partialPercent, contentRect.height);
                    Color progress = FlowerDecreeBaseColor;
                    progress.a = 0.65f;
                    Widgets.DrawBoxSolid(progressRect, progress);
                }
            }
        }

        private HediffComp_FlowerDecree GetFlowerDecreeComp()
        {
            if (cachedComp == null || cachedComp.Pawn != pawn)
            {
                cachedComp = PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
            }

            return cachedComp;
        }

        private string BuildTip(HediffComp_FlowerDecree comp)
        {
            if (comp == null)
            {
                return "MX_QH_FlowerDecreeValueLine".Translate(0, 3);
            }

            int current = Mathf.FloorToInt(comp.CurrentValue);
            int max = Mathf.FloorToInt(comp.MaxValue);
            // One decimal: the value advances ~0.83 points per settle, so whole percent would
            // visibly skip a number every few steps.
            string recoveryProgress = (Mathf.Repeat(comp.CurrentValue, 1f) * 100f).ToString("F1");
            string tip = "MX_QH_FlowerDecreeValueLine".Translate(current, max).ToString()
                         + "\n" + "MX_QH_RecoveryProgressLine".Translate(recoveryProgress)
                         + "\n" + "MX_QH_RecoverySpeedLine".Translate(comp.RecoveryPerSecond.ToString("F2"));
            if (!comp.ResourceDescription.NullOrEmpty())
            {
                tip += "\n\n" + comp.ResourceDescription;
            }

            return tip;
        }
    }
}
