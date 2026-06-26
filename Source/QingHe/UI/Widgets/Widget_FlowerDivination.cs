using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_FlowerDivinationDiamond : DiamondWidget_Base
    {
        private const int TipSalt = 910208;
        private const float BorderThickness = 2f;
        private const float OuterPadding = 1f;

        private readonly Pawn pawn;

        private static readonly Color BorderColor = new Color(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color EmptyFillColor = new Color(0.08f, 0.09f, 0.09f, 0.9f);
        private static readonly Color MaskColor = new Color(0f, 0f, 0f, 0.58f);

        public Widget_FlowerDivinationDiamond(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            HediffComp_FlowerDivination divination = FlowerCourtUtility.EnsureFlowerDivination(pawn);
            bool learned = state?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerDance) == true;
            Rect diamondRect = GetAlignedRect(rect, new Vector2(Mathf.Min(rect.width, rect.height), Mathf.Min(rect.width, rect.height)), null).ContractedBy(OuterPadding);
            bool mouseOverDiamond = MouseIsOverHitbox(diamondRect);
            bool canStart = learned && divination != null && divination.CanStartDivination(out _) && mouseOverDiamond;
            Color tint = canStart ? GenUI.MouseoverColor : Color.white;
            Rect innerRect = diamondRect.ContractedBy(BorderThickness);

            DrawDiamond(diamondRect, MX_QHRenderStatics.DiamondSolidTex, BorderColor);
            DrawDiamond(innerRect, MX_QHRenderStatics.DiamondSolidTex, EmptyFillColor);

            if (learned)
            {
                DrawDiamond(innerRect, MX_QHRenderStatics.FlowerDivinationTex, tint);
                DrawStateMask(innerRect, divination);
            }
            if (mouseOverDiamond)
            {
                TooltipHandler.TipRegion(rect, () => BuildTip(divination, learned), GetStableTipId());
            }
            if (canStart && Widgets.ButtonInvisible(diamondRect))
            {
                TryStartDivination(divination);
            }
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

        private static void DrawStateMask(Rect rect, HediffComp_FlowerDivination divination)
        {
            if (divination == null || divination.Ready)
            {
                return;
            }

            if (divination.Active)
            {
                DrawTopMask(rect.ExpandedBy(1f), QuantizeFillPercent(divination.ActiveElapsedPercent));
                return;
            }

            if (divination.OnCooldown)
            {
                DrawTopMask(rect.ExpandedBy(1f), QuantizeFillPercent(divination.CooldownRemainingPercent));
            }
        }

        private static float QuantizeFillPercent(float fillPercent)
        {
            fillPercent = Mathf.Clamp01(fillPercent);
            if (fillPercent <= 0f)
            {
                return 0f;
            }

            return Mathf.CeilToInt(fillPercent * 16f) / 16f;
        }

        private static void DrawTopMask(Rect rect, float fillPercent)
        {
            fillPercent = Mathf.Clamp01(fillPercent);
            if (fillPercent <= 0f || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Rect maskRect = new Rect(rect.x, rect.y, rect.width, rect.height * fillPercent);
            Rect texCoords = new Rect(0f, 1f - fillPercent, 1f, fillPercent);
            Color oldColor = GUI.color;
            GUI.color = MaskColor;
            GUI.DrawTextureWithTexCoords(maskRect, MX_QHRenderStatics.DiamondSolidTex, texCoords, true);
            GUI.color = oldColor;
        }

        private static string BuildTip(HediffComp_FlowerDivination divination, bool learned)
        {
            if (!learned)
            {
                return "花神降临\n\n尚未习得花之舞。";
            }

            if (divination == null)
            {
                return "花神降临\n\n清荷尚未建立花神庭。";
            }

            string tip = divination.Label;
            if (divination.Active)
            {
                return tip + "\n\n当前状态: 降临中\n剩余时间: " + divination.ActiveTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            if (divination.OnCooldown)
            {
                return tip + "\n\n当前状态: 冷却中\n剩余时间: " + divination.CooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
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
