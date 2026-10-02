using Verse;
using MiliraXian.Characters.Common.PerkSystem;
using MiliraXian.Characters.QingHe.Hediffs;

namespace MiliraXian.Characters.QingHe
{
    /// <summary>
    /// Qinghe's perk nodes gate on aura mastery: the stored level auto-unlocks when the earned
    /// mastery reaches the threshold, and the power-level setting seals anything above it without
    /// touching stored state.
    /// </summary>
    public class MX_QH_AuraPerkNodeDef : CharacterPerkNodeDef
    {
        public int requiredAuraMasteryLevel;

        public override bool ShouldAutoUnlock(Pawn pawn)
        {
            return MX_QH_HediffUtility.GetAuraMasteryComp(pawn)?.CurrentLevel >= requiredAuraMasteryLevel;
        }

        public override int EffectiveLevel(Pawn pawn, int storedLevel)
        {
            return requiredAuraMasteryLevel <= QinghePowerBalance.MaxEffectiveLevel ? storedLevel : 0;
        }
    }
}
