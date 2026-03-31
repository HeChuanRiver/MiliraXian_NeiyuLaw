using System;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    internal static class ZhaoliEffectUtility
    {
        private static readonly Lazy<ThingDef> GuiyiLinkLineMoteDefLazy = new Lazy<ThingDef>(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiLinkLine"));
        private static readonly Lazy<ThingDef> GuiyiLinkPulseMoteDefLazy = new Lazy<ThingDef>(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiLinkPulse"));
        private static readonly Lazy<ThingDef> GuiyiLinkStripeMoteDefLazy = new Lazy<ThingDef>(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiLinkStripe"));
        private static readonly Lazy<ThingDef> GuiyiHealGlowMoteDefLazy = new Lazy<ThingDef>(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiHealGlow"));
        private static readonly Lazy<ThingDef> DeathFieldAreaMoteDefLazy = new Lazy<ThingDef>(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DeathFieldArea"));
        private static readonly Lazy<ThingDef> DeathFieldMarkMoteDefLazy = new Lazy<ThingDef>(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DeathFieldMark"));
        private static readonly Lazy<ThingDef> SoulAbsorbPulseMoteDefLazy = new Lazy<ThingDef>(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_SoulAbsorbPulse"));
        private static readonly Lazy<ThingDef> MinshenWarnAreaMoteDefLazy = new Lazy<ThingDef>(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_MinshenWarnArea"));
        private static readonly Lazy<ThingDef> GroundCrackHugeMoteDefLazy = new Lazy<ThingDef>(() => DefDatabase<ThingDef>.GetNamedSilentFail("GroundCrackHuge"));
        private static readonly Lazy<FleckDef> DeathRefusalBubbleFleckDefLazy = new Lazy<FleckDef>(() => DefDatabase<FleckDef>.GetNamedSilentFail("DeathRefusalBubble"));
        private static readonly Lazy<FleckDef> DeathRefusalPulseFleckDefLazy = new Lazy<FleckDef>(() => DefDatabase<FleckDef>.GetNamedSilentFail("DeathRefusalPulse"));
        private static readonly Lazy<HediffDef> MinghuoHediffDefLazy = new Lazy<HediffDef>(() => DefDatabase<HediffDef>.GetNamedSilentFail(ZhaoliMinghuoUtility.MinghuoHediffDefName));
        private static readonly Lazy<HediffDef> DormancyHediffDefLazy = new Lazy<HediffDef>(() => DefDatabase<HediffDef>.GetNamedSilentFail(ZhaoliKarmaUtility.DormancyHediffDefName));

        public static ThingDef GuiyiLinkLineMoteDef => GuiyiLinkLineMoteDefLazy.Value;

        public static ThingDef GuiyiLinkPulseMoteDef => GuiyiLinkPulseMoteDefLazy.Value;

        public static ThingDef GuiyiLinkStripeMoteDef => GuiyiLinkStripeMoteDefLazy.Value;

        public static ThingDef GuiyiHealGlowMoteDef => GuiyiHealGlowMoteDefLazy.Value;

        public static ThingDef DeathFieldAreaMoteDef => DeathFieldAreaMoteDefLazy.Value;

        public static ThingDef DeathFieldMarkMoteDef => DeathFieldMarkMoteDefLazy.Value;

        public static ThingDef SoulAbsorbPulseMoteDef => SoulAbsorbPulseMoteDefLazy.Value;

        public static ThingDef MinshenWarnAreaMoteDef => MinshenWarnAreaMoteDefLazy.Value;

        public static ThingDef GroundCrackHugeMoteDef => GroundCrackHugeMoteDefLazy.Value;

        public static FleckDef DeathRefusalBubbleFleckDef => DeathRefusalBubbleFleckDefLazy.Value;

        public static FleckDef DeathRefusalPulseFleckDef => DeathRefusalPulseFleckDefLazy.Value;

        public static HediffDef MinghuoHediffDef => MinghuoHediffDefLazy.Value;

        public static HediffDef DormancyHediffDef => DormancyHediffDefLazy.Value;
    }
}
