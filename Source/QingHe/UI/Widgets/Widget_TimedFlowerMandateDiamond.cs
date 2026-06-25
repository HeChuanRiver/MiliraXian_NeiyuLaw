using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_TimedFlowerMandateDiamond : DiamondWidget_Base
    {
        private const int TipSalt = 910207;
        private const float BorderThickness = 2f;
        private const float OuterPadding = 1f;

        private readonly Pawn pawn;

        private static readonly Color BorderColor = new Color(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color EmptyFillColor = new Color(0.08f, 0.09f, 0.09f, 0.9f);
        private static readonly Color CooldownMaskColor = new Color(0f, 0f, 0f, 0.58f);

        public Widget_TimedFlowerMandateDiamond(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            bool learned = state?.HasNode(QingheSkillTreeSystem.NodeSishiLiuzhuan) == true;
            Rect diamondRect = GetAlignedRect(rect, new Vector2(Mathf.Min(rect.width, rect.height), Mathf.Min(rect.width, rect.height)), null).ContractedBy(OuterPadding);
            bool mouseOverDiamond = MouseIsOverHitbox(diamondRect);
            bool canClick = learned && state.TimedFlowerMandateOnCooldown == false && mouseOverDiamond;
            Color buttonTint = canClick ? GenUI.MouseoverColor : Color.white;

            DrawDiamond(diamondRect, MX_QHRenderStatics.DiamondSolidTex, BorderColor);
            Rect innerRect = diamondRect.ContractedBy(BorderThickness);
            if (learned)
            {
                Texture2D selectedTex = MX_QHRenderStatics.TimedFlowerMandateTexForDefName(state.SelectedTimedFlowerMandateDefName);
                if (selectedTex != null)
                {
                    DrawDiamond(innerRect, selectedTex, buttonTint);
                }
                else
                {
                    DrawDiamond(innerRect, MX_QHRenderStatics.DiamondSolidTex, EmptyFillColor);
                    DrawPlus(innerRect, buttonTint);
                }

                if (state.TimedFlowerMandateOnCooldown)
                {
                    DrawCooldownMask(innerRect, state.TimedFlowerMandateCooldownRemainingPercent);
                }
            }
            else
            {
                DrawDiamond(innerRect, MX_QHRenderStatics.DiamondSolidTex, EmptyFillColor);
            }

            if (mouseOverDiamond)
            {
                TooltipHandler.TipRegion(rect, () => BuildTip(state, learned), GetStableTipId());
            }
            if (canClick && Widgets.ButtonInvisible(diamondRect))
            {
                OpenChoiceMenu(state);
            }
        }

        private static void DrawDiamond(Rect rect, Texture2D texture, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f || texture == null)
            {
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
        }

        private static void DrawPlus(Rect rect, Color tint)
        {
            float size = Mathf.Min(rect.width, rect.height) * 0.62f;
            Rect plusRect = new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size);
            Color oldColor = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(plusRect, TexButton.Plus, ScaleMode.ScaleToFit, true);
            GUI.color = oldColor;
        }

        private static void DrawCooldownMask(Rect rect, float remainingPercent)
        {
            float height = rect.height * Mathf.Clamp01(remainingPercent);
            if (height <= 0f)
            {
                return;
            }

            Rect maskRect = new Rect(rect.x, rect.yMax - height, rect.width, height);
            Rect texCoords = new Rect(0f, 0f, 1f, Mathf.Clamp01(remainingPercent));
            Color oldColor = GUI.color;
            GUI.color = CooldownMaskColor;
            GUI.DrawTextureWithTexCoords(maskRect, MX_QHRenderStatics.DiamondSolidTex, texCoords, true);
            GUI.color = oldColor;
        }

        private void OpenChoiceMenu(HediffComp_FlowerResonance state)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            IReadOnlyList<string> mandates = QingheFlowerChoiceUtility.FlowerMandates;
            for (int i = 0; i < mandates.Count; i++)
            {
                string defName = mandates[i];
                string label = QingheFlowerChoiceUtility.LabelForDefName(defName);
                if (state.TimedFlowerMandateOnCooldown)
                {
                    options.Add(new FloatMenuOption(label + "（切换冷却中）", null));
                    continue;
                }

                if (defName == state.SelectedFlowerMandateDefName)
                {
                    options.Add(new FloatMenuOption(label + "（当前主飞花令）", null));
                    continue;
                }

                if (defName == state.SelectedTimedFlowerMandateDefName)
                {
                    options.Add(new FloatMenuOption(label + "（已选择）", null));
                    continue;
                }

                options.Add(new FloatMenuOption(label, delegate
                {
                    if (!state.TrySetTimedFlowerMandate(defName, out string reason))
                    {
                        Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Messages.Message("清荷已将飞花令·寄时切换为“" + QingheFlowerChoiceUtility.LabelForDefName(defName) + "”。", pawn, MessageTypeDefOf.PositiveEvent, historical: false);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string BuildTip(HediffComp_FlowerResonance state, bool learned)
        {
            if (!learned)
            {
                return "飞花令·寄时\n\n尚未习得四时流转。";
            }

            string selected = state?.SelectedTimedFlowerMandateDefName;
            string tip = "飞花令·寄时\n\n当前: " + (selected.NullOrEmpty() ? "未选择" : QingheFlowerChoiceUtility.LabelForDefName(selected));
            if (state != null && state.TimedFlowerMandateOnCooldown)
            {
                tip += "\n冷却剩余: " + state.TimedFlowerMandateCooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            return tip;
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }
}
