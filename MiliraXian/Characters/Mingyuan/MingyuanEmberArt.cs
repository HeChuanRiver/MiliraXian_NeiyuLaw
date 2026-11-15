using System.Collections.Generic;
using MiliraXian.Characters.Common.UI;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

internal enum EmberNodeVisual { Locked, Available, Learned, Mastered }

/// <summary>Shared artwork is loaded at startup; labels and interactions remain native UI.</summary>
[StaticConstructorOnStartup]
internal static class MingyuanEmberArt
{
    private const string Folder = "MiliraXianMingyuan/UI/EmberBook/";
    internal static readonly Texture2D Backdrop = ContentFinder<Texture2D>.Get(Folder + "Backdrop");
    private static readonly Texture2D Panel = ContentFinder<Texture2D>.Get(Folder + "Panel");
    private static readonly Dictionary<MingyuanEmberNodeDef, Texture2D> Icons = LoadIcons();
    internal static readonly Color Ink = new(.94f, .87f, .74f);
    internal static readonly Color Muted = new(.67f, .61f, .53f);
    internal static readonly Color Gold = new(.91f, .64f, .32f);
    internal static readonly Color Rule = new(.34f, .26f, .18f);
    internal static readonly Color Dark = new(.09f, .075f, .065f);
    private static readonly Vector2[] RingPoints = MakeRing();
    private static readonly Texture2D[] StatusSymbols =
    {
        MakeSymbol(EmberNodeVisual.Locked), MakeSymbol(EmberNodeVisual.Available),
        MakeSymbol(EmberNodeVisual.Learned), MakeSymbol(EmberNodeVisual.Mastered)
    };

    private static Dictionary<MingyuanEmberNodeDef, Texture2D> LoadIcons()
    {
        Dictionary<MingyuanEmberNodeDef, Texture2D> result = new();
        foreach (var node in MingyuanEmberBook.Config.Nodes) result[node] = node.ResolveIcon(true, out _);
        return result;
    }
    internal static Texture2D Icon(MingyuanEmberNodeDef node) => Icons[node];
    internal static string T(string id, string source) => EmberText.Get(id, source);

    internal static void Frame(Rect rect, Color tint, float corner = 24f, bool drawCenter = true) =>
        CharacterUiFrame.Draw(rect, Panel, tint, corner, drawCenter);

