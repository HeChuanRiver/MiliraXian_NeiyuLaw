using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.UI;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class MX_QHSkillUtility
    {
        public static void SyncChoices(Pawn pawn)
        {
            HediffComp_SkillTreeState state = MX_QH_HediffUtility.EnsureFlowerResonance(pawn);
            SyncChoices(pawn, state);
        }

        public static void SyncChoices(Pawn pawn, HediffComp_SkillTreeState state)
        {
            if (pawn?.abilities == null || state == null)
            {
                return;
            }

            SyncAbility(pawn, MX_QHDefOf.MX_QH_SpringFlowAbility, true);
            SyncAbility(pawn, MX_QHDefOf.MX_QH_SpiritBurstAbility, state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Chuanhun));
            SyncAbility(pawn, MX_QHDefOf.MX_QH_LunarMirrorAbility, state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Shuangyuejing));
            SyncAbility(pawn, MX_QHDefOf.MX_QH_FlowerDanceAbility, state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_NishangDance));
            SyncAbility(pawn, MX_QHDefOf.MX_QH_AscentSlashAbility, state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Jueying));
            SyncAbility(pawn, MX_QHDefOf.MX_QH_LuoshenRibbonAbility, state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Luoshenfu));
            MX_QH_HediffUtility.SyncDivineGrace(pawn, state);
            MX_QH_HediffUtility.GetDivineFortune(pawn)?.Recalculate();
            HediffComp_LuoshenContract.SyncForQinghe(pawn, state);
        }

        public static bool HasAllFlowerMandates(HediffComp_SkillTreeState state)
        {
            return state?.IsCollectionCompleted(MX_QHSkillNodeDefOf.MX_QH_Tree_FlowerMandate) == true;
        }

        public static IEnumerable<Gizmo> GetGizmos(Pawn pawn, HediffComp_SkillTreeState state)
        {
            if (pawn == null || pawn.Dead || state == null || Find.Selector.SingleSelectedThing != pawn)
            {
                yield break;
            }

            yield return new Gizmo_QH_FlowerDecree(pawn);

            if (!Prefs.DevMode)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "MX_QH_DevLearnAllNodesLabel".Translate(),
                defaultDesc = "MX_QH_DevLearnAllNodesDesc".Translate(),
                action = delegate
                {
                    foreach (SkillNodeCollectionDef collectionDef in DefDatabase<SkillNodeCollectionDef>.AllDefsListForReading)
                    {
                        state.LearnAllNodesInCollection(collectionDef);
                    }
                    Messages.Message("MX_QH_DevLearnAllNodesMessage".Translate(), pawn, MessageTypeDefOf.NeutralEvent, historical: false);
                }
            };


        }

        private static void SyncAbility(Pawn pawn, AbilityDef abilityDef, bool shouldHave)
        {
            if (abilityDef == null)
            {
                return;
            }

            if (shouldHave)
            {
                if (pawn.abilities.GetAbility(abilityDef, includeTemporary: false) == null)
                {
                    pawn.abilities.GainAbility(abilityDef);
                }
                return;
            }

            pawn.abilities.RemoveAbility(abilityDef);
        }
    }
}

