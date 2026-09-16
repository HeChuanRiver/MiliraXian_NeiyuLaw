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
        public static readonly Texture2D Backdrop = ContentFinder<Texture2D>.Get(Folder + "Backdrop");
        public static readonly Texture2D Panel = ContentFinder<Texture2D>.Get(Folder + "Panel");
        public static readonly Texture2D Button = ContentFinder<Texture2D>.Get(Folder + "Button");
        public static readonly Texture2D ButtonActive = ContentFinder<Texture2D>.Get(Folder + "ButtonActive");
        private static readonly Texture2D Wing = ContentFinder<Texture2D>.Get(Folder + "Wing");
        private static readonly Texture2D Arrow = ContentFinder<Texture2D>.Get(Folder + "Arrow");
        private static readonly Texture2D Halo = ContentFinder<Texture2D>.Get(Folder + "Halo");
        private static readonly Texture2D Law = ContentFinder<Texture2D>.Get(Folder + "Law");

        public static Texture2D ForBranch(CultivationBranch branch) => branch switch
        {
            CultivationBranch.Wing => Wing,
            CultivationBranch.Arrow => Arrow,
            CultivationBranch.Halo => Halo,
            _ => Law
        };

        public static void DrawFrame(Rect rect, Texture2D texture, Color tint, float corner = 28f)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f) return;
            // DrawAtlas assumes square corner cells. These generated rectangular frames
            // need proportional corners, so reuse the native texture-part renderer.
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
}
