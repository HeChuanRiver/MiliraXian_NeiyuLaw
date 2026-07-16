using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI
{
    public class Dialog_QH_FlowerDecreeHelp : Window
    {
        private const float WindowWidth = 520f;
        private const float WindowHeight = 430f;

        private static readonly Color HeaderColor = new(0.72f, 0.86f, 0.76f, 1f);
        private static readonly Color BodyColor = new(0.86f, 0.88f, 0.86f, 1f);

        public override Vector2 InitialSize => new(WindowWidth, WindowHeight);

        public Dialog_QH_FlowerDecreeHelp()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseButton = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = HeaderColor;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "MX_QH_FlowerPanelHelpTitle".Translate());

            GUI.color = BodyColor;
            Text.Font = GameFont.Small;
            float y = inRect.y + 48f;
            DrawItem(inRect, ref y, "MX_QH_FlowerPanelHelpFlowerDecreeTitle".Translate(), "MX_QH_FlowerPanelHelpFlowerDecreeBody".Translate());
            DrawItem(inRect, ref y, "MX_QH_FlowerPanelHelpShieldTitle".Translate(), "MX_QH_FlowerPanelHelpShieldBody".Translate());
            DrawItem(inRect, ref y, "MX_QH_FlowerPanelHelpLongBreathTitle".Translate(), "MX_QH_FlowerPanelHelpLongBreathBody".Translate());
            DrawItem(inRect, ref y, "MX_QH_FlowerPanelHelpFlowerCourtTitle".Translate(), "MX_QH_FlowerPanelHelpFlowerCourtBody".Translate());

            GUI.color = Color.white;
        }

        private static void DrawItem(Rect inRect, ref float y, string title, string body)
        {
            Text.Font = GameFont.Small;
            GUI.color = HeaderColor;
            Widgets.Label(new Rect(inRect.x + 8f, y, 112f, 24f), title);

            GUI.color = BodyColor;
            Widgets.Label(new Rect(inRect.x + 122f, y, inRect.width - 132f, 42f), body);
            y += 46f;
        }
    }
}
