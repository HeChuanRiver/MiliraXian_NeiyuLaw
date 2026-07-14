using System.Collections.Generic;
using System.Text;
using HarmonyLib;
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
        private bool legacyLockSpecialPawnConsciousness = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableAriandelSpecialPawnIntegration, "EnableAriandelSpecialPawnIntegration", true);
            Scribe_Values.Look(ref EnableUpdateLogLetters, "EnableUpdateLogLetters", true);

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

        public NeiyuLawMod(ModContentPack content)
            : base(content)
        {
            Settings = GetSettings<NeiyuLawSettings>();
            Instance = this;
        }

        public override string SettingsCategory()
        {
            return "MX_NL_ModSettingsTitle".Translate().ToString();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
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
            listing.End();
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

        private static readonly List<NeiyuLawUpdateLogEntry> Entries = new List<NeiyuLawUpdateLogEntry>
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
            StringBuilder builder = new StringBuilder();
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

            float minimumConsciousness = MinimumConsciousnessFromSettings();
            if (minimumConsciousness <= 0f || __result >= minimumConsciousness)
            {
                return;
            }

            Pawn pawn = diffSet?.pawn;
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
                || MXCharacterIdentityUtility.IsQinghe(pawn);
        }

        private static float MinimumConsciousnessFromSettings()
        {
            NeiyuLawSettings settings = NeiyuLawMod.Instance?.Settings;
            if (settings == null)
            {
                return 0f;
            }

            switch (settings.ConsciousnessLockMode)
            {
                case SpecialPawnConsciousnessLockMode.Lock100:
                    return 1f;
                case SpecialPawnConsciousnessLockMode.Lock35:
                    return 0.35f;
                default:
                    return 0f;
            }
        }
    }
}
