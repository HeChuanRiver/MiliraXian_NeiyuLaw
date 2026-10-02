using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.PerkSystem
{
    public class HediffComp_CharacterPerkTree : HediffComp
    {
        private bool initialized;
        private Dictionary<CharacterPerkNodeDef, int> nodeLevels;

        public HediffCompProperties_CharacterPerkTree Props => (HediffCompProperties_CharacterPerkTree)props;

        public IEnumerable<CharacterPerkNodeDef> LearnedNodes
        {
            get
            {
                NormalizeCollections();
                return nodeLevels.Where(pair => pair.Value > 0).Select(pair => pair.Key);
            }
        }

        public int LearnedNodeCount
        {
            get
            {
                NormalizeCollections();
                return nodeLevels.Count(pair => pair.Value > 0);
            }
        }

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void CompPostMake()
        {
            InitializeNewState();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            InitializeNewState();
            NotifyStateChanged();
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref initialized, "mx_perkTree_initialized", false);
            Scribe_Collections.Look(ref nodeLevels, "mx_perkTree_nodeLevels", LookMode.Def, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                NormalizeCollections();
                InitializeNewState();
                NotifyStateChanged();
            }
        }

        public bool HasNode(CharacterPerkNodeDef node)
        {
            return GetNodeLevel(node) > 0;
        }

        public int GetNodeLevel(CharacterPerkNodeDef node)
        {
            NormalizeCollections();
            if (node == null)
            {
                return 0;
            }

            int level;
            return nodeLevels.TryGetValue(node, out level) ? Mathf.Max(0, level) : 0;
        }

        /// <summary>
        /// The stored level after the character's seal applies. Stored state is never written by the
        /// seal, so everything asking "does this take effect right now" comes through here.
        /// Subclasses supply the seal; with none, stored is effective.
        /// </summary>
        public virtual int EffectiveNodeLevel(CharacterPerkNodeDef node)
        {
            return GetNodeLevel(node);
        }

        public int SyncNodesByAuraMasteryLevel(int auraMasteryLevel)
        {
            NormalizeCollections();
            int learnedCount = 0;
            List<CharacterPerkNodeDef> unlockedNodes = new();
            foreach (CharacterPerkNodeDef node in RelevantNodes())
            {
                if (node.requiredAuraMasteryLevel <= auraMasteryLevel && GetNodeLevel(node) <= 0)
                {
                    nodeLevels[node] = 1;
                    unlockedNodes.Add(node);
                    learnedCount++;
                }
            }

            if (learnedCount > 0)
            {
                NotifyStateChanged();
            }

            foreach (CharacterPerkNodeDef node in unlockedNodes)
            {
                node.Notify_Unlocked(Pawn);
            }

            return learnedCount;
        }

        public int LearnNodes(IEnumerable<CharacterPerkNodeDef> nodes)
        {
            NormalizeCollections();
            if (nodes == null)
            {
                return 0;
            }

            int learnedCount = 0;
            List<CharacterPerkNodeDef> unlockedNodes = new();
            foreach (CharacterPerkNodeDef node in nodes)
            {
                if (node != null && IsRelevantNode(node) && GetNodeLevel(node) <= 0)
                {
                    nodeLevels[node] = 1;
                    unlockedNodes.Add(node);
                    learnedCount++;
                }
            }

            if (learnedCount > 0)
            {
                NotifyStateChanged();
            }

            foreach (CharacterPerkNodeDef node in unlockedNodes)
            {
                node.Notify_Unlocked(Pawn);
            }

            return learnedCount;
        }

        private void InitializeNewState()
        {
            NormalizeCollections();
            if (initialized)
            {
                return;
            }

            initialized = true;
        }

        private IEnumerable<CharacterPerkNodeDef> RelevantNodes()
        {
            return DefDatabase<CharacterPerkNodeDef>.AllDefsListForReading.Where(IsRelevantNode);
        }

        public bool IsRelevantNode(CharacterPerkNodeDef node)
        {
            if (node == null)
            {
                return false;
            }

            return IsRelevantCategory(node.category);
        }

        public bool AllowsCategory(CharacterPerkCategoryDef category)
        {
            return IsRelevantCategory(category);
        }

        private bool IsRelevantCategory(CharacterPerkCategoryDef category)
        {
            return Props.categories == null || Props.categories.Count == 0 || Props.categories.Contains(category);
        }

        private void NotifyStateChanged()
        {
            SyncGrantedDefs();

            if (parent is IPerkEventListener parentListener)
            {
                parentListener.Notify_PerkTreeChanged(Pawn, this);
            }

            if (parent?.comps == null)
            {
                return;
            }

            foreach (HediffComp comp in parent.comps)
            {
                if (comp is IPerkEventListener listener)
                {
                    listener.Notify_PerkTreeChanged(Pawn, this);
                }
            }
        }

        public void SyncGrantedDefs()
        {
            Pawn pawn = Pawn;
            if (pawn == null)
            {
                return;
            }

            List<CharacterPerkNodeDef> relevantNodes = RelevantNodes().ToList();
            SyncGrantedAbilities(pawn, relevantNodes);
            SyncGrantedHediffs(pawn, relevantNodes);
        }

        private void SyncGrantedAbilities(Pawn pawn, List<CharacterPerkNodeDef> relevantNodes)
        {
            if (pawn.abilities == null)
            {
                return;
            }

            // Only ever grants. Removing would discard cooldowns, charges and comp state, so
            // whether a granted ability currently works is AbilityGate's business at read time.
            for (int i = 0; i < relevantNodes.Count; i++)
            {
                CharacterPerkNodeDef node = relevantNodes[i];
                if (node?.grantedAbilities == null || GetNodeLevel(node) <= 0)
                {
                    continue;
                }

                for (int j = 0; j < node.grantedAbilities.Count; j++)
                {
                    AbilityDef ability = node.grantedAbilities[j];
                    if (ability != null && pawn.abilities.GetAbility(ability, includeTemporary: false) == null)
                    {
                        pawn.abilities.GainAbility(ability);
                    }
                }
            }
        }

        private void SyncGrantedHediffs(Pawn pawn, List<CharacterPerkNodeDef> relevantNodes)
        {
            if (pawn.health?.hediffSet == null)
            {
                return;
            }

            HashSet<HediffDef> activeGranted = new();
            for (int i = 0; i < relevantNodes.Count; i++)
            {
                CharacterPerkNodeDef node = relevantNodes[i];
                if (node?.grantedHediffs == null || GetNodeLevel(node) <= 0)
                {
                    continue;
                }

                for (int j = 0; j < node.grantedHediffs.Count; j++)
                {
                    HediffDef hediffDef = node.grantedHediffs[j];
                    if (hediffDef != null)
                    {
                        activeGranted.Add(hediffDef);
                    }
                }
            }

            foreach (HediffDef hediffDef in activeGranted)
            {
                if (pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) == null)
                {
                    Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                    pawn.health.AddHediff(hediff);
                }
            }
        }

        private void NormalizeCollections()
        {
            nodeLevels ??= new();
        }
    }

}
