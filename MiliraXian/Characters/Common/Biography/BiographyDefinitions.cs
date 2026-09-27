using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Biography
{
    [DefOf]
    public static class BiographyDefOf
    {
        public static HediffDef MX_BiographyTracker;
        public static ThingDef Milira_Race;
    }

    public sealed class BiographyExtension : DefModExtension
    {
        public List<BiographyStory> stories = new();

        [Unsaved(false)]
        private PawnKindDef parentPawnKind;

        public PawnKindDef ParentPawnKind => parentPawnKind;

        public override void ResolveReferences(Def parentDef)
        {
            parentPawnKind = parentDef as PawnKindDef;
        }

        public BiographyStory GetStory(string storyName)
        {
            if (stories == null || storyName.NullOrEmpty())
            {
                return null;
            }

            for (int i = 0; i < stories.Count; i++)
            {
                BiographyStory story = stories[i];
                if (story != null && story.storyName == storyName)
                {
                    return story;
                }
            }

            return null;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            string owner = parentPawnKind?.defName ?? "unknown PawnKindDef";
            if (parentPawnKind == null)
            {
                yield return "BiographyExtension must be attached to a PawnKindDef.";
                yield break;
            }

            ThingDef supportedRace = BiographyDefOf.Milira_Race;
            if (parentPawnKind.race == null || (supportedRace != null
                    ? parentPawnKind.race != supportedRace
                    : parentPawnKind.race.defName != "Milira_Race"))
            {
                yield return owner + " has BiographyExtension, but its resolved race is not Milira_Race.";
            }

            int extensionCount = 0;
            if (parentPawnKind.modExtensions != null)
            {
                for (int i = 0; i < parentPawnKind.modExtensions.Count; i++)
                {
                    if (parentPawnKind.modExtensions[i] is BiographyExtension)
                    {
                        extensionCount++;
                    }
                }
            }

            if (extensionCount != 1)
            {
                yield return owner + " must have exactly one BiographyExtension; found " + extensionCount + ".";
            }

            if (stories.NullOrEmpty())
            {
                yield return owner + " BiographyExtension has no stories.";
                yield break;
            }

            HashSet<string> storyNames = new(StringComparer.Ordinal);
            for (int i = 0; i < stories.Count; i++)
            {
                BiographyStory story = stories[i];
                string path = owner + ".stories[" + i + "]";
                if (story == null)
                {
                    yield return path + " is null.";
                    continue;
                }

                if (!BiographyIdentifierUtility.IsValid(story.storyName))
                {
                    yield return path + ".storyName must contain only letters, numbers, underscores, or dashes.";
                }
                else if (!storyNames.Add(story.storyName))
                {
                    yield return owner + " contains duplicate storyName '" + story.storyName + "'.";
                }

                if (story.label.NullOrEmpty())
                {
                    yield return path + ".label is required.";
                }

                if (story.storyText.NullOrEmpty())
                {
                    yield return path + ".storyText is required.";
                }

                if (story.unlockCondition == null)
                {
                    yield return path + ".unlockCondition is required. Use BiographyCondition_Always for an initially unlocked story.";
                }
                else
                {
                    foreach (string error in story.unlockCondition.ConfigErrors(this, story, path + ".unlockCondition"))
                    {
                        yield return error;
                    }
                }

                if (story.rewards == null)
                {
                    continue;
                }

                HashSet<string> rewardNames = new(StringComparer.Ordinal);
                for (int rewardIndex = 0; rewardIndex < story.rewards.Count; rewardIndex++)
                {
                    BiographyReward reward = story.rewards[rewardIndex];
                    string rewardPath = path + ".rewards[" + rewardIndex + "]";
                    if (reward == null)
                    {
                        yield return rewardPath + " is null.";
                        continue;
                    }

                    if (!BiographyIdentifierUtility.IsValid(reward.rewardName))
                    {
                        yield return rewardPath + ".rewardName must contain only letters, numbers, underscores, or dashes.";
                    }
                    else if (!rewardNames.Add(reward.rewardName))
                    {
                        yield return path + " contains duplicate rewardName '" + reward.rewardName + "'.";
                    }

                    foreach (string error in reward.ConfigErrors(this, story, rewardPath))
                    {
                        yield return error;
                    }
                }
            }

            foreach (string error in StoryDependencyConfigErrors(owner))
            {
                yield return error;
            }
        }

        private IEnumerable<string> StoryDependencyConfigErrors(string owner)
        {
            Dictionary<string, List<string>> graph = new(StringComparer.Ordinal);
            for (int i = 0; i < stories.Count; i++)
            {
                BiographyStory story = stories[i];
                if (story == null || !BiographyIdentifierUtility.IsValid(story.storyName))
                {
                    continue;
                }

                List<string> dependencies = new();
                story.unlockCondition?.CollectReferencedStoryNames(dependencies);
                graph[story.storyName] = dependencies;
            }

            Dictionary<string, int> states = new(StringComparer.Ordinal);
            List<string> path = new();
            foreach (string storyName in graph.Keys)
            {
                if (!states.TryGetValue(storyName, out int state) || state == 0)
                {
                    if (TryFindDependencyCycle(storyName, graph, states, path, out string cycle))
                    {
                        yield return owner + " biography stories contain a circular unlock dependency: " + cycle + ".";
                        yield break;
                    }
                }
            }
        }

        private static bool TryFindDependencyCycle(
            string storyName,
            Dictionary<string, List<string>> graph,
            Dictionary<string, int> states,
            List<string> path,
            out string cycle)
        {
            states[storyName] = 1;
            path.Add(storyName);
            List<string> dependencies = graph[storyName];
            for (int i = 0; i < dependencies.Count; i++)
            {
                string dependency = dependencies[i];
                if (!graph.ContainsKey(dependency))
                {
                    continue;
                }

                states.TryGetValue(dependency, out int dependencyState);
                if (dependencyState == 1)
                {
                    int cycleStart = path.IndexOf(dependency);
                    List<string> cycleParts = path.GetRange(cycleStart, path.Count - cycleStart);
                    cycleParts.Add(dependency);
                    cycle = string.Join(" -> ", cycleParts.ToArray());
                    return true;
                }

                if (dependencyState == 0
                    && TryFindDependencyCycle(dependency, graph, states, path, out cycle))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            states[storyName] = 2;
            cycle = null;
            return false;
        }
    }

    public sealed class BiographyStory
    {
        [NoTranslate]
        public string storyName;

        [MustTranslate]
        public string label;

        [MustTranslate]
        public string storyText;

        public BiographyUnlockCondition unlockCondition;
        public List<BiographyReward> rewards = new();
        public bool notifyOnUnlock = true;
    }

    public abstract class BiographyUnlockCondition
    {
        public abstract bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker);

        public abstract string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker);

        public virtual IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            yield break;
        }

        public virtual void CollectReferencedStoryNames(List<string> storyNames)
        {
        }
    }

    public abstract class BiographyReward
    {
        [NoTranslate]
        public string rewardName;

        public abstract string GetDescription();

        public virtual string GetDisabledReason(Pawn pawn)
        {
            return null;
        }

        public abstract bool TryGrant(Pawn pawn, out string failureReason);

        public virtual IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            yield break;
        }
    }

    public static class BiographyIdentifierUtility
    {
        public static bool IsValid(string value)
        {
            if (value.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                {
                    return false;
                }
            }

            return true;
        }
    }

    public static class BiographyDatabase
    {
        private static Dictionary<PawnKindDef, BiographyExtension> extensionsByPawnKind;

        public static bool HasAnyConfigurations
        {
            get
            {
                EnsureInitialized();
                return extensionsByPawnKind.Count > 0;
            }
        }

        public static bool TryGet(PawnKindDef pawnKind, out BiographyExtension extension)
        {
            EnsureInitialized();
            if (pawnKind != null)
            {
                return extensionsByPawnKind.TryGetValue(pawnKind, out extension);
            }

            extension = null;
            return false;
        }

        public static void Rebuild()
        {
            Dictionary<PawnKindDef, BiographyExtension> rebuilt = new();
            List<PawnKindDef> pawnKinds = DefDatabase<PawnKindDef>.AllDefsListForReading;
            ThingDef supportedRace = BiographyDefOf.Milira_Race;
            for (int i = 0; i < pawnKinds.Count; i++)
            {
                PawnKindDef pawnKind = pawnKinds[i];
                if (pawnKind == null || pawnKind.race == null || (supportedRace != null
                        ? pawnKind.race != supportedRace
                        : pawnKind.race.defName != "Milira_Race"))
                {
                    continue;
                }

                BiographyExtension extension = pawnKind.GetModExtension<BiographyExtension>();
                if (extension != null && !extension.stories.NullOrEmpty())
                {
                    rebuilt[pawnKind] = extension;
                }
            }

            extensionsByPawnKind = rebuilt;
        }

        private static void EnsureInitialized()
        {
            if (extensionsByPawnKind == null)
            {
                Rebuild();
            }
        }
    }
}
