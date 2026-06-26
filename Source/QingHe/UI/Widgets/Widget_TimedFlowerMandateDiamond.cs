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
            HediffComp_FlowerChoices choices = FlowerCourtUtility.EnsureFlowerChoices(pawn);
            bool learned = state?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_SishiLiuzhuan) == true;
            Rect diamondRect = GetAlignedRect(rect, new Vector2(Mathf.Min(rect.width, rect.height), Mathf.Min(rect.width, rect.height)), null).ContractedBy(OuterPadding);
            bool mouseOverDiamond = MouseIsOverHitbox(diamondRect);
            bool canClick = learned && choices != null && choices.TimedFlowerMandateOnCooldown == false && mouseOverDiamond;
            Color buttonTint = canClick ? GenUI.MouseoverColor : Color.white;

            DrawDiamond(diamondRect, MX_QHRenderStatics.DiamondSolidTex, BorderColor);
            Rect innerRect = diamondRect.ContractedBy(BorderThickness);
            if (learned)
            {
                Texture2D selectedTex = MX_QHRenderStatics.TimedFlowerMandateTexForDef(choices?.SelectedTimedFlowerMandate);
                if (selectedTex != null)
                {
                    DrawDiamond(innerRect, selectedTex, buttonTint);
                }
                else
                {
                    DrawDiamond(innerRect, MX_QHRenderStatics.DiamondSolidTex, EmptyFillColor);
                    DrawPlus(innerRect, buttonTint);
                }

                if (choices != null && choices.TimedFlowerMandateOnCooldown)
                {
                    DrawCooldownMask(innerRect, choices.TimedFlowerMandateCooldownRemainingPercent);
                }
            }
            else
            {
                DrawDiamond(innerRect, MX_QHRenderStatics.DiamondSolidTex, EmptyFillColor);
            }

            if (mouseOverDiamond)
            {
                TooltipHandler.TipRegion(rect, () => BuildTip(choices, learned), GetStableTipId());
            }
            if (canClick && Widgets.ButtonInvisible(diamondRect))
            {
                OpenChoiceMenu(choices);
            }
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
            float clampedPercent = Mathf.Clamp01(remainingPercent);
            if (clampedPercent <= 0f)
            {
                return;
            }

            float quantizedPercent = Mathf.CeilToInt(clampedPercent * 16f) / 16f;
            DrawDiamondFill(rect.ExpandedBy(1f), MX_QHRenderStatics.DiamondSolidTex, quantizedPercent, CooldownMaskColor);
        }

        private void OpenChoiceMenu(HediffComp_FlowerChoices choices)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            IReadOnlyList<AbilityDef> mandates = QingheFlowerChoiceUtility.FlowerMandates;
            for (int i = 0; i < mandates.Count; i++)
            {
                AbilityDef def = mandates[i];
                string label = QingheFlowerChoiceUtility.LabelForDef(def);
                if (choices.TimedFlowerMandateOnCooldown)
                {
                    options.Add(new FloatMenuOption(label + "（切换冷却中）", null));
                    continue;
                }

                if (def == choices.SelectedFlowerMandate)
                {
                    options.Add(new FloatMenuOption(label + "（当前主飞花令）", null));
                    continue;
                }

                if (def == choices.SelectedTimedFlowerMandate)
                {
                    options.Add(new FloatMenuOption(label + "（已选择）", null));
                    continue;
                }

                options.Add(new FloatMenuOption(label, delegate
                {
                    if (!choices.TrySetTimedFlowerMandate(def, out string reason))
                    {
                        Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Messages.Message("清荷已将飞花令·寄时切换为“" + QingheFlowerChoiceUtility.LabelForDef(def) + "”。", pawn, MessageTypeDefOf.PositiveEvent, historical: false);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string BuildTip(HediffComp_FlowerChoices choices, bool learned)
        {
            if (!learned)
            {
                return "飞花令·寄时\n\n尚未习得四时流转。";
            }

            AbilityDef selected = choices?.SelectedTimedFlowerMandate;
            string tip = "飞花令·寄时\n\n当前: " + QingheFlowerChoiceUtility.LabelForDef(selected);
            if (choices != null && choices.TimedFlowerMandateOnCooldown)
            {
                tip += "\n冷却剩余: " + choices.TimedFlowerMandateCooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            return tip;
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }

    public class Widget_FlowerMandateDiamond : DiamondWidget_Base
    {
        private const int TipSalt = 910209;
        private const float BorderThickness = 2f;
        private const float OuterPadding = 1f;

        private readonly Pawn pawn;

        private static readonly Color BorderColor = new Color(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color EmptyFillColor = new Color(0.08f, 0.09f, 0.09f, 0.9f);

        public Widget_FlowerMandateDiamond(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            HediffComp_FlowerChoices choices = FlowerCourtUtility.EnsureFlowerChoices(pawn);
            bool learned = state?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerMandate) == true;
            Rect diamondRect = GetAlignedRect(rect, new Vector2(Mathf.Min(rect.width, rect.height), Mathf.Min(rect.width, rect.height)), null).ContractedBy(OuterPadding);
            bool mouseOverDiamond = MouseIsOverHitbox(diamondRect);
            bool canClick = learned && choices != null && mouseOverDiamond;
            Color buttonTint = canClick ? GenUI.MouseoverColor : Color.white;

            DrawDiamond(diamondRect, MX_QHRenderStatics.DiamondSolidTex, BorderColor);
            Rect innerRect = diamondRect.ContractedBy(BorderThickness);
            if (learned)
            {
                Texture2D selectedTex = MX_QHRenderStatics.TimedFlowerMandateTexForDef(choices?.SelectedFlowerMandate);
                if (selectedTex != null)
                {
                    DrawDiamond(innerRect, selectedTex, buttonTint);
                }
                else
                {
                    DrawDiamond(innerRect, MX_QHRenderStatics.DiamondSolidTex, EmptyFillColor);
                    DrawPlus(innerRect, buttonTint);
                }
            }
            else
            {
                DrawDiamond(innerRect, MX_QHRenderStatics.DiamondSolidTex, EmptyFillColor);
            }

            if (mouseOverDiamond)
            {
                TooltipHandler.TipRegion(rect, () => BuildTip(choices, learned), GetStableTipId());
            }
            if (canClick && Widgets.ButtonInvisible(diamondRect))
            {
                OpenChoiceMenu(choices);
            }
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

        private void OpenChoiceMenu(HediffComp_FlowerChoices choices)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            IReadOnlyList<AbilityDef> mandates = QingheFlowerChoiceUtility.FlowerMandates;
            for (int i = 0; i < mandates.Count; i++)
            {
                AbilityDef def = mandates[i];
                string label = QingheFlowerChoiceUtility.LabelForDef(def);
                if (def == choices.SelectedFlowerMandate)
                {
                    options.Add(new FloatMenuOption(label + " (selected)", null));
                    continue;
                }

                options.Add(new FloatMenuOption(label, delegate
                {
                    if (!QingheSkillTreeSystem.TrySetFlowerMandate(choices, def, out string reason))
                    {
                        Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    QingheSkillTreeSystem.SyncChoices(pawn);
                    Messages.Message("Primary Flower Mandate: " + QingheFlowerChoiceUtility.LabelForDef(def), pawn, MessageTypeDefOf.PositiveEvent, historical: false);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string BuildTip(HediffComp_FlowerChoices choices, bool learned)
        {
            if (!learned)
            {
                return "Primary Flower Mandate\n\nNot learned.";
            }

            return "Primary Flower Mandate\n\nCurrent: " + QingheFlowerChoiceUtility.LabelForDef(choices?.SelectedFlowerMandate);
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }
}
