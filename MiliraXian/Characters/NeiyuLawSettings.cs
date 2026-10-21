using System.Reflection;
using HarmonyLib;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Mingyuan;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.QingHe;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public enum SpecialPawnConsciousnessLockMode
    {
        Lock100,
        Lock35,
        None
    }

    public class NeiyuLawSettings : ModSettings
    {
        public bool EnableAriandelSpecialPawnIntegration = true;
        public bool EnableUpdateLogLetters = true;
        public bool EnableScreenBloom = true;
        public ArcaneVfxQuality ArcaneVisualQuality = ArcaneVfxQuality.Medium;
        public SpecialPawnConsciousnessLockMode ConsciousnessLockMode = SpecialPawnConsciousnessLockMode.Lock100;
        public bool NeiyuDecorativeMode;
        public bool ZhaoliDecorativeMode;
        public bool MingyuanDecorativeMode;
        public bool QingheDecorativeMode;
        private bool legacyLockSpecialPawnConsciousness = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableAriandelSpecialPawnIntegration, "EnableAriandelSpecialPawnIntegration", true);
            Scribe_Values.Look(ref EnableUpdateLogLetters, "EnableUpdateLogLetters", true);
            Scribe_Values.Look(ref EnableScreenBloom, "EnableScreenBloom", true);
            Scribe_Values.Look(ref ArcaneVisualQuality, "ArcaneVisualQuality", ArcaneVfxQuality.Medium);
            if (ArcaneVisualQuality < ArcaneVfxQuality.Low || ArcaneVisualQuality > ArcaneVfxQuality.High)
                ArcaneVisualQuality = ArcaneVfxQuality.Medium;
            CharacterPowerProfile.LookDecorative(ref NeiyuDecorativeMode, "NeiyuDecorativeMode", "NeiyuPowerLevel");
            CharacterPowerProfile.LookDecorative(ref ZhaoliDecorativeMode, "ZhaoliDecorativeMode", "ZhaoliPowerLevel");
            CharacterPowerProfile.LookDecorative(ref MingyuanDecorativeMode, "MingyuanDecorativeMode", "MingyuanPowerLevel");
            CharacterPowerProfile.LookDecorative(ref QingheDecorativeMode, "QingheDecorativeMode", "QinghePowerLevel");

            bool hasNewConsciousnessLockMode = Scribe.mode != LoadSaveMode.LoadingVars
                || Scribe.loader.curXmlParent["SpecialPawnConsciousnessLockMode"] != null;
            Scribe_Values.Look(ref ConsciousnessLockMode, "SpecialPawnConsciousnessLockMode", SpecialPawnConsciousnessLockMode.Lock100);
            Scribe_Values.Look(ref legacyLockSpecialPawnConsciousness, "LockSpecialPawnConsciousness", true);

            if (Scribe.mode == LoadSaveMode.LoadingVars && !hasNewConsciousnessLockMode)
            {
                ConsciousnessLockMode = legacyLockSpecialPawnConsciousness
                    ? SpecialPawnConsciousnessLockMode.Lock100
                    : SpecialPawnConsciousnessLockMode.None;
            }
        }
    }

    public class NeiyuLawMod : Mod
    {
        public static NeiyuLawMod Instance { get; private set; }

        public NeiyuLawSettings Settings;
        private Vector2 settingsScroll;
        private float settingsHeight = 950f;

        public NeiyuLawMod(ModContentPack content)
            : base(content)
        {
            // ShaderDefRegistry discovers Shaders automatically. Keep this mod's source
            // in its established authoring directory using Waylith's public search API.
            Waylith.Rendering.ShaderSourceLoader.RegisterShaderSourceDirectory(
                System.IO.Path.Combine(content.RootDir, "Content", "ShaderSources"));
            // Def-loading hooks (e.g. PawnSpecialResourceStats) must be registered before PlayDataLoader runs;
            // StaticConstructorOnStartup fires too late, so PatchAll lives in the mod constructor.
            new Harmony("HeChuanRiver.MiliraXian.Characters").PatchAll(Assembly.GetExecutingAssembly());
            Settings = GetSettings<NeiyuLawSettings>();
            Instance = this;
            NeiyuPowerBalance.SetDecorative(Settings.NeiyuDecorativeMode);
            ZhaoliPowerBalance.SetDecorative(Settings.ZhaoliDecorativeMode);
            MingyuanPowerBalance.SetDecorative(Settings.MingyuanDecorativeMode);
            QinghePowerBalance.SetDecorative(Settings.QingheDecorativeMode);
        }

        public override string SettingsCategory()
        {
            return "MX_NL_ModSettingsTitle".Translate().ToString();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new();
            Rect viewRect = new(0f, 0f, inRect.width - 20f, settingsHeight);
            Widgets.BeginScrollView(inRect, ref settingsScroll, viewRect);
            listing.Begin(viewRect);
            listing.CheckboxLabeled(
                "MX_NL_EnableSpecialPawnIntegrationLabel".Translate().ToString(),
                ref Settings.EnableAriandelSpecialPawnIntegration,
                "MX_NL_EnableSpecialPawnIntegrationDesc".Translate().ToString());
            listing.Gap();
            listing.Label("世界奥术打击：特效质量");
            if (listing.RadioButton("低", Settings.ArcaneVisualQuality == ArcaneVfxQuality.Low))
                Settings.ArcaneVisualQuality = ArcaneVfxQuality.Low;
            if (listing.RadioButton("中", Settings.ArcaneVisualQuality == ArcaneVfxQuality.Medium))
                Settings.ArcaneVisualQuality = ArcaneVfxQuality.Medium;
            if (listing.RadioButton("高", Settings.ArcaneVisualQuality == ArcaneVfxQuality.High))
                Settings.ArcaneVisualQuality = ArcaneVfxQuality.High;
            listing.Gap();
            listing.CheckboxLabeled(
                "MX_NL_EnableUpdateLogLettersLabel".Translate().ToString(),
                ref Settings.EnableUpdateLogLetters,
                "MX_NL_EnableUpdateLogLettersDesc".Translate().ToString());
            listing.Gap();
            listing.CheckboxLabeled(
                "MX_NL_EnableScreenBloomLabel".Translate().ToString(),
                ref Settings.EnableScreenBloom,
                "MX_NL_EnableScreenBloomDesc".Translate().ToString());
            if (listing.ButtonText("MX_NL_ViewUpdateLogsButton".Translate().ToString()))
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    NeiyuLawUpdateLogUtility.AllUpdateLogsText(),
                    title: "MX_NL_UpdateLogDialogTitle".Translate().ToString()));
            }
            listing.Gap();
            DrawDecorativeOption(listing, "MX_NeiyuDecorativeMode", "霓羽：观赏模式",
                "MX_NL_NeiyuPowerLevelDecorativeDesc", ref Settings.NeiyuDecorativeMode, NeiyuPowerBalance.SetDecorative);
            DrawDecorativeOption(listing, "MX_ZhaoliDecorativeMode", "昭离：观赏模式",
                "MX_Power_Zhaoli_Decorative", ref Settings.ZhaoliDecorativeMode, ZhaoliPowerBalance.SetDecorative);
            DrawDecorativeOption(listing, "MX_MingyuanDecorativeMode", "明渊：观赏模式",
                "MX_Power_Mingyuan_Decorative", ref Settings.MingyuanDecorativeMode, MingyuanPowerBalance.SetDecorative);
            DrawDecorativeOption(listing, "MX_QingheDecorativeMode", "清荷：观赏模式",
                "MX_Power_Qinghe_Decorative", ref Settings.QingheDecorativeMode, QinghePowerBalance.ApplyDecorative);
            listing.Gap();
            listing.Label("MX_NL_SpecialPawnConsciousnessLockLabel".Translate().ToString());
            DrawConsciousnessLockOption(
                listing,
                SpecialPawnConsciousnessLockMode.Lock100,
                "MX_NL_SpecialPawnConsciousnessLock100Label",
                "MX_NL_SpecialPawnConsciousnessLock100Desc");
            DrawConsciousnessLockOption(
                listing,
                SpecialPawnConsciousnessLockMode.Lock35,
                "MX_NL_SpecialPawnConsciousnessLock35Label",
                "MX_NL_SpecialPawnConsciousnessLock35Desc");
            DrawConsciousnessLockOption(
                listing,
                SpecialPawnConsciousnessLockMode.None,
                "MX_NL_SpecialPawnConsciousnessLockNoneLabel",
                "MX_NL_SpecialPawnConsciousnessLockNoneDesc");
            settingsHeight = listing.CurHeight + 20f;
            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawDecorativeOption(Listing_Standard listing, string labelKey, string sourceLabel,
            string tooltipKey, ref bool selected, System.Action<bool> apply)
        {
            bool previous = selected;
            listing.CheckboxLabeled(labelKey.CanTranslate() ? labelKey.Translate().ToString() : sourceLabel,
                ref selected, tooltipKey.CanTranslate() ? tooltipKey.Translate().ToString()
                    : "保留外观和剧情，封印主动技能与战斗被动。修习等级和已解锁节点仍会保留。");
            if (selected != previous) apply(selected);
        }

        private void DrawConsciousnessLockOption(Listing_Standard listing, SpecialPawnConsciousnessLockMode mode, string labelKey, string tooltipKey)
        {
            if (listing.RadioButton(
                labelKey.Translate().ToString(),
                Settings.ConsciousnessLockMode == mode,
                24f,
                tooltipKey.Translate().ToString()))
            {
                Settings.ConsciousnessLockMode = mode;
            }
        }

        public override void WriteSettings()
        {
            NeiyuPowerBalance.SetDecorative(Settings.NeiyuDecorativeMode);
            ZhaoliPowerBalance.SetDecorative(Settings.ZhaoliDecorativeMode);
            MingyuanPowerBalance.SetDecorative(Settings.MingyuanDecorativeMode);
            QinghePowerBalance.ApplyDecorative(Settings.QingheDecorativeMode);
            base.WriteSettings();
        }
    }
}
