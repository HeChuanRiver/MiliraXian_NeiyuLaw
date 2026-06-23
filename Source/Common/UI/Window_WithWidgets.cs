using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.UI
{
    public abstract class Window_WithWidgets : Window
    {
        private readonly List<Widget_Base> widgets = new List<Widget_Base>();
        private bool widgetsBuilt;
        private bool widgetsOpened;

        protected IReadOnlyList<Widget_Base> WidgetList => widgets;

        protected abstract Rect WidgetRootRect { get; }

        public override void DoWindowContents(Rect inRect)
        {
            EnsureWidgetsOpen();
            DrawWidgets(inRect);
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            EnsureWidgetsBuilt();
            for (int i = 0; i < widgets.Count; i++)
            {
                widgets[i].WidgetTick();
            }
        }

        public override void PostOpen()
        {
            base.PostOpen();
            EnsureWidgetsOpen();
        }

        public override void PreClose()
        {
            CloseWidgets();
            base.PreClose();
        }

        public override void PostClose()
        {
            DetachWidgets();
            base.PostClose();
        }

        protected abstract void BuildWidgets(List<Widget_Base> outWidgets);

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
            WidgetHost host = new WidgetHost(WidgetHostKind.Window, this);
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
