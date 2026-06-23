using UnityEngine;
using Verse;

namespace MiliraXian.Characters.UI
{
    public abstract class Widget_Base
    {
        private WidgetHost host;

        protected Widget_Base(Rect localRect, TextAnchor alignment)
        {
            LocalRect = localRect;
            Alignment = alignment;
        }

        public Rect LocalRect { get; private set; }

        public Rect GlobalRect { get; private set; }

        public TextAnchor Alignment { get; private set; }

        public bool Visible { get; set; } = true;

        public bool Enabled { get; set; } = true;

        protected WidgetHost Host => host;

        public void SetLayout(Rect localRect, TextAnchor alignment)
        {
            LocalRect = localRect;
            Alignment = alignment;
        }

        public void Draw(Rect parentRect)
        {
            GlobalRect = ToGlobalRect(parentRect, LocalRect);
            if (!Visible)
            {
                return;
            }

            bool oldEnabled = GUI.enabled;
            TextAnchor oldAnchor = Text.Anchor;
            GUI.enabled = oldEnabled && Enabled;
            Text.Anchor = Alignment;
            GUI.BeginGroup(GlobalRect);
            DrawContents(new Rect(0f, 0f, LocalRect.width, LocalRect.height));
            GUI.EndGroup();
            Text.Anchor = oldAnchor;
            GUI.enabled = oldEnabled;
        }

        protected Rect ToGlobalRect(Rect parentRect, Rect localRect)
        {
            return new Rect(parentRect.x + localRect.x, parentRect.y + localRect.y, localRect.width, localRect.height);
        }

        public virtual void WidgetTick()
        {
        }

        public virtual void Notify_Attached(WidgetHost newHost)
        {
            host = newHost;
        }

        public virtual void Notify_Detached()
        {
            host = null;
        }

        public virtual void Notify_Open()
        {
        }

        public virtual void Notify_Close()
        {
        }

        protected abstract void DrawContents(Rect rect);
    }

    public enum WidgetHostKind
    {
        Gizmo,
        Window
    }

    public class WidgetHost
    {
        public readonly WidgetHostKind Kind;
        public readonly object Owner;

        public WidgetHost(WidgetHostKind kind, object owner)
        {
            Kind = kind;
            Owner = owner;
        }
    }
}
