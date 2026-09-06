using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffComp_SkillTreeState : HediffComp
    {
        private bool initialized;
        private Dictionary<SkillNodeDef, int> nodeLevels;

        public HediffCompProperties_SkillTreeState Props => (HediffCompProperties_SkillTreeState)props;

        public IEnumerable<SkillNodeDef> LearnedNodes
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
            Scribe_Values.Look(ref initialized, "mx_skillTree_initialized", false);
            Scribe_Collections.Look(ref nodeLevels, "mx_skillTree_nodeLevels", LookMode.Def, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                NormalizeCollections();
                InitializeNewState();
                NotifyStateChanged();
            }
        }

        public bool HasNode(SkillNodeDef node)
        {
            return GetNodeLevel(node) > 0;
        }

        public int GetNodeLevel(SkillNodeDef node)
        {
            NormalizeCollections();
            if (node == null)
            {
                return 0;
            }

            int level;
            return nodeLevels.TryGetValue(node, out level) && level > 0 ? 1 : 0;
        }

        public int SyncNodesByGraceLevel(int graceLevel)
        {
            NormalizeCollections();
            int learnedCount = 0;
            foreach (SkillNodeDef node in RelevantNodes())
            {
                if (node.requiredGraceLevel <= graceLevel && GetNodeLevel(node) <= 0)
                {
                    nodeLevels[node] = 1;
                    learnedCount++;
                }
            }

            if (learnedCount > 0)
            {
                NotifyStateChanged();
            }

            return learnedCount;
        }

        public int LearnNodes(IEnumerable<SkillNodeDef> nodes)
        {
            NormalizeCollections();
            if (nodes == null)
            {
                return 0;
            }

            int learnedCount = 0;
            foreach (SkillNodeDef node in nodes)
            {
                if (node != null && IsRelevantNode(node) && GetNodeLevel(node) <= 0)
                {
                    nodeLevels[node] = 1;
                    learnedCount++;
                }
            }

            if (learnedCount > 0)
            {
                NotifyStateChanged();
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

        private IEnumerable<SkillNodeDef> RelevantNodes()
        {
            return DefDatabase<SkillNodeDef>.AllDefsListForReading.Where(IsRelevantNode);
        }

        public bool IsRelevantNode(SkillNodeDef node)
        {
            if (node == null)
            {
                return false;
            }

            return IsRelevantCategory(node.category);
        }

        public bool AllowsCategory(SkillNodeCategoryDef category)
        {
            return IsRelevantCategory(category);
        }

        private bool IsRelevantCategory(SkillNodeCategoryDef category)
        {
            return Props.categories == null || Props.categories.Count == 0 || Props.categories.Contains(category);
        }

        private void NotifyStateChanged()
        {
            SyncGrantedDefs();

            if (parent is ISkillTreeStateListener parentListener)
            {
                parentListener.Notify_SkillTreeStateChanged(Pawn, this);
            }

            if (parent?.comps == null)
            {
                return;
            }

            foreach (HediffComp comp in parent.comps)
            {
                if (comp is ISkillTreeStateListener listener)
                {
                    listener.Notify_SkillTreeStateChanged(Pawn, this);
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

            List<SkillNodeDef> relevantNodes = RelevantNodes().ToList();
            SyncGrantedAbilities(pawn, relevantNodes);
            SyncGrantedHediffs(pawn, relevantNodes);
        }

        private void SyncGrantedAbilities(Pawn pawn, List<SkillNodeDef> relevantNodes)
        {
            if (pawn.abilities == null)
            {
                return;
            }

            HashSet<AbilityDef> allGranted = new();
            HashSet<AbilityDef> activeGranted = new();
            for (int i = 0; i < relevantNodes.Count; i++)
            {
                SkillNodeDef node = relevantNodes[i];
                if (node?.grantedAbilities == null)
                {
                    continue;
                }

                bool active = GetNodeLevel(node) > 0;
                for (int j = 0; j < node.grantedAbilities.Count; j++)
                {
                    AbilityDef ability = node.grantedAbilities[j];
                    if (ability == null)
                    {
                        continue;
                    }

                    allGranted.Add(ability);
                    if (active)
                    {
                        activeGranted.Add(ability);
                    }
                }
            }

            foreach (AbilityDef ability in allGranted)
            {
                if (activeGranted.Contains(ability))
                {
                    if (pawn.abilities.GetAbility(ability, includeTemporary: false) == null)
                    {
                        pawn.abilities.GainAbility(ability);
                    }
                }
                else
                {
                    pawn.abilities.RemoveAbility(ability);
                }
            }
        }

        private void SyncGrantedHediffs(Pawn pawn, List<SkillNodeDef> relevantNodes)
        {
            if (pawn.health?.hediffSet == null)
            {
                return;
            }

            HashSet<HediffDef> activeGranted = new();
            for (int i = 0; i < relevantNodes.Count; i++)
            {
                SkillNodeDef node = relevantNodes[i];
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
