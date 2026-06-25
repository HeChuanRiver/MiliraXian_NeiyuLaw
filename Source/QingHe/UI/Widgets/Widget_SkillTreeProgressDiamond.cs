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

        private readonly Pawn pawn;

        private static readonly Color BorderColor = new Color(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color EmptyFillColor = new Color(0.08f, 0.09f, 0.09f, 0.9f);
        private static readonly Color MasteryCompleteFillColor = new Color(0.22f, 0.24f, 0.30f, 0.95f);
        private static readonly Color ExperienceColor = new Color(0.58f, 0.84f, 1f, 1f);
        private static readonly Color MasteryExperienceColor = new Color(0.30f, 0.42f, 0.62f, 1f);
        private static readonly Color MasteryTextColor = new Color(1f, 0.82f, 0.22f, 1f);

        public Widget_SkillTreeProgressDiamond(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            bool canClick = state != null;
            Rect diamondRect = GetAlignedRect(rect, new Vector2(Mathf.Min(rect.width, rect.height), Mathf.Min(rect.width, rect.height)), null).ContractedBy(OuterPadding);
            bool mouseOverDiamond = MouseIsOverHitbox(diamondRect);
            Color tint = canClick && mouseOverDiamond ? GenUI.MouseoverColor : Color.white;
            Rect innerRect = diamondRect.ContractedBy(BorderThickness);

            DrawDiamond(diamondRect, BorderColor);
            DrawDiamond(innerRect, state?.MusicMasteryComplete == true ? MasteryCompleteFillColor : EmptyFillColor);

            if (state != null && state.ExperienceProgressPercent > 0.0001f)
            {
                Color fillColor = state.MusicMasteryLevel > 0 ? MasteryExperienceColor : ExperienceColor;
                DrawDiamondFill(innerRect, state.ExperienceProgressPercent, fillColor * tint);
            }

            if (state != null && state.MusicMasteryLevel > 0)
            {
                DrawCenterLabel(rect, ToRoman(state.MusicMasteryLevel), MasteryTextColor * tint);
            }
            else if (state != null && state.SkillPoints > 0)
            {
                Color labelColor = state.ExperienceProgressPercent > 0.5f ? Color.black : Color.white;
                DrawCenterLabel(rect, state.SkillPoints.ToString(), labelColor * tint);
            }

            if (mouseOverDiamond)
            {
                TooltipHandler.TipRegion(rect, () => BuildTip(state), GetStableTipId());
            }
            if (canClick && mouseOverDiamond && Widgets.ButtonInvisible(diamondRect))
            {
                Find.WindowStack.Add(new Dialog_QH_SkillTree(pawn, state));
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

        private static string BuildTip(HediffComp_FlowerResonance state)
        {
            if (state == null)
            {
                return "花神庭\n\n清荷尚未建立花神庭。";
            }

            string experienceText = state.MusicMasteryComplete
                ? "经验: 0 / 0"
                : "经验: " + state.Experience.ToString("F0") + " / " + state.ExperienceToNextPoint.ToString("F0");
            string masteryText = state.MusicMasteryLevel > 0
                ? "\n音律精通: " + ToRoman(state.MusicMasteryLevel) + " / XII"
                : "";

            return "花神庭\n\n" + experienceText
                   + "\n可用技能点: " + state.SkillPoints
                   + masteryText;
        }

        private static string ToRoman(int value)
        {
            switch (Mathf.Clamp(value, 1, HediffComp_FlowerResonance.MaxMusicMasteryLevel))
            {
                case 1:
                    return "I";
                case 2:
                    return "II";
                case 3:
                    return "III";
                case 4:
                    return "IV";
                case 5:
                    return "V";
                case 6:
                    return "VI";
                case 7:
                    return "VII";
                case 8:
                    return "VIII";
                case 9:
                    return "IX";
                case 10:
                    return "X";
                case 11:
                    return "XI";
                default:
                    return "XII";
            }
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }
}
