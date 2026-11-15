using System.Collections.Generic;
using MiliraXian.Characters.Common.PerkSystem;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    // Resolve shared artwork during startup, never while opening or drawing a window.
    [StaticConstructorOnStartup]
    internal static class CultivationArt
    {
        private const string Folder = "MiliraXianNeiyu/UI/Cultivation/";
        public static readonly Texture2D Emblem = ContentFinder<Texture2D>.Get(Folder + "Emblem");
        public static readonly Texture2D SoftBackdrop = ContentFinder<Texture2D>.Get(Folder + "SoftBackdrop");
        public static readonly Texture2D Panel = ContentFinder<Texture2D>.Get(Folder + "Panel");
        public static readonly Texture2D Button = ContentFinder<Texture2D>.Get(Folder + "Button");
        public static readonly Texture2D ButtonActive = ContentFinder<Texture2D>.Get(Folder + "ButtonActive");
        private static readonly Dictionary<CharacterPerkCategoryDef, Texture2D> CategoryIcons = LoadCategoryIcons();
        private static Dictionary<CharacterPerkCategoryDef, Texture2D> LoadCategoryIcons()
        {
            Dictionary<CharacterPerkCategoryDef, Texture2D> icons = new();
            foreach (var category in DefDatabase<CharacterPerkCategoryDef>.AllDefsListForReading)
                if (!category.iconPath.NullOrEmpty()) icons[category] = ContentFinder<Texture2D>.Get(category.iconPath);
            return icons;
        }
        public static Texture2D ForCategory(CharacterPerkCategoryDef category) =>
            category != null && CategoryIcons.TryGetValue(category, out var icon) ? icon : Emblem;

        public static void DrawFrame(Rect rect, Texture2D texture, Color tint, float corner = 28f) =>
            Common.UI.CharacterUiFrame.Draw(rect, texture, tint, corner);
    }
}
