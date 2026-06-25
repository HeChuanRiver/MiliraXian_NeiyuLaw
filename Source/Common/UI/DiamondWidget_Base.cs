using UnityEngine;
using Verse;

namespace MiliraXian.Characters.UI
{
    public abstract class DiamondWidget_Base : Widget_Base
    {
        protected DiamondWidget_Base(Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
        }

        protected override bool MouseIsOverHitbox(Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }

            Vector2 mousePosition = Event.current.mousePosition;
            if (!rect.Contains(mousePosition))
            {
                return false;
            }

            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;
            if (halfWidth <= 0f || halfHeight <= 0f)
            {
                return false;
            }

            float normalizedX = Mathf.Abs(mousePosition.x - rect.center.x) / halfWidth;
            float normalizedY = Mathf.Abs(mousePosition.y - rect.center.y) / halfHeight;
            return normalizedX + normalizedY <= 1f;
        }
    }
}
