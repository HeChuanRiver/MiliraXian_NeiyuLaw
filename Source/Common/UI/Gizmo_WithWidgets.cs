using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.UI
{
    public abstract class Gizmo_WithWidgets : Gizmo
    {
        private readonly List<Widget_Base> widgets = new List<Widget_Base>();
        private bool widgetsBuilt;
        private bool widgetsOpened;

        protected abstract float GizmoWidth { get; }

        protected abstract float GizmoHeight { get; }

        protected abstract Rect WidgetRootRect { get; }

        protected IReadOnlyList<Widget_Base> WidgetList => widgets;

        public override float GetWidth(float maxWidth)
        {
            return GizmoWidth;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            EnsureWidgetsOpen();
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), GizmoHeight);
            DrawBackground(rect);
            DrawWidgets(rect);
            return new GizmoResult(GizmoState.Clear);
        }

        protected abstract void BuildWidgets(List<Widget_Base> outWidgets);

        protected virtual void DrawBackground(Rect rect)
        {
            Verse.Widgets.DrawWindowBackground(rect);
        }

        protected virtual void DrawWidgets(Rect rect)
        {
            Rect rootRect = ToGlobalRect(rect, WidgetRootRect);
            for (int i = 0; i < widgets.Count; i++)
            {
                Widget_Base widget = widgets[i];
                widget.Draw(rootRect);
            }
        }

        protected Rect ToGlobalRect(Rect parentRect, Rect localRect)
        {
            return new Rect(parentRect.x + localRect.x, parentRect.y + localRect.y, localRect.width, localRect.height);
        }

        protected void RebuildWidgets()
        {
            CloseWidgets();
            DetachWidgets();
            widgets.Clear();
            widgetsBuilt = false;
            widgetsOpened = false;
            EnsureWidgetsOpen();
        }

        private void EnsureWidgetsOpen()
        {
            EnsureWidgetsBuilt();
            if (widgetsOpened)
            {
                return;
            }

            for (int i = 0; i < widgets.Count; i++)
            {
                widgets[i].Notify_Open();
            }
            widgetsOpened = true;
        }

        private void EnsureWidgetsBuilt()
        {
            if (widgetsBuilt)
            {
                return;
            }

            BuildWidgets(widgets);
            WidgetHost host = new WidgetHost(WidgetHostKind.Gizmo, this);
            for (int i = 0; i < widgets.Count; i++)
            {
                widgets[i].Notify_Attached(host);
            }
            widgetsBuilt = true;
        }

        private void CloseWidgets()
        {
            if (!widgetsOpened)
            {
                return;
            }

            for (int i = 0; i < widgets.Count; i++)
            {
                widgets[i].Notify_Close();
            }
            widgetsOpened = false;
        }

        private void DetachWidgets()
        {
            for (int i = 0; i < widgets.Count; i++)
            {
                widgets[i].Notify_Detached();
            }
        }
    }
}
