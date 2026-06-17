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

        public static Gizmo BuildFlowerDivinationGizmo(Pawn pawn)
        {
            HediffComp_FlowerDivination divination = FlowerCourtUtility.EnsureFlowerDivination(pawn);
            Command_Action command = new Command_Action
            {
                defaultLabel = divination?.Label ?? "花神降临",
                defaultDesc = BuildDivinationTip(divination),
                icon = TexCommand.DesirePower,
                action = delegate
                {
                    TryStartDivination(pawn, divination);
                }
            };

            if (divination == null)
            {
                command.Disable("清荷尚未建立花神庭。");
            }
            else if (!divination.CanStartDivination(out string reason))
            {
                command.Disable(reason);
            }

            return command;
        }

        private static void TryStartDivination(Pawn pawn, HediffComp_FlowerDivination divination)
        {
            if (divination == null)
            {
                Messages.Message("清荷尚未建立花神庭。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (!divination.CanStartDivination(out string reason))
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (MX_QHDefOf.MX_QH_FlowerDivination == null || pawn?.jobs == null)
            {
                Messages.Message("花神降临尚未准备好。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Job job = JobMaker.MakeJob(MX_QHDefOf.MX_QH_FlowerDivination);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private static string BuildDivinationTip(HediffComp_FlowerDivination divination)
        {
            if (divination == null)
            {
                return "花神降临\n\n清荷尚未建立花神庭。";
            }

            string tip = divination.Label;
            if (divination.Active)
            {
                tip += "\n\n当前状态: 降临中"
                       + "\n剩余时间: " + divination.ActiveTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
                return tip;
            }

            if (divination.OnCooldown)
            {
                tip += "\n\n当前状态: 冷却中"
                       + "\n剩余时间: " + divination.CooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
                return tip;
            }

            if (divination.CanStartDivination(out string reason))
            {
                return tip + "\n\n当前状态: 可用";
            }

            return tip + "\n\n" + reason;
        }
    }

    [StaticConstructorOnStartup]
    public class Gizmo_QH_FlowerResources : Gizmo
    {
        private const int FlowerDecreeTipSalt = 910202;
        private const int ShieldTipSalt = 910203;
        private const float BarLeftPadding = 10f;
        private const float BarRightPadding = 8f;
        private const float ResourceBarWidth = 150f;
        private const float FlowerDecreeOffsetY = 8f;
        private const float FlowerDecreeHeight = 12f;
        private const float ShieldOffsetY = 36f;
        private const float ShieldHeight = 12f;
        private const float FlowerDecreeSegmentGap = 2f;

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
            CompLotusShield lotusShield = pawn?.GetComp<CompLotusShield>();
            Color accentColor = new Color(0.72f, 0.86f, 0.76f, 1f);

            Rect decreeRect = DrawFlowerDecreeRow(inner, FlowerDecreeOffsetY, flowerDecree);
            Rect shieldRect = DrawShieldBar(inner, ShieldOffsetY, lotusShield, accentColor);

            TooltipHandler.TipRegion(decreeRect, () => BuildFlowerDecreeTip(flowerDecree), GetStableTipId(FlowerDecreeTipSalt));
            TooltipHandler.TipRegion(shieldRect, () => BuildShieldBarTip(lotusShield), GetStableTipId(ShieldTipSalt));
            return new GizmoResult(GizmoState.Clear);
        }

        private static Rect DrawFlowerDecreeRow(Rect inner, float offsetY, HediffComp_FlowerDecree comp)
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
            Color decreeColor = FlowerDecreeBaseColor;
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
            float availableWidth = inner.width - BarLeftPadding - BarRightPadding;
            float width = Mathf.Min(ResourceBarWidth, availableWidth);
            return new Rect(inner.x + BarLeftPadding, inner.y + offsetY, width, height);
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

    }
}
