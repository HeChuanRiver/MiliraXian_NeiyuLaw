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
        public const string NodeShang = "MX_QH_Node_Shang";
        public const string NodeQingjue = "MX_QH_Node_Qingjue";
        public const string NodeGaoshan = "MX_QH_Node_Gaoshan";
        public const string NodeZhi = "MX_QH_Node_Zhi";
        public const string NodeYu = "MX_QH_Node_Yu";
        public const string NodeLuoyu = "MX_QH_Node_Luoyu";
        public const string NodeSishiLiuzhuan = "MX_QH_Node_SishiLiuzhuan";
        public const string NodeLuoshenfu = "MX_QH_Node_Luoshenfu";
        public const string NodeYingyue = "MX_QH_Node_Yingyue";

        public static void SyncChoices(Pawn pawn)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            SyncChoices(pawn, state);
        }

        public static void SyncChoices(Pawn pawn, HediffComp_FlowerResonance state)
        {
            if (state == null)
            {
                return;
            }

            QingheFlowerChoiceUtility.SyncFlowerMandates(
                pawn,
                state.HasNode(NodeFlowerMandate) ? state.SelectedFlowerMandateDefName : null,
                state.HasNode(NodeSishiLiuzhuan) ? state.SelectedTimedFlowerMandateDefName : null);
            QingheFlowerChoiceUtility.SyncFlowerSigil(pawn, state.HasNode(NodeFlowerSigil) ? state.SelectedFlowerSigilDefName : null);
            QingheFlowerChoiceUtility.SyncFlowerWord(pawn, state.HasNode(NodeFlowerWord) ? state.SelectedFlowerWordDefName : null);
            QingheLuoshenContractUtility.SyncForQinghe(pawn, state);
            QingheFlowerChoiceUtility.SyncFlowerDivinationSlash(pawn, state.HasNode(NodeFlowerDance));
        }

        public static bool TrySetFlowerMandate(HediffComp_FlowerResonance state, string abilityDefName, out string reason)
        {
            if (!QingheFlowerChoiceUtility.FlowerMandates.Contains(abilityDefName))
            {
                reason = "未知的飞花令。";
                return false;
            }

            state.SetFlowerMandate(abilityDefName);
            reason = null;
            return true;
        }

        public static bool TrySetFlowerSigil(HediffComp_FlowerResonance state, string hediffDefName, out string reason)
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
            reason = null;
            return true;
        }

        public static bool TrySetFlowerWord(HediffComp_FlowerResonance state, string traitDefName, out string reason)
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
            reason = null;
            return true;
        }

        public static IEnumerable<Gizmo> GetGizmos(Pawn pawn, HediffComp_FlowerResonance state)
        {
            if (pawn == null || pawn.Dead || state == null || Find.Selector.SingleSelectedThing != pawn)
            {
                yield break;
            }

            if (state.HasNode(NodeFlowerMandate))
            {
                yield return new Gizmo_QH_FlowerDecree(pawn);
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
