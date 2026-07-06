using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_SkillTreeProgressDiamond : DiamondWidget_Base
    {
        private const int TipSalt = 910209;
        private const float BorderThickness = 2f;
        private const float OuterPadding = 1f;
        private const float CenterOverlayScale = 0.78f;

        private readonly Pawn pawn;

        private static readonly Color BorderColor = new Color(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color EmptyFillColor = new Color(0.08f, 0.09f, 0.09f, 0.9f);
        private static readonly Color CenterBorderColor = new Color(0.50f, 0.52f, 0.52f, 1f);
        private static readonly Color CenterOverlayColor = new Color(0.12f, 0.16f, 0.20f, 0.94f);
        private static readonly Color StillnessFillColor = new Color(0.44f, 0.92f, 0.58f, 1f);

        public Widget_SkillTreeProgressDiamond(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            HediffComp_PawnSpecialResource stillness = FlowerCourtUtility.EnsureMeditativeStillness(pawn);
            bool canClick = state != null;
            Rect diamondRect = GetAlignedRect(rect, new Vector2(Mathf.Min(rect.width, rect.height), Mathf.Min(rect.width, rect.height)), null).ContractedBy(OuterPadding);
            bool mouseOverDiamond = MouseIsOverHitbox(diamondRect);
            Color tint = canClick && mouseOverDiamond ? GenUI.MouseoverColor : Color.white;
            Rect innerRect = diamondRect.ContractedBy(BorderThickness);

            DrawDiamond(diamondRect, MX_QHRenderStatics.DiamondSolidTex, BorderColor);
            float stillnessFillPercent = QuantizeFillPercent(stillness?.ValuePercent ?? 0f);
            if (stillnessFillPercent > 0.0001f)
            {
                DrawDiamondFill(diamondRect, MX_QHRenderStatics.DiamondSolidTex, stillnessFillPercent, StillnessFillColor * tint);
            }
            DrawDiamond(innerRect, MX_QHRenderStatics.DiamondSolidTex, EmptyFillColor);

            if (state != null)
            {
                Rect centerRect = CenteredSquare(innerRect, CenterOverlayScale);
                DrawDiamond(centerRect.ExpandedBy(1f), MX_QHRenderStatics.DiamondSolidTex, CenterBorderColor * tint);
                DrawDiamond(centerRect, MX_QHRenderStatics.DiamondSolidTex, CenterOverlayColor * tint);
                DrawCenterLabel(rect, state.LearnedNodeCount.ToString(), Color.white * tint);
            }

            if (mouseOverDiamond)
            {
                TooltipHandler.TipRegion(rect, () => BuildTip(state, stillness), GetStableTipId());
            }
            if (canClick && mouseOverDiamond && Widgets.ButtonInvisible(diamondRect))
            {
                Find.WindowStack.Add(new Dialog_QH_SkillTree(pawn, state));
            }
        }

        private static void DrawCenterLabel(Rect rect, string label, Color color)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = color;
            Widgets.Label(rect, label);

            GUI.color = oldColor;
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
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

        private static string BuildTip(HediffComp_FlowerResonance state, HediffComp_PawnSpecialResource stillness)
        {
            if (state == null)
            {
                return "MX_QH_FlowerCourtTitle".Translate() + "\n\n" + "MX_QH_FlowerCourtMissing".Translate();
            }

            return "MX_QH_FlowerCourtTitle".Translate() + "\n\n"
                   + "MX_QH_FlowerCourtUnlockedTreesLine".Translate(state.UnlockedTreeCount) + "\n"
                   + "MX_QH_FlowerCourtLearnedNodesLine".Translate(state.LearnedNodeCount) + "\n"
                   + "MX_QH_FlowerCourtStillnessLine".Translate((stillness?.CurrentValue ?? 0f).ToString("0"), (stillness?.MaxValue ?? 100f).ToString("0"));
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }
}
