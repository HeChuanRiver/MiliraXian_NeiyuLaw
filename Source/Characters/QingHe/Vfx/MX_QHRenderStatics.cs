using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Vfx
{
    [StaticConstructorOnStartup]
    public static class MX_QHRenderStatics
    {
        private const string DiamondSolidTexPath = "MiliraXianQinghe/UI/MX_QH_DiamondSolid";

        public static readonly Color AfterimageTint = new(1f, 0.94f, 0.97f, 1f);
        public static readonly Texture2D DiamondSolidTex = ContentFinder<Texture2D>.Get(DiamondSolidTexPath);
    }
}
