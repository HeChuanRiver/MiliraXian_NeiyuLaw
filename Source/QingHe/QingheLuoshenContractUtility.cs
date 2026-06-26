using System;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class Hediff_LuoshenContract : HediffWithTarget
    {
        private string mirroredFlowerWordDefName;
        private bool mirroredFlowerWordAddedByContract;

        public string MirroredFlowerWordDefName => mirroredFlowerWordDefName;

        public bool MirroredFlowerWordAddedByContract => mirroredFlowerWordAddedByContract;

        public override bool Visible => false;

        public void SetTargetPawn(Pawn targetPawn)
        {
            target = targetPawn;
        }

        public void SetMirroredFlowerWord(string traitDefName, bool addedByContract)
        {
            mirroredFlowerWordDefName = traitDefName;
            mirroredFlowerWordAddedByContract = addedByContract;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mirroredFlowerWordDefName, "mx_qh_luoshenContract_mirroredFlowerWordDefName");
            Scribe_Values.Look(ref mirroredFlowerWordAddedByContract, "mx_qh_luoshenContract_mirroredFlowerWordAddedByContract", false);
        }
    }

    public class ThoughtWorker_QingheLuoshenContractMaintained : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return QingheLuoshenContractUtility.IsContractMaintainedFor(p)
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }

    public static class QingheLuoshenContractUtility
    {
        private const string ContractHediffDefName = "MX_QH_LuoshenContract";
        private const string BrokenThoughtDefName = "MX_QH_LuoshenContractBroken";

        public static bool IsContractUnlocked(Pawn qinghe)
        {
            if (!MX_QHUtility.IsQinghe(qinghe) || qinghe.Dead)
            {
                return false;
            }

            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(qinghe);
            return state?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Luoshenfu) == true;
        }

        public static bool IsContractMaintainedFor(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return false;
            }

            if (MX_QHUtility.IsQinghe(pawn))
            {
                Pawn spouse = GetLivingSpouse(pawn);
                return spouse != null && HasContractWith(pawn, spouse);
            }

            Pawn qinghe = GetQingheContractPartner(pawn);
            return qinghe != null && HasContractWith(qinghe, pawn);
        }

        public static void SyncForQinghe(Pawn qinghe, HediffComp_FlowerResonance state, HediffComp_FlowerChoices choices)
        {
            if (!MX_QHUtility.IsQinghe(qinghe))
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
            SyncToPartner(qinghe, spouse, state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerWord) ? choices?.SelectedFlowerWord : null);
        }

        public static void NotifySpouseRelationAdded(Pawn pawn, Pawn otherPawn)
        {
            Pawn qinghe = MX_QHUtility.IsQinghe(pawn) ? pawn : (MX_QHUtility.IsQinghe(otherPawn) ? otherPawn : null);
            if (qinghe == null)
            {
                return;
            }

            QingheSkillTreeSystem.SyncChoices(qinghe);
            NotifyThoughtsDirty(pawn);
            NotifyThoughtsDirty(otherPawn);
        }

        public static void NotifySpouseRelationRemoved(Pawn pawn, Pawn otherPawn)
        {
            Pawn qinghe = MX_QHUtility.IsQinghe(pawn) ? pawn : (MX_QHUtility.IsQinghe(otherPawn) ? otherPawn : null);
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

        public static void NotifyPawnKilled(Pawn deadPawn)
        {
            if (deadPawn == null)
            {
                return;
            }

            Pawn qinghe = MX_QHUtility.IsQinghe(deadPawn) ? deadPawn : GetQingheContractPartner(deadPawn);
            Pawn spouse = qinghe == deadPawn ? GetContractPartner(qinghe) : deadPawn;
            if (qinghe == null || spouse == null)
            {
                return;
            }

            bool wasContract = HasContractWith(qinghe, spouse) || HasContractWith(spouse, qinghe);
            RemoveContract(qinghe, spouse);
            NotifyThoughtsDirty(qinghe);
            NotifyThoughtsDirty(spouse);
            if (!wasContract)
            {
                return;
            }

            Pawn survivor = qinghe.Dead ? spouse : qinghe;
            Pawn lost = survivor == qinghe ? spouse : qinghe;
            GiveBrokenThought(survivor, lost);
        }

        private static void SyncToPartner(Pawn qinghe, Pawn spouse, TraitDef selectedFlowerWord)
        {
            if (qinghe?.health?.hediffSet == null || spouse?.health?.hediffSet == null || spouse.story?.traits == null)
            {
                return;
            }

            Hediff_LuoshenContract qingheContract = EnsureContractHediff(qinghe, spouse);
            Hediff_LuoshenContract spouseContract = EnsureContractHediff(spouse, qinghe);
            RemoveMirroredTrait(spouse, spouseContract);

            if (QingheFlowerChoiceUtility.IsFlowerWordTraitDef(selectedFlowerWord))
            {
                bool addedByContract = EnsureTrait(spouse, selectedFlowerWord);
                spouseContract.SetMirroredFlowerWord(selectedFlowerWord.defName, addedByContract);
            }
            else
            {
                spouseContract.SetMirroredFlowerWord(null, false);
            }

            qingheContract.SetMirroredFlowerWord(null, false);
            NotifyThoughtsDirty(qinghe);
            NotifyThoughtsDirty(spouse);
        }

        private static Hediff_LuoshenContract EnsureContractHediff(Pawn pawn, Pawn targetPawn)
        {
            HediffDef hediffDef = ContractHediffDef;
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return null;
            }

            Hediff_LuoshenContract hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) as Hediff_LuoshenContract;
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(hediffDef, pawn) as Hediff_LuoshenContract;
                pawn.health.AddHediff(hediff);
            }

            hediff?.SetTargetPawn(targetPawn);
            return hediff;
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
            Hediff_LuoshenContract hediff = GetContractHediff(pawn);
            if (hediff == null)
            {
                return;
            }

            if (targetPawn != null && hediff.target != targetPawn)
            {
                return;
            }

            RemoveMirroredTrait(pawn, hediff);
            pawn.health.RemoveHediff(hediff);
        }

        private static void RemoveMirroredTrait(Pawn pawn, Hediff_LuoshenContract hediff)
        {
            string defName = hediff?.MirroredFlowerWordDefName;
            TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (!hediff.MirroredFlowerWordAddedByContract || !QingheFlowerChoiceUtility.IsFlowerWordTraitDef(traitDef) || pawn?.story?.traits == null)
            {
                return;
            }

            Trait trait = traitDef != null ? pawn.story.traits.GetTrait(traitDef) : null;
            if (trait != null)
            {
                pawn.story.traits.RemoveTrait(trait);
            }

            hediff.SetMirroredFlowerWord(null, false);
        }

        private static bool EnsureTrait(Pawn pawn, TraitDef traitDef)
        {
            if (traitDef != null && pawn.story?.traits?.HasTrait(traitDef) == false)
            {
                pawn.story.traits.GainTrait(new Trait(traitDef));
                return true;
            }

            return false;
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
            return MX_QHUtility.IsQinghe(partner) ? partner : null;
        }

        private static bool HasContractWith(Pawn pawn, Pawn targetPawn)
        {
            return GetContractHediff(pawn)?.target == targetPawn;
        }

        private static Hediff_LuoshenContract GetContractHediff(Pawn pawn)
        {
            HediffDef hediffDef = ContractHediffDef;
            return hediffDef != null ? pawn?.health?.hediffSet?.GetFirstHediffOfDef(hediffDef) as Hediff_LuoshenContract : null;
        }

        private static void GiveBrokenThought(Pawn pawn, Pawn otherPawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return;
            }

            ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(BrokenThoughtDefName);
            if (thoughtDef != null)
            {
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thoughtDef, otherPawn);
            }
        }

        private static void NotifyThoughtsDirty(Pawn pawn)
        {
            if (pawn != null && !pawn.Dead)
            {
                pawn.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty();
            }
        }

        private static HediffDef ContractHediffDef => DefDatabase<HediffDef>.GetNamedSilentFail(ContractHediffDefName);
    }
}
