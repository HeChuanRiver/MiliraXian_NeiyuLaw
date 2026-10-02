using UnityEngine;
using Verse;
using MiliraXian.Characters.Common.PerkSystem;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_AuraPerkTree : HediffCompProperties_CharacterPerkTree
    {
        public HediffCompProperties_AuraPerkTree()
        {
            compClass = typeof(HediffComp_AuraPerkTree);
        }
    }

    /// <summary>
    /// Qinghe's perk tree. Nodes unlock by aura mastery level; the power-level setting seals how
    /// much of that takes effect without ever touching stored state.
    /// </summary>
    public class HediffComp_AuraPerkTree : HediffComp_CharacterPerkTree
    {
        public override int EffectiveNodeLevel(CharacterPerkNodeDef node)
        {
            int stored = GetNodeLevel(node);
            if (stored <= 0 || node == null)
            {
                return stored;
            }

            return node.requiredAuraMasteryLevel <= QinghePowerBalance.MaxEffectiveLevel ? stored : 0;
        }
    }
}
