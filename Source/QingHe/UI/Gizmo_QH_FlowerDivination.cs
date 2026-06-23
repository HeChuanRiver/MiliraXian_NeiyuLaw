using System.Collections.Generic;
using MiliraXian.Characters.QingHe.UI.WidgetControls;
using MiliraXian.Characters.UI;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.UI
{
    [StaticConstructorOnStartup]
    public class Gizmo_QH_FlowerDivination : Gizmo_WithWidgets
    {
        private readonly Pawn pawn;

        protected override float GizmoWidth => 150f;

        protected override float GizmoHeight => 75f;

        protected override Rect WidgetRootRect => new Rect(6f, 6f, GizmoWidth - 12f, GizmoHeight - 12f);

        public Gizmo_QH_FlowerDivination(Pawn pawn)
        {
            this.pawn = pawn;
            Order = -98f;
        }

        protected override void BuildWidgets(List<Widget_Base> outWidgets)
        {
            outWidgets.Add(new Widget_FlowerDivinationStatus(pawn, new Rect(0f, 0f, WidgetRootRect.width, WidgetRootRect.height), TextAnchor.UpperLeft));
        }
    }
}
