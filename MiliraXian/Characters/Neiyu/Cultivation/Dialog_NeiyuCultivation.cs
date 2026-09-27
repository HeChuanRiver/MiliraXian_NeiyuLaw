using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    public sealed class Dialog_NeiyuCultivation : Window
    {
        private readonly CultivationView view;
        private readonly CultivationViewModel model;
        public override Vector2 InitialSize => FitSize(Verse.UI.screenWidth, Verse.UI.screenHeight);
        protected override float Margin => 0f;

        public Dialog_NeiyuCultivation(Pawn pawn)
        {
            model = new CultivationViewModel(pawn);
            view = new CultivationView();
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = true;
            doCloseX = false;
            doWindowBackground = false;
            draggable = false;
        }

        internal static Vector2 FitSize(float width, float height) => new(
            Mathf.Min(1260f, Mathf.Max(240f, width - 32f)),
            Mathf.Min(820f, Mathf.Max(240f, height - 32f)));

        public override void DoWindowContents(Rect inRect)
        {
            if (model.Pawn == null || model.Pawn.Destroyed || model.Pawn.Dead) { Close(); return; }
            Vector2 fit = InitialSize;
            if (Mathf.Abs(windowRect.width - fit.x) > 1f || Mathf.Abs(windowRect.height - fit.y) > 1f)
            {
                windowRect = new Rect((Verse.UI.screenWidth - fit.x) / 2f, (Verse.UI.screenHeight - fit.y) / 2f, fit.x, fit.y);
                return;
            }
            model.Update();
            if (view.Draw(inRect, model)) Close();
        }
    }
}
