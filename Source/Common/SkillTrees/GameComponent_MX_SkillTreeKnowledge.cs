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
    public class GameComponent_MX_SkillTreeKnowledge : GameComponent
    {
        private List<SkillNodeDef> extractedNodes;

        public GameComponent_MX_SkillTreeKnowledge(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref extractedNodes, "mx_skillTree_extractedNodes", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Normalize();
            }
        }

        public static bool IsNodeExtracted(SkillNodeDef node)
        {
            GameComponent_MX_SkillTreeKnowledge component = Current.Game?.GetComponent<GameComponent_MX_SkillTreeKnowledge>();
            return component != null && component.HasNode(node);
        }

        public static HashSet<SkillNodeDef> ExtractedNodesSnapshot()
        {
            GameComponent_MX_SkillTreeKnowledge component = Current.Game?.GetComponent<GameComponent_MX_SkillTreeKnowledge>();
            if (component == null)
            {
                return new HashSet<SkillNodeDef>();
            }

            component.Normalize();
            return new HashSet<SkillNodeDef>(component.extractedNodes);
        }

        public static void NotifyNodeExtracted(SkillNodeDef node)
        {
            if (node == null)
            {
                return;
            }

            Current.Game?.GetComponent<GameComponent_MX_SkillTreeKnowledge>()?.RegisterNode(node);
        }

        private bool HasNode(SkillNodeDef node)
        {
            Normalize();
            return node != null && extractedNodes.Contains(node);
        }

        private void RegisterNode(SkillNodeDef node)
        {
            Normalize();
            if (node != null && !extractedNodes.Contains(node))
            {
                extractedNodes.Add(node);
            }
        }

        private void Normalize()
        {
            if (extractedNodes == null)
            {
                extractedNodes = new List<SkillNodeDef>();
            }

            extractedNodes.RemoveAll(node => node == null);
        }
    }
}
