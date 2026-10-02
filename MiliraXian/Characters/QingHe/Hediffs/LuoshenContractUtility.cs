using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using Verse;
using MiliraXian.Characters.Common.PerkSystem;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    /// <summary>
    /// The Luoshen contract is stateless: it asks whether the perk is in effect and whether Qinghe
    /// currently has a living spouse. Nothing is stored, so sealing the perk simply stops the
    /// thought from applying and there is no binding to maintain or dissolve.
    /// </summary>
    public static class LuoshenContractUtility
    {
        public static int MaintainedThoughtStageFor(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return -1;
            }

            if (MX_QHCharacterUtility.IsQinghe(pawn))
            {
                return PerkInEffect(pawn) ? (GetLivingSpouse(pawn) != null ? 0 : 1) : -1;
            }

            Pawn qinghe = GetLivingSpouse(pawn);
            return MX_QHCharacterUtility.IsQinghe(qinghe) && PerkInEffect(qinghe) ? 0 : -1;
        }

        public static void NotifySpouseRelationAdded(Pawn pawn, Pawn otherPawn)
        {
            NotifyThoughtsDirty(pawn);
            NotifyThoughtsDirty(otherPawn);
        }

        public static void NotifySpouseRelationRemoved(Pawn pawn, Pawn otherPawn)
        {
            Pawn qinghe = MX_QHCharacterUtility.IsQinghe(pawn) ? pawn
                : MX_QHCharacterUtility.IsQinghe(otherPawn) ? otherPawn
                : null;
            NotifyThoughtsDirty(pawn);
            NotifyThoughtsDirty(otherPawn);
            if (qinghe == null || !PerkInEffect(qinghe))
            {
                return;
            }

            Pawn spouse = qinghe == pawn ? otherPawn : pawn;
            GiveBrokenThought(qinghe, spouse);
            GiveBrokenThought(spouse, qinghe);
        }

        private static bool PerkInEffect(Pawn qinghe)
        {
            HediffComp_CharacterPerkTree tree = MX_QH_HediffUtility.GetFlowerResonance(qinghe);
            return tree != null && tree.EffectiveNodeLevel(MX_QHCharacterPerkNodeDefOf.MX_QH_Node_Luoshenfu) > 0;
        }

        private static Pawn GetLivingSpouse(Pawn pawn)
        {
            return pawn?.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse, other => other != null && !other.Dead);
        }

        private static void GiveBrokenThought(Pawn pawn, Pawn otherPawn)
        {
            if (pawn != null && !pawn.Dead && MX_QHDefOf.MX_QH_LuoshenContractBroken != null)
            {
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(MX_QHDefOf.MX_QH_LuoshenContractBroken, otherPawn);
            }
        }

        private static void NotifyThoughtsDirty(Pawn pawn)
        {
            if (pawn != null && !pawn.Dead)
            {
                pawn.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty();
            }
        }
    }
}
