using System;
using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.Common.PerkSystem;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.UI;

internal readonly record struct PerkCanvasState(int Rank, bool Lit, string Tooltip);

/// <summary>The shared tree viewport owns navigation only. Purchases stay in the perk handler.</summary>
internal sealed class CharacterPerkCanvas<T>(IReadOnlyList<T> nodes, Func<T, Vector2> position,
    Func<T, PerkCanvasState> state, Action<Rect, T, bool, bool> drawNode,
    Color ink, Color muted, Color rule, Color accent, bool horizontal = false) where T : CharacterPerkNodeDef
{
    private readonly Dictionary<T, Rect> hits = new();
    internal readonly HashSet<T> SelectedPath = new();
    private Vector2 pan, dragStart, panStart;
    private bool dragging;
    internal Vector2 Size { get; set; }
    internal float Zoom { get; private set; } = .5f;

    internal void Select(T selected)
    {
        SelectedPath.Clear();
        void Visit(T node)
        {
            if (node == null || !SelectedPath.Add(node) || node.prerequisites == null) return;
            foreach (var parent in node.prerequisites) Visit(parent.node as T);
        }
        Visit(selected);
    }
    internal void Fit(IEnumerable<T> shown)
    {
        var visible = shown.ToList();
        if (visible.Count == 0) return;
        Vector2 min = new(visible.Min(n => position(n).x), visible.Min(n => position(n).y));
        Vector2 max = new(visible.Max(n => position(n).x), visible.Max(n => position(n).y));
        Zoom = Mathf.Clamp(Mathf.Min((Size.x - 126) / Mathf.Max(230, max.x - min.x),
            (Size.y - 112) / Mathf.Max(200, max.y - min.y)), .18f, .84f);
        pan = Size * .5f - (min + max) * .5f * Zoom - new Vector2(0, 12);
    }
    internal void Focus(T node) { Zoom = .70f; pan = Size * .5f - position(node) * Zoom; }
    internal void ZoomAt(float factor, Vector2 point)
    {
        float previous = Zoom; Zoom = Mathf.Clamp(Zoom * factor, .18f, 1.5f);
        pan = point - (point - pan) * (Zoom / previous);
    }
    internal T Draw(T selected, Predicate<T> dim, Predicate<T> shown = null)
    {
        Rect viewport = new(0, 0, Size.x, Size.y);
        bool captions = Zoom >= .29f;
        float diameter = Mathf.Clamp(Zoom * 92, 26, 62);
        float labelWidth = Mathf.Min(116, Mathf.Max(70, Zoom * 220));
        hits.Clear();
        foreach (T node in nodes)
        {
            if (shown != null && !shown(node)) continue;
            Vector2 c = pan + position(node) * Zoom;
            float width = captions ? labelWidth : Mathf.Max(32, diameter);
            hits[node] = new Rect(c.x - width * .5f, c.y - diameter * .5f, width, diameter + (captions ? 32 : 5));
        }
        Event e = Event.current;
        bool overNode = hits.Any(p => !dim(p.Key) && p.Value.Contains(e.mousePosition));
        if (e.type == EventType.ScrollWheel && viewport.Contains(e.mousePosition))
        { ZoomAt(e.delta.y < 0 ? 1.12f : 1 / 1.12f, e.mousePosition); e.Use(); }
        if (e.type == EventType.MouseDown && viewport.Contains(e.mousePosition) && (e.button == 2 || e.button == 0 && !overNode))
        { dragging = true; dragStart = e.mousePosition; panStart = pan; GUI.FocusControl(null); e.Use(); }
        if (dragging && e.type == EventType.MouseDrag) { pan = panStart + e.mousePosition - dragStart; e.Use(); }
        if (dragging && e.rawType == EventType.MouseUp) { dragging = false; e.Use(); }
        if (e.type == EventType.Ignore) dragging = false;
        foreach (T node in nodes)
        {
            if (!hits.ContainsKey(node) || node.prerequisites == null) continue;
            foreach (var requirement in node.prerequisites)
            {
                if (requirement.node is not T parent || !hits.ContainsKey(parent)) continue;
                Vector2 a = pan + position(parent) * Zoom, b = pan + position(node) * Zoom;
                if (horizontal) { a.x += diameter * .5f + 5; b.x -= diameter * .5f + 5; }
                else { a.y += diameter * .5f + (captions ? 31 : 4); b.y -= diameter * .5f + 5; }
                bool learned = state(node).Lit && state(parent).Lit;
                bool path = SelectedPath.Contains(node) && SelectedPath.Contains(parent);
                Color color = path ? accent : learned ? Color.Lerp(rule, accent, .65f) : rule;
                if (dim(node)) color.a = .18f;
                Vector2 bendA = horizontal ? new(Mathf.Lerp(a.x, b.x, .45f), a.y) : new(a.x, Mathf.Lerp(a.y, b.y, .45f));
                Vector2 bendB = horizontal ? new(bendA.x, b.y) : new(b.x, bendA.y);
                Connection(a, bendA, bendB, b, color, path || learned);
            }
        }
        T clicked = null;
        foreach (T node in nodes)
        {
            if (!hits.TryGetValue(node, out Rect hit) || !hit.Overlaps(viewport)) continue;
            bool faded = dim(node);
            PerkCanvasState view = state(node);
            Rect icon = new(hit.center.x - diameter * .5f, hit.y, diameter, diameter);
            drawNode(icon, node, node == selected, faded);
            if (captions)
            {
                Color old = GUI.color; GameFont font = Text.Font; TextAnchor anchor = Text.Anchor;
                GUI.color = faded ? rule : view.Lit ? ink : muted; Text.Font = GameFont.Tiny; Text.Anchor = TextAnchor.MiddleCenter;
                Rect caption = new(hit.x, icon.yMax + 6, hit.width, 20);
                Widgets.Label(caption, node.LabelCap);
                Text.Anchor = anchor; Text.Font = font; GUI.color = old;
                if (node.maxLevel > 1)
                    for (int i = 0; i < node.maxLevel; i++)
                    {
                        Rect pip = new(hit.center.x - (node.maxLevel - 1) * 5 + i * 10 - 2, caption.yMax + 3, 4, 4);
                        Widgets.DrawBoxSolid(pip, i < view.Rank && !faded ? accent : rule);
                    }
            }
            if (faded) continue;
            if (!dragging && Widgets.ButtonInvisible(hit)) clicked = node;
            TooltipHandler.TipRegion(hit, view.Tooltip);
        }
        return clicked;
    }
    private static void Connection(Vector2 a, Vector2 bendA, Vector2 bendB, Vector2 b, Color color, bool lit)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (lit)
        {
            Color glow = new(color.r, color.g, color.b, color.a * .12f);
            Widgets.DrawLine(a, bendA, glow, 3); Widgets.DrawLine(bendA, bendB, glow, 3); Widgets.DrawLine(bendB, b, glow, 3);
        }
        Widgets.DrawLine(a, bendA, color, lit ? 1.2f : .65f);
        Widgets.DrawLine(bendA, bendB, color, lit ? 1.2f : .65f);
        Widgets.DrawLine(bendB, b, color, lit ? 1.2f : .65f);
    }
}
