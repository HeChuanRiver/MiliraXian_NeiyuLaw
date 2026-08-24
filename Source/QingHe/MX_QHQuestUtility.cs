using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    internal static class MX_QHQuestUtility
    {
        public const int NeiyuTriggerTicks = 3 * GenDate.TicksPerDay;
        public const int GuaranteedTriggerTicks = 14 * GenDate.TicksPerDay;
        public const float RequiredImpressiveness = 40f;

        public static bool QuestBlocksNewOffer(Quest quest)
        {
            if (quest == null)
            {
                return false;
            }

            QuestState state = quest.State;
            return state == QuestState.NotYetAccepted || state == QuestState.Ongoing;
        }

        public static bool QuestExists()
        {
            if (Find.QuestManager == null)
            {
                return false;
            }

            List<Quest> quests = Find.QuestManager.QuestsListForReading;
            for (int index = 0; index < quests.Count; index++)
            {
                Quest quest = quests[index];
                if (quest?.root == MX_QHDefOf.MX_QH_FlowerCourtQuestScript && QuestBlocksNewOffer(quest))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool PlayerHasQinghe()
        {
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (pawn != null && !pawn.Dead && pawn.Faction == Faction.OfPlayer && MX_QHCharacterUtility.IsQinghe(pawn))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool PlayerHasNeiyu()
        {
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (pawn != null && !pawn.Dead && pawn.Faction == Faction.OfPlayer && pawn.kindDef == MX_QHDefOf.MiliraXian_Neiyu)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsQingheStartScenario()
        {
            ScenarioDef scenarioDef = MX_QHDefOf.MXNL_QingheFlowerCourtStart;
            return scenarioDef?.scenario != null && Find.Scenario == scenarioDef.scenario;
        }

        public static bool IsLotusPondBuildSignal(Signal signal)
        {
            Frame frame = signal.args.GetArg<Frame>("SUBJECT");
            return frame?.BuildDef == MX_QHDefOf.MX_QH_LotusPond;
        }

        public static Building FindLotusPond(Map map)
        {
            if (map == null || MX_QHDefOf.MX_QH_LotusPond == null)
            {
                return null;
            }

            foreach (Building building in map.listerBuildings.AllBuildingsColonistOfDef(MX_QHDefOf.MX_QH_LotusPond))
            {
                if (building != null && !building.Destroyed)
                {
                    return building;
                }
            }

            return null;
        }

        public static bool LotusPavilionReady(Building lotusPond)
        {
            Room room = lotusPond?.GetRoom();
            return room != null && room.GetStat(RoomStatDefOf.Impressiveness) >= RequiredImpressiveness;
        }

        public static Pawn GenerateQinghePawn()
        {
            PawnKindDef qingheKind = MX_QHDefOf.MiliraXian_Qinghe;
            if (qingheKind == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] Missing Qinghe PawnKindDef.");
                return null;
            }

            PawnGenerationRequest request = new(
                qingheKind,
                ResolveAncientsFaction(),
                PawnGenerationContext.NonPlayer,
                -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: true,
                mustBeCapableOfViolence: true,
                colonistRelationChanceFactor: 20f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowPregnant: true,
                allowFood: true,
                allowAddictions: true,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false);

            Pawn pawn = PawnGenerator.GeneratePawn(request);
            MX_QHCharacterUtility.EnsureDefaultLoadout(pawn);
            MX_QHCharacterUtility.MarkForLoadoutStabilization(pawn);
            return pawn;
        }

        public static bool TrySpawnQinghe(Map map, out Pawn pawn)
        {
            pawn = GenerateQinghePawn();
            if (pawn == null || map == null)
            {
                return false;
            }

            if (pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }

            IntVec3 cell;
            if (!CellFinder.TryFindRandomEdgeCellWith(c => map.reachability.CanReachColony(c) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out cell))
            {
                cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 10);
            }

            GenSpawn.Spawn(pawn, cell, map);
            return true;
        }

        private static Faction ResolveAncientsFaction()
        {
            FactionManager factionManager = Find.FactionManager;
            if (factionManager == null)
            {
                return null;
            }

            return factionManager.OfAncients ?? factionManager.FirstFactionOfDef(FactionDefOf.Ancients);
        }
    }

    public class QuestNode_Root_QingheFlowerCourt_AvailableQuest : QuestNode
    {
        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            if (!slate.TryGet("map", out Map map))
            {
                map = QuestGen_Get.GetMap(mustBeInfestable: false, null, canBeSpace: false);
            }

            if (map == null)
            {
                return;
            }

            slate.Set("playerFaction", Faction.OfPlayer);
            quest.AcceptanceRequirementNotSpace(map.Parent);

            bool qingheStart = slate.Get("qingheStart", defaultValue: false);
            string builtBuildingSignal = QuestGenUtility.HardcodedSignalWithQuestID("playerFaction.BuiltBuilding");
            quest.AddPart(new QuestPart_QingheFlowerCourt
            {
                inSignalEnable = quest.InitiateSignal,
                builtBuildingSignal = builtBuildingSignal,
                mapParent = map.Parent,
                qingheStart = qingheStart
            });
        }

        protected override bool TestRunInt(Slate slate)
        {
            if (!slate.TryGet("map", out Map _))
            {
                return QuestGen_Get.GetMap(mustBeInfestable: false, null, canBeSpace: false) != null;
            }

            return Find.AnyPlayerHomeMap != null;
        }
    }

    public class QuestPart_QingheFlowerCourt : QuestPartActivable
    {
        private const int CheckInterval = 250;

        public string builtBuildingSignal;
        public MapParent mapParent;
        public bool qingheStart;
        private bool lotusPondBuilt;
        private bool qingheArrived;
        private bool secondStageMessageSent;
        private int nextCheckTick = -1;

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                foreach (GlobalTargetInfo target in base.QuestLookTargets)
                {
                    yield return target;
                }

                Map map = mapParent?.Map ?? Find.AnyPlayerHomeMap;
                Building lotusPond = MX_QHQuestUtility.FindLotusPond(map);
                if (lotusPond != null)
                {
                    yield return lotusPond;
                }
                else if (mapParent != null)
                {
                    yield return mapParent;
                }
            }
        }

        public override void PreQuestAccept()
        {
            base.PreQuestAccept();
            Current.Game?.GetComponent<GameComponent_QingheFlowerCourtQuest>()?.UnlockLotusPondDesign();
        }

        protected override void ProcessQuestSignal(Signal signal)
        {
            base.ProcessQuestSignal(signal);
            if (signal.tag == builtBuildingSignal && MX_QHQuestUtility.IsLotusPondBuildSignal(signal))
            {
                lotusPondBuilt = true;
                nextCheckTick = -1;
                Messages.Message("MX_QH_FlowerCourtLotusPondBuiltMessage".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
            }
        }

        public override void QuestPartTick()
        {
            base.QuestPartTick();
            if (!lotusPondBuilt || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (nextCheckTick >= 0 && currentTick < nextCheckTick)
            {
                return;
            }

            nextCheckTick = currentTick + CheckInterval;
            Map map = mapParent?.Map ?? Find.AnyPlayerHomeMap;
            Building lotusPond = MX_QHQuestUtility.FindLotusPond(map);
            if (lotusPond == null)
            {
                return;
            }

            if (!qingheStart && !qingheArrived)
            {
                if (!MX_QHQuestUtility.TrySpawnQinghe(map, out Pawn pawn))
                {
                    return;
                }

                qingheArrived = true;
                Find.LetterStack.ReceiveLetter(
                    "MX_QH_FlowerCourtQingheArrivedLetterLabel".Translate(),
                    "MX_QH_FlowerCourtQingheArrivedLetterText".Translate(),
                    LetterDefOf.PositiveEvent,
                    pawn);
                SendSecondStageMessage(lotusPond);
                return;
            }

            if (!MX_QHQuestUtility.LotusPavilionReady(lotusPond))
            {
                SendSecondStageMessage(lotusPond);
                return;
            }

            CompleteFlowerCourt(lotusPond);
        }

        private void SendSecondStageMessage(Building lotusPond)
        {
            if (secondStageMessageSent)
            {
                return;
            }

            secondStageMessageSent = true;
            Find.LetterStack.ReceiveLetter(
                "MX_QH_FlowerCourtSecondStageLetterLabel".Translate(),
                "MX_QH_FlowerCourtSecondStageLetterText".Translate(),
                LetterDefOf.NeutralEvent,
                lotusPond);
        }

        private void CompleteFlowerCourt(Building lotusPond)
        {
            Thing reward = MakeLostScoreFragmentReward();
            if (reward != null)
            {
                GenPlace.TryPlaceThing(reward, lotusPond.Position, lotusPond.Map, ThingPlaceMode.Near);
            }

            Find.LetterStack.ReceiveLetter(
                "MX_QH_FlowerCourtCompletedLetterLabel".Translate(),
                "MX_QH_FlowerCourtCompletedLetterText".Translate(),
                LetterDefOf.PositiveEvent,
                reward ?? lotusPond);
            Current.Game?.GetComponent<GameComponent_QingheFlowerCourtQuest>()?.MarkCompleted();
            quest.End(QuestEndOutcome.Success, sendLetter: false, playSound: true);
        }

        private static Thing MakeLostScoreFragmentReward()
        {
            if (MX_QHDefOf.MX_QH_LostMusicScoreFragment == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] Missing Qinghe lost score fragment ThingDef.");
                return null;
            }

            Thing reward = ThingMaker.MakeThing(MX_QHDefOf.MX_QH_LostMusicScoreFragment);
            reward.stackCount = 1;
            return reward;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref builtBuildingSignal, "builtBuildingSignal");
            Scribe_References.Look(ref mapParent, "mapParent");
            Scribe_Values.Look(ref qingheStart, "qingheStart", defaultValue: false);
            Scribe_Values.Look(ref lotusPondBuilt, "lotusPondBuilt", defaultValue: false);
            Scribe_Values.Look(ref qingheArrived, "qingheArrived", defaultValue: false);
            Scribe_Values.Look(ref secondStageMessageSent, "secondStageMessageSent", defaultValue: false);
            Scribe_Values.Look(ref nextCheckTick, "nextCheckTick", -1);
        }
    }

    public class IncidentWorker_QingheFlowerCourtQuest : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms) || parms.target is not Map map || !map.IsPlayerHome)
            {
                return false;
            }

            GameComponent_QingheFlowerCourtQuest component = Current.Game?.GetComponent<GameComponent_QingheFlowerCourtQuest>();
            if (component == null || !component.CanOfferStandardQuest(map))
            {
                return false;
            }

            QuestScriptDef questDef = def.questScriptDef ?? parms.questScriptDef;
            return questDef == null || questDef.CanRun(parms.points, parms.target);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (parms.target is not Map map)
            {
                return false;
            }

            QuestScriptDef questDef = def.questScriptDef ?? parms.questScriptDef;
            if (questDef == null)
            {
                return false;
            }

            Slate slate = new();
            slate.Set("points", parms.points);
            slate.Set("map", map);
            slate.Set("qingheStart", false);

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
            if (quest == null)
            {
                return false;
            }

            if (!quest.hidden && questDef.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }

            Current.Game?.GetComponent<GameComponent_QingheFlowerCourtQuest>()?.MarkOffered(map);
            return true;
        }
    }

    public class GameComponent_QingheFlowerCourtQuest : GameComponent
    {
        private const int CheckInterval = 250;

        private bool lotusPondDesignUnlocked;
        private bool questOffered;
        private bool questCompleted;
        private bool qingheStartQuestAccepted;
        private int nextCheckTick = -1;
        private Map questMap;

        public bool LotusPondDesignUnlocked => lotusPondDesignUnlocked;

        public GameComponent_QingheFlowerCourtQuest(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (nextCheckTick >= 0 && currentTick < nextCheckTick)
            {
                return;
            }

            nextCheckTick = currentTick + CheckInterval;
            Map map = ResolveBestHomeMap(questMap);
            if (map == null)
            {
                return;
            }

            if (!qingheStartQuestAccepted && MX_QHQuestUtility.IsQingheStartScenario())
            {
                TryStartQingheScenarioQuest(map);
                return;
            }

            if (!questCompleted && !questOffered)
            {
                TryOfferStandardQuest(map);
            }
        }

        public bool CanOfferStandardQuest(Map map)
        {
            if (questCompleted || questOffered || MX_QHQuestUtility.IsQingheStartScenario())
            {
                return false;
            }

            if (map == null || !map.IsPlayerHome || map.mapPawns.FreeColonistsCount <= 0)
            {
                return false;
            }

            if (MX_QHQuestUtility.PlayerHasQinghe() || MX_QHQuestUtility.QuestExists())
            {
                return false;
            }

            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            return ticksGame >= MX_QHQuestUtility.GuaranteedTriggerTicks
                || (ticksGame >= MX_QHQuestUtility.NeiyuTriggerTicks && MX_QHQuestUtility.PlayerHasNeiyu());
        }

        public void UnlockLotusPondDesign()
        {
            if (lotusPondDesignUnlocked)
            {
                return;
            }

            lotusPondDesignUnlocked = true;
            Messages.Message("MX_QH_FlowerCourtDesignUnlockedMessage".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
        }

        public void MarkOffered(Map map)
        {
            questOffered = true;
            questMap = ResolveBestHomeMap(map);
        }

        public void MarkCompleted()
        {
            questCompleted = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lotusPondDesignUnlocked, "lotusPondDesignUnlocked", defaultValue: false);
            Scribe_Values.Look(ref questOffered, "questOffered", defaultValue: false);
            Scribe_Values.Look(ref questCompleted, "questCompleted", defaultValue: false);
            Scribe_Values.Look(ref qingheStartQuestAccepted, "qingheStartQuestAccepted", defaultValue: false);
            Scribe_Values.Look(ref nextCheckTick, "nextCheckTick", -1);
            Scribe_References.Look(ref questMap, "questMap");
        }

        private void TryOfferStandardQuest(Map map)
        {
            if (!CanOfferStandardQuest(map))
            {
                return;
            }

            IncidentDef incident = MX_QHDefOf.MX_QH_FlowerCourtQuest;
            if (incident == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] Missing Qinghe Flower Court IncidentDef.");
                questOffered = true;
                return;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.target = map;
            parms.forced = true;
            parms.bypassStorytellerSettings = true;
            incident.Worker.TryExecute(parms);
        }

        private void TryStartQingheScenarioQuest(Map map)
        {
            if (qingheStartQuestAccepted || MX_QHQuestUtility.QuestExists())
            {
                return;
            }

            QuestScriptDef questDef = MX_QHDefOf.MX_QH_FlowerCourtQuestScript;
            if (questDef == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] Missing Qinghe Flower Court QuestScriptDef.");
                qingheStartQuestAccepted = true;
                return;
            }

            Slate slate = new();
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));
            slate.Set("map", map);
            slate.Set("qingheStart", true);

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
            if (quest == null)
            {
                return;
            }

            quest.Accept(null);
            qingheStartQuestAccepted = true;
            questOffered = true;
            questMap = map;
            Find.LetterStack.ReceiveLetter(
                "MX_QH_FlowerCourtAutoAcceptedLetterLabel".Translate(),
                "MX_QH_FlowerCourtAutoAcceptedLetterText".Translate(),
                LetterDefOf.NeutralEvent,
                map.Parent);
        }

        private static Map ResolveBestHomeMap(Map preferred)
        {
            if (preferred != null && !preferred.Disposed && preferred.IsPlayerHome)
            {
                return preferred;
            }

            return Find.AnyPlayerHomeMap;
        }
    }
}
