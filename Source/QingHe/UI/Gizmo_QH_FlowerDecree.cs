using System.Collections.Generic;
using MiliraXian.Characters.QingHe.UI.WidgetControls;
using MiliraXian.Characters.UI;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.UI
{
    [StaticConstructorOnStartup]
    public class Gizmo_QH_FlowerDecree : Gizmo_WithWidgets
    {
        private readonly Pawn pawn;

        protected override float GizmoWidth => 150f;

        protected override float GizmoHeight => 75f;

        protected override Rect WidgetRootRect => new Rect(6f, 6f, GizmoWidth - 12f, GizmoHeight - 12f);

        public Gizmo_QH_FlowerDecree(Pawn pawn)
        {
            this.pawn = pawn;
            Order = -99f;
        }

        protected override void BuildWidgets(List<Widget_Base> outWidgets)
        {
            outWidgets.Add(new Widget_FlowerDecreeBar(pawn, new Rect(0f, 8f, WidgetRootRect.width, 24f), TextAnchor.UpperLeft));
            outWidgets.Add(new Widget_LotusShieldBar(pawn, new Rect(0f, 36f, WidgetRootRect.width, 24f), TextAnchor.UpperLeft));
        }
    }
}
