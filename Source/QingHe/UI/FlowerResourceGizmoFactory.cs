using RimWorld;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things;
using UnityEngine;
using Verse;
using Verse.AI;

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
        private const int FlowerDecreeTipSalt = 910202;
        private const int ShieldTipSalt = 910203;
        private const int DescentTipSalt = 910204;
        private const float BarLeftPadding = 10f;
        private const float BarRightPadding = 8f;
        private const float ResourceBarWidth = 100f;
        private const float FlowerDecreeOffsetY = 8f;
        private const float FlowerDecreeHeight = 12f;
        private const float ShieldOffsetY = 36f;
        private const float ShieldHeight = 12f;
        private const float FlowerDecreeSegmentGap = 2f;
        private const float DescentGap = 10f;

        private readonly Pawn pawn;

        private static readonly Color SegmentEmptyColor = new Color(0.03f, 0.035f, 0.05f, 1f);
        private static readonly Color FlowerDecreeBaseColor = new Color(0.88f, 0.42f, 0.62f, 1f);
        private static readonly Color FlowerDecreeHighlightColor = new Color(1f, 0.90f, 0.74f, 1f);
        private static readonly Color ShieldBackgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static readonly Color ShieldBaseColor = new Color(0.55f, 0.7f, 1f, 1f);
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
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 64f);
            var inner = rect.ContractedBy(6f);
            Widgets.DrawWindowBackground(rect);

            HediffComp_FlowerDecree flowerDecree = PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
            HediffComp_SeasonResonance resonance = FlowerCourtUtility.EnsureSeasonResonance(pawn);
            HediffComp_FlowerGodDescent descent = resonance?.FlowerGodDescent;
            CompLotusShield lotusShield = pawn?.GetComp<CompLotusShield>();
            AttunedSeason attunedSeason = resonance?.CurrentAttunedSeason ?? AttunedSeason.None;
            Color seasonColor = ResolveSeasonColor(attunedSeason);

            Rect decreeRect = DrawFlowerDecreeRow(inner, FlowerDecreeOffsetY, flowerDecree, attunedSeason);
            Rect shieldRect = DrawShieldBar(inner, ShieldOffsetY, lotusShield, seasonColor);
            Rect descentRect = DrawDescentButton(inner, attunedSeason, descent);
            HandleDescentInput(descentRect, descent);

            TooltipHandler.TipRegion(decreeRect, () => BuildFlowerDecreeTip(flowerDecree), GetStableTipId(FlowerDecreeTipSalt));
            TooltipHandler.TipRegion(shieldRect, () => BuildShieldBarTip(lotusShield), GetStableTipId(ShieldTipSalt));
            TooltipHandler.TipRegion(descentRect, () => BuildDescentTip(descent), GetStableTipId(DescentTipSalt));
            return new GizmoResult(GizmoState.Clear);
        }

        private static Rect DrawFlowerDecreeRow(Rect inner, float offsetY, HediffComp_FlowerDecree comp, AttunedSeason season)
        {
            var barRect = GetResourceBarRect(inner, offsetY, FlowerDecreeHeight);
            float valuePerDecree = Mathf.Max(1f, comp?.ValuePerDecree ?? 100f);
            int max = Mathf.Max(1, Mathf.RoundToInt((comp?.MaxValue ?? 500f) / valuePerDecree));
            float currentValue = Mathf.Clamp(comp?.CurrentValue ?? 0f, 0f, comp?.MaxValue ?? 500f);
            int fullSegments = Mathf.Clamp(Mathf.FloorToInt(currentValue / valuePerDecree), 0, max);
            float partialPercent = Mathf.Clamp01((currentValue - fullSegments * valuePerDecree) / valuePerDecree);
            float gap = FlowerDecreeSegmentGap;
            float segmentWidth = (barRect.width - gap * (max - 1)) / max;
            float highlight = comp?.HighlightPercent ?? 0f;
            Color decreeColor = ResolveDecreeColor(season);
            int highlightedSegment = highlight > 0.0001f ? Mathf.Clamp(fullSegments - 1, -1, max - 1) : -1;
            for (int i = 0; i < max; i++)
            {
                Rect segmentRect = new Rect(barRect.x + i * (segmentWidth + gap), barRect.y, segmentWidth, barRect.height);
                Widgets.DrawBoxSolid(segmentRect, Color.black);
                Rect contentRect = new Rect(segmentRect.x + 1f, segmentRect.y + 1f, segmentRect.width - 2f, segmentRect.height - 2f);
                Widgets.DrawBoxSolid(contentRect, SegmentEmptyColor);
                if (i < fullSegments)
                {
                    bool latestFilledSegment = i == highlightedSegment;
                    Color fill = latestFilledSegment ? Color.Lerp(decreeColor, FlowerDecreeHighlightColor, highlight) : decreeColor;
                    Widgets.DrawBoxSolid(contentRect, fill);
                }
                else if (i == fullSegments && i < max && partialPercent > 0.0001f)
                {
                    var progressRect = new Rect(contentRect.x, contentRect.y, contentRect.width * partialPercent, contentRect.height);
                    Color progress = decreeColor;
                    progress.a = 0.65f;
                    Widgets.DrawBoxSolid(progressRect, progress);
                }
            }

            if (Mouse.IsOver(barRect))
            {
                Widgets.DrawHighlight(barRect, 0.45f);
            }

            return barRect;
        }

        private static Rect DrawShieldBar(Rect inner, float offsetY, CompLotusShield shield, Color seasonColor)
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
                Widgets.DrawBoxSolid(fillRect, ResolveShieldBarColor(seasonColor));
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

        private static Rect DrawDescentButton(Rect inner, AttunedSeason season, HediffComp_FlowerGodDescent descent)
        {
            float iconTop = inner.y + FlowerDecreeOffsetY;
            float iconHeight = ShieldOffsetY + ShieldHeight - FlowerDecreeOffsetY;
            var rect = new Rect(inner.xMax - iconHeight - 1f, iconTop, iconHeight, iconHeight);
            Color background = ResolveDescentColor(season);
            if (descent == null || !descent.CanStartDescent(out _))
            {
                background = Color.Lerp(background, Color.black, 0.45f);
            }

            if (descent?.Active == true)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin((Find.TickManager?.TicksGame ?? 0) / 7f);
                background = Color.Lerp(background, Color.white, 0.18f + pulse * 0.18f);
            }

            Widgets.DrawBoxSolid(rect, background);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect, 0.35f);
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(rect, ResolveDescentLabel(descent));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            return rect;
        }

        private void HandleDescentInput(Rect rect, HediffComp_FlowerGodDescent descent)
        {
            if (!Widgets.ButtonInvisible(rect, false))
            {
                return;
            }

            if (descent == null)
            {
                Messages.Message("清荷尚未建立四时共鸣。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (!descent.CanStartDescent(out string reason))
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (MX_QHDefOf.MX_QH_FlowerGodDescent == null || pawn?.jobs == null)
            {
                Messages.Message("花神降临尚未准备好。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Verse.AI.Job job = JobMaker.MakeJob(MX_QHDefOf.MX_QH_FlowerGodDescent);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private static void DrawBreakBackground(Rect barRect)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            float pulse = 0.5f + 0.5f * Mathf.Sin(tick / 8f);
            float highlight = Mathf.Clamp01(0.32f + pulse * 0.6f);
            Widgets.DrawBoxSolid(barRect, Color.Lerp(ShieldBreakDarkColor, ShieldBreakBrightColor, highlight));
        }

        private static Color ResolveShieldBarColor(Color seasonColor)
        {
            return Color.Lerp(ShieldBaseColor, seasonColor, 0.35f);
        }

        private static Rect GetResourceBarRect(Rect inner, float offsetY, float height)
        {
            float iconHeight = ShieldOffsetY + ShieldHeight - FlowerDecreeOffsetY;
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

        private static string BuildFlowerDecreeTip(HediffComp_FlowerDecree comp)
        {
            if (comp == null)
            {
                return "花令: 0 / 5";
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

        private static string BuildShieldBarTip(CompLotusShield shield)
        {
            return shield == null ? "护盾未激活" : shield.BuildShieldTooltip();
        }

        private static string ResolveDescentLabel(HediffComp_FlowerGodDescent descent)
        {
            if (descent == null)
            {
                return "花神\n降临";
            }

            if (descent.Active)
            {
                return "降临\n" + descent.ActiveTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            if (descent.OnCooldown)
            {
                return "冷却\n" + descent.CooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            return "花神\n降临";
        }

        private static string BuildDescentTip(HediffComp_FlowerGodDescent descent)
        {
            if (descent == null)
            {
                return "花神降临\n\n清荷尚未建立四时共鸣。";
            }

            string tip = descent.Label;
            if (descent.Active)
            {
                tip += "\n\n当前状态: 降临中"
                       + "\n剩余时间: " + descent.ActiveTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
                return tip;
            }

            if (descent.OnCooldown)
            {
                tip += "\n\n当前状态: 冷却中"
                       + "\n剩余时间: " + descent.CooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
                return tip;
            }

            if (descent.CanStartDescent(out string reason))
            {
                return tip + "\n\n当前状态: 可用";
            }

            return tip + "\n\n" + reason;
        }
    }
}
