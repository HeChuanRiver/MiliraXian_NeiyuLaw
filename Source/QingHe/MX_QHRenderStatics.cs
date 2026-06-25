using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    [StaticConstructorOnStartup]
    public static class MX_QHRenderStatics
    {
        private const string DiamondSolidTexPath = "MiliraXianQinghe/UI/MX_QH_DiamondSolid";
        private const string FlowerBellEnhanceTexPath = "MiliraXianQinghe/UI/MX_QH_FlowerBellEnhance_Diamond";
        private const string FlowerBellEnhanceGrayTexPath = "MiliraXianQinghe/UI/MX_QH_FlowerBellEnhance_Diamond_Gray";
        private const string TimedFlowerMandatePeachTexPath = "MiliraXianQinghe/UI/MX_QH_TimedFlowerMandate_Peach_Diamond";
        private const string TimedFlowerMandatePomegranateTexPath = "MiliraXianQinghe/UI/MX_QH_TimedFlowerMandate_Pomegranate_Diamond";
        private const string TimedFlowerMandateChrysanthemumTexPath = "MiliraXianQinghe/UI/MX_QH_TimedFlowerMandate_Chrysanthemum_Diamond";
        private const string TimedFlowerMandateWintersweetTexPath = "MiliraXianQinghe/UI/MX_QH_TimedFlowerMandate_Wintersweet_Diamond";

        public static readonly MaterialPropertyBlock SharedPropertyBlock = new MaterialPropertyBlock();
        public static readonly Texture2D DiamondSolidTex = ContentFinder<Texture2D>.Get(DiamondSolidTexPath);
        public static readonly Texture2D FlowerBellEnhanceTex = ContentFinder<Texture2D>.Get(FlowerBellEnhanceTexPath);
        public static readonly Texture2D FlowerBellEnhanceGrayTex = ContentFinder<Texture2D>.Get(FlowerBellEnhanceGrayTexPath);
        public static readonly Texture2D TimedFlowerMandatePeachTex = ContentFinder<Texture2D>.Get(TimedFlowerMandatePeachTexPath);
        public static readonly Texture2D TimedFlowerMandatePomegranateTex = ContentFinder<Texture2D>.Get(TimedFlowerMandatePomegranateTexPath);
        public static readonly Texture2D TimedFlowerMandateChrysanthemumTex = ContentFinder<Texture2D>.Get(TimedFlowerMandateChrysanthemumTexPath);
        public static readonly Texture2D TimedFlowerMandateWintersweetTex = ContentFinder<Texture2D>.Get(TimedFlowerMandateWintersweetTexPath);

        public static Texture2D TimedFlowerMandateTexForDefName(string defName)
        {
            switch (defName)
            {
                case "MX_QH_FlowerMandate_Peach":
                    return TimedFlowerMandatePeachTex;
                case "MX_QH_FlowerMandate_Pomegranate":
                    return TimedFlowerMandatePomegranateTex;
                case "MX_QH_FlowerMandate_Chrysanthemum":
                    return TimedFlowerMandateChrysanthemumTex;
                case "MX_QH_FlowerMandate_Wintersweet":
                    return TimedFlowerMandateWintersweetTex;
                default:
                    return null;
            }
        }
    }
}
