using System.Reflection;
using HarmonyLib;
using MiliraXian.Characters.Common;
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
        public SpecialPawnConsciousnessLockMode ConsciousnessLockMode = SpecialPawnConsciousnessLockMode.Lock100;
        public CharacterPowerLevel NeiyuPowerLevel = CharacterPowerLevel.Original;
        public CharacterPowerLevel ZhaoliPowerLevel = CharacterPowerLevel.Original;
        public CharacterPowerLevel MingyuanPowerLevel = CharacterPowerLevel.Original;
        public CharacterPowerLevel QinghePowerLevel = CharacterPowerLevel.Original;
        private bool legacyLockSpecialPawnConsciousness = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableAriandelSpecialPawnIntegration, "EnableAriandelSpecialPawnIntegration", true);
            Scribe_Values.Look(ref EnableUpdateLogLetters, "EnableUpdateLogLetters", true);
            Scribe_Values.Look(ref NeiyuPowerLevel, "NeiyuPowerLevel", CharacterPowerLevel.Original);
            Scribe_Values.Look(ref ZhaoliPowerLevel, "ZhaoliPowerLevel", CharacterPowerLevel.Original);
            Scribe_Values.Look(ref MingyuanPowerLevel, "MingyuanPowerLevel", CharacterPowerLevel.Original);
            Scribe_Values.Look(ref QinghePowerLevel, "QinghePowerLevel", CharacterPowerLevel.Original);
            if (ZhaoliPowerLevel < CharacterPowerLevel.Original || ZhaoliPowerLevel > CharacterPowerLevel.Decorative)
                ZhaoliPowerLevel = CharacterPowerLevel.Original;
            if (MingyuanPowerLevel < CharacterPowerLevel.Original || MingyuanPowerLevel > CharacterPowerLevel.Decorative)
                MingyuanPowerLevel = CharacterPowerLevel.Original;
            if (QinghePowerLevel < CharacterPowerLevel.Original || QinghePowerLevel > CharacterPowerLevel.Decorative)
                QinghePowerLevel = CharacterPowerLevel.Original;

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

            if (NeiyuPowerLevel != CharacterPowerLevel.Original
                && NeiyuPowerLevel != CharacterPowerLevel.Balanced
                && NeiyuPowerLevel != CharacterPowerLevel.Decorative)
            {
                NeiyuPowerLevel = CharacterPowerLevel.Original;
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
            // Def-loading hooks (e.g. PawnSpecialResourceStats) must be registered before PlayDataLoader runs;
            // StaticConstructorOnStartup fires too late, so PatchAll lives in the mod constructor.
            new Harmony("HeChuanRiver.MiliraXian.Characters").PatchAll(Assembly.GetExecutingAssembly());
            Settings = GetSettings<NeiyuLawSettings>();
            Instance = this;
            NeiyuPowerBalance.SetLevel(Settings.NeiyuPowerLevel);
            ZhaoliPowerBalance.SetLevel(Settings.ZhaoliPowerLevel);
            MingyuanPowerBalance.SetLevel(Settings.MingyuanPowerLevel);
            QinghePowerBalance.SetLevel(Settings.QinghePowerLevel);
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
            listing.CheckboxLabeled(
                "MX_NL_EnableUpdateLogLettersLabel".Translate().ToString(),
                ref Settings.EnableUpdateLogLetters,
                "MX_NL_EnableUpdateLogLettersDesc".Translate().ToString());
            if (listing.ButtonText("MX_NL_ViewUpdateLogsButton".Translate().ToString()))
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    NeiyuLawUpdateLogUtility.AllUpdateLogsText(),
                    title: "MX_NL_UpdateLogDialogTitle".Translate().ToString()));
            }
            listing.Gap();
            listing.Label("MX_NL_NeiyuPowerLevelLabel".Translate().ToString());
            DrawNeiyuPowerLevelOption(
                listing,
                CharacterPowerLevel.Original,
                "MX_NL_NeiyuPowerLevelOriginalLabel",
                "MX_NL_NeiyuPowerLevelOriginalDesc");
            DrawNeiyuPowerLevelOption(
                listing,
                CharacterPowerLevel.Balanced,
                "MX_NL_NeiyuPowerLevelBalancedLabel",
                "MX_NL_NeiyuPowerLevelBalancedDesc");
            DrawNeiyuPowerLevelOption(
                listing,
                CharacterPowerLevel.Decorative,
                "MX_NL_NeiyuPowerLevelDecorativeLabel",
                "MX_NL_NeiyuPowerLevelDecorativeDesc");
            listing.Gap();
            DrawCharacterPowerOptions(listing, "MX_Power_Zhaoli", ref Settings.ZhaoliPowerLevel, ZhaoliPowerBalance.SetLevel);
            DrawCharacterPowerOptions(listing, "MX_Power_Mingyuan", ref Settings.MingyuanPowerLevel, MingyuanPowerBalance.SetLevel);
            DrawCharacterPowerOptions(listing, "MX_Power_Qinghe", ref Settings.QinghePowerLevel, QinghePowerBalance.ApplyLevel);
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

        private static void DrawCharacterPowerOptions(Listing_Standard listing, string key, ref CharacterPowerLevel selected, System.Action<CharacterPowerLevel> apply)
        {
            listing.Label(key.Translate());
            for (int i = 0; i < 3; i++)
            {
                var level = (CharacterPowerLevel)i;
                if (listing.RadioButton(("MX_NL_NeiyuPowerLevel" + level + "Label").Translate(), selected == level,
                    24f, (key + "_" + level).Translate()))
                {
                    selected = level;
                    apply(level);
                }
            }
            listing.Gap();
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

        private void DrawNeiyuPowerLevelOption(Listing_Standard listing, CharacterPowerLevel level, string labelKey, string tooltipKey)
        {
            if (listing.RadioButton(
                labelKey.Translate().ToString(),
                Settings.NeiyuPowerLevel == level,
                24f,
                tooltipKey.Translate().ToString()))
            {
                Settings.NeiyuPowerLevel = level;
                NeiyuPowerBalance.SetLevel(level);
            }
        }

        public override void WriteSettings()
        {
            NeiyuPowerBalance.SetLevel(Settings.NeiyuPowerLevel);
            ZhaoliPowerBalance.SetLevel(Settings.ZhaoliPowerLevel);
            MingyuanPowerBalance.SetLevel(Settings.MingyuanPowerLevel);
            QinghePowerBalance.ApplyLevel(Settings.QinghePowerLevel);
            base.WriteSettings();
        }
    }
}
