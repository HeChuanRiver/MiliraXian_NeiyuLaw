using System.Collections.Generic;
using Verse;
using MiliraXian.Characters.Common.Abilities;

namespace MiliraXian.Characters.Common.PerkSystem
{
    public class AbilityRequirement_Perk : AbilityRequirement
    {
        public CharacterPerkNodeDef perk;

        public override bool Met(Pawn pawn)
        {
            if (perk == null)
            {
                return true;
            }

            HediffComp_CharacterPerkTree tree = PerkTreeUtility.GetTreeFor(pawn, perk);
            return tree != null && tree.EffectiveNodeLevel(perk) > 0;
        }

        public override string DisabledReason(Pawn pawn)
        {
            return "MX_Perk_RequiresPerk".Translate(perk?.LabelCap ?? "");
        }
    }

    public static class PerkTreeUtility
    {
        /// <summary>Finds the tree that owns this node's category, if the pawn has one.</summary>
        public static HediffComp_CharacterPerkTree GetTreeFor(Pawn pawn, CharacterPerkNodeDef node)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null)
            {
                return null;
            }

            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is HediffWithComps withComps
                    && withComps.TryGetComp<HediffComp_CharacterPerkTree>() is HediffComp_CharacterPerkTree tree
                    && tree.IsRelevantNode(node))
                {
                    return tree;
                }
            }

            return null;
        }
    }
}
