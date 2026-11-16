using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.UI;

/// <summary>Shared native drawing with character-authored colors and nine-slice materials.</summary>
internal sealed class CharacterPerkArt(Texture2D panel, Color ink, Color muted, Color accent, Color rule,
    Color dark, Color iconTint, Color selection)
{
    internal readonly Color Ink = ink, Muted = muted, Gold = accent, Rule = rule, Dark = dark;
    private readonly Color IconTint = iconTint, Selection = selection;
    private static readonly Vector2[] RingPoints = MakeRing();
    internal void Frame(Rect rect, Color tint, float corner = 24f, bool drawCenter = true) =>
        CharacterUiFrame.Draw(rect, panel, tint, corner, drawCenter);
    internal void Label(Rect rect, string text, Color color, GameFont font = GameFont.Small,
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
    internal float Height(string text, float width, GameFont font = GameFont.Small)
    {
        GameFont previous = Text.Font; bool wrap = Text.WordWrap;
        try { Text.Font = font; Text.WordWrap = true; return Mathf.Max(22, Text.CalcHeight(text ?? "", width)); }
        finally { Text.Font = previous; Text.WordWrap = wrap; }
    }
    internal void TextBlock(float width, ref float y, string text, Color color, GameFont font = GameFont.Small)
    {
        float height = Height(text, width, font);
        Label(new Rect(0, y, width, height), text, color, font); y += height + 8;
    }
    internal bool Button(Rect rect, string label, bool active = false, bool enabled = true, bool quiet = false)
    {
        bool hover = enabled && Mouse.IsOver(rect);
        if (!quiet || active || hover)
        {
            Frame(rect, enabled ? active || hover ? Color.white : new Color(.72f, .69f, .64f) : new Color(.48f, .46f, .43f), 13);
            if (active) Widgets.DrawBoxSolid(rect.ContractedBy(4), Selection);
        }
        Label(rect.ContractedBy(5), label, enabled ? active || hover ? Ink : Muted : new Color(.45f, .42f, .37f),
            GameFont.Small, TextAnchor.MiddleCenter);
        return enabled && Widgets.ButtonInvisible(rect);
    }
    internal void Texture(Rect rect, Texture2D texture, Color tint)
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
    internal void Ring(Vector2 center, float radius, Color color, float width = 1)
    {
        if (Event.current.type != EventType.Repaint) return;
        for (int i = 0; i < RingPoints.Length; i++)
            Widgets.DrawLine(center + RingPoints[i] * radius, center + RingPoints[(i + 1) % RingPoints.Length] * radius, color, width);
    }
    internal void Node(Rect rect, Texture2D icon, PerkNodeVisual state, bool selected, bool dim)
    {
        float opacity = dim ? .22f : 1f;
        Color edge = state == PerkNodeVisual.Locked ? Rule : state == PerkNodeVisual.Learned ? Ink : Gold;
        edge.a = opacity;
        if (!dim && (selected || state == PerkNodeVisual.Available))
            Ring(rect.center, rect.width * .5f + 2, new Color(Gold.r, Gold.g, Gold.b, selected ? .16f : .08f), 4);
        Texture(rect.ContractedBy(3), icon, new Color(IconTint.r, IconTint.g, IconTint.b, opacity * (state == PerkNodeVisual.Locked ? .4f : 1)));
        Ring(rect.center, rect.width * .5f, edge);
        if (state == PerkNodeVisual.Mastered) Ring(rect.center, rect.width * .5f + 3, edge, .7f);
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
    internal void Symbol(Rect rect, PerkNodeVisual state, Color color) =>
        CharacterUiSymbols.Draw(rect, state, color);

    internal void Ranks(Vector2 center, int rank, int maximum, Color color)
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
