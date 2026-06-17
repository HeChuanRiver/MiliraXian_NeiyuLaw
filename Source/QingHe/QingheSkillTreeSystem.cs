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
        public const string NodeFlowerMandate = "MX_QH_Node_FlowerMandate";
        public const string NodeFlowerWord = "MX_QH_Node_FlowerWord";
        public const string NodeFlowerSigil = "MX_QH_Node_FlowerSigil";
        public const string NodeFlowerDance = "MX_QH_Node_FlowerDance";

        public static void SyncChoices(Pawn pawn)
        {
            HediffComp_QingheSkillTreeState state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            if (state == null)
            {
                return;
            }

            QingheFlowerChoiceUtility.SyncFlowerMandate(pawn, state.HasNode(NodeFlowerMandate) ? state.SelectedFlowerMandateDefName : null);
            QingheFlowerChoiceUtility.SyncFlowerSigil(pawn, state.HasNode(NodeFlowerSigil) ? state.SelectedFlowerSigilDefName : null);
            QingheFlowerChoiceUtility.SyncFlowerWord(pawn, state.HasNode(NodeFlowerWord) ? state.SelectedFlowerWordDefName : null);
            QingheFlowerChoiceUtility.SyncFlowerDivinationSlash(pawn, state.HasNode(NodeFlowerDance));
        }

        public static bool TrySetFlowerMandate(HediffComp_QingheSkillTreeState state, string abilityDefName, out string reason)
        {
            if (!QingheFlowerChoiceUtility.FlowerMandates.Contains(abilityDefName))
            {
                reason = "未知的飞花令。";
                return false;
            }

            state.SetFlowerMandate(abilityDefName);
            SyncChoices(state.Pawn);
            reason = null;
            return true;
        }

        public static bool TrySetFlowerSigil(HediffComp_QingheSkillTreeState state, string hediffDefName, out string reason)
        {
            if (!state.HasNode(NodeFlowerSigil))
            {
                reason = "尚未习得花神签节点。";
                return false;
            }

            if (!QingheFlowerChoiceUtility.FlowerSigils.Contains(hediffDefName))
            {
                reason = "未知的花神签。";
                return false;
            }

            state.SetFlowerSigil(hediffDefName);
            SyncChoices(state.Pawn);
            reason = null;
            return true;
        }

        public static bool TrySetFlowerWord(HediffComp_QingheSkillTreeState state, string traitDefName, out string reason)
        {
            if (!state.HasNode(NodeFlowerWord))
            {
                reason = "尚未习得花语节点。";
                return false;
            }

            if (!QingheFlowerChoiceUtility.FlowerWords.Contains(traitDefName))
            {
                reason = "未知的花语。";
                return false;
            }

            state.SetFlowerWord(traitDefName);
            SyncChoices(state.Pawn);
            reason = null;
            return true;
        }

        public static IEnumerable<Gizmo> GetGizmos(Pawn pawn, HediffComp_QingheSkillTreeState state)
        {
            if (pawn == null || pawn.Dead || state == null || Find.Selector.SingleSelectedThing != pawn)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "花神庭",
                defaultDesc = "打开清荷的技能树界面。",
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_QingheSkillTree(pawn, state));
                }
            };

            if (state.HasNode(NodeFlowerMandate))
            {
                yield return FlowerResourceGizmoFactory.BuildResourceStatusGizmo(pawn);
            }

            if (state.HasNode(NodeFlowerDance))
            {
                yield return FlowerResourceGizmoFactory.BuildFlowerDivinationGizmo(pawn);
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
