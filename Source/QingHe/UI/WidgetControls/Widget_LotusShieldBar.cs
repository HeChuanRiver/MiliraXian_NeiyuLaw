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
        private const float BarHeight = 9f;
        private static readonly RectOffset BarMargin = new RectOffset((int)BarLeftPadding, (int)BarRightPadding, 0, 0);

        private readonly Pawn pawn;

        private static readonly Color SeasonColor = new Color(0.72f, 0.86f, 0.76f, 1f);
        private static readonly Color OuterBorderColor = new Color(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color ShieldBackgroundColor = new Color(0.16f, 0.17f, 0.18f, 1f);
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
            CompDivineProtectionShield shield = pawn?.GetComp<CompDivineProtectionShield>();
            Rect outerRect = GetResourceBarRect(rect, BarHeight);
            DrawBar(rect, outerRect, shield);

            TooltipHandler.TipRegion(outerRect, () => shield == null ? "MX_QH_ShieldInactive".Translate().ToString() : shield.BuildShieldTooltip(), GetStableTipId());
            if (Mouse.IsOver(outerRect))
            {
                Widgets.DrawHighlight(outerRect, 0.45f);
            }
        }

        private static void DrawBar(Rect widgetRect, Rect outerRect, CompDivineProtectionShield shield)
        {
            Widgets.DrawBoxSolid(outerRect, OuterBorderColor);

            Rect barRect = outerRect.ContractedBy(1f);
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

            DrawPercentLabel(new Rect(outerRect.x, widgetRect.y, outerRect.width, widgetRect.height), fillPercent);
        }

        private static void DrawPercentLabel(Rect rect, float fillPercent)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = fillPercent < 0.4f ? Color.white : Color.black;
            Widgets.Label(rect, Mathf.RoundToInt(fillPercent * 100f).ToString() + "%");

            GUI.color = oldColor;
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
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

        private Rect GetResourceBarRect(Rect rect, float height)
        {
            float availableWidth = rect.width - BarLeftPadding - BarRightPadding;
            float width = Mathf.Min(ResourceBarWidth, availableWidth);
            return GetAlignedRect(rect, new Vector2(width, height), BarMargin);
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }
}
