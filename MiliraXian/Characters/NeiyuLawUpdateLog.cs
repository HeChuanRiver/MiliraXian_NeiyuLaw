using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
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
}
