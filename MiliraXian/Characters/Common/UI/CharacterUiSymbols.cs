using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.UI;

internal enum PerkNodeVisual { Locked, Available, Learned, Mastered }

[StaticConstructorOnStartup]
internal static class CharacterUiSymbols
{
    private static readonly Texture2D[] Symbols =
    {
        MakeSymbol(PerkNodeVisual.Locked), MakeSymbol(PerkNodeVisual.Available),
        MakeSymbol(PerkNodeVisual.Learned), MakeSymbol(PerkNodeVisual.Mastered)
    };

    internal static void Draw(Rect rect, PerkNodeVisual state, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        float size = Mathf.Min(rect.width, rect.height);
        Rect square = new(rect.center.x - size * .5f, rect.center.y - size * .5f, size, size);
        Color previous = GUI.color;
        try
        {
            GUI.color = color;
            GUI.DrawTexture(LudeonTK.UIScaling.AdjustRectToUIScaling(square), Symbols[(int)state], ScaleMode.ScaleToFit);
        }
        finally { GUI.color = previous; }
    }

    // Bake each vector silhouette once, including its joins, rather than rotating
    // separate tiny GUI strokes. Every use shares the same proportions and padding.
    private static Texture2D MakeSymbol(PerkNodeVisual state)
    {
        const int size = 64;
        (Vector2 a, Vector2 b)[] strokes = state switch
        {
            PerkNodeVisual.Available => new[]
            {
                (new Vector2(.20f, .50f), new Vector2(.80f, .50f)),
                (new Vector2(.50f, .20f), new Vector2(.50f, .80f))
            },
            PerkNodeVisual.Mastered => new[]
            {
                (new Vector2(.18f, .51f), new Vector2(.40f, .73f)),
                (new Vector2(.40f, .73f), new Vector2(.83f, .25f))
            },
            PerkNodeVisual.Locked => new[]
            {
                (new Vector2(.23f, .45f), new Vector2(.77f, .45f)),
                (new Vector2(.77f, .45f), new Vector2(.77f, .83f)),
                (new Vector2(.77f, .83f), new Vector2(.23f, .83f)),
                (new Vector2(.23f, .83f), new Vector2(.23f, .45f)),
                (new Vector2(.33f, .45f), new Vector2(.33f, .28f)),
                (new Vector2(.33f, .28f), new Vector2(.41f, .20f)),
                (new Vector2(.41f, .20f), new Vector2(.59f, .20f)),
                (new Vector2(.59f, .20f), new Vector2(.67f, .28f)),
                (new Vector2(.67f, .28f), new Vector2(.67f, .45f))
            },
            _ => System.Array.Empty<(Vector2, Vector2)>()
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new((x + .5f) / size, 1f - (y + .5f) / size);
                float distance = float.MaxValue;
                foreach (var (a, b) in strokes)
                {
                    Vector2 delta = b - a;
                    Vector2 nearest = a + delta * Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude);
                    distance = Mathf.Min(distance, (p - nearest).magnitude);
                }
                float coverage = state == PerkNodeVisual.Learned
                    ? (.28f - Mathf.Abs(p.x - .5f) - Mathf.Abs(p.y - .5f)) * size
                    : ((state == PerkNodeVisual.Locked ? .055f : .075f) - distance) * size;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(coverage + .5f));
            }
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "Perk status " + state,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
