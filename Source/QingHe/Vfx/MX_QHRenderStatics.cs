using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Vfx
{
    [StaticConstructorOnStartup]
    public static class MX_QHRenderStatics
    {
        private const string DiamondSolidTexPath = "MiliraXianQinghe/UI/MX_QH_DiamondSolid";

        public static readonly MaterialPropertyBlock SharedPropertyBlock = new MaterialPropertyBlock();
        public static readonly Texture2D DiamondSolidTex = ContentFinder<Texture2D>.Get(DiamondSolidTexPath);
    }
}
