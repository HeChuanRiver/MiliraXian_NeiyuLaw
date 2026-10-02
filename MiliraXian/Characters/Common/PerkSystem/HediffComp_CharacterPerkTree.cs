using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.PerkSystem
{
    /// <summary>
    /// The stored side of a perk tree: which nodes this pawn has learned and at what level.
    /// Unlocking, granting and broadcasting belong to the event handler comp beside it.
    /// A category is a partition of nodes: one tree may hold several categories, but a category
    /// never spans two trees.
    /// </summary>
    public class HediffComp_CharacterPerkTree : HediffComp
    {
        private Dictionary<CharacterPerkNodeDef, int> nodeLevels = new();

        public HediffCompProperties_CharacterPerkTree Props => (HediffCompProperties_CharacterPerkTree)props;

        public IReadOnlyList<CharacterPerkNodeDef> LearnedNodes
        {
            get
            {
                NormalizeCollections();
                return nodeLevels.Where(pair => pair.Key != null && pair.Value > 0)
                    .Select(pair => pair.Key)
                    .ToList();
            }
        }

        private void NormalizeCollections()
        {
            nodeLevels ??= new Dictionary<CharacterPerkNodeDef, int>();
        }

        public bool IsRelevantCategory(CharacterPerkCategoryDef category)
        {
            List<CharacterPerkCategoryDef> categories = Props.categories;
            return category != null && categories != null && categories.Contains(category);
        }

        public bool IsRelevantNode(CharacterPerkNodeDef node)
        {
            return node != null && IsRelevantCategory(node.category);
        }

        public List<CharacterPerkNodeDef> RelevantNodes()
        {
            List<CharacterPerkNodeDef> relevant = new();
            foreach (CharacterPerkNodeDef node in DefDatabase<CharacterPerkNodeDef>.AllDefs)
            {
                if (IsRelevantNode(node))
                {
                    relevant.Add(node);
                }
            }

            return relevant;
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

        /// <summary>Stored level narrowed by the node's own seal; the seal never rewrites storage.</summary>
        public int EffectiveNodeLevel(CharacterPerkNodeDef node)
        {
            return node == null ? 0 : node.EffectiveLevel(Pawn, GetNodeLevel(node));
        }

        internal void SetNodeLevel(CharacterPerkNodeDef node, int level)
        {
            NormalizeCollections();
            nodeLevels[node] = level;
        }

        public bool HasNode(CharacterPerkNodeDef node)
        {
            return GetNodeLevel(node) > 0;
        }

        public override void CompExposeData()
        {
            Scribe_Collections.Look(ref nodeLevels, "mx_perkTree_nodeLevels", LookMode.Def, LookMode.Value);
        }
    }
}
