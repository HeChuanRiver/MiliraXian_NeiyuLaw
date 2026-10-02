using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using MiliraXian.Characters.Common.Conditions;

namespace MiliraXian.Characters.Common.PerkSystem
{
    public class HediffCompProperties_PerkEventHandler : HediffCompProperties
    {
        public HediffCompProperties_PerkEventHandler()
        {
            compClass = typeof(HediffComp_PerkEventHandler);
        }
    }

    /// <summary>
    /// Handles every way a perk level can change: auto-unlock from external events, explicit
    /// unlock requests, granting content, and broadcasting state changes. The tree it sits beside
    /// only stores levels.
    /// </summary>
    public class HediffComp_PerkEventHandler : HediffComp
    {
        public HediffComp_CharacterPerkTree Tree => parent.TryGetComp<HediffComp_CharacterPerkTree>();

        /// <summary>Passive unlocks driven by an external change (mastery level-up, load, settings).</summary>
        public void TryAutoUnlock()
        {
            HediffComp_CharacterPerkTree tree = Tree;
            if (tree == null || Pawn == null)
            {
                return;
            }

            List<CharacterPerkNodeDef> nodes = tree.RelevantNodes();
            for (int i = 0; i < nodes.Count; i++)
            {
                CharacterPerkNodeDef node = nodes[i];
                // displayOnly entries showcase innate passives; they are never written into stored state.
                if (node == null || node.displayOnly || tree.GetNodeLevel(node) > 0
                    || !node.ShouldAutoUnlock(Pawn) || !PrerequisitesMet(tree, node, out _))
                {
                    continue;
                }

                Unlock(node);
            }

            // The trigger may have changed the seal without unlocking anything, and listeners
            // (e.g. the cached stage) still need to refresh.
            Broadcast();
        }

        /// <summary>Explicit unlock request from UI. Validates prerequisites, condition and costs.</summary>
        public bool TryUnlock(CharacterPerkNodeDef node, out string failReason)
        {
            failReason = null;
            HediffComp_CharacterPerkTree tree = Tree;
            if (tree == null || Pawn == null || node == null || node.displayOnly)
            {
                return false;
            }

            int currentLevel = tree.GetNodeLevel(node);
            if (currentLevel >= node.maxLevel)
            {
                failReason = "MX_Perk_MaxLevel".Translate();
                return false;
            }

            if (!PrerequisitesMet(tree, node, out failReason))
            {
                return false;
            }

            if (!ConditionsMet(node, out failReason))
            {
                return false;
            }

            if (!HasCosts(node))
            {
                failReason = "MX_Perk_MaterialsMissing".Translate();
                return false;
            }

            ConsumeCosts(node);
            Unlock(node);
            Broadcast();
            return true;
        }

        /// <summary>Every unmet prerequisite is reported, not just the first.</summary>
        private bool PrerequisitesMet(HediffComp_CharacterPerkTree tree, CharacterPerkNodeDef node, out string failReason)
        {
            failReason = null;
            if (node.prerequisites.NullOrEmpty())
            {
                return true;
            }

            StringBuilder missing = new();
            for (int i = 0; i < node.prerequisites.Count; i++)
            {
                CharacterPerkNodeRef prerequisite = node.prerequisites[i];
                if (prerequisite?.node == null)
                {
                    continue;
                }

                if (tree.GetNodeLevel(prerequisite.node) < prerequisite.minLevel)
                {
                    if (missing.Length > 0)
                    {
                        missing.AppendLine();
                    }

                    missing.Append("MX_Perk_RequiresPrerequisite".Translate(
                        prerequisite.node.LabelCap, prerequisite.minLevel));
                }
            }

            failReason = missing.Length > 0 ? missing.ToString() : null;
            return missing.Length == 0;
        }

        /// <summary>requiresAll must all hold; requiresAny needs one. Empty lists pass.</summary>
        private bool ConditionsMet(CharacterPerkNodeDef node, out string failReason)
        {
            failReason = null;
            if (!node.requiresAll.NullOrEmpty())
            {
                StringBuilder missing = new();
                for (int i = 0; i < node.requiresAll.Count; i++)
                {
                    UnlockCondition condition = node.requiresAll[i];
                    if (condition != null && !condition.IsSatisfied(Pawn, null))
                    {
                        if (missing.Length > 0)
                        {
                            missing.AppendLine();
                        }

                        missing.Append(condition.GetProgressText(Pawn, null));
                    }
                }

                if (missing.Length > 0)
                {
                    failReason = missing.ToString();
                    return false;
                }
            }

            if (!node.requiresAny.NullOrEmpty())
            {
                string firstProgress = null;
                for (int i = 0; i < node.requiresAny.Count; i++)
                {
                    UnlockCondition condition = node.requiresAny[i];
                    if (condition == null)
                    {
                        continue;
                    }

                    if (condition.IsSatisfied(Pawn, null))
                    {
                        return true;
                    }

                    firstProgress ??= condition.GetProgressText(Pawn, null);
                }

                failReason = firstProgress;
                return false;
            }

            return true;
        }

        private bool HasCosts(CharacterPerkNodeDef node)
        {
            return PerkCostUtility.HasCosts(Pawn, node.costs);
        }

        private void ConsumeCosts(CharacterPerkNodeDef node)
        {
            PerkCostUtility.ConsumeCosts(Pawn, node.costs);
        }

        private void Unlock(CharacterPerkNodeDef node)
        {
            HediffComp_CharacterPerkTree tree = Tree;
            tree.SetNodeLevel(node, tree.GetNodeLevel(node) + 1);
            GrantContent(node);
            node.Notify_Unlocked(Pawn);
        }

        private void GrantContent(CharacterPerkNodeDef node)
        {
            if (!node.grantedAbilities.NullOrEmpty())
            {
                for (int i = 0; i < node.grantedAbilities.Count; i++)
                {
                    AbilityDef ability = node.grantedAbilities[i];
                    if (ability != null && Pawn.abilities.GetAbility(ability, includeTemporary: false) == null)
                    {
                        Pawn.abilities.GainAbility(ability);
                    }
                }
            }

            if (!node.grantedHediffs.NullOrEmpty())
            {
                for (int i = 0; i < node.grantedHediffs.Count; i++)
                {
                    HediffDef hediffDef = node.grantedHediffs[i];
                    if (hediffDef != null && Pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) == null)
                    {
                        Pawn.health.AddHediff(HediffMaker.MakeHediff(hediffDef, Pawn));
                    }
                }
            }
        }

        private void Broadcast()
        {
            if (parent is IPerkEventListener parentListener)
            {
                parentListener.Notify_PerkTreeChanged(Pawn, Tree);
            }

            List<HediffComp> comps = (parent as HediffWithComps)?.comps;
            if (comps == null)
            {
                return;
            }

            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] != this && comps[i] is IPerkEventListener listener)
                {
                    listener.Notify_PerkTreeChanged(Pawn, Tree);
                }
            }
        }
    }
}
