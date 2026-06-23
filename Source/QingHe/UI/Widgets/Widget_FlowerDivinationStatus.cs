using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_FlowerDivinationStatus : Widget_Base
    {
        private const int TipSalt = 910204;
        private const float BarHeight = 12f;
        private const float ButtonWidth = 58f;

        private readonly Pawn pawn;

        private static readonly Color ReadyColor = new Color(0.74f, 0.90f, 0.78f, 1f);
        private static readonly Color ActiveColor = new Color(0.96f, 0.62f, 0.78f, 1f);
        private static readonly Color CooldownColor = new Color(0.45f, 0.58f, 0.72f, 1f);
        private static readonly Color DisabledColor = new Color(0.28f, 0.28f, 0.30f, 1f);
        private static readonly Color BarBackgroundColor = new Color(0.06f, 0.065f, 0.08f, 1f);

        public Widget_FlowerDivinationStatus(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_FlowerDivination divination = FlowerCourtUtility.EnsureFlowerDivination(pawn);
            DrawHeader(rect, divination);
            Rect barRect = DrawProgressBar(rect, divination);
            Rect buttonRect = DrawActionButton(rect, divination);

            TooltipHandler.TipRegion(rect, () => BuildTip(divination), GetStableTipId());
            if (Mouse.IsOver(barRect) || Mouse.IsOver(buttonRect))
            {
                Widgets.DrawHighlight(rect, 0.25f);
            }
        }

        private static void DrawHeader(Rect rect, HediffComp_FlowerDivination divination)
        {
            Rect labelRect = new Rect(rect.x + 4f, rect.y + 1f, rect.width - ButtonWidth - 10f, 22f);
            Text.Font = GameFont.Small;
            Widgets.Label(labelRect, divination?.Label ?? "花神降临");
        }

        private static Rect DrawProgressBar(Rect rect, HediffComp_FlowerDivination divination)
        {
            Rect barOuter = new Rect(rect.x + 4f, rect.y + 32f, rect.width - ButtonWidth - 14f, BarHeight);
            Widgets.DrawBoxSolid(barOuter, Color.black);
            Rect barRect = barOuter.ContractedBy(1f);
            Widgets.DrawBoxSolid(barRect, BarBackgroundColor);

            float fillPercent = ResolveFillPercent(divination);
            if (fillPercent > 0.0001f)
            {
                Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * fillPercent, barRect.height);
                Widgets.DrawBoxSolid(fillRect, ResolveBarColor(divination));
            }

            return barOuter;
        }

        private Rect DrawActionButton(Rect rect, HediffComp_FlowerDivination divination)
        {
            Rect buttonRect = new Rect(rect.xMax - ButtonWidth - 4f, rect.y + 20f, ButtonWidth, 28f);
            string label = ResolveButtonLabel(divination);
            bool canStart = divination != null && divination.CanStartDivination(out _);

            Color oldColor = GUI.color;
            if (!canStart)
            {
                GUI.color = DisabledColor;
            }
            bool clicked = Widgets.ButtonText(buttonRect, label, drawBackground: true, doMouseoverSound: true, active: canStart);
            GUI.color = oldColor;

            if (clicked)
            {
                TryStartDivination(divination);
            }

            return buttonRect;
        }

        private void TryStartDivination(HediffComp_FlowerDivination divination)
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

        private static float ResolveFillPercent(HediffComp_FlowerDivination divination)
        {
            if (divination == null)
            {
                return 0f;
            }

            if (divination.Active)
            {
                return divination.ActiveRemainingPercent;
            }

            if (divination.OnCooldown)
            {
                return divination.CooldownReadyPercent;
            }

            return divination.Ready ? 1f : 0f;
        }

        private static Color ResolveBarColor(HediffComp_FlowerDivination divination)
        {
            if (divination == null)
            {
                return DisabledColor;
            }

            if (divination.Active)
            {
                return ActiveColor;
            }

            if (divination.OnCooldown)
            {
                return CooldownColor;
            }

            return ReadyColor;
        }

        private static string ResolveButtonLabel(HediffComp_FlowerDivination divination)
        {
            if (divination == null)
            {
                return "未就绪";
            }

            if (divination.Active)
            {
                return "降临中";
            }

            if (divination.OnCooldown)
            {
                return "冷却";
            }

            return "降临";
        }

        private static string BuildTip(HediffComp_FlowerDivination divination)
        {
            if (divination == null)
            {
                return "花神降临\n\n清荷尚未建立花神庭。";
            }

            string tip = divination.Label;
            if (divination.Active)
            {
                return tip
                       + "\n\n当前状态: 降临中"
                       + "\n剩余时间: " + divination.ActiveTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            if (divination.OnCooldown)
            {
                return tip
                       + "\n\n当前状态: 冷却中"
                       + "\n剩余时间: " + divination.CooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            if (divination.CanStartDivination(out string reason))
            {
                return tip + "\n\n当前状态: 可用";
            }

            return tip + "\n\n" + reason;
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }
}
