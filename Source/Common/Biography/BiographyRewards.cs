using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Biography
{
    public sealed class BiographyReward_AddHediff : BiographyReward
    {
        public HediffDef hediff;
        public float severity = 1f;
        public bool addSeverityToExisting;

        public override string GetDescription()
        {
            string label = hediff?.LabelCap.ToString() ?? "MX_Biography_UnknownDef".Translate();
            return "MX_Biography_RewardHediff".Translate(label, severity.ToString("0.##"));
        }

        public override string GetDisabledReason(Pawn pawn)
        {
            return pawn?.health == null ? "MX_Biography_ClaimMissingHealth".Translate() : null;
        }

        public override bool TryGrant(Pawn pawn, out string failureReason)
        {
            failureReason = GetDisabledReason(pawn);
            if (!failureReason.NullOrEmpty() || hediff == null)
            {
                if (failureReason.NullOrEmpty())
                {
                    failureReason = "MX_Biography_RewardInvalidConfiguration".Translate();
                }

                return false;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(hediff);
            if (existing != null)
            {
                existing.Severity = addSeverityToExisting
                    ? existing.Severity + severity
                    : Mathf.Max(existing.Severity, severity);
                return true;
            }

            Hediff added = pawn.health.AddHediff(hediff);
            if (added == null)
            {
                failureReason = "MX_Biography_RewardHediffFailed".Translate();
                return false;
            }

            added.Severity = severity;
            return true;
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (hediff == null)
            {
                yield return path + ".hediff is required.";
            }

            if (severity <= 0f)
            {
                yield return path + ".severity must be greater than zero.";
            }
        }
    }

    public sealed class BiographyReward_GrantAbility : BiographyReward
    {
        public AbilityDef ability;

        public override string GetDescription()
        {
            string label = ability?.LabelCap.ToString() ?? "MX_Biography_UnknownDef".Translate();
            return "MX_Biography_RewardAbility".Translate(label);
        }

        public override string GetDisabledReason(Pawn pawn)
        {
            return pawn?.abilities == null ? "MX_Biography_ClaimMissingAbilities".Translate() : null;
        }

        public override bool TryGrant(Pawn pawn, out string failureReason)
        {
            failureReason = GetDisabledReason(pawn);
            if (!failureReason.NullOrEmpty() || ability == null)
            {
                if (failureReason.NullOrEmpty())
                {
                    failureReason = "MX_Biography_RewardInvalidConfiguration".Translate();
                }

                return false;
            }

            if (pawn.abilities.GetAbility(ability, includeTemporary: false) == null)
            {
                pawn.abilities.GainAbility(ability);
            }

            return true;
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (ability == null)
            {
                yield return path + ".ability is required.";
            }
        }
    }

    public sealed class BiographyReward_GiveItem : BiographyReward
    {
        private const int MaxRewardStacks = 100;

        public ThingDef thing;
        public ThingDef stuff;
        public int count = 1;

        public override string GetDescription()
        {
            string thingLabel = thing?.LabelCap.ToString() ?? "MX_Biography_UnknownDef".Translate();
            if (stuff != null)
            {
                return "MX_Biography_RewardItemWithStuff".Translate(thingLabel, stuff.LabelCap, count);
            }

            return "MX_Biography_RewardItem".Translate(thingLabel, count);
        }

        public override string GetDisabledReason(Pawn pawn)
        {
            return pawn?.inventory?.innerContainer == null ? "MX_Biography_ClaimMissingInventory".Translate() : null;
        }

        public override bool TryGrant(Pawn pawn, out string failureReason)
        {
            failureReason = GetDisabledReason(pawn);
            if (!failureReason.NullOrEmpty() || thing == null || count <= 0)
            {
                if (failureReason.NullOrEmpty())
                {
                    failureReason = "MX_Biography_RewardInvalidConfiguration".Translate();
                }

                return false;
            }

            int stackLimit = Mathf.Max(1, thing.stackLimit);
            long requiredStacks = ((long)count + stackLimit - 1L) / stackLimit;
            if (requiredStacks > MaxRewardStacks)
            {
                failureReason = "MX_Biography_RewardTooLarge".Translate(MaxRewardStacks);
                return false;
            }

            ThingDef resolvedStuff = null;
            if (thing.MadeFromStuff)
            {
                resolvedStuff = stuff ?? GenStuff.DefaultStuffFor(thing);
                if (resolvedStuff == null)
                {
                    failureReason = "MX_Biography_RewardMissingStuff".Translate();
                    return false;
                }
            }

            Thing first = ThingMaker.MakeThing(thing, resolvedStuff);
            first.stackCount = count;
            ThingOwner<Thing> inventory = pawn.inventory.innerContainer;
            if (inventory.GetCountCanAccept(first, canMergeWithExistingStacks: false) < count)
            {
                first.Destroy();
                failureReason = "MX_Biography_RewardInventoryFull".Translate();
                return false;
            }

            List<Thing> addedThings = new();
            int remaining = count;
            Thing currentItem = null;
            try
            {
                while (remaining > 0)
                {
                    currentItem = addedThings.Count == 0 ? first : ThingMaker.MakeThing(thing, resolvedStuff);
                    currentItem.stackCount = Mathf.Min(remaining, stackLimit);
                    if (!inventory.TryAdd(currentItem, canMergeWithExistingStacks: false))
                    {
                        TryRemoveAndDestroy(inventory, currentItem);
                        currentItem = null;
                        RollBack(inventory, addedThings);
                        failureReason = "MX_Biography_RewardInventoryFull".Translate();
                        return false;
                    }

                    addedThings.Add(currentItem);
                    remaining -= currentItem.stackCount;
                    currentItem = null;
                }
            }
            catch
            {
                TryRemoveAndDestroy(inventory, currentItem);
                RollBack(inventory, addedThings);
                throw;
            }

            return true;
        }

        public override IEnumerable<string> ConfigErrors(BiographyExtension extension, BiographyStory story, string path)
        {
            if (thing == null)
            {
                yield return path + ".thing is required.";
            }
            else if (thing.category != ThingCategory.Item)
            {
                yield return path + ".thing must be an inventory item.";
            }

            if (count <= 0)
            {
                yield return path + ".count must be greater than zero.";
            }
            else if (thing != null)
            {
                int stackLimit = Mathf.Max(1, thing.stackLimit);
                long requiredStacks = ((long)count + stackLimit - 1L) / stackLimit;
                if (requiredStacks > MaxRewardStacks)
                {
                    yield return path + ".count may require at most " + MaxRewardStacks + " item stacks.";
                }
            }

            if (stuff != null)
            {
                if (!stuff.IsStuff)
                {
                    yield return path + ".stuff must reference a valid stuff ThingDef.";
                }
                else if (thing != null && (!thing.MadeFromStuff || !stuff.stuffProps.CanMake(thing)))
                {
                    yield return path + ".stuff cannot be used to make " + thing.defName + ".";
                }
            }
        }

        private static void RollBack(ThingOwner<Thing> inventory, List<Thing> addedThings)
        {
            for (int i = addedThings.Count - 1; i >= 0; i--)
            {
                TryRemoveAndDestroy(inventory, addedThings[i]);
            }
        }

        private static void TryRemoveAndDestroy(ThingOwner<Thing> inventory, Thing item)
        {
            if (item == null || item.Destroyed)
            {
                return;
            }

            try
            {
                bool removed = inventory.Remove(item);
                if ((removed || item.holdingOwner == null) && !item.Destroyed)
                {
                    item.Destroy();
                }
            }
            catch (Exception exception)
            {
                if (item.holdingOwner == null && !item.Destroyed)
                {
                    item.Destroy();
                }

                Log.Error("Exception while rolling back a biography item reward: " + exception);
            }
        }
    }
}
