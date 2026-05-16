using RimWorld;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.UI
{
    public static class FlowerResourceGizmoFactory
    {
        public static Gizmo BuildResourceStatusGizmo(Pawn pawn)
        {
            return new Gizmo_QH_FlowerResources(pawn);
        }
    }

    [StaticConstructorOnStartup]
    public class Gizmo_QH_FlowerResources : Gizmo
    {
        private const int FlowerTidingsTipSalt = 910201;
        private const int FlowerDecreeTipSalt = 910202;
        private const int ShieldTipSalt = 910203;
        private const int DescentTipSalt = 910204;
        private const float BarLeftPadding = 10f;
        private const float BarRightPadding = 8f;
        private const float BarContentInset = 1f;
        private const float ResourceBarWidth = 100f;
        private const float FlowerTidingsOffsetY = 5f;
        private const float FlowerTidingsHeight = 16f;
        private const float FlowerDecreeOffsetY = 29f;
        private const float FlowerDecreeHeight = 8f;
        private const float ShieldOffsetY = 48f;
        private const float ShieldHeight = 11f;
        private const float FlowerDecreeSegmentGap = 2f;
        private const float DescentGap = 10f;

        private readonly Pawn pawn;

        private static readonly Color EmptyBarColor = new Color(0.03f, 0.035f, 0.05f, 1f);
        private static readonly Color SegmentEmptyColor = new Color(0.03f, 0.035f, 0.05f, 1f);
        private static readonly Color FlowerTidingsBaseColor = new Color(0.72f, 0.86f, 0.76f, 1f);
        private static readonly Color FlowerDecreeBaseColor = new Color(0.88f, 0.42f, 0.62f, 1f);
        private static readonly Color FlowerDecreeHighlightColor = new Color(1f, 0.90f, 0.74f, 1f);
        private static readonly Color ShieldBackgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static readonly Color ShieldBaseColor = new Color(0.55f, 0.7f, 1f, 1f);
        private static readonly Color ShieldFlowerColor = new Color(0.72f, 1f, 0.76f, 1f);
        private static readonly Color ShieldBreakDarkColor = new Color(0.22f, 0.05f, 0.06f, 1f);
        private static readonly Color ShieldBreakBrightColor = new Color(1f, 0.95f, 0.95f, 1f);

        public Gizmo_QH_FlowerResources(Pawn pawn)
        {
            this.pawn = pawn;
            Order = -99f;
        }

        public override float GetWidth(float maxWidth)
        {
            return 186f;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            var inner = rect.ContractedBy(6f);
            Widgets.DrawWindowBackground(rect);

            HediffComp_PawnSpecialResource flowerTidings = PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerTidings);
            HediffComp_FlowerDecree flowerDecree = PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
            HediffComp_SeasonResonance resonance = FlowerCourtUtility.EnsureSeasonResonance(pawn);
            CompLotusShield lotusShield = pawn?.GetComp<CompLotusShield>();
            AttunedSeason attunedSeason = resonance?.CurrentAttunedSeason ?? AttunedSeason.None;
            Color seasonColor = ResolveSeasonColor(attunedSeason);

            Rect tidingsRect = DrawResourceRow(inner, FlowerTidingsOffsetY, flowerTidings, "花信", attunedSeason);
            Rect decreeRect = DrawFlowerDecreeRow(inner, FlowerDecreeOffsetY, flowerDecree, attunedSeason);
            Rect shieldRect = DrawShieldBar(inner, ShieldOffsetY, lotusShield, flowerTidings, seasonColor);
            Rect descentRect = DrawDescentPlaceholder(inner, attunedSeason);

            TooltipHandler.TipRegion(tidingsRect, () => BuildResourceBarTip(flowerTidings, "花信"), GetStableTipId(FlowerTidingsTipSalt));
            TooltipHandler.TipRegion(decreeRect, () => BuildFlowerDecreeTip(flowerDecree), GetStableTipId(FlowerDecreeTipSalt));
            TooltipHandler.TipRegion(shieldRect, () => BuildShieldBarTip(lotusShield), GetStableTipId(ShieldTipSalt));
            TooltipHandler.TipRegion(descentRect, "花神降临");
            return new GizmoResult(GizmoState.Clear);
        }

        private static Rect DrawResourceRow(Rect inner, float offsetY, HediffComp_PawnSpecialResource comp, string fallbackLabel, AttunedSeason season)
        {
            var barRect = GetResourceBarRect(inner, offsetY, FlowerTidingsHeight);
            var current = comp?.CurrentValue ?? 0f;
            var max = Mathf.Max(1f, comp?.MaxValue ?? 100f);
            var percent = Mathf.Clamp01(current / max);
            DrawThinFillableBar(barRect, percent, ResolveTidingsColor(season), EmptyBarColor, 2f);

            if (Mouse.IsOver(barRect))
            {
                Widgets.DrawHighlight(barRect, 0.45f);
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, comp?.ResourceLabel ?? fallbackLabel);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            return barRect;
        }

        private static Rect DrawFlowerDecreeRow(Rect inner, float offsetY, HediffComp_FlowerDecree comp, AttunedSeason season)
        {
            var barRect = GetResourceBarRect(inner, offsetY, FlowerDecreeHeight);
            int max = Mathf.Max(1, Mathf.RoundToInt(comp?.MaxValue ?? 5f));
            int current = Mathf.Clamp(Mathf.FloorToInt(comp?.CurrentValue ?? 0f), 0, max);
            float gap = FlowerDecreeSegmentGap;
            //Rect contentRect = GetBarContentRect(barRect, 1f);
            float segmentWidth = (barRect.width - gap * (max - 1)) / max;
            float highlight = comp?.HighlightPercent ?? 0f;
            Color decreeColor = ResolveDecreeColor(season);
            for (int i = 0; i < max; i++)
            {
                Rect segmentRect = new Rect(barRect.x + i * (segmentWidth + gap), barRect.y, segmentWidth, barRect.height);
                Widgets.DrawBoxSolid(segmentRect, Color.black);
                Rect contentRect = new Rect(segmentRect.x + 1f, segmentRect.y + 1f, segmentRect.width - 2f, segmentRect.height - 2f);
                Widgets.DrawBoxSolid(contentRect, SegmentEmptyColor);
                if (i < current)
                {
                    bool latestFilledSegment = i == current - 1;
                    Color fill = latestFilledSegment ? Color.Lerp(decreeColor, FlowerDecreeHighlightColor, highlight) : decreeColor;
                    Widgets.DrawBoxSolid(contentRect, fill);
                }
                else if (i == current && current < max)
                {
                    float recoveryPercent = comp?.RecoveryProgressPercent ?? 0f;
                    if (recoveryPercent > 0.0001f)
                    {
                        var progressRect = new Rect(contentRect.x, contentRect.y, contentRect.width * recoveryPercent, contentRect.height);
                        Color progress = decreeColor;
                        progress.a = 0.5f;
                        Widgets.DrawBoxSolid(progressRect, progress);
                    }
                }
            }

            if (Mouse.IsOver(barRect))
            {
                Widgets.DrawHighlight(barRect, 0.45f);
            }

            return barRect;
        }

        private static Rect DrawShieldBar(Rect inner, float offsetY, CompLotusShield shield, HediffComp_PawnSpecialResource flowerTidings, Color seasonColor)
        {
            var outerRect = GetResourceBarRect(inner, offsetY, ShieldHeight);
            Widgets.DrawBoxSolid(outerRect, Color.black);

            var barRect = new Rect(outerRect.x + 1f, outerRect.y + 1f, outerRect.width - 2f, outerRect.height - 2f);
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
                var fillRect = new Rect(barRect.x, barRect.y, barRect.width * fillPercent, barRect.height);
                Widgets.DrawBoxSolid(fillRect, ResolveShieldBarColor(flowerTidings, seasonColor));
            }

            // Hit-flash overlay: draw semi-transparent red on the filled portion after absorbing damage.
            if (shield != null && !shield.InBreak)
            {
                float flash = shield.AbsorbFlashPercent;
                if (flash > 0.001f)
                {
                    var flashRect = new Rect(barRect.x, barRect.y, barRect.width * fillPercent, barRect.height);
                    Color flashColor = new Color(1f, 0.3f, 0.3f, 0.35f * flash);
                    Widgets.DrawBoxSolid(flashRect, flashColor);
                }
            }

            if (Mouse.IsOver(outerRect))
            {
                Widgets.DrawHighlight(outerRect, 0.45f);
            }

            return outerRect;
        }

        private static void DrawThinFillableBar(Rect outerRect, float fillPercent, Color fillColor, Color backgroundColor, float verticalInset)
        {
            Widgets.DrawBoxSolid(outerRect, Color.black);
            Rect contentRect = GetBarContentRect(outerRect, verticalInset);
            Widgets.DrawBoxSolid(contentRect, backgroundColor);

            fillPercent = Mathf.Clamp01(fillPercent);
            if (fillPercent <= 0.0001f)
            {
                return;
            }

            var fillRect = new Rect(contentRect.x, contentRect.y, contentRect.width * fillPercent, contentRect.height);
            Widgets.DrawBoxSolid(fillRect, fillColor);
        }

        private static Rect GetBarContentRect(Rect outerRect, float verticalInset)
        {
            return new Rect(
                outerRect.x + BarContentInset,
                outerRect.y + verticalInset,
                Mathf.Max(0f, outerRect.width - BarContentInset * 2f),
                Mathf.Max(0f, outerRect.height - verticalInset * 2f));
        }

        private static Rect DrawDescentPlaceholder(Rect inner, AttunedSeason season)
        {
            float iconTop = inner.y + FlowerTidingsOffsetY;
            float iconHeight = ShieldOffsetY + ShieldHeight - FlowerTidingsOffsetY;
            var rect = new Rect(inner.xMax - iconHeight - 1f, iconTop, iconHeight, iconHeight);
            Color background = ResolveDescentColor(season);
            Widgets.DrawBoxSolid(rect, background);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect, "花神\n降临");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            return rect;
        }

        private static void DrawBreakBackground(Rect barRect)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            float pulse = 0.5f + 0.5f * Mathf.Sin(tick / 8f);
            float highlight = Mathf.Clamp01(0.32f + pulse * 0.6f);
            Widgets.DrawBoxSolid(barRect, Color.Lerp(ShieldBreakDarkColor, ShieldBreakBrightColor, highlight));
        }

        private static Color ResolveShieldBarColor(HediffComp_PawnSpecialResource flowerTidings, Color seasonColor)
        {
            float percent = flowerTidings?.ValuePercent ?? 0f;
            Color flowerColor = Color.Lerp(ShieldFlowerColor, seasonColor, 0.45f);
            return Color.Lerp(ShieldBaseColor, flowerColor, percent);
        }

        private static Rect GetResourceBarRect(Rect inner, float offsetY, float height)
        {
            float iconHeight = ShieldOffsetY + ShieldHeight - FlowerTidingsOffsetY;
            float availableWidth = inner.width - iconHeight - DescentGap - BarLeftPadding - BarRightPadding;
            float width = Mathf.Min(ResourceBarWidth, availableWidth);
            return new Rect(inner.x + BarLeftPadding, inner.y + offsetY, width, height);
        }

        private static Color ResolveSeasonColor(AttunedSeason season)
        {
            switch (season)
            {
                case AttunedSeason.Spring:
                    return new Color(0.58f, 0.88f, 0.56f, 1f);
                case AttunedSeason.Summer:
                    return new Color(1.00f, 0.48f, 0.72f, 1f);
                case AttunedSeason.Autumn:
                    return new Color(0.82f, 0.62f, 0.28f, 1f);
                case AttunedSeason.Winter:
                    return new Color(0.42f, 0.66f, 0.86f, 1f);
                default:
                    return new Color(0.72f, 0.86f, 0.76f, 1f);
            }
        }

        private static Color ResolveTidingsColor(AttunedSeason season)
        {
            if (season == AttunedSeason.None)
            {
                return FlowerTidingsBaseColor;
            }

            return Color.Lerp(FlowerTidingsBaseColor, ResolveSeasonColor(season), 0.65f);
        }

        private static Color ResolveDecreeColor(AttunedSeason season)
        {
            if (season == AttunedSeason.None)
            {
                return FlowerDecreeBaseColor;
            }

            return Color.Lerp(FlowerDecreeBaseColor, ResolveSeasonColor(season), 0.65f);
        }

        private static Color ResolveDescentColor(AttunedSeason season)
        {
            switch (season)
            {
                case AttunedSeason.Spring:
                    return new Color(0.58f, 0.24f, 0.34f, 1f);
                case AttunedSeason.Summer:
                    return new Color(0.58f, 0.20f, 0.15f, 1f);
                case AttunedSeason.Autumn:
                    return new Color(0.45f, 0.34f, 0.16f, 1f);
                case AttunedSeason.Winter:
                    return new Color(0.22f, 0.34f, 0.46f, 1f);
                default:
                    return new Color(0.24f, 0.34f, 0.28f, 1f);
            }
        }

        private int GetStableTipId(int salt)
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, salt);
        }

        private static string BuildResourceBarTip(HediffComp_PawnSpecialResource comp, string fallbackLabel)
        {
            string label = comp?.ResourceLabel ?? fallbackLabel;
            float current = comp?.CurrentValue ?? 0f;
            float max = Mathf.Max(1f, comp?.MaxValue ?? 100f);
            string tip = label + ": " + current.ToString("F0") + " / " + max.ToString("F0");
            if (comp != null && !comp.ResourceDescription.NullOrEmpty())
            {
                tip += "\n\n" + comp.ResourceDescription;
            }

            return tip;
        }

        private static string BuildFlowerDecreeTip(HediffComp_FlowerDecree comp)
        {
            if (comp == null)
            {
                return "花令: 0 / 5";
            }

            string tip = comp.ResourceLabel + ": " + comp.CurrentValue.ToString("F0") + " / " + comp.MaxValue.ToString("F0")
                         + "\n恢复进度: " + comp.RecoveryProgress.ToString("F0") + " / " + comp.RecoveryProgressMax.ToString("F0")
                         + "\n恢复速度: " + comp.CurrentRecoveryProgressPerSecond.ToString("F2") + " /s";
            if (!comp.ResourceDescription.NullOrEmpty())
            {
                tip += "\n\n" + comp.ResourceDescription;
            }

            return tip;
        }

        private static string BuildShieldBarTip(CompLotusShield shield)
        {
            return shield == null ? "护盾未激活" : shield.BuildShieldTooltip();
        }
    }
}
