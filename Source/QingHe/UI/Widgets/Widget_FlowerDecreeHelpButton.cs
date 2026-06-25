using MiliraXian.Characters.UI;
using MiliraXian.Characters.QingHe.UI;
using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_FlowerDecreeHelpButton : Widget_Base
    {
        private const int TipSalt = 910210;

        private readonly Pawn pawn;

        private static readonly Color BorderColor = new Color(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color FillColor = new Color(0.08f, 0.09f, 0.09f, 0.95f);

        public Widget_FlowerDecreeHelpButton(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            Color tint = Mouse.IsOver(rect) ? GenUI.MouseoverColor : Color.white;
            DrawButton(rect, tint);

            TooltipHandler.TipRegion(rect, () => "清荷面板说明", GetStableTipId());
            if (Widgets.ButtonInvisible(rect))
            {
                Find.WindowStack.Add(new Dialog_QH_FlowerDecreeHelp());
            }
        }

        private static void DrawButton(Rect rect, Color tint)
        {
            Widgets.DrawBoxSolid(rect, BorderColor);
            Widgets.DrawBoxSolid(rect.ContractedBy(1f), FillColor);

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white * tint;
            Widgets.Label(rect, "?");

            GUI.color = oldColor;
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }
}
