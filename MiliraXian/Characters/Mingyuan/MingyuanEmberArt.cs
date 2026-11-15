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

    private static Dictionary<MingyuanEmberNodeDef, Texture2D> LoadIcons()
    {
        Dictionary<MingyuanEmberNodeDef, Texture2D> result = new();
        foreach (var node in MingyuanEmberBook.Config.Nodes) result[node] = node.ResolveIcon(true, out _);
        return result;
    }
    internal static Texture2D Icon(MingyuanEmberNodeDef node) => Icons[node];
    internal static string T(string id, string source) => EmberText.Get(id, source);

    internal static void Frame(Rect rect, Color tint, float corner = 24f) => CharacterUiFrame.Draw(rect, Panel, tint, corner);

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
        Rect badge = new(rect.xMax - 8, rect.yMax - 10, 15, 15);
        Widgets.DrawBoxSolid(badge, Dark);
        Symbol(badge.ContractedBy(2), state, edge);
    }
    internal static void Symbol(Rect rect, EmberNodeVisual state, Color color)
    {
        Vector2 c = rect.center;
        if (state == EmberNodeVisual.Available)
        {
            Widgets.DrawLine(new(rect.x, c.y), new(rect.xMax, c.y), color, 1.2f);
            Widgets.DrawLine(new(c.x, rect.y), new(c.x, rect.yMax), color, 1.2f);
        }
        else if (state == EmberNodeVisual.Mastered)
        {
            Widgets.DrawLine(new(rect.x, c.y), new(c.x - 1, rect.yMax), color, 1.1f);
            Widgets.DrawLine(new(c.x - 1, rect.yMax), new(rect.xMax, rect.y), color, 1.1f);
        }
        else if (state == EmberNodeVisual.Learned) Widgets.DrawBoxSolid(rect.ContractedBy(2), color);
        else
        {
            Rect body = new(rect.x, c.y, rect.width, rect.height * .5f);
            Color old = GUI.color; GUI.color = color; Widgets.DrawBox(body); GUI.color = old;
            Widgets.DrawLine(new(rect.x + 2, c.y), new(rect.x + 2, rect.y), color, 1);
            Widgets.DrawLine(new(rect.x + 2, rect.y), new(rect.xMax - 2, rect.y), color, 1);
            Widgets.DrawLine(new(rect.xMax - 2, rect.y), new(rect.xMax - 2, c.y), color, 1);
        }
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