    internal static void Label(Rect rect, string text, Color color, GameFont font = GameFont.Small,
        TextAnchor anchor = TextAnchor.UpperLeft)
    {
        Color old = GUI.color; GameFont oldFont = Text.Font; TextAnchor oldAnchor = Text.Anchor; bool wrap = Text.WordWrap;
        try
        {
            GUI.color = color; Text.Font = font; Text.Anchor = anchor; Text.WordWrap = true;
            Widgets.Label(rect, text ?? "");
        }
        finally { GUI.color = old; Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = wrap; }
    }
    internal static float Height(string text, float width, GameFont font = GameFont.Small)
    {
        GameFont previous = Text.Font; bool wrap = Text.WordWrap;
        try { Text.Font = font; Text.WordWrap = true; return Mathf.Max(22, Text.CalcHeight(text ?? "", width)); }
        finally { Text.Font = previous; Text.WordWrap = wrap; }
    }
    internal static void TextBlock(float width, ref float y, string text, Color color, GameFont font = GameFont.Small)
    {
        float height = Height(text, width, font);
        Label(new Rect(0, y, width, height), text, color, font); y += height + 8;
    }
    internal static bool Button(Rect rect, string label, bool active = false, bool enabled = true, bool quiet = false)
    {
        bool hover = enabled && Mouse.IsOver(rect);
        if (!quiet || active || hover)
        {
            Frame(rect, enabled ? active || hover ? Color.white : new Color(.72f, .69f, .64f) : new Color(.48f, .46f, .43f), 13);
            if (active) Widgets.DrawBoxSolid(rect.ContractedBy(4), new Color(.52f, .19f, .045f, .19f));
        }
        Label(rect.ContractedBy(5), label, enabled ? active || hover ? Ink : Muted : new Color(.45f, .42f, .37f),
            GameFont.Small, TextAnchor.MiddleCenter);
        return enabled && Widgets.ButtonInvisible(rect);
    }
    internal static void Texture(Rect rect, Texture2D texture, Color tint)
    {
        Color old = GUI.color;
        GUI.color = tint; GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit); GUI.color = old;
    }
    private static Vector2[] MakeRing()
    {
        Vector2[] points = new Vector2[12];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / points.Length;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        return points;
    }
    internal static void Ring(Vector2 center, float radius, Color color, float width = 1)
    {
        if (Event.current.type != EventType.Repaint) return;
        for (int i = 0; i < RingPoints.Length; i++)
            Widgets.DrawLine(center + RingPoints[i] * radius, center + RingPoints[(i + 1) % RingPoints.Length] * radius, color, width);
    }
    internal static void Node(Rect rect, Texture2D icon, EmberNodeVisual state, bool selected, bool dim)
    {
        float opacity = dim ? .22f : 1f;
        Color edge = state == EmberNodeVisual.Locked ? Rule : state == EmberNodeVisual.Learned ? Ink : Gold;
        edge.a = opacity;
        if (!dim && (selected || state == EmberNodeVisual.Available))
            Ring(rect.center, rect.width * .5f + 2, new Color(Gold.r, Gold.g, Gold.b, selected ? .16f : .08f), 4);
        Texture(rect.ContractedBy(3), icon, new Color(1, .91f, .78f, opacity * (state == EmberNodeVisual.Locked ? .4f : 1)));
        Ring(rect.center, rect.width * .5f, edge);
        if (state == EmberNodeVisual.Mastered) Ring(rect.center, rect.width * .5f + 3, edge, .7f);
        if (selected)
        {
            Rect mark = rect.ExpandedBy(7);
            float length = Mathf.Min(10, rect.width * .22f);
            foreach (Vector2 p in new[] { mark.min, new Vector2(mark.xMax, mark.y), new Vector2(mark.x, mark.yMax), mark.max })
            {
                Widgets.DrawLine(p, p + new Vector2(p.x == mark.x ? length : -length, 0), Ink, 1.4f);
                Widgets.DrawLine(p, p + new Vector2(0, p.y == mark.y ? length : -length), Ink, 1.4f);
            }
        }
        Rect badge = new(rect.xMax - 9, rect.yMax - 10, 16, 16);
        Widgets.DrawBoxSolid(badge, Dark);
        Symbol(badge, state, edge);
    }
    internal static void Symbol(Rect rect, EmberNodeVisual state, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        float size = Mathf.Min(rect.width, rect.height);
        Rect square = new(rect.center.x - size * .5f, rect.center.y - size * .5f, size, size);
        Texture(LudeonTK.UIScaling.AdjustRectToUIScaling(square), StatusSymbols[(int)state], color);
    }

    // Bake each vector silhouette once, including its joins, rather than rotating
    // separate tiny GUI strokes. Every use shares the same proportions and padding.
    private static Texture2D MakeSymbol(EmberNodeVisual state)
    {
        const int size = 64;
        (Vector2 a, Vector2 b)[] strokes = state switch
        {
            EmberNodeVisual.Available => new[]
            {
                (new Vector2(.20f, .50f), new Vector2(.80f, .50f)),
                (new Vector2(.50f, .20f), new Vector2(.50f, .80f))
            },
            EmberNodeVisual.Mastered => new[]
            {
                (new Vector2(.18f, .51f), new Vector2(.40f, .73f)),
                (new Vector2(.40f, .73f), new Vector2(.83f, .25f))
            },
            EmberNodeVisual.Locked => new[]
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
                float coverage = state == EmberNodeVisual.Learned
                    ? (.28f - Mathf.Abs(p.x - .5f) - Mathf.Abs(p.y - .5f)) * size
                    : ((state == EmberNodeVisual.Locked ? .055f : .075f) - distance) * size;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(coverage + .5f));
            }
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "Mingyuan status " + state,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
    internal static void Ranks(Vector2 center, int rank, int maximum, Color color)
    {
        float start = center.x - (maximum - 1) * 5;
        for (int i = 0; i < maximum; i++)
        {
            Rect pip = new(start + i * 10 - 2, center.y - 2, 4, 4);
            if (i < rank) Widgets.DrawBoxSolid(pip, color);
            else { Color old = GUI.color; GUI.color = Rule; Widgets.DrawBox(pip); GUI.color = old; }
        }
    }
}
