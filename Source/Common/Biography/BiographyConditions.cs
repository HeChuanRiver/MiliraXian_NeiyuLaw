using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Biography
{
    internal static class BiographyConditionText
    {
        public static string Status(bool complete)
        {
            return (complete ? "MX_Biography_StatusComplete" : "MX_Biography_StatusIncomplete").Translate();
        }

        public static string Indent(string text)
        {
            return text.NullOrEmpty() ? string.Empty : text.Replace("\n", "\n    ");
        }
    }

    public sealed class BiographyCondition_Always : BiographyUnlockCondition
    {
        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            return true;
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            return "MX_Biography_ConditionAlways".Translate(BiographyConditionText.Status(true));
        }
    }

    public sealed class BiographyCondition_All : BiographyUnlockCondition
    {
        public List<BiographyUnlockCondition> conditions = new();

        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            if (conditions.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] == null || !conditions[i].IsSatisfied(pawn, tracker))
                {
                    return false;
                }
            }

            return true;
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            StringBuilder builder = new();
            builder.AppendLine("MX_Biography_ConditionAll".Translate());
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    BiographyUnlockCondition condition = conditions[i];
                    string progress = condition != null
                        ? condition.GetProgressText(pawn, tracker)
                        : "MX_Biography_ConditionMissing".Translate().ToString();
                    builder.Append("  - ");
                    builder.Append(BiographyConditionText.Indent(progress));
                    if (i < conditions.Count - 1)
                    {
                        builder.AppendLine();
                    }
                }
            }

            return builder.ToString();
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (conditions.NullOrEmpty())
            {
                yield return path + ".conditions must contain at least one condition.";
                yield break;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                BiographyUnlockCondition condition = conditions[i];
                string childPath = path + ".conditions[" + i + "]";
                if (condition == null)
                {
                    yield return childPath + " is null.";
                    continue;
                }

                foreach (string error in condition.ConfigErrors(extension, story, childPath))
                {
                    yield return error;
                }
            }
        }

        public override void CollectReferencedStoryNames(List<string> storyNames)
        {
            if (conditions == null)
            {
                return;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                conditions[i]?.CollectReferencedStoryNames(storyNames);
            }
        }
    }

    public sealed class BiographyCondition_Any : BiographyUnlockCondition
    {
        public List<BiographyUnlockCondition> conditions = new();

        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            if (conditions.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] != null && conditions[i].IsSatisfied(pawn, tracker))
                {
                    return true;
                }
            }

            return false;
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            StringBuilder builder = new();
            builder.AppendLine("MX_Biography_ConditionAny".Translate());
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    BiographyUnlockCondition condition = conditions[i];
                    string progress = condition != null
                        ? condition.GetProgressText(pawn, tracker)
                        : "MX_Biography_ConditionMissing".Translate().ToString();
                    builder.Append("  - ");
                    builder.Append(BiographyConditionText.Indent(progress));
                    if (i < conditions.Count - 1)
                    {
                        builder.AppendLine();
                    }
                }
            }

            return builder.ToString();
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (conditions.NullOrEmpty())
            {
                yield return path + ".conditions must contain at least one condition.";
                yield break;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                BiographyUnlockCondition condition = conditions[i];
                string childPath = path + ".conditions[" + i + "]";
                if (condition == null)
                {
                    yield return childPath + " is null.";
                    continue;
                }

                foreach (string error in condition.ConfigErrors(extension, story, childPath))
                {
                    yield return error;
                }
            }
        }

        public override void CollectReferencedStoryNames(List<string> storyNames)
        {
            if (conditions == null)
            {
                return;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                conditions[i]?.CollectReferencedStoryNames(storyNames);
            }
        }
    }

    public sealed class BiographyCondition_PlayerFaction : BiographyUnlockCondition
    {
        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            return pawn != null && pawn.Faction == Faction.OfPlayer;
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            bool complete = IsSatisfied(pawn, tracker);
            return "MX_Biography_ConditionPlayerFaction".Translate(BiographyConditionText.Status(complete));
        }
    }

    public sealed class BiographyCondition_ColonistDays : BiographyUnlockCondition
    {
        public float days = 1f;

        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            return CurrentDays(pawn) >= days;
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            float current = CurrentDays(pawn);
            return "MX_Biography_ConditionColonistDays".Translate(
                current.ToString("0.##"),
                days.ToString("0.##"),
                BiographyConditionText.Status(current >= days));
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (days <= 0f)
            {
                yield return path + ".days must be greater than zero.";
            }
        }

        private static float CurrentDays(Pawn pawn)
        {
            if (pawn?.records == null || RecordDefOf.TimeAsColonistOrColonyAnimal == null)
            {
                return 0f;
            }

            return pawn.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) / GenDate.TicksPerDay;
        }
    }

    public sealed class BiographyCondition_RecordValue : BiographyUnlockCondition
    {
        public RecordDef record;
        public float minimumValue = 1f;

        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            return CurrentValue(pawn) >= minimumValue;
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            float current = CurrentValue(pawn);
            string recordLabel = record?.LabelCap.ToString() ?? "MX_Biography_UnknownDef".Translate();
            return "MX_Biography_ConditionRecordValue".Translate(
                recordLabel,
                current.ToString("0.##"),
                minimumValue.ToString("0.##"),
                BiographyConditionText.Status(current >= minimumValue));
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (record == null)
            {
                yield return path + ".record is required.";
            }

            if (minimumValue <= 0f)
            {
                yield return path + ".minimumValue must be greater than zero.";
            }
        }

        private float CurrentValue(Pawn pawn)
        {
            return pawn?.records != null && record != null ? pawn.records.GetValue(record) : 0f;
        }
    }

    public sealed class BiographyCondition_HasHediff : BiographyUnlockCondition
    {
        public HediffDef hediff;
        public float minimumSeverity;

        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            Hediff current = CurrentHediff(pawn);
            return current != null && current.Severity >= minimumSeverity;
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            Hediff current = CurrentHediff(pawn);
            float currentSeverity = current?.Severity ?? 0f;
            bool complete = current != null && currentSeverity >= minimumSeverity;
            string hediffLabel = hediff?.LabelCap.ToString() ?? "MX_Biography_UnknownDef".Translate();
            return "MX_Biography_ConditionHediff".Translate(
                hediffLabel,
                currentSeverity.ToString("0.##"),
                minimumSeverity.ToString("0.##"),
                BiographyConditionText.Status(complete));
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (hediff == null)
            {
                yield return path + ".hediff is required.";
            }

            if (minimumSeverity < 0f)
            {
                yield return path + ".minimumSeverity cannot be negative.";
            }
        }

        private Hediff CurrentHediff(Pawn pawn)
        {
            return pawn?.health?.hediffSet != null && hediff != null
                ? pawn.health.hediffSet.GetFirstHediffOfDef(hediff)
                : null;
        }
    }

    public sealed class BiographyCondition_HasTrait : BiographyUnlockCondition
    {
        public TraitDef trait;
        public bool requireDegree;
        public int degree;

        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            if (pawn?.story?.traits == null || trait == null)
            {
                return false;
            }

            return requireDegree
                ? pawn.story.traits.HasTrait(trait, degree)
                : pawn.story.traits.HasTrait(trait);
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            bool complete = IsSatisfied(pawn, tracker);
            string traitLabel = trait?.LabelCap.ToString() ?? "MX_Biography_UnknownDef".Translate();
            return requireDegree
                ? "MX_Biography_ConditionTraitDegree".Translate(traitLabel, degree, BiographyConditionText.Status(complete))
                : "MX_Biography_ConditionTrait".Translate(traitLabel, BiographyConditionText.Status(complete));
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (trait == null)
            {
                yield return path + ".trait is required.";
            }
        }
    }

    public sealed class BiographyCondition_HasAbility : BiographyUnlockCondition
    {
        public AbilityDef ability;
        public bool includeTemporary;

        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            return pawn?.abilities != null && ability != null
                && pawn.abilities.GetAbility(ability, includeTemporary) != null;
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            bool complete = IsSatisfied(pawn, tracker);
            string abilityLabel = ability?.LabelCap.ToString() ?? "MX_Biography_UnknownDef".Translate();
            return "MX_Biography_ConditionAbility".Translate(abilityLabel, BiographyConditionText.Status(complete));
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (ability == null)
            {
                yield return path + ".ability is required.";
            }
        }
    }

    public sealed class BiographyCondition_SkillLevel : BiographyUnlockCondition
    {
        public SkillDef skill;
        public int minimumLevel = 1;

        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            return CurrentLevel(pawn) >= minimumLevel;
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            int current = CurrentLevel(pawn);
            string skillLabel = skill?.LabelCap.ToString() ?? "MX_Biography_UnknownDef".Translate();
            return "MX_Biography_ConditionSkill".Translate(
                skillLabel,
                current,
                minimumLevel,
                BiographyConditionText.Status(current >= minimumLevel));
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (skill == null)
            {
                yield return path + ".skill is required.";
            }

            if (minimumLevel < 0 || minimumLevel > SkillRecord.MaxLevel)
            {
                yield return path + ".minimumLevel must be between 0 and " + SkillRecord.MaxLevel + ".";
            }
        }

        private int CurrentLevel(Pawn pawn)
        {
            return pawn?.skills != null && skill != null ? pawn.skills.GetSkill(skill).Level : 0;
        }
    }

    public sealed class BiographyCondition_StoryUnlocked : BiographyUnlockCondition
    {
        [NoTranslate]
        public string storyName;

        public override bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            return tracker != null && tracker.IsStoryUnlocked(storyName);
        }

        public override string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker)
        {
            bool complete = IsSatisfied(pawn, tracker);
            string storyLabel = storyName;
            if (BiographyDatabase.TryGet(pawn?.kindDef, out BiographyExtension extension))
            {
                BiographyStory referencedStory = extension.GetStory(storyName);
                if (referencedStory != null && !referencedStory.label.NullOrEmpty())
                {
                    storyLabel = referencedStory.label;
                }
            }

            return "MX_Biography_ConditionStoryUnlocked".Translate(
                storyLabel ?? "MX_Biography_UnknownStory".Translate(),
                BiographyConditionText.Status(complete));
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (!BiographyIdentifierUtility.IsValid(storyName))
            {
                yield return path + ".storyName must contain only letters, numbers, underscores, or dashes.";
                yield break;
            }

            if (story != null && story.storyName == storyName)
            {
                yield return path + " cannot require its own storyName.";
            }

            if (extension?.GetStory(storyName) == null)
            {
                yield return path + " references missing storyName '" + storyName + "'.";
            }
        }

        public override void CollectReferencedStoryNames(List<string> storyNames)
        {
            if (!storyName.NullOrEmpty())
            {
                storyNames.Add(storyName);
            }
        }
    }
}
