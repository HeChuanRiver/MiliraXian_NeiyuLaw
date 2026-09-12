using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class QuestNode_Root_QingheFlowerCourtGuide : QuestNode
    {
        public bool masteryGuide;

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            quest.AddPart(new QuestPart_QingheFlowerCourtGuide
            {
                inSignalEnable = quest.InitiateSignal,
                mapParent = QuestGen.slate.Get<Map>("map").Parent,
                qinghe = QuestGen.slate.Get<Pawn>("qinghe"),
                masteryGuide = masteryGuide
            });
        }

        protected override bool TestRunInt(Slate slate)
        {
            return slate.Get<Map>("map") != null && slate.Get<Pawn>("qinghe") != null;
        }
    }

    public class QuestPart_QingheFlowerCourtGuide : QuestPartActivable
    {
        public MapParent mapParent;
        public Pawn qinghe;
        public bool masteryGuide;
        private int nextCheckTick = -1;

        public override string DescriptionPart
        {
            get
            {
                if (quest.State != QuestState.Ongoing)
                {
                    return null;
                }

                if (masteryGuide)
                {
                    if (qinghe == null || qinghe.Dead || qinghe.Destroyed)
                    {
                        return "MX_QH_FlowerCourtQingheUnavailable".Translate();
                    }

                    return "MX_QH_FlowerCourtMasteryProgress".Translate(
                        MX_QH_HediffUtility.GetAuraMasteryComp(qinghe).CurrentLevel);
                }

                Room room = MX_QHQuestUtility.FindLotusPond(mapParent?.Map)?.GetRoom();
                return room == null
                    ? "MX_QH_FlowerCourtPondUnavailable".Translate()
                    : "MX_QH_FlowerCourtImpressivenessProgress".Translate(
                        room.GetStat(RoomStatDefOf.Impressiveness).ToString("0.##"),
                        MX_QHQuestUtility.RequiredImpressiveness.ToString("0"));
            }
        }

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                if (masteryGuide)
                {
                    if (qinghe != null && !qinghe.Destroyed)
                    {
                        yield return qinghe;
                    }
                }
                else
                {
                    Building pond = MX_QHQuestUtility.FindLotusPond(mapParent?.Map);
                    if (pond != null)
                    {
                        yield return pond;
                    }
                    else if (mapParent != null)
                    {
                        yield return mapParent;
                    }
                }
            }
        }

        public override void QuestPartTick()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick < nextCheckTick)
            {
                return;
            }

            nextCheckTick = currentTick + 250;
            if (masteryGuide)
            {
                if (qinghe == null || qinghe.Dead || qinghe.Destroyed)
                {
                    quest.End(QuestEndOutcome.Fail);
                }
                else if (MX_QH_HediffUtility.GetAuraMasteryComp(qinghe).CurrentLevel >= 1)
                {
                    quest.End(QuestEndOutcome.Success);
                }
            }
            else
            {
                Map map = mapParent?.Map;
                if (map == null)
                {
                    quest.End(QuestEndOutcome.Fail);
                    return;
                }

                Room room = MX_QHQuestUtility.FindLotusPond(map)?.GetRoom();
                if (room != null && room.GetStat(RoomStatDefOf.Impressiveness) >= MX_QHQuestUtility.RequiredImpressiveness)
                {
                    quest.End(QuestEndOutcome.Success);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref mapParent, "mapParent");
            Scribe_References.Look(ref qinghe, "qinghe");
            Scribe_Values.Look(ref masteryGuide, "masteryGuide");
            Scribe_Values.Look(ref nextCheckTick, "nextCheckTick", -1);
        }
    }
}
