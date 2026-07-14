using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.UI;
using RimWorld;
using UnityEngine;
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
            if (pawn == null || state == null)
            {
                return;
            }

            state.SyncGrantedDefs();
            HediffComp_LuoshenContract.SyncForQinghe(pawn, state);
        }

        public static bool HasAllFlowerMandates(HediffComp_SkillTreeState state)
        {
            return state?.IsCollectionCompleted(MX_QHSkillNodeDefOf.MX_QH_Tree_FlowerMandate) == true;
        }

        public static float GetSpecialAbilityEffectFactor(Pawn pawn)
        {
            if (pawn == null || MX_QHDefOf.MX_QH_SpecialAbilityEffectFactor == null)
            {
                return 1f;
            }

            return Mathf.Max(0f, pawn.GetStatValue(MX_QHDefOf.MX_QH_SpecialAbilityEffectFactor));
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

            yield return new Command_Action
            {
                defaultLabel = "MX_QH_DevAddDivineGraceLevelLabel".Translate(),
                defaultDesc = "MX_QH_DevAddDivineGraceLevelDesc".Translate(),
                action = delegate
                {
                    AddDivineGraceLevel(pawn, state);
                }
            };

        }

        private static void AddDivineGraceLevel(Pawn pawn, HediffComp_SkillTreeState state)
        {
            SkillNodeDef node = MX_QHSkillNodeDefOf.MX_QH_Node_DivineGrace;
            if (pawn == null || state == null || node == null)
            {
                return;
            }

            if (!state.TryLearn(node, out string reason))
            {
                if (!reason.NullOrEmpty())
                {
                    Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return;
            }

            Messages.Message("MX_QH_DevAddDivineGraceLevelMessage".Translate(state.GetNodeLevel(node).ToString("0")), pawn, MessageTypeDefOf.NeutralEvent, historical: false);
        }

    }
}
