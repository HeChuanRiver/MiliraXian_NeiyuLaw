using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.UI;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class MX_QHSkillSystem
    {
        public static void SyncChoices(Pawn pawn)
        {
            HediffComp_FlowerResonance state = MX_QH_HediffUtility.EnsureFlowerResonance(pawn);
            SyncChoices(pawn, state);
        }

        public static void SyncChoices(Pawn pawn, HediffComp_FlowerResonance state)
        {
            if (pawn?.abilities == null || state == null)
            {
                return;
            }

            SyncAbility(pawn, "MX_QH_SpringFlow", true);
            SyncAbility(pawn, "MX_QH_SpiritBurst", state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Chuanhun));
            SyncAbility(pawn, "MX_QH_LunarMirror", state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Shuangyuejing));
            SyncAbility(pawn, "MX_QH_FlowerDance", state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_NishangDance));
            SyncAbility(pawn, "MX_QH_AscentSlash", state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Jueying));
            SyncAbility(pawn, "MX_QH_LuoshenRibbon", state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Luoshenfu));
            Hediff_LuoshenContract.SyncForQinghe(pawn, state);
        }

        public static bool HasAllFlowerMandates(HediffComp_FlowerResonance state)
        {
            return state != null
                && state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_SpringFlow)
                && state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Chuanhun)
                && state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Shuangyuejing);
        }

        public static IEnumerable<Gizmo> GetGizmos(Pawn pawn, HediffComp_FlowerResonance state)
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
                    foreach (QingheSkillTreeDef treeDef in DefDatabase<QingheSkillTreeDef>.AllDefsListForReading)
                    {
                        state.LearnAllNodesInTree(treeDef);
                    }
                    Messages.Message("MX_QH_DevLearnAllNodesMessage".Translate(), pawn, MessageTypeDefOf.NeutralEvent, historical: false);
                }
            };


        }

        private static void SyncAbility(Pawn pawn, string abilityDefName, bool shouldHave)
        {
            AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail(abilityDefName);
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
