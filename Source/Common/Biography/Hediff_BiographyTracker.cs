using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Biography
{
    public sealed class Hediff_BiographyTracker : Hediff
    {
        private List<string> unlockedStoryNames = new List<string>();
        private List<string> claimedRewardKeys = new List<string>();
        private Dictionary<string, float> customProgress = new Dictionary<string, float>();

        [Unsaved(false)]
        private HashSet<string> unlockedStoryLookup;

        [Unsaved(false)]
        private HashSet<string> claimedRewardLookup;

        [Unsaved(false)]
        private int revision;

        public override bool Visible => false;

        public override bool ShouldRemove => false;

        public int Revision => revision;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref unlockedStoryNames, "mxBiography_unlockedStories", LookMode.Value);
            Scribe_Collections.Look(ref claimedRewardKeys, "mxBiography_claimedRewards", LookMode.Value);
            Scribe_Collections.Look(ref customProgress, "mxBiography_customProgress", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                unlockedStoryNames = SanitizeStableIds(unlockedStoryNames);
                claimedRewardKeys = SanitizeStableIds(claimedRewardKeys);
                if (customProgress == null)
                {
                    customProgress = new Dictionary<string, float>();
                }

                RebuildLookups();
            }
        }

        public bool IsStoryUnlocked(string storyName)
        {
            EnsureLookups();
            return !storyName.NullOrEmpty() && unlockedStoryLookup.Contains(storyName);
        }

        public bool IsRewardClaimed(string storyName, string rewardName)
        {
            EnsureLookups();
            return claimedRewardLookup.Contains(RewardKey(storyName, rewardName));
        }

        public float GetProgress(string progressName)
        {
            if (customProgress == null || progressName.NullOrEmpty())
            {
                return 0f;
            }

            return customProgress.TryGetValue(progressName, out float value) ? value : 0f;
        }

        public void SetProgress(string progressName, float value)
        {
            if (!BiographyIdentifierUtility.IsValid(progressName))
            {
                Log.Error("Biography progressName must contain only letters, numbers, underscores, or dashes: " + progressName);
                return;
            }

            if (customProgress == null)
            {
                customProgress = new Dictionary<string, float>();
            }

            if (!customProgress.TryGetValue(progressName, out float existing) || !Mathf.Approximately(existing, value))
            {
                customProgress[progressName] = value;
                revision++;
            }
        }

        public void AddProgress(string progressName, float amount)
        {
            SetProgress(progressName, GetProgress(progressName) + amount);
        }

        public bool EvaluateUnlocks(BiographyExtension extension, bool sendNotifications)
        {
            if (extension?.stories.NullOrEmpty() != false)
            {
                return false;
            }

            EnsureLookups();
            bool anyUnlocked = false;
            int storyCount = extension.stories.Count;
            for (int pass = 0; pass < storyCount; pass++)
            {
                bool unlockedThisPass = false;
                for (int i = 0; i < storyCount; i++)
                {
                    BiographyStory story = extension.stories[i];
                    if (story == null || story.storyName.NullOrEmpty() || IsStoryUnlocked(story.storyName)
                        || story.unlockCondition == null)
                    {
                        continue;
                    }

                    bool satisfied;
                    try
                    {
                        satisfied = story.unlockCondition.IsSatisfied(pawn, this);
                    }
                    catch (Exception exception)
                    {
                        int logKey = (pawn?.thingIDNumber ?? 0) * 397 ^ story.storyName.GetHashCode();
                        Log.ErrorOnce(
                            "Exception while evaluating biography story '" + story.storyName + "' for "
                            + pawn.ToStringSafe() + ": " + exception,
                            logKey);
                        continue;
                    }

                    if (!satisfied)
                    {
                        continue;
                    }

                    UnlockStory(story.storyName);
                    anyUnlocked = true;
                    unlockedThisPass = true;
                    if (sendNotifications && story.notifyOnUnlock && pawn?.Faction == Faction.OfPlayer)
                    {
                        Messages.Message(
                            "MX_Biography_StoryUnlockedMessage".Translate(pawn.LabelShortCap, story.label),
                            pawn,
                            MessageTypeDefOf.PositiveEvent,
                            historical: false);
                    }
                }

                if (!unlockedThisPass)
                {
                    break;
                }
            }

            return anyUnlocked;
        }

        public bool HasUnclaimedRewards(BiographyStory story)
        {
            if (story?.rewards.NullOrEmpty() != false)
            {
                return false;
            }

            for (int i = 0; i < story.rewards.Count; i++)
            {
                BiographyReward reward = story.rewards[i];
                if (reward != null && !IsRewardClaimed(story.storyName, reward.rewardName))
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanClaimRewards(BiographyStory story, out string disabledReason)
        {
            if (pawn?.Faction != Faction.OfPlayer)
            {
                disabledReason = "MX_Biography_ClaimRequiresPlayerFaction".Translate();
                return false;
            }

            if (story == null || !IsStoryUnlocked(story.storyName))
            {
                disabledReason = "MX_Biography_ClaimNotUnlocked".Translate();
                return false;
            }

            if (!HasUnclaimedRewards(story))
            {
                disabledReason = story.rewards.NullOrEmpty()
                    ? "MX_Biography_NoRewards".Translate()
                    : "MX_Biography_AllRewardsClaimed".Translate();
                return false;
            }

            for (int i = 0; i < story.rewards.Count; i++)
            {
                BiographyReward reward = story.rewards[i];
                if (reward == null || IsRewardClaimed(story.storyName, reward.rewardName))
                {
                    continue;
                }

                string rewardReason = reward.GetDisabledReason(pawn);
                if (!rewardReason.NullOrEmpty())
                {
                    disabledReason = rewardReason;
                    return false;
                }
            }

            disabledReason = null;
            return true;
        }

        public bool TryClaimRewards(BiographyStory story, out string resultMessage)
        {
            if (!CanClaimRewards(story, out resultMessage))
            {
                return false;
            }

            int grantedThisAttempt = 0;
            for (int i = 0; i < story.rewards.Count; i++)
            {
                BiographyReward reward = story.rewards[i];
                if (reward == null || IsRewardClaimed(story.storyName, reward.rewardName))
                {
                    continue;
                }

                bool granted;
                string failureReason;
                try
                {
                    granted = reward.TryGrant(pawn, out failureReason);
                }
                catch (Exception exception)
                {
                    granted = false;
                    failureReason = exception.Message;
                    int logKey = (pawn?.thingIDNumber ?? 0) * 397 ^ RewardKey(story.storyName, reward.rewardName).GetHashCode();
                    Log.ErrorOnce(
                        "Exception while granting biography reward '" + reward.rewardName + "' from story '"
                        + story.storyName + "' to " + pawn.ToStringSafe() + ": " + exception,
                        logKey);
                }

                if (!granted)
                {
                    string resolvedFailure = failureReason.NullOrEmpty()
                        ? "MX_Biography_UnknownFailure".Translate().ToString()
                        : failureReason;
                    resultMessage = grantedThisAttempt > 0
                        ? "MX_Biography_ClaimPartiallyFailed".Translate(grantedThisAttempt, resolvedFailure).ToString()
                        : "MX_Biography_ClaimFailed".Translate(resolvedFailure).ToString();
                    return false;
                }

                MarkRewardClaimed(story.storyName, reward.rewardName);
                grantedThisAttempt++;
            }

            resultMessage = "MX_Biography_ClaimSucceeded".Translate(story.label);
            return true;
        }

        private void UnlockStory(string storyName)
        {
            EnsureLookups();
            if (unlockedStoryLookup.Add(storyName))
            {
                unlockedStoryNames.Add(storyName);
                revision++;
            }
        }

        private void MarkRewardClaimed(string storyName, string rewardName)
        {
            EnsureLookups();
            string key = RewardKey(storyName, rewardName);
            if (claimedRewardLookup.Add(key))
            {
                claimedRewardKeys.Add(key);
                revision++;
            }
        }

        private static string RewardKey(string storyName, string rewardName)
        {
            return (storyName ?? string.Empty) + ":" + (rewardName ?? string.Empty);
        }

        private void EnsureLookups()
        {
            if (unlockedStoryLookup == null || claimedRewardLookup == null)
            {
                RebuildLookups();
            }
        }

        private void RebuildLookups()
        {
            if (unlockedStoryNames == null)
            {
                unlockedStoryNames = new List<string>();
            }

            if (claimedRewardKeys == null)
            {
                claimedRewardKeys = new List<string>();
            }

            unlockedStoryLookup = new HashSet<string>(unlockedStoryNames, StringComparer.Ordinal);
            claimedRewardLookup = new HashSet<string>(claimedRewardKeys, StringComparer.Ordinal);
        }

        private static List<string> SanitizeStableIds(List<string> values)
        {
            if (values == null)
            {
                return new List<string>();
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = values.Count - 1; i >= 0; i--)
            {
                if (values[i].NullOrEmpty() || !seen.Add(values[i]))
                {
                    values.RemoveAt(i);
                }
            }

            return values;
        }
    }
}
