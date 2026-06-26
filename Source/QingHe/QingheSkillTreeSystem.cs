using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.UI;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class QingheSkillTreeSystem
    {
        public static void SyncChoices(Pawn pawn)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            HediffComp_FlowerChoices choices = FlowerCourtUtility.EnsureFlowerChoices(pawn);
            SyncChoices(pawn, state, choices);
        }

        public static void SyncChoices(Pawn pawn, HediffComp_FlowerResonance state, HediffComp_FlowerChoices choices)
        {
            if (state == null || choices == null)
            {
                return;
            }

            QingheFlowerChoiceUtility.SyncFlowerMandates(
                pawn,
                state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerMandate) ? choices.SelectedFlowerMandate : null,
                state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_SishiLiuzhuan) ? choices.SelectedTimedFlowerMandate : null);
            QingheFlowerChoiceUtility.SyncFlowerSigil(pawn, state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerSigil) ? choices.SelectedFlowerSigil : null);
            QingheFlowerChoiceUtility.SyncFlowerWord(pawn, state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerWord) ? choices.SelectedFlowerWord : null);
            QingheLuoshenContractUtility.SyncForQinghe(pawn, state, choices);
            QingheFlowerChoiceUtility.SyncFlowerDivinationSlash(pawn, state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerDance));
        }

        public static bool TrySetFlowerMandate(HediffComp_FlowerChoices choices, AbilityDef abilityDef, out string reason)
        {
            if (choices == null)
            {
                reason = "清荷尚未建立花神庭。";
                return false;
            }

            return choices.TrySetFlowerMandate(abilityDef, out reason);
        }

        public static bool TrySetFlowerSigil(HediffComp_FlowerResonance state, HediffComp_FlowerChoices choices, HediffDef hediffDef, out string reason)
        {
            if (choices == null)
            {
                reason = "清荷尚未建立花神庭。";
                return false;
            }

            if (!state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerSigil))
            {
                reason = "尚未习得花神签节点。";
                return false;
            }

            return choices.TrySetFlowerSigil(hediffDef, out reason);
        }

        public static bool TrySetFlowerWord(HediffComp_FlowerResonance state, HediffComp_FlowerChoices choices, TraitDef traitDef, out string reason)
        {
            if (choices == null)
            {
                reason = "清荷尚未建立花神庭。";
                return false;
            }

            if (!state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerWord))
            {
                reason = "尚未习得花语节点。";
                return false;
            }

            return choices.TrySetFlowerWord(traitDef, out reason);
        }

        public static IEnumerable<Gizmo> GetGizmos(Pawn pawn, HediffComp_FlowerResonance state)
        {
            if (pawn == null || pawn.Dead || state == null || Find.Selector.SingleSelectedThing != pawn)
            {
                yield break;
            }

            if (state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerMandate))
            {
                yield return new Gizmo_QH_FlowerDecree(pawn);
            }

            if (state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerDance))
            {
                yield return new Gizmo_QH_FlowerDivination(pawn);
            }

            if (!Prefs.DevMode)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "DEV: 技能点 +1",
                defaultDesc = "为清荷技能树增加 1 点技能点。",
                action = delegate
                {
                    state.AddSkillPoints(1);
                    Messages.Message("清荷获得 1 点技能点。", pawn, MessageTypeDefOf.NeutralEvent, historical: false);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: 经验 +100",
                defaultDesc = "为清荷技能树增加 100 点经验。",
                action = delegate
                {
                    state.AddExperience(100f);
                    Messages.Message("清荷获得 100 点技能树经验。", pawn, MessageTypeDefOf.NeutralEvent, historical: false);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "DEV: 解锁曲谱",
                defaultDesc = "解锁所有清荷技能树曲谱，用于测试界面与节点。",
                action = delegate
                {
                    foreach (QingheSkillTreeDef treeDef in DefDatabase<QingheSkillTreeDef>.AllDefsListForReading)
                    {
                        state.UnlockTree(treeDef);
                    }
                    Messages.Message("清荷已解锁所有曲谱集。", pawn, MessageTypeDefOf.NeutralEvent, historical: false);
                }
            };
        }

    }
}
