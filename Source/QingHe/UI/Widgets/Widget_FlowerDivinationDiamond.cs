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
        private static readonly Color ReadyColor = new Color(0.36f, 0.82f, 0.48f, 1f);
        private static readonly Color ActiveColor = new Color(0.95f, 0.42f, 0.64f, 1f);
        private static readonly Color CooldownColor = new Color(0.23f, 0.50f, 0.92f, 1f);

        public Widget_FlowerDivinationDiamond(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            HediffComp_FlowerDivination divination = FlowerCourtUtility.EnsureFlowerDivination(pawn);
            bool learned = state?.HasNode(QingheSkillTreeSystem.NodeFlowerDance) == true;
            Rect diamondRect = GetAlignedRect(rect, new Vector2(Mathf.Min(rect.width, rect.height), Mathf.Min(rect.width, rect.height)), null).ContractedBy(OuterPadding);
            bool mouseOverDiamond = MouseIsOverHitbox(diamondRect);
            bool canStart = learned && divination != null && divination.CanStartDivination(out _) && mouseOverDiamond;
            Color tint = canStart ? GenUI.MouseoverColor : Color.white;
            Rect innerRect = diamondRect.ContractedBy(BorderThickness);

            DrawDiamond(diamondRect, BorderColor);
            DrawDiamond(innerRect, EmptyFillColor);

            if (learned)
            {
                float fillPercent = ResolveFillPercent(divination);
                if (fillPercent > 0.0001f)
                {
                    DrawDiamondFill(innerRect, fillPercent, ResolveFillColor(divination) * tint);
                }
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

        private static void DrawDiamond(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, MX_QHRenderStatics.DiamondSolidTex, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
        }

        private static void DrawDiamondFill(Rect rect, float fillPercent, Color color)
        {
            fillPercent = Mathf.Clamp01(fillPercent);
            float height = rect.height * fillPercent;
            if (height <= 0f)
            {
                return;
            }

            Rect fillRect = new Rect(rect.x, rect.yMax - height, rect.width, height);
            Rect texCoords = new Rect(0f, 0f, 1f, fillPercent);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(fillRect, MX_QHRenderStatics.DiamondSolidTex, texCoords, true);
            GUI.color = oldColor;
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

        private static Color ResolveFillColor(HediffComp_FlowerDivination divination)
        {
            if (divination == null)
            {
                return EmptyFillColor;
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
