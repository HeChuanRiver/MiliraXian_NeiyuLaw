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

        protected override float GizmoWidth => 185f;

        protected override float GizmoHeight => 75f;

        protected override Rect WidgetRootRect => new Rect(6f, 6f, GizmoWidth - 12f, GizmoHeight - 12f);

        public Gizmo_QH_FlowerDecree(Pawn pawn)
        {
            this.pawn = pawn;
            Order = -99f;
        }

        protected override void BuildWidgets(List<Widget_Base> outWidgets)
        {
            outWidgets.Add(new Widget_SkillTree(pawn, new Rect(124f, 13f, 47f, 47f), TextAnchor.MiddleCenter));
            outWidgets.Add(new Widget_FlowerDecreeHelpButton(pawn, new Rect(160f, 0f, 15f, 15f), TextAnchor.MiddleCenter));
            outWidgets.Add(new Widget_DivineBlessing(pawn, new Rect(80f, 34f, 32f, 8f), TextAnchor.MiddleRight));
            outWidgets.Add(new TextWidget("MX_QH_FlowerDecreeLabel".Translate(), new Rect(8f, 2f, 32f, 24f), TextAnchor.MiddleLeft, GameFont.Tiny));
            outWidgets.Add(new Widget_FlowerDecreeBar(pawn, new Rect(0f, 12, 120f, 24f), TextAnchor.MiddleLeft));
            outWidgets.Add(new TextWidget("MX_QH_LotusShieldLabel".Translate(), new Rect(8f, 28f, 32f, 24f), TextAnchor.MiddleLeft, GameFont.Tiny));
            outWidgets.Add(new Widget_DivineProtectionShieldBar(pawn, new Rect(0f, 38, 120f, 24f), TextAnchor.MiddleLeft));
        }
    }
}
