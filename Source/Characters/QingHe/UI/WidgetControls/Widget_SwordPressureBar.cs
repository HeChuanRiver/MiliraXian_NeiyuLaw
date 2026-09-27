using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.UI;
using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_SwordPressureBar : Widget_Base
    {
        private const int TipSalt = 910203;
        private const float SegmentGap = 2f;
        private readonly Pawn pawn;
        private HediffComp_SwordPressure cachedPressure;

        private static readonly Color EmptyColor = new(0.16f, 0.17f, 0.18f, 1f);
        private static readonly Color BorderColor = new(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color LowPressureColor = new(0.58f, 0.60f, 0.62f, 1f);
        private static readonly Color OnePressureColor = new(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color TwoPressureColor = new(1f, 0.90f, 0.55f, 1f);
        private static readonly Color FullPressureColor = new(1f, 0.58f, 0.58f, 1f);

        public Widget_SwordPressureBar(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_SwordPressure pressure = GetSwordPressureComp();
            Rect barRect = new(rect.x + 10f, rect.y + (rect.height - 9f) * 0.5f, Mathf.Min(150f, rect.width - 18f), 9f);
            DrawSegments(barRect, pressure);
            TooltipHandler.TipRegion(barRect, () => BuildTip(pressure), Gen.HashCombineInt(pawn?.thingIDNumber ?? 0, TipSalt));
            if (Mouse.IsOver(barRect))
            {
                Widgets.DrawHighlight(barRect, 0.45f);
            }
        }

        private static void DrawSegments(Rect barRect, HediffComp_SwordPressure pressure)
        {
            int max = Mathf.Max(1, Mathf.RoundToInt(pressure?.MaxResourceValue ?? 3f));
            float current = Mathf.Clamp(pressure?.CurrentResourceValue ?? 0f, 0f, max);
            Color filledColor = FilledColorFor(current, max);
            float segmentWidth = (barRect.width - SegmentGap * (max - 1)) / max;
            for (int i = 0; i < max; i++)
            {
                Rect segment = new(barRect.x + i * (segmentWidth + SegmentGap), barRect.y, segmentWidth, barRect.height);
                Widgets.DrawBoxSolid(segment, BorderColor);
                Rect content = segment.ContractedBy(1f);
                Widgets.DrawBoxSolid(content, EmptyColor);
                float fill = Mathf.Clamp01(current - i);
                if (fill > 0f)
                {
                    Color color = filledColor;
                    color.a = Mathf.Lerp(0.65f, 1f, fill);
                    Widgets.DrawBoxSolid(new Rect(content.x, content.y, content.width * fill, content.height), color);
                }
            }
        }

        private HediffComp_SwordPressure GetSwordPressureComp()
        {
            if (cachedPressure == null || cachedPressure.Pawn != pawn)
            {
                cachedPressure = PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_SwordPressure) as HediffComp_SwordPressure;
            }

            return cachedPressure;
        }

        private static Color FilledColorFor(float current, float max)
        {
            if (current >= max - 0.0001f)
            {
                return FullPressureColor;
            }
            if (current >= 2f)
            {
                return TwoPressureColor;
            }
            if (current >= 1f)
            {
                return OnePressureColor;
            }
            return LowPressureColor;
        }

        private static string BuildTip(HediffComp_SwordPressure pressure)
        {
            float current = pressure?.CurrentResourceValue ?? 0f;
            float max = pressure?.MaxResourceValue ?? 3f;
            return "MX_QH_SwordPressureValueLine".Translate(current.ToString("F2"), max.ToString("F0")).ToString();
        }
    }
}
