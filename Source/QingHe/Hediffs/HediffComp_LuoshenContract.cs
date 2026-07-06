using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_LuoshenContract : HediffCompProperties
    {
        public HediffCompProperties_LuoshenContract()
        {
            compClass = typeof(HediffComp_LuoshenContract);
        }
    }

    public class HediffComp_LuoshenContract : HediffComp
    {
        public override bool CompDisallowVisible()
        {
            return true;
        }

        public static bool IsMaintainedFor(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return false;
            }

            if (MX_QHCharacterUtility.IsQinghe(pawn))
            {
                Pawn spouse = GetLivingSpouse(pawn);
                return spouse != null && HasContractWith(pawn, spouse);
            }

            Pawn qinghe = GetQingheContractPartner(pawn);
            return qinghe != null && HasContractWith(qinghe, pawn);
        }

        public static void SyncForQinghe(Pawn qinghe, HediffComp_SkillTreeState state)
        {
            if (!MX_QHCharacterUtility.IsQinghe(qinghe))
            {
                return;
            }

            if (state == null || !state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Luoshenfu))
            {
                RemoveContractFromPartner(qinghe);
                return;
            }

            Pawn spouse = GetLivingSpouse(qinghe);
            if (spouse == null)
            {
                RemoveContractFromPartner(qinghe);
                return;
            }

            RemoveContractFromOtherPartners(qinghe, spouse);
            SyncToPartner(qinghe, spouse);
        }

        public static void NotifySpouseRelationAdded(Pawn pawn, Pawn otherPawn)
        {
            Pawn qinghe = MX_QHCharacterUtility.IsQinghe(pawn) ? pawn : (MX_QHCharacterUtility.IsQinghe(otherPawn) ? otherPawn : null);
            if (qinghe == null)
            {
                return;
            }

            MX_QHSkillSystem.SyncChoices(qinghe);
            NotifyThoughtsDirty(pawn);
            NotifyThoughtsDirty(otherPawn);
        }

        public static void NotifySpouseRelationRemoved(Pawn pawn, Pawn otherPawn)
        {
            Pawn qinghe = MX_QHCharacterUtility.IsQinghe(pawn) ? pawn : (MX_QHCharacterUtility.IsQinghe(otherPawn) ? otherPawn : null);
            Pawn spouse = qinghe == pawn ? otherPawn : pawn;
            if (qinghe == null || spouse == null)
            {
                return;
            }

            bool wasContract = HasContractWith(qinghe, spouse) || HasContractWith(spouse, qinghe);
            RemoveContract(qinghe, spouse);
            NotifyThoughtsDirty(qinghe);
            NotifyThoughtsDirty(spouse);
            if (wasContract)
            {
                GiveBrokenThought(qinghe, spouse);
                GiveBrokenThought(spouse, qinghe);
            }
        }

        private static void SyncToPartner(Pawn qinghe, Pawn spouse)
        {
            if (qinghe?.health?.hediffSet == null || spouse?.health?.hediffSet == null || spouse.story?.traits == null)
            {
                return;
            }

            SetContractTarget(qinghe, spouse);
            SetContractTarget(spouse, qinghe);
            NotifyThoughtsDirty(qinghe);
            NotifyThoughtsDirty(spouse);
        }

        private static void SetContractTarget(Pawn pawn, Pawn targetPawn)
        {
            if (pawn?.health?.hediffSet == null || targetPawn == null || MX_QHDefOf.MX_QH_LuoshenContract == null)
            {
                return;
            }

            HediffWithTarget hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_LuoshenContract) as HediffWithTarget;
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_LuoshenContract, pawn) as HediffWithTarget;
                if (hediff == null)
                {
                    return;
                }

                hediff.target = targetPawn;
                pawn.health.AddHediff(hediff);
                return;
            }

            hediff.target = targetPawn;
        }

        private static void RemoveContractFromPartner(Pawn qinghe)
        {
            Pawn partner = GetContractPartner(qinghe);
            if (partner != null)
            {
                RemoveContract(qinghe, partner);
            }
        }

        private static void RemoveContractFromOtherPartners(Pawn qinghe, Pawn currentSpouse)
        {
            Pawn partner = GetContractPartner(qinghe);
            if (partner != null && partner != currentSpouse)
            {
                RemoveContract(qinghe, partner);
            }
        }

        private static void RemoveContract(Pawn first, Pawn second)
        {
            RemoveContractHediff(first, second);
            RemoveContractHediff(second, first);
        }

        private static void RemoveContractHediff(Pawn pawn, Pawn targetPawn)
        {
            HediffWithTarget hediff = GetContractHediff(pawn);
            if (hediff == null)
            {
                return;
            }

            if (targetPawn != null && hediff.target != targetPawn)
            {
                return;
            }

            pawn.health.RemoveHediff(hediff);
        }

        private static Pawn GetLivingSpouse(Pawn pawn)
        {
            return pawn?.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse, other => other != null && !other.Dead);
        }

        private static Pawn GetContractPartner(Pawn pawn)
        {
            return GetContractHediff(pawn)?.target as Pawn;
        }

        private static Pawn GetQingheContractPartner(Pawn pawn)
        {
            Pawn partner = GetContractPartner(pawn);
            return MX_QHCharacterUtility.IsQinghe(partner) ? partner : null;
        }

        private static bool HasContractWith(Pawn pawn, Pawn targetPawn)
        {
            return GetContractHediff(pawn)?.target == targetPawn;
        }

        private static HediffWithTarget GetContractHediff(Pawn pawn)
        {
            return MX_QHDefOf.MX_QH_LuoshenContract != null
                ? pawn?.health?.hediffSet?.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_LuoshenContract) as HediffWithTarget
                : null;
        }

        private static void GiveBrokenThought(Pawn pawn, Pawn otherPawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return;
            }

            if (MX_QHDefOf.MX_QH_LuoshenContractBroken != null)
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



