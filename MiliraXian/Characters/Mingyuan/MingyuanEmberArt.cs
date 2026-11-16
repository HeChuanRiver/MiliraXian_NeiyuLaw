using System.Collections.Generic;
using MiliraXian.Characters.Common.UI;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

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
    internal static readonly CharacterPerkArt Theme = new(Panel, Ink, Muted, Gold, Rule, Dark,
        new Color(1, .91f, .78f), new Color(.52f, .19f, .045f, .19f));
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

    internal static void Label(Rect r, string text, Color color, GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.UpperLeft) => Theme.Label(r, text, color, font, anchor);
    internal static float Height(string text, float width, GameFont font = GameFont.Small) => Theme.Height(text, width, font);
    internal static void TextBlock(float width, ref float y, string text, Color color, GameFont font = GameFont.Small) => Theme.TextBlock(width, ref y, text, color, font);
    internal static bool Button(Rect r, string text, bool active = false, bool enabled = true, bool quiet = false) => Theme.Button(r, text, active, enabled, quiet);
    internal static void Texture(Rect r, Texture2D texture, Color tint) => Theme.Texture(r, texture, tint);
    internal static void Ring(Vector2 center, float radius, Color color, float width = 1) => Theme.Ring(center, radius, color, width);
    internal static void Node(Rect r, Texture2D icon, PerkNodeVisual state, bool selected, bool dim) => Theme.Node(r, icon, state, selected, dim);
    internal static void Symbol(Rect r, PerkNodeVisual state, Color color) => CharacterUiSymbols.Draw(r, state, color);
}
