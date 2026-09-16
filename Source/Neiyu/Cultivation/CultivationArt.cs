using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    // Resolve the five shared sprites during startup, never while opening or drawing a window.
    [StaticConstructorOnStartup]
    internal static class CultivationArt
    {
        private const string Folder = "MiliraXianNeiyu/UI/Cultivation/";
        public static readonly Texture2D Emblem = ContentFinder<Texture2D>.Get(Folder + "Emblem");
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
    }
}
