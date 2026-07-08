using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace MiliraXian.Characters
{
    public class HediffComp_SkillTreeState : HediffComp
    {
        private bool initialized;
        private Dictionary<SkillNodeDef, int> nodeLevels;
        private Dictionary<SkillNodeDef, float> nodeReadingProgress;

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

        public int UnlockedCollectionCount => RelevantCollections().Count(IsCollectionUnlocked);

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
            Scribe_Collections.Look(ref nodeReadingProgress, "mx_skillTree_nodeReadingProgress", LookMode.Def, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                NormalizeCollections();
                InitializeNewState();
                EnsureInitiallyLearnedNodes();
                NotifyStateChanged();
            }
        }

        public bool IsCollectionUnlocked(SkillNodeCollectionDef collectionDef)
        {
            NormalizeCollections();
            return collectionDef != null && nodeLevels.Any(pair => pair.Value > 0 && pair.Key?.collection == collectionDef);
        }

        public bool IsCollectionCompleted(SkillNodeCollectionDef collectionDef)
        {
            NormalizeCollections();
            if (collectionDef == null)
            {
                return false;
            }

            List<SkillNodeDef> nodes = RelevantNodes()
                .Where(node => node.collection == collectionDef)
                .ToList();
            return nodes.Count > 0 && nodes.All(node => GetNodeLevel(node) > 0);
        }

        public bool IsCollectionCompletionEffectActive(SkillNodeCollectionDef collectionDef)
        {
            return collectionDef != null && collectionDef.HasCompletionEffect && IsCollectionCompleted(collectionDef);
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
            return nodeLevels.TryGetValue(node, out level) ? Mathf.Clamp(level, 0, node.MaxLevel) : 0;
        }

        public bool CanLearn(SkillNodeDef node, out string reason)
        {
            NormalizeCollections();
            if (node == null)
            {
                reason = "MX_Common_Unknown".Translate();
                return false;
            }

            if (!IsRelevantNode(node))
            {
                reason = "MX_Common_Unknown".Translate();
                return false;
            }

            if (GetNodeLevel(node) >= node.MaxLevel)
            {
                reason = Props.alreadyLearnedReasonKey.Translate();
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryLearn(SkillNodeDef node, out string reason)
        {
            if (!CanLearn(node, out reason))
            {
                return false;
            }

            nodeLevels[node] = GetNodeLevel(node) + 1;
            NotifyStateChanged();
            reason = null;
            return true;
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

        public float GetNodeReadingProgressTicks(SkillNodeDef node)
        {
            NormalizeCollections();
            if (node == null)
            {
                return 0f;
            }

            float progress;
            return nodeReadingProgress.TryGetValue(node, out progress) ? Mathf.Max(0f, progress) : 0f;
        }

        public float GetNodeReadingProgressPercent(SkillNodeDef node)
        {
            if (node == null)
            {
                return 0f;
            }

            if (GetNodeLevel(node) >= node.MaxLevel)
            {
                return 1f;
            }

            return Mathf.Clamp01(GetNodeReadingProgressTicks(node) / Mathf.Max(1, node.RequiredReadingTicks));
        }

        public bool AddNodeReadingProgress(SkillNodeDef node, float progressTicks)
        {
            NormalizeCollections();
            if (node == null || !IsRelevantNode(node))
            {
                return false;
            }

            float progress = GetNodeReadingProgressTicks(node) + Mathf.Max(0f, progressTicks);
            int requiredTicks = Mathf.Max(1, node.RequiredReadingTicks);
            if (progress >= requiredTicks)
            {
                nodeReadingProgress[node] = requiredTicks;
                return true;
            }

            nodeReadingProgress[node] = progress;
            return false;
        }

        public void ClearNodeReadingProgress(SkillNodeDef node)
        {
            NormalizeCollections();
            if (node != null)
            {
                nodeReadingProgress.Remove(node);
            }
        }

        public void LearnAllNodesInCollection(SkillNodeCollectionDef collectionDef)
        {
            NormalizeCollections();
            if (collectionDef == null)
            {
                return;
            }

            foreach (SkillNodeDef node in DefDatabase<SkillNodeDef>.AllDefsListForReading)
            {
                if (node.collection == collectionDef && IsRelevantNode(node) && GetNodeLevel(node) <= 0)
                {
                    nodeLevels[node] = 1;
                }
            }

            NotifyStateChanged();
        }

        private void InitializeNewState()
        {
            NormalizeCollections();
            if (initialized)
            {
                return;
            }

            initialized = true;
            EnsureInitiallyLearnedNodes();
        }

        private void EnsureInitiallyLearnedNodes()
        {
            LearnNodes(RelevantNodes().Where(node => node.initiallyLearned));
        }

        private IEnumerable<SkillNodeDef> RelevantNodes()
        {
            return DefDatabase<SkillNodeDef>.AllDefsListForReading.Where(IsRelevantNode);
        }

        private List<SkillNodeCollectionDef> RelevantCollections()
        {
            return DefDatabase<SkillNodeCollectionDef>.AllDefsListForReading
                .Where(collection => collection != null && IsRelevantCategory(collection.category))
                .ToList();
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

        private void NormalizeCollections()
        {
            if (nodeLevels == null)
            {
                nodeLevels = new Dictionary<SkillNodeDef, int>();
            }

            if (nodeReadingProgress == null)
            {
                nodeReadingProgress = new Dictionary<SkillNodeDef, float>();
            }
        }
    }

}
