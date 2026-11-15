using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.UI;

/// <summary>Nine-slice artwork with fixed display corners, independent of source resolution.</summary>
internal static class CharacterUiFrame
{
    internal static void Draw(Rect rect, Texture2D texture, Color tint, float corner = 28f)
    {
        if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f) return;
        float aspect = (float)texture.height / texture.width;
        float xEdge = Mathf.Min(corner, rect.width * .5f, rect.height * .5f / aspect);
        float yEdge = xEdge * aspect;
        Color previous = GUI.color;
        GUI.color = tint;
        try
        {
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                {
                    Rect cell = new(
                        column == 0 ? rect.x : column == 1 ? rect.x + xEdge : rect.xMax - xEdge,
                        row == 0 ? rect.y : row == 1 ? rect.y + yEdge : rect.yMax - yEdge,
                        column == 1 ? rect.width - xEdge * 2f : xEdge,
                        row == 1 ? rect.height - yEdge * 2f : yEdge);
                    if (cell.width <= 0f || cell.height <= 0f) continue;
                    Rect uv = new(column == 0 ? 0f : column == 1 ? .25f : .75f,
                        row == 0 ? 0f : row == 1 ? .25f : .75f,
                        column == 1 ? .5f : .25f, row == 1 ? .5f : .25f);
                    Widgets.DrawTexturePart(cell, uv, texture);
                }
        }
        finally { GUI.color = previous; }
    }
}
