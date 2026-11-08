using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common.PerkSystem
{
    public class HediffCompProperties_PerkEventHandler : HediffCompProperties
    {
        public HediffCompProperties_PerkEventHandler() => compClass = typeof(HediffComp_PerkEventHandler);
    }

    /// <summary>One writer for purchases, initial grants, migration and automatic unlocks.</summary>
    public class HediffComp_PerkEventHandler : HediffComp
    {
        public HediffComp_CharacterPerkTree Tree => parent.TryGetComp<HediffComp_CharacterPerkTree>();

        public void TryAutoUnlock()
        {
            if (Tree == null || Pawn == null) return;
            foreach (var node in Tree.RelevantNodes())
                if (node != null && !node.displayOnly && Tree.GetNodeLevel(node) == 0
                    && node.ShouldAutoUnlock(Pawn) && ConditionsMet(node, out _))
                    TryGrant(node, notify: true, broadcast: false);
            RefreshEffects();
        }

        public bool CanUnlock(CharacterPerkNodeDef node, out string reason, bool checkCosts = true)
        {
            reason = null;
            if (Tree == null || Pawn == null || node == null || node.displayOnly || !Tree.IsRelevantNode(node))
            { reason = "无法领悟这项修行。"; return false; }
            if (Tree.GetNodeLevel(node) >= node.maxLevel)
            { reason = "MX_Perk_MaxLevel".Translate(); return false; }
            reason = node.PurchaseBlockedReason(Pawn);
            if (reason != null || !PrerequisitesMet(node, out reason) || !ConditionsMet(node, out reason)) return false;
            if (checkCosts && !PerkCostUtility.HasCosts(Pawn, node.costs))
            { reason = "MX_Perk_MaterialsMissing".Translate(); return false; }
            return true;
        }

        public bool TryUnlock(CharacterPerkNodeDef node, out string failReason)
        {
            if (!CanUnlock(node, out failReason)) return false;
            if (!PerkCostUtility.TryStageCosts(Pawn, node.costs, out var payment))
            { failReason = "材料状态已变化，请重新尝试。"; return false; }
            using (payment) Commit(node, payment, notify: true, broadcast: true);
            return true;
        }

        /// <summary>Skips purchase requirements only; ownership, limits and stored prerequisites still apply.</summary>
        public bool TryGrant(CharacterPerkNodeDef node, bool notify = false, bool broadcast = true)
        {
            if (Tree == null || node == null || node.displayOnly || !Tree.IsRelevantNode(node)
                || Tree.GetNodeLevel(node) >= node.maxLevel || !PrerequisitesMet(node, out _)) return false;
            Commit(node, null, notify, broadcast);
            return true;
        }

        private void Commit(CharacterPerkNodeDef node, PerkCostTransaction payment, bool notify, bool broadcast)
        {
            Tree.SetNodeLevel(node, Tree.GetNodeLevel(node) + 1);
            payment?.Commit();
            // Effects are reconstructible. A notification failure must not allow another paid increment.
            try { GrantContent(node); }
            catch (Exception exception) { Log.Error("[MiliraXian] Perk grant " + node.defName + ": " + exception); }
            if (notify)
                try { node.Notify_Unlocked(Pawn); }
                catch (Exception exception) { Log.Error("[MiliraXian] Perk notification " + node.defName + ": " + exception); }
            if (broadcast) RefreshEffects();
        }

        private bool PrerequisitesMet(CharacterPerkNodeDef node, out string reason)
        {
            StringBuilder missing = new();
            if (node.prerequisites != null)
                foreach (var prerequisite in node.prerequisites)
                {
                    if (prerequisite?.node == null)
                    { missing.AppendLine("修行前置配置缺失。"); continue; }
                    if (Tree.GetNodeLevel(prerequisite.node) < prerequisite.minLevel)
                        missing.AppendLine("MX_Perk_RequiresPrerequisite".Translate(prerequisite.node.LabelCap, prerequisite.minLevel));
                }
            reason = missing.Length > 0 ? missing.ToString().TrimEnd() : null;
            return reason == null;
        }

        private bool ConditionsMet(CharacterPerkNodeDef node, out string reason)
        {
            StringBuilder missing = new();
            if (node.requiresAll != null)
                foreach (var condition in node.requiresAll)
                    if (condition != null && !condition.IsSatisfied(Pawn, null))
                        missing.AppendLine(condition.GetProgressText(Pawn, null));
            reason = missing.Length > 0 ? missing.ToString().TrimEnd() : null;
            if (reason != null) return false;
            if (!node.requiresAny.NullOrEmpty())
            {
                foreach (var condition in node.requiresAny)
                    if (condition != null && condition.IsSatisfied(Pawn, null)) return true;
                foreach (var condition in node.requiresAny)
                    if (condition != null) missing.AppendLine(condition.GetProgressText(Pawn, null));
                reason = missing.Length > 0 ? missing.ToString().TrimEnd() : "修行条件尚未满足。";
                return false;
            }
            return true;
        }

        private void GrantContent(CharacterPerkNodeDef node)
        {
            if (node.grantedAbilities != null && Pawn.abilities != null)
                foreach (var ability in node.grantedAbilities)
                    if (ability != null && Pawn.abilities.GetAbility(ability, includeTemporary: false) == null)
                        Pawn.abilities.GainAbility(ability);
            if (node.grantedHediffs != null)
                foreach (var hediff in node.grantedHediffs)
                    if (hediff != null && !Pawn.health.hediffSet.HasHediff(hediff))
                        Pawn.health.AddHediff(HediffMaker.MakeHediff(hediff, Pawn));
        }

        public void RestoreGrantedContent()
        {
            if (Tree == null) return;
            foreach (var node in Tree.LearnedNodes) GrantContent(node);
            RefreshEffects();
        }

        public void RefreshEffects()
        {
            if (Tree == null || Pawn?.health?.hediffSet == null) return;
            // A snapshot lets a listener safely add a required container without invalidating iteration.
            foreach (var hediff in Pawn.health.hediffSet.hediffs.ToArray())
            {
                Notify(hediff);
                if (hediff is HediffWithComps withComps && withComps.comps != null)
                    foreach (var comp in withComps.comps)
                        if (comp != this) Notify(comp);
            }
            if (Pawn.equipment?.Primary is ThingWithComps weapon)
                foreach (var comp in weapon.AllComps) Notify(comp);
            if (Pawn.apparel != null)
                foreach (var apparel in Pawn.apparel.WornApparel)
                    foreach (var comp in apparel.AllComps) Notify(comp);
            Pawn.health.hediffSet.DirtyCache();
            Pawn.health.capacities.Notify_CapacityLevelsDirty();
        }

        private void Notify(object target)
        {
            if (target is not IPerkEventListener listener) return;
            try { listener.Notify_PerkTreeChanged(Pawn, Tree); }
            catch (Exception exception) { Log.Error("[MiliraXian] Perk effect refresh: " + exception); }
        }
    }
}
