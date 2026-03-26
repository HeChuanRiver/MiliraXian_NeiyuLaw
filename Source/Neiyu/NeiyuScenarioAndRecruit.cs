using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TicksAbs), MethodType.Getter)]
    internal static class Patch_TickManager_TicksAbs_StartupCompat
    {
        public static bool Prefix(TickManager __instance, ref int __result)
        {
            if (__instance == null || __instance.gameStartAbsTick != 0)
            {
                return true;
            }

            if (Current.ProgramState != ProgramState.Playing && Find.GameInitData != null && Find.GameInitData.gameToLoad.NullOrEmpty())
            {
                __result = GenTicks.ConfiguredTicksAbsAtGameStart + __instance.TicksGame;
                return false;
            }

            return true;
        }
    }

    public class QuestNode_Root_NeiyuProjectionRecruit : QuestNode_Root_WandererJoin
    {
        private const int TimeoutTicks = 60000;

        private string signalAccept;
        private string signalReject;

        public override Pawn GeneratePawn()
        {
            Pawn pawn = NeiyuRecruitUtility.GenerateRecruitPawn();
            if (pawn != null && !pawn.IsWorldPawn())
            {
                Find.WorldPawns.PassToWorld(pawn);
            }

            return pawn;
        }

        protected override void RunInt()
        {
            base.RunInt();
            Quest quest = QuestGen.quest;
            quest.Delay(TimeoutTicks, delegate
            {
                QuestGen_End.End(quest, QuestEndOutcome.Fail);
            });
        }

        protected override void AddSpawnPawnQuestParts(Quest quest, Map map, Pawn pawn)
        {
            signalAccept = QuestGenUtility.HardcodedSignalWithQuestID("Accept");
            signalReject = QuestGenUtility.HardcodedSignalWithQuestID("Reject");
            quest.Signal(signalAccept, delegate
            {
                quest.SetFaction(Gen.YieldSingle(pawn), Faction.OfPlayer);
                quest.PawnsArrive(Gen.YieldSingle(pawn), null, map.Parent);
                QuestGen_End.End(quest, QuestEndOutcome.Success);
            });
            quest.Signal(signalReject, delegate
            {
                quest.GiveDiedOrDownedThoughts(pawn, PawnDiedOrDownedThoughtsKind.DeniedJoining);
                QuestGen_End.End(quest, QuestEndOutcome.Fail);
            });
        }

        [System.Obsolete]
        public override void SendLetter(Quest quest, Pawn pawn)
        {
            SendLetter_NewTemp(quest, pawn, Find.AnyPlayerHomeMap);
        }

        public override void SendLetter_NewTemp(Quest quest, Pawn pawn, Map map)
        {
            TaggedString title = "异界羽影";
            TaggedString letterText = "殖民地附近出现了一道稳定下来的羽状投影。来者自称霓羽，她并非本体，而是一位米莉拉在此界投下的一道轻快分影。与其说她像某种高高在上的神性残片，不如说更像一阵带着笑意闯进来的风。她表示自己可以留下，也可以就此散去。选择权在你�?";
            QuestNode_Root_WandererJoin_WalkIn.AppendCharityInfoToLetter("JoinerCharityInfo".Translate(pawn), ref letterText);
            PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref letterText, ref title, pawn);
            QuestNode_Root_WandererJoin_WalkIn.ApplyBestSkillInfoToLetter(ref letterText, pawn);

            ChoiceLetter_AcceptJoiner letter = (ChoiceLetter_AcceptJoiner)LetterMaker.MakeLetter(title, letterText, LetterDefOf.AcceptJoiner);
            letter.signalAccept = signalAccept;
            letter.signalReject = signalReject;
            letter.quest = quest;
            letter.overrideMap = map;
            letter.StartTimeout(TimeoutTicks);
            Find.LetterStack.ReceiveLetter(letter);
        }
    }

    public class QuestNode_Root_NeiyuProjectionRecruit_AvailableQuest : QuestNode
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

            quest.AcceptanceRequirementNotSpace(map.Parent);
            quest.Signal(quest.InitiateSignal, delegate
            {
                Pawn pawn = NeiyuRecruitUtility.GenerateRecruitPawn();
                if (pawn == null)
                {
                    QuestGen_End.End(quest, QuestEndOutcome.Fail);
                    return;
                }

                quest.SetFaction(Gen.YieldSingle(pawn), Faction.OfPlayer);
                quest.PawnsArrive(Gen.YieldSingle(pawn), null, map.Parent, PawnsArrivalModeDefOf.EdgeWalkIn, joinPlayer: false, walkInSpot: null, null, null, null, null, isSingleReward: false, rewardDetailsHidden: false, sendStandardLetter: false);
                QuestGen_End.End(quest, QuestEndOutcome.Success);
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

    public class IncidentWorker_NeiyuProjectionRecruit : IncidentWorker
    {
        private const int MinDaysPassed = 8;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            GameComponent_NeiyuProjectionRecruit component = Current.Game?.GetComponent<GameComponent_NeiyuProjectionRecruit>();
            if (component != null)
            {
                if (component.EventAlreadyTriggered)
                {
                    return false;
                }

                if (component.IsBlockedByScenario())
                {
                    return false;
                }
            }

            if (NeiyuRecruitUtility.NeiyuRecruitQuestExists())
            {
                return false;
            }

            if (!(parms.target is Map map) || !map.IsPlayerHome)
            {
                return false;
            }

            if (Find.TickManager == null || Find.TickManager.TicksGame < MinDaysPassed * GenDate.TicksPerDay)
            {
                return false;
            }

            if (map.mapPawns.FreeColonistsCount <= 0)
            {
                return false;
            }

            QuestScriptDef questDef = def.questScriptDef ?? parms.questScriptDef;
            if (questDef != null && !questDef.CanRun(parms.points, parms.target))
            {
                return false;
            }

            return !NeiyuRecruitUtility.NeiyuExistsAnywhere();
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map))
            {
                return false;
            }

            QuestScriptDef questDef = def.questScriptDef ?? parms.questScriptDef;
            if (questDef == null)
            {
                return false;
            }

            Slate slate = new Slate();
            slate.Set("points", parms.points);
            slate.Set("map", map);

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
            if (quest == null)
            {
                return false;
            }

            if (!quest.hidden && questDef.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }

            Current.Game?.GetComponent<GameComponent_NeiyuProjectionRecruit>()?.MarkTriggered();
            return true;
        }
    }

    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.NewGeneratedStartingPawn))]
    internal static class Patch_StartingPawnUtility_NewGeneratedStartingPawn_NeiyuWeapon
    {
        public static void Postfix(Pawn __result)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(__result))
            {
                return;
            }

            NeiyuEquipmentUtility.MarkForLoadoutStabilization(__result);
            NeiyuEquipmentUtility.EnsureDefaultLoadout(__result);
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    internal static class Patch_PawnGenerator_GeneratePawn_NeiyuLoadout
    {
        public static void Postfix(ref Pawn __result)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(__result))
            {
                return;
            }

            NeiyuEquipmentUtility.EnsureDefaultLoadout(__result);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_Pawn_SpawnSetup_NeiyuLoadout
    {
        public static void Postfix(Pawn __instance)
        {
            if (!NeiyuEquipmentUtility.ShouldFinalizeLoadout(__instance))
            {
                return;
            }

            NeiyuEquipmentUtility.EnsureDefaultLoadout(__instance);
            NeiyuEquipmentUtility.CleanupDroppedEarringsOnMap(__instance);
            Current.Game?.GetComponent<GameComponent_NeiyuProjectionRecruit>()?.RegisterPendingLoadout(__instance);

            NeiyuSpecialPawnIntegration.TryRegister(__instance);
        }
    }


    public class ChoiceLetter_NeiyuProjectionRecruit : ChoiceLetter
    {
        public Pawn pawn;
        public Map targetMap;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || pawn == null || pawn.Dead)
                {
                    yield return Option_Close;
                    yield break;
                }

                DiaOption accept = new DiaOption("接纳�?");
                accept.resolveTree = true;
                Map map = targetMap ?? Find.AnyPlayerHomeMap;
                if (map == null)
                {
                    accept.Disable("没有可用的玩家基地地图�?");
                }
                accept.action = delegate
                {
                    Map spawnMap = targetMap ?? Find.AnyPlayerHomeMap;
                    if (spawnMap == null || pawn == null || pawn.Dead)
                    {
                        Find.LetterStack.RemoveLetter(this);
                        return;
                    }

                    if (pawn.Faction != Faction.OfPlayer)
                    {
                        pawn.SetFaction(Faction.OfPlayer);
                    }

                    if (pawn.IsWorldPawn())
                    {
                        Find.WorldPawns.RemovePawn(pawn);
                    }

                    IntVec3 cell;
                    if (!CellFinder.TryFindRandomEdgeCellWith(c => spawnMap.reachability.CanReachColony(c) && !c.Fogged(spawnMap), spawnMap, CellFinder.EdgeRoadChance_Neutral, out cell))
                    {
                        cell = CellFinder.RandomClosewalkCellNear(spawnMap.Center, spawnMap, 10);
                    }

                    GenSpawn.Spawn(pawn, cell, spawnMap);
                    Messages.Message("霓羽决定在此界停留，并正式加入了你的殖民地�?", pawn, MessageTypeDefOf.PositiveEvent, true);
                    Find.LetterStack.RemoveLetter(this);
                };
                yield return accept;

                DiaOption reject = new DiaOption("暂不接纳");
                reject.resolveTree = true;
                reject.action = delegate
                {
                    if (pawn != null)
                    {
                        if (pawn.IsWorldPawn())
                        {
                            Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
                        }
                        else if (pawn.Spawned)
                        {
                            pawn.Destroy();
                        }
                    }

                    Messages.Message("羽影暂时散去。若缘分未尽，她之后仍可能再次出现�?", MessageTypeDefOf.NeutralEvent, false);
                    Find.LetterStack.RemoveLetter(this);
                };
                yield return reject;

                if (lookTargets != null && lookTargets.IsValid)
                {
                    yield return Option_JumpToLocationAndPostpone;
                }

                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref targetMap, "targetMap");
        }
    }

    public class GameComponent_NeiyuProjectionRecruit : GameComponent
    {
        private const string RecruitIncidentDefName = "MXNL_NeiyuProjectionRecruit";
        private const string RecruitScenarioDefName = "MXNL_NeiyuProjectionStart";
        private const string RecruitQuestDefName = "MXNL_NeiyuProjectionRecruitQuest";
        private const int GuaranteedTick = 8 * GenDate.TicksPerDay;
        private const int TickCheckInterval = 250;
        private const int LoadoutCheckInterval = 30;
        private const int LoadoutFinalizeDurationTicks = 600;

        private bool eventTriggered;
        private bool initialTriggerCheckDone;
        private readonly List<PendingLoadoutFinalize> pendingLoadoutFinalizations = new List<PendingLoadoutFinalize>();

        public bool EventAlreadyTriggered => eventTriggered;

        private class PendingLoadoutFinalize
        {
            public Pawn pawn;
            public int expireTick;
        }

        public GameComponent_NeiyuProjectionRecruit(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (eventTriggered || Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                ProcessPendingLoadoutFinalizations();
                return;
            }

            ProcessPendingLoadoutFinalizations();

            if (NeiyuRecruitUtility.NeiyuRecruitQuestExists(RecruitQuestDefName))
            {
                eventTriggered = true;
                return;
            }

            if (IsBlockedByScenario())
            {
                eventTriggered = true;
                return;
            }

            bool shouldCheckNow = !initialTriggerCheckDone || Find.TickManager.TicksGame % TickCheckInterval == 0;
            initialTriggerCheckDone = true;
            if (!shouldCheckNow || Find.TickManager.TicksGame < GuaranteedTick)
            {
                return;
            }

            Map map = Find.AnyPlayerHomeMap;
            if (map == null || map.mapPawns.FreeColonistsCount <= 0 || NeiyuRecruitUtility.NeiyuExistsAnywhere())
            {
                return;
            }

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail(RecruitIncidentDefName);
            if (incident == null)
            {
                Log.Error("[MiliraXian.Characters.Neiyu] Missing IncidentDef: " + RecruitIncidentDefName);
                eventTriggered = true;
                return;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.target = map;
            parms.forced = true;
            parms.bypassStorytellerSettings = true;

            if (incident.Worker.TryExecute(parms))
            {
                eventTriggered = true;
            }
        }

        public void MarkTriggered()
        {
            eventTriggered = true;
        }

        public void RegisterPendingLoadout(Pawn pawn)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || Find.TickManager == null)
            {
                return;
            }

            int expireTick = Find.TickManager.TicksGame + LoadoutFinalizeDurationTicks;
            for (int index = 0; index < pendingLoadoutFinalizations.Count; index++)
            {
                if (pendingLoadoutFinalizations[index].pawn == pawn)
                {
                    pendingLoadoutFinalizations[index].expireTick = expireTick;
                    return;
                }
            }

            pendingLoadoutFinalizations.Add(new PendingLoadoutFinalize
            {
                pawn = pawn,
                expireTick = expireTick
            });
        }

        public bool IsBlockedByScenario()
        {
            ScenarioDef scenarioDef = DefDatabase<ScenarioDef>.GetNamedSilentFail(RecruitScenarioDefName);
            return scenarioDef?.scenario != null && Find.Scenario == scenarioDef.scenario;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref eventTriggered, "eventTriggered", defaultValue: false);
            Scribe_Values.Look(ref initialTriggerCheckDone, "initialTriggerCheckDone", defaultValue: false);
        }

        private void ProcessPendingLoadoutFinalizations()
        {
            if (Find.TickManager == null || Find.TickManager.TicksGame % LoadoutCheckInterval != 0 || pendingLoadoutFinalizations.Count == 0)
            {
                return;
            }

            for (int index = pendingLoadoutFinalizations.Count - 1; index >= 0; index--)
            {
                PendingLoadoutFinalize pending = pendingLoadoutFinalizations[index];
                Pawn pawn = pending.pawn;
                if (pawn == null || pawn.DestroyedOrNull() || pawn.Dead)
                {
                    NeiyuEquipmentUtility.ClearLoadoutStabilization(pawn);
                    pendingLoadoutFinalizations.RemoveAt(index);
                    continue;
                }

                if (pawn.Spawned)
                {
                    NeiyuEquipmentUtility.EnsureDefaultLoadout(pawn);
                    NeiyuEquipmentUtility.CleanupDroppedEarringsOnMap(pawn);
                    NeiyuSpecialPawnIntegration.TryRegister(pawn);
                }

                if (NeiyuEquipmentUtility.HasInitialLoadoutEquipped(pawn) || Find.TickManager.TicksGame >= pending.expireTick)
                {
                    NeiyuEquipmentUtility.ClearLoadoutStabilization(pawn);
                    pendingLoadoutFinalizations.RemoveAt(index);
                }
            }
        }

        private void EnsureAllSpawnedNeiyuLoadouts()
        {


        }
    }

    internal static class NeiyuEquipmentUtility
    {
        private const string NeiyuPawnKindDefName = "MiliraXian_Neiyu";
        private const string DefaultWeaponDefName = "MX_Neiyu_Form_Flower";
        private const string DefaultInnerClothingDefName = "MiliraXian_NeiyuInner";
        private const string DefaultClothingDefName = "MiliraXian_NeiyuNormal";
        private const string DefaultEarringDefName = "MX_Apparel_EarringsZhenzhu";
        private static readonly HashSet<int> PendingLoadoutStabilizationPawnIds = new HashSet<int>();

        public static bool IsNeiyu(Pawn pawn)
        {
            return pawn?.kindDef?.defName == NeiyuPawnKindDefName;
        }

        public static void EnsureDefaultLoadout(Pawn pawn)
        {
            EnsureDefaultWeapon(pawn);
            EnsureDefaultInnerClothing(pawn);
            EnsureDefaultClothing(pawn);
            EnsureDefaultEarrings(pawn);
        }

        public static void MarkForLoadoutStabilization(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            PendingLoadoutStabilizationPawnIds.Add(pawn.thingIDNumber);
        }

        public static bool ShouldFinalizeLoadout(Pawn pawn)
        {
            return pawn != null && PendingLoadoutStabilizationPawnIds.Contains(pawn.thingIDNumber);
        }

        public static void ClearLoadoutStabilization(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            PendingLoadoutStabilizationPawnIds.Remove(pawn.thingIDNumber);
        }

        public static void EnsureDefaultWeapon(Pawn pawn)
        {
            if (!IsNeiyu(pawn) || pawn.equipment == null || pawn.equipment.Primary != null)
            {
                return;
            }

            ThingDef weaponDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultWeaponDefName);
            if (weaponDef == null)
            {
                Log.Error("[MiliraXian.Characters.Neiyu] Missing ThingDef: " + DefaultWeaponDefName);
                return;
            }

            ThingWithComps weapon = ThingMaker.MakeThing(weaponDef) as ThingWithComps;
            if (weapon == null)
            {
                Log.Error("[MiliraXian.Characters.Neiyu] Default weapon is not ThingWithComps: " + DefaultWeaponDefName);
                return;
            }

            PawnGenerator.PostProcessGeneratedGear(weapon, pawn);
            CompEquippable compEquippable = weapon.TryGetComp<CompEquippable>();
            if (compEquippable != null)
            {
                if (pawn.kindDef.weaponStyleDef != null)
                {
                    compEquippable.parent.StyleDef = pawn.kindDef.weaponStyleDef;
                }
                else if (pawn.Ideo != null)
                {
                    compEquippable.parent.StyleDef = pawn.Ideo.GetStyleFor(weapon.def);
                }
            }

            pawn.equipment.AddEquipment(weapon);
        }

        public static void EnsureDefaultEarrings(Pawn pawn)
        {
            if (!IsNeiyu(pawn) || pawn.apparel == null)
            {
                return;
            }

            Apparel existing = FindWornApparel(pawn, DefaultEarringDefName);
            if (existing != null)
            {
                EnsureForcedApparel(pawn, existing);
                return;
            }

            ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultEarringDefName);
            if (apparelDef == null)
            {
                Log.Error("[MiliraXian.Characters.Neiyu] Missing ThingDef: " + DefaultEarringDefName);
                return;
            }

            Apparel earrings = FindDroppedEarringsOnMap(pawn, apparelDef);
            if (earrings == null)
            {
                earrings = ThingMaker.MakeThing(apparelDef) as Apparel;
            }
            if (earrings == null)
            {
                Log.Error("[MiliraXian.Characters.Neiyu] Default earrings are not Apparel: " + DefaultEarringDefName);
                return;
            }

            PawnGenerator.PostProcessGeneratedGear(earrings, pawn);
            pawn.apparel.Wear(earrings, dropReplacedApparel: true);
            EnsureForcedApparel(pawn, earrings);
            CleanupDroppedEarringsOnMap(pawn);
        }

        public static void EnsureDefaultClothing(Pawn pawn)
        {
            if (!IsNeiyu(pawn) || pawn.apparel == null)
            {
                return;
            }

            Apparel existing = FindWornApparel(pawn, DefaultClothingDefName);
            if (existing != null)
            {
                EnsureForcedApparel(pawn, existing);
                return;
            }

            ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultClothingDefName);
            if (apparelDef == null)
            {
                Log.Error("[MiliraXian.Characters.Neiyu] Missing ThingDef: " + DefaultClothingDefName);
                return;
            }

            Apparel clothing = FindDroppedApparelOnMap(pawn, apparelDef);
            if (clothing == null)
            {
                clothing = ThingMaker.MakeThing(apparelDef) as Apparel;
            }
            if (clothing == null)
            {
                Log.Error("[MiliraXian.Characters.Neiyu] Default clothing is not Apparel: " + DefaultClothingDefName);
                return;
            }

            PawnGenerator.PostProcessGeneratedGear(clothing, pawn);
            pawn.apparel.Wear(clothing, dropReplacedApparel: true);
            EnsureForcedApparel(pawn, clothing);
        }

        public static void EnsureDefaultInnerClothing(Pawn pawn)
        {
            if (!IsNeiyu(pawn) || pawn.apparel == null)
            {
                return;
            }

            Apparel existing = FindWornApparel(pawn, DefaultInnerClothingDefName);
            if (existing != null)
            {
                EnsureForcedApparel(pawn, existing);
                return;
            }

            ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultInnerClothingDefName);
            if (apparelDef == null)
            {
                Log.Error("[MiliraXian.Characters.Neiyu] Missing ThingDef: " + DefaultInnerClothingDefName);
                return;
            }

            Apparel innerClothing = FindDroppedApparelOnMap(pawn, apparelDef);
            if (innerClothing == null)
            {
                innerClothing = ThingMaker.MakeThing(apparelDef) as Apparel;
            }
            if (innerClothing == null)
            {
                Log.Error("[MiliraXian.Characters.Neiyu] Default inner clothing is not Apparel: " + DefaultInnerClothingDefName);
                return;
            }

            PawnGenerator.PostProcessGeneratedGear(innerClothing, pawn);
            pawn.apparel.Wear(innerClothing, dropReplacedApparel: true);
            EnsureForcedApparel(pawn, innerClothing);
        }

        public static bool HasDefaultEarringsEquipped(Pawn pawn)
        {
            return pawn?.apparel != null && pawn.apparel.WornApparel.Any(apparel => apparel?.def?.defName == DefaultEarringDefName);
        }

        public static bool HasDefaultClothingEquipped(Pawn pawn)
        {
            return pawn?.apparel != null && pawn.apparel.WornApparel.Any(apparel => apparel?.def?.defName == DefaultClothingDefName);
        }

        public static bool HasDefaultInnerClothingEquipped(Pawn pawn)
        {
            return pawn?.apparel != null && pawn.apparel.WornApparel.Any(apparel => apparel?.def?.defName == DefaultInnerClothingDefName);
        }

        public static bool HasInitialLoadoutEquipped(Pawn pawn)
        {
            return pawn?.equipment?.Primary != null
                && HasDefaultInnerClothingEquipped(pawn)
                && HasDefaultClothingEquipped(pawn)
                && HasDefaultEarringsEquipped(pawn);
        }

        public static void CleanupDroppedEarringsOnMap(Pawn pawn)
        {
            if (!IsNeiyu(pawn) || pawn.MapHeld == null)
            {
                return;
            }

            ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultEarringDefName);
            if (apparelDef == null)
            {
                return;
            }

            List<Thing> things = new List<Thing>(pawn.MapHeld.listerThings.ThingsOfDef(apparelDef));
            for (int index = things.Count - 1; index >= 0; index--)
            {
                Apparel apparel = things[index] as Apparel;
                if (apparel == null || apparel.Destroyed || apparel.Wearer != null)
                {
                    continue;
                }

                apparel.Destroy();
            }
        }

        private static Apparel FindDroppedApparelOnMap(Pawn pawn, ThingDef apparelDef)
        {
            if (pawn?.MapHeld == null)
            {
                return null;
            }

            List<Thing> things = pawn.MapHeld.listerThings.ThingsOfDef(apparelDef);
            for (int index = 0; index < things.Count; index++)
            {
                Apparel apparel = things[index] as Apparel;
                if (apparel != null && apparel.def == apparelDef && apparel.Wearer == null)
                {
                    return apparel;
                }
            }

            return null;
        }

        private static void EnsureForcedApparel(Pawn pawn, Apparel apparel)
        {
            if (pawn?.apparel == null || apparel == null)
            {
                return;
            }

            if (pawn.apparel.IsLocked(apparel))
            {
                pawn.apparel.Unlock(apparel);
            }

            if (pawn.outfits?.forcedHandler != null)
            {
                pawn.outfits.forcedHandler.SetForced(apparel, forced: true);
            }
        }

        private static Apparel FindWornApparel(Pawn pawn, string defName)
        {
            if (pawn?.apparel == null)
            {
                return null;
            }

            for (int index = 0; index < pawn.apparel.WornApparel.Count; index++)
            {
                Apparel apparel = pawn.apparel.WornApparel[index];
                if (apparel?.def?.defName == defName)
                {
                    return apparel;
                }
            }

            return null;
        }

        private static Apparel FindDroppedEarringsOnMap(Pawn pawn, ThingDef apparelDef)
        {
            return FindDroppedApparelOnMap(pawn, apparelDef);
        }
    }

    internal static class NeiyuRecruitUtility
    {
        private const string NeiyuPawnKindDefName = "MiliraXian_Neiyu";
        private const string MiliraFactionDefName = "Milira_Faction";

        public static bool NeiyuExistsAnywhere()
        {
            PawnKindDef neiyuKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(NeiyuPawnKindDefName);
            if (neiyuKind == null)
            {
                return false;
            }

            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (pawn?.kindDef == neiyuKind)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool NeiyuRecruitQuestExists(string questDefName = "MXNL_NeiyuProjectionRecruitQuest")
        {
            if (Find.QuestManager == null)
            {
                return false;
            }

            List<Quest> quests = Find.QuestManager.QuestsListForReading;
            for (int index = 0; index < quests.Count; index++)
            {
                Quest quest = quests[index];
                if (quest?.root?.defName == questDefName)
                {
                    return true;
                }
            }

            return false;
        }

        public static Pawn GenerateRecruitPawn()
        {
            PawnKindDef neiyuKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(NeiyuPawnKindDefName);
            if (neiyuKind == null)
            {
                Log.Error("[MiliraXian.Characters.Neiyu] Missing PawnKindDef: " + NeiyuPawnKindDefName);
                return null;
            }

            Faction miliraFaction = null;
            FactionDef miliraFactionDef = DefDatabase<FactionDef>.GetNamedSilentFail(MiliraFactionDefName);
            if (miliraFactionDef != null)
            {
                miliraFaction = Find.FactionManager.FirstFactionOfDef(miliraFactionDef);
            }

            PawnGenerationRequest request = new PawnGenerationRequest(
                neiyuKind,
                miliraFaction,
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
            NeiyuEquipmentUtility.EnsureDefaultLoadout(pawn);
            NeiyuEquipmentUtility.MarkForLoadoutStabilization(pawn);
            return pawn;
        }
    }
}
