using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_FlowerBellEnhanceDiamond : DiamondWidget_Base
    {
        private const int TipSalt = 910206;
        private const float BorderThickness = 2f;
        private const float OuterPadding = 1f;
        private const float ActiveCheckSize = 12f;
        private const float InactiveCrossSize = 12f;
        private const float MinimumFlowerDecreeToEnable = 1f;

        private readonly Pawn pawn;
        private readonly Texture2D overlayTexture;

        private static readonly Color BorderColor = new Color(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color EmptyFillColor = new Color(0.08f, 0.09f, 0.09f, 0.9f);
        private static readonly Color UnlockedTint = new Color(1f, 1f, 1f, 1f);

        public Widget_FlowerBellEnhanceDiamond(Pawn pawn, Rect localRect, TextAnchor alignment, Texture2D texture = null)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
            overlayTexture = texture;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            HediffComp_FlowerChoices choices = FlowerCourtUtility.EnsureFlowerChoices(pawn);
            bool learned = state?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Qingjue) == true;
            bool active = learned && choices?.FlowerBellEnhanced == true;
            Rect diamondRect = GetAlignedRect(rect, new Vector2(Mathf.Min(rect.width, rect.height), Mathf.Min(rect.width, rect.height)), null).ContractedBy(OuterPadding);
            bool mouseOverDiamond = MouseIsOverHitbox(diamondRect);
            bool canClick = learned && mouseOverDiamond;
            Color buttonTint = canClick ? GenUI.MouseoverColor : Color.white;

            DrawDiamond(diamondRect, MX_QHRenderStatics.DiamondSolidTex, BorderColor);
            Rect innerRect = diamondRect.ContractedBy(BorderThickness);
            if (learned)
            {
                DrawDiamond(innerRect, ResolveTexture(active), ResolveTint(active) * buttonTint);
                if (active)
                {
                    DrawActiveCheck(diamondRect, buttonTint);
                }
                else
                {
                    DrawInactiveCross(diamondRect, buttonTint);
                }
            }
            else
            {
                DrawDiamond(innerRect, MX_QHRenderStatics.DiamondSolidTex, EmptyFillColor);
            }

            if (mouseOverDiamond)
            {
                TooltipHandler.TipRegion(rect, () => BuildTip(choices, learned, active), GetStableTipId());
            }
            if (canClick && Widgets.ButtonInvisible(diamondRect))
            {
                TryToggleEnhanced(choices);
            }
        }

        private void TryToggleEnhanced(HediffComp_FlowerChoices choices)
        {
            if (choices == null)
            {
                return;
            }

            bool enable = !choices.FlowerBellEnhanced;
            if (enable && !HasEnoughFlowerDecreeToEnable())
            {
                Messages.Message("花令不足，无法开启花信铃强化。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            choices.SetFlowerBellEnhanced(enable);
        }

        private bool HasEnoughFlowerDecreeToEnable()
        {
            HediffComp_FlowerDecree decree = FlowerCourtUtility.GetFlowerDecree(pawn);
            return decree != null && decree.CurrentResourceValue > MinimumFlowerDecreeToEnable;
        }

        private static void DrawActiveCheck(Rect rect, Color tint)
        {
            float size = Mathf.Min(ActiveCheckSize, rect.width, rect.height);
            Rect checkRect = new Rect(rect.xMax - size, rect.yMax - size, size, size);
            Color oldColor = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(checkRect, Widgets.CheckboxOnTex);
            GUI.color = oldColor;
        }

        private static void DrawInactiveCross(Rect rect, Color tint)
        {
            float size = Mathf.Min(InactiveCrossSize, rect.width, rect.height);
            Rect crossRect = new Rect(rect.xMax - size, rect.yMax - size, size, size);
            Color oldColor = GUI.color;
            GUI.color = Color.red * tint;
            GUI.DrawTexture(crossRect, TexButton.CloseXSmall);
            GUI.color = oldColor;
        }

        private Texture2D ResolveTexture(bool active)
        {
            if (!active)
            {
                return MX_QHRenderStatics.FlowerBellEnhanceGrayTex;
            }

            return overlayTexture ?? MX_QHRenderStatics.FlowerBellEnhanceTex;
        }

        private static Color ResolveTint(bool active)
        {
            if (!active)
            {
                return UnlockedTint;
            }

            return Color.white;
        }

        private static string BuildTip(HediffComp_FlowerChoices choices, bool learned, bool active)
        {
            if (!learned)
            {
                return "花信铃强化\n\n尚未习得清角节点。";
            }

            string tip = "花信铃强化\n\n状态: " + (active ? "开启" : "关闭");
            if (choices != null)
            {
                tip += "\n当前飞花令: " + QingheFlowerChoiceUtility.LabelForDef(choices.SelectedFlowerMandate);
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
