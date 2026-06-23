using MiliraXian.Characters.QingHe.Things;
using MiliraXian.Characters.UI;
using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_LotusShieldBar : Widget_Base
    {
        private const int TipSalt = 910203;
        private const float BarLeftPadding = 10f;
        private const float BarRightPadding = 8f;
        private const float ResourceBarWidth = 150f;
        private const float BarHeight = 12f;

        private readonly Pawn pawn;

        private static readonly Color SeasonColor = new Color(0.72f, 0.86f, 0.76f, 1f);
        private static readonly Color ShieldBackgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static readonly Color ShieldBaseColor = new Color(0.55f, 0.7f, 1f, 1f);
        private static readonly Color ShieldBreakDarkColor = new Color(0.22f, 0.05f, 0.06f, 1f);
        private static readonly Color ShieldBreakBrightColor = new Color(1f, 0.95f, 0.95f, 1f);

        public Widget_LotusShieldBar(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            CompLotusShield shield = pawn?.GetComp<CompLotusShield>();
            Rect outerRect = GetResourceBarRect(rect, BarHeight);
            DrawBar(outerRect, shield);

            TooltipHandler.TipRegion(outerRect, () => shield == null ? "护盾未激活" : shield.BuildShieldTooltip(), GetStableTipId());
            if (Mouse.IsOver(outerRect))
            {
                Widgets.DrawHighlight(outerRect, 0.45f);
            }
        }

        private static void DrawBar(Rect outerRect, CompLotusShield shield)
        {
            Widgets.DrawBoxSolid(outerRect, Color.black);

            Rect barRect = new Rect(outerRect.x + 1f, outerRect.y + 1f, outerRect.width - 2f, outerRect.height - 2f);
            if (shield != null && shield.InBreak)
            {
                DrawBreakBackground(barRect);
            }
            else
            {
                Widgets.DrawBoxSolid(barRect, ShieldBackgroundColor);
            }

            float fillPercent = shield?.MaxEnergy > 0f ? Mathf.Clamp01(shield.Energy / shield.MaxEnergy) : 0f;
            if (fillPercent > 0.0001f)
            {
                Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * fillPercent, barRect.height);
                Widgets.DrawBoxSolid(fillRect, ResolveShieldBarColor());
            }

            if (shield != null && !shield.InBreak)
            {
                float flash = shield.AbsorbFlashPercent;
                if (flash > 0.001f)
                {
                    Rect flashRect = new Rect(barRect.x, barRect.y, barRect.width * fillPercent, barRect.height);
                    Color flashColor = new Color(1f, 0.3f, 0.3f, 0.35f * flash);
                    Widgets.DrawBoxSolid(flashRect, flashColor);
                }
            }
        }

        private static void DrawBreakBackground(Rect barRect)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            float pulse = 0.5f + 0.5f * Mathf.Sin(tick / 8f);
            float highlight = Mathf.Clamp01(0.32f + pulse * 0.6f);
            Widgets.DrawBoxSolid(barRect, Color.Lerp(ShieldBreakDarkColor, ShieldBreakBrightColor, highlight));
        }

        private static Color ResolveShieldBarColor()
        {
            return Color.Lerp(ShieldBaseColor, SeasonColor, 0.35f);
        }

        private static Rect GetResourceBarRect(Rect rect, float height)
        {
            float availableWidth = rect.width - BarLeftPadding - BarRightPadding;
            float width = Mathf.Min(ResourceBarWidth, availableWidth);
            return new Rect(rect.x + BarLeftPadding, rect.y + 4f, width, height);
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }
}
