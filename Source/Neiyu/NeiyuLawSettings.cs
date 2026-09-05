using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MiliraXian.Characters.QingHe;
using MiliraXian.Characters.Mingyuan;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
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
        private bool legacyLockSpecialPawnConsciousness = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableAriandelSpecialPawnIntegration, "EnableAriandelSpecialPawnIntegration", true);
            Scribe_Values.Look(ref EnableUpdateLogLetters, "EnableUpdateLogLetters", true);
            Scribe_Values.Look(ref NeiyuPowerLevel, "NeiyuPowerLevel", CharacterPowerLevel.Original);
            Scribe_Values.Look(ref ZhaoliPowerLevel, "ZhaoliPowerLevel", CharacterPowerLevel.Original);
            Scribe_Values.Look(ref MingyuanPowerLevel, "MingyuanPowerLevel", CharacterPowerLevel.Original);
            if (ZhaoliPowerLevel < CharacterPowerLevel.Original || ZhaoliPowerLevel > CharacterPowerLevel.Decorative)
                ZhaoliPowerLevel = CharacterPowerLevel.Original;
            if (MingyuanPowerLevel < CharacterPowerLevel.Original || MingyuanPowerLevel > CharacterPowerLevel.Decorative)
                MingyuanPowerLevel = CharacterPowerLevel.Original;

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
            Settings = GetSettings<NeiyuLawSettings>();
            Instance = this;
            NeiyuPowerBalance.SetLevel(Settings.NeiyuPowerLevel);
            ZhaoliPowerBalance.SetLevel(Settings.ZhaoliPowerLevel);
            MingyuanPowerBalance.SetLevel(Settings.MingyuanPowerLevel);
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
            base.WriteSettings();
        }
    }

    internal sealed class NeiyuLawUpdateLogEntry
    {
        public NeiyuLawUpdateLogEntry(string version, string bodyKey, bool pushLetter)
        {
            Version = version;
            BodyKey = bodyKey;
            PushLetter = pushLetter;
        }

        public string Version { get; }
        public string BodyKey { get; }
        public bool PushLetter { get; }
    }

    internal static class NeiyuLawUpdateLogUtility
    {
        public const string CurrentVersion = "v1.1.103";

        private static readonly List<NeiyuLawUpdateLogEntry> Entries = new()
        {
            new NeiyuLawUpdateLogEntry(CurrentVersion, "MX_NL_UpdateLog_v1_1_103_Body", true),
            new NeiyuLawUpdateLogEntry("v1.1.010", "MX_NL_UpdateLog_v1_1_010_Body", true),
            new NeiyuLawUpdateLogEntry("v1.1.001", "MX_NL_UpdateLog_v1_1_001_Body", true)
        };

        public static NeiyuLawUpdateLogEntry LatestEntry
        {
            get
            {
                for (int index = 0; index < Entries.Count; index++)
                {
                    if (Entries[index].Version == CurrentVersion)
                    {
                        return Entries[index];
                    }
                }

                return Entries[0];
            }
        }

        public static TaggedString LetterLabel(NeiyuLawUpdateLogEntry entry)
        {
            return "MX_NL_UpdateLogLetterLabel".Translate(entry.Version);
        }

        public static TaggedString LetterText(NeiyuLawUpdateLogEntry entry)
        {
            return entry.BodyKey.Translate();
        }

        public static TaggedString AllUpdateLogsText()
        {
            StringBuilder builder = new();
            for (int index = 0; index < Entries.Count; index++)
            {
                NeiyuLawUpdateLogEntry entry = Entries[index];
                if (index > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("----------");
                    builder.AppendLine();
                }

                builder.AppendLine("<color=#79CBFF>" + entry.Version + "</color>");
                builder.AppendLine();
                builder.AppendLine(entry.BodyKey.Translate().ToString());
            }

            return builder.ToString();
        }
    }

    public class GameComponent_NeiyuLawUpdateLog : GameComponent
    {
        private string checkedUpdateLogVersion;
        private bool checkedUpdateLogShouldPush;
        private bool checkedUpdateLogPushed;

        public GameComponent_NeiyuLawUpdateLog(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.LetterStack == null)
            {
                return;
            }

            TryProcessLatestUpdateLog();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref checkedUpdateLogVersion, "mxnl_checkedUpdateLogVersion");
            Scribe_Values.Look(ref checkedUpdateLogShouldPush, "mxnl_checkedUpdateLogShouldPush", false);
            Scribe_Values.Look(ref checkedUpdateLogPushed, "mxnl_checkedUpdateLogPushed", false);
        }

        private void TryProcessLatestUpdateLog()
        {
            NeiyuLawUpdateLogEntry entry = NeiyuLawUpdateLogUtility.LatestEntry;
            if (checkedUpdateLogVersion == entry.Version)
            {
                return;
            }

            checkedUpdateLogVersion = entry.Version;
            checkedUpdateLogShouldPush = entry.PushLetter
                && (NeiyuLawMod.Instance?.Settings?.EnableUpdateLogLetters ?? true);
            checkedUpdateLogPushed = false;

            if (!checkedUpdateLogShouldPush)
            {
                return;
            }

            Find.LetterStack.ReceiveLetter(
                NeiyuLawUpdateLogUtility.LetterLabel(entry),
                NeiyuLawUpdateLogUtility.LetterText(entry),
                LetterDefOf.PositiveEvent);
            checkedUpdateLogPushed = true;
        }
    }

    [HarmonyPatch(typeof(PawnCapacityUtility), nameof(PawnCapacityUtility.CalculateCapacityLevel))]
    internal static class Patch_PawnCapacityUtility_CalculateCapacityLevel_SpecialPawnConsciousness
    {
        [HarmonyPostfix]
        private static void Postfix(HediffSet diffSet, PawnCapacityDef capacity, ref float __result)
        {
            if (capacity != PawnCapacityDefOf.Consciousness)
            {
                return;
            }

            Pawn pawn = diffSet?.pawn;
            float minimumConsciousness = MinimumConsciousnessFromSettings();
            if (pawn != null && NeiyuEquipmentUtility.IsNeiyu(pawn))
            {
                minimumConsciousness = NeiyuPowerBalance.LimitConsciousnessMinimum(minimumConsciousness);
            }
            if (ZhaoliKarmaUtility.IsZhaoli(pawn) && ZhaoliPowerBalance.Sealed)
                minimumConsciousness = 0f;
            if (minimumConsciousness <= 0f || __result >= minimumConsciousness)
            {
                return;
            }

            if (pawn == null || pawn.Dead || !IsSupportedSpecialPawn(pawn))
            {
                return;
            }

            __result = minimumConsciousness;
        }

        private static bool IsSupportedSpecialPawn(Pawn pawn)
        {
            return NeiyuEquipmentUtility.IsNeiyu(pawn)
                || ZhaoliKarmaUtility.IsZhaoli(pawn)
                || MX_QHCharacterUtility.IsQinghe(pawn);
        }

        private static float MinimumConsciousnessFromSettings()
        {
            NeiyuLawSettings settings = NeiyuLawMod.Instance?.Settings;
            if (settings == null)
            {
                return 0f;
            }

            return settings.ConsciousnessLockMode switch
            {
                SpecialPawnConsciousnessLockMode.Lock100 => 1f,
                SpecialPawnConsciousnessLockMode.Lock35 => 0.35f,
                _ => 0f,
            };
        }
    }
}
