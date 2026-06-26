using System.Collections.Generic;
using MiliraXian.Characters.QingHe.UI.WidgetControls;
using MiliraXian.Characters.UI;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.UI
{
    public class Gizmo_QH_FlowerDecree : Gizmo_WithWidgets
    {
        private readonly Pawn pawn;

        protected override float GizmoWidth => 250f;

        protected override float GizmoHeight => 75f;

        protected override Rect WidgetRootRect => new Rect(6f, 6f, GizmoWidth - 12f, GizmoHeight - 12f);

        public Gizmo_QH_FlowerDecree(Pawn pawn)
        {
            this.pawn = pawn;
            Order = -99f;
        }

        protected override void BuildWidgets(List<Widget_Base> outWidgets)
        {
            outWidgets.Add(new Widget_FlowerBellEnhanceDiamond(pawn, new Rect(120f, 0f, 41f, 41f), TextAnchor.MiddleCenter, MX_QHRenderStatics.FlowerBellEnhanceTex));
            outWidgets.Add(new Widget_TimedFlowerMandateDiamond(pawn, new Rect(170f, 0f, 41f, 41f), TextAnchor.MiddleCenter));
            outWidgets.Add(new Widget_FlowerMandateDiamond(pawn, new Rect(145f, 25f, 41f, 41f), TextAnchor.MiddleCenter));
            outWidgets.Add(new Widget_SkillTreeProgressDiamond(pawn, new Rect(195f, 25f, 41f, 41f), TextAnchor.MiddleCenter));
            outWidgets.Add(new Widget_FlowerDecreeHelpButton(pawn, new Rect(225f, 0f, 15f, 15f), TextAnchor.MiddleCenter));
            outWidgets.Add(new Widget_LongBreathChargeBlocks(pawn, new Rect(80f, 34f, 32f, 8f), TextAnchor.MiddleRight));
            outWidgets.Add(new TextWidget("花令", new Rect(8f, 2f, 32f, 24f), TextAnchor.MiddleLeft, GameFont.Tiny));
            outWidgets.Add(new Widget_FlowerDecreeBar(pawn, new Rect(0f, 12, 120f, 24f), TextAnchor.MiddleLeft));
            outWidgets.Add(new TextWidget("护盾", new Rect(8f, 28f, 32f, 24f), TextAnchor.MiddleLeft, GameFont.Tiny));
            outWidgets.Add(new Widget_LotusShieldBar(pawn, new Rect(0f, 38, 120f, 24f), TextAnchor.MiddleLeft));
        }
    }
}
