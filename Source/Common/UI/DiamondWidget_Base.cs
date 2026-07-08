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

        protected static void DrawDiamond(Rect rect, Texture2D texture, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f || texture == null)
            {
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
        }

        protected static void DrawDiamondOriginal(Rect rect, Texture2D texture)
        {
            if (rect.width <= 0f || rect.height <= 0f || texture == null)
            {
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
        }

        protected static void DrawDiamondFill(Rect rect, Texture2D texture, float fillPercent, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f || texture == null)
            {
                return;
            }

            fillPercent = Mathf.Clamp01(fillPercent);
            float height = rect.height * fillPercent;
            if (height <= 0f)
            {
                return;
            }

            Rect fillRect = new Rect(rect.x, rect.yMax - height, rect.width, height);
            Rect texCoords = new Rect(0f, 0f, 1f, fillPercent);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(fillRect, texture, texCoords, true);
            GUI.color = oldColor;
        }

        protected static Rect CenteredSquare(Rect rect, float scale)
        {
            float size = Mathf.Min(rect.width, rect.height) * Mathf.Clamp01(scale);
            return new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size);
        }
    }
}
