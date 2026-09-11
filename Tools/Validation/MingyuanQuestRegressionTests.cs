using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using MiliraXian.Characters.Mingyuan;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using System.Xml;

// Exercises registration and acceptance with real Quest/GameComponent types.
// Stop at map resolution, before Unity spawning and visual effects are required.
internal static class MingyuanQuestRegressionTests
{
    private const string QuestName = "MX_Mingyuan_WhiteFlameQuest";
    private static int checks;
    private static int spawnAttempts;
    private static Map requestedMap;

    private static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, evt) => {
                string filename = new AssemblyName(evt.Name).Name + ".dll";
                foreach (string directory in args)
                {
                    string path = Path.Combine(directory, filename);
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            Run();
            Console.WriteLine("PASS: " + checks + " Mingyuan quest/assault checks; Unity spawning and live combat are not exercised.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Run()
    {
        var component = Setup();
        Map map = Bare<Map>();
        Quest offer = MakeQuest(map, false);
        Current.Game.questManager.QuestsListForReading.Add(offer);
        Set(component, "nextOfferTick", 9999999);
        Call(component, "ProcessWaiting", 1000);
        Check(State(component) == "Offered" && Get(component, "activeQuest") == offer,
            "manual offer is registered before day 60 and despite the natural-offer cooldown");
        Check(Get(component, "targetMap") == map, "recovery keeps the map stored in the quest part");

        Type utility = typeof(GameComponent_MingyuanWhiteFlameQuest).Assembly.GetType(
            "MiliraXian.Characters.Mingyuan.MingyuanWhiteFlameUtility", true);
        var harmony = new Harmony("MiliraXian.Tests.MingyuanQuest");
        harmony.Patch(utility.GetMethod("ResolveSpawnMap"), prefix: new HarmonyMethod(
            typeof(MingyuanQuestRegressionTests).GetMethod("StopBeforeSpawning", BindingFlags.NonPublic | BindingFlags.Static)));

        component = Setup();
        Quest accepted = MakeQuest(map, true);
        var part = (QuestPart_MingyuanWhiteFlame)accepted.PartsListForReading[0];
        part.Notify_QuestSignalReceived(new Signal("Unrelated.Signal"));
        Check(State(component) == "Waiting", "unrelated signals cannot register or start the quest");
        ExpectSpawn(() => part.Notify_QuestSignalReceived(new Signal(part.inSignal)));
        Check(Get(component, "activeQuest") == accepted && requestedMap == map,
            "manual acceptance registers the quest and reaches the defense spawn path immediately");

        foreach (string stage in new[] { "Waiting", "Omen", "Offered" })
        {
            component = Setup();
            SetStage(component, stage);
            Current.Game.questManager.QuestsListForReading.Add(accepted);
            Set(component, "nextProcessTick", 9999999);
            component.LoadedGame();
            Check(State(component) == "Offered" && Get(component, "activeQuest") == accepted,
                "loading repairs an accepted quest with missing registration from " + stage);
            Check((int)Get(component, "nextProcessTick") == 1000, "repaired quest is due immediately after loading");
            ExpectSpawn(() => Call(component, "ProcessOffered", 1000));
            Check(requestedMap == map, "loaded quest resumes on its original map");
        }

        foreach (string stage in new[] { "Defending", "Reforming", "AwaitingDecision", "Completed" })
        {
            component = Setup();
            SetStage(component, stage);
            Set(component, "activeQuest", accepted);
            Set(component, "targetMap", map);
            Current.Game.questManager.QuestsListForReading.Add(accepted);
            component.LoadedGame();
            int before = spawnAttempts;
            component.BeginDefense(accepted, map);
            Check(State(component) == stage && spawnAttempts == before,
                "duplicate acceptance cannot restart a quest in " + stage);
        }

        component = Setup();
        SetStage(component, "Offered");
        Set(component, "activeQuest", accepted);
        int attempts = spawnAttempts;
        component.BeginDefense(MakeQuest(Bare<Map>(), true), null);
        Check(Get(component, "activeQuest") == accepted && spawnAttempts == attempts,
            "another quest cannot replace the registered live quest");

        component = Setup();
        Quest unrelated = MakeQuest(map, true);
        unrelated.root = new QuestScriptDef { defName = "UnrelatedQuest" };
        component.BeginDefense(unrelated, map);
        component.BeginDefense(null, map);
        Check(State(component) == "Waiting", "null and unrelated quests are ignored");
        Quest ended = MakeQuest(map, true);
        Set(ended, "cleanedUp", true);
        ended.End(QuestEndOutcome.Success, false, false);
        component.BeginDefense(ended, map);
        Check(State(component) == "Waiting", "ended quests cannot be restarted");

        component = Setup();
        Call(component, "ProcessWaiting", 1000);
        Check(State(component) == "Waiting", "natural offers still require their original age threshold");
        Console.WriteLine("Acceptance/recovery reached the defense spawn boundary " + spawnAttempts + " times.");
        TestFlameAssault(harmony);
    }

    private static Job vanillaCombatJob;

    private sealed class FlameJobProbe : JobGiver_MingyuanAttackFlame
    {
        public Job GiveJob(Pawn pawn) { return TryGiveJob(pawn); }
        public Thing SelectTarget(Pawn pawn) { return FindAttackTarget(pawn); }
    }

    private static bool SupplyVanillaCombatJob(ref Job __result)
    {
        __result = vanillaCombatJob;
        return false;
    }

    private static bool MakeJobWithoutUnityPool(JobDef __0, LocalTargetInfo __1, ref Job __result)
    {
        // The game's pool uses Queue.TryDequeue, absent in the desktop CLR used
        // by this Harmony fixture. Keep real Job construction and target fields.
        __result = new Job(__0, __1);
        return false;
    }

    private static void TestFlameAssault(Harmony harmony)
    {
        Setup();
        Set(Current.Game, "maps", new List<Map> { Bare<Map>() });
        var pawn = Bare<Pawn>();
        pawn.mindState = Bare<Pawn_MindState>();
        var marker = new Thing_MingyuanQuestRebirthFlame();
        var probe = new FlameJobProbe();
        Check(probe.SelectTarget(pawn) == null, "missing duty cannot select a flame");
        MX_MingyuanDefOf.MX_Mingyuan_AssaultRebirthFlame = new DutyDef { defName = "MX_Mingyuan_AssaultRebirthFlame" };
        pawn.mindState.duty = new PawnDuty(MX_MingyuanDefOf.MX_Mingyuan_AssaultRebirthFlame, marker);
        Check(probe.SelectTarget(pawn) == null, "despawned flame cannot be attacked");

        var graph = new LordJob_MingyuanAssaultFlame(marker).CreateGraph();
        Check(graph.lordToils.Count == 2 && graph.lordToils[0] is LordToil_MingyuanAssaultFlame
            && graph.lordToils[1] is LordToil_ExitMapAndDefendSelf,
            "assault keeps the vanilla leave-after-objective lifecycle");
        Check(graph.transitions.Count == 1, "assault includes its objective-loss transition");
        var lord = Bare<Lord>();
        lord.ownedPawns = new List<Pawn> { pawn };
        var toil = (LordToil_MingyuanAssaultFlame)graph.lordToils[0];
        toil.lord = lord;
        AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(marker, (sbyte)0);
        pawn.mindState.duty = null;
        toil.UpdateAllDuties();
        Check(pawn.mindState.duty != null && pawn.mindState.duty.def == MX_MingyuanDefOf.MX_Mingyuan_AssaultRebirthFlame
            && pawn.mindState.duty.focus.Thing == marker, "wave duty targets the actual flame Thing");
        PawnDuty duty = pawn.mindState.duty;
        toil.UpdateAllDuties();
        Check(pawn.mindState.duty == duty, "unchanged duties are retained across periodic updates");
        pawn.mindState.duty = new PawnDuty(MX_MingyuanDefOf.MX_Mingyuan_AssaultRebirthFlame, new Thing());
        toil.UpdateAllDuties();
        Check(pawn.mindState.duty.focus.Thing == marker, "a stale focus is restored to the flame");

        // Isolate Unity's firing-position search. The production adapter must retain
        // vanilla movement/melee and turn a shooting-position wait into a fixed shot.
        MethodInfo baseGive = AccessTools.Method(typeof(JobGiver_AIFightEnemy), "TryGiveJob");
        harmony.Patch(baseGive, prefix: new HarmonyMethod(typeof(MingyuanQuestRegressionTests), "SupplyVanillaCombatJob"));
        MethodInfo makeJob = AccessTools.Method(typeof(JobMaker), "MakeJob", new[] { typeof(JobDef), typeof(LocalTargetInfo) });
        harmony.Patch(makeJob, prefix: new HarmonyMethod(typeof(MingyuanQuestRegressionTests), "MakeJobWithoutUnityPool"));
        JobDefOf.Wait_Combat = new JobDef { defName = "Wait_Combat" };
        JobDefOf.AttackStatic = new JobDef { defName = "AttackStatic" };
        pawn.mindState.enemyTarget = marker;
        vanillaCombatJob = new Job(JobDefOf.Wait_Combat) { expiryInterval = 487 };
        Job attack = probe.GiveJob(pawn);
        Check(attack.def == JobDefOf.AttackStatic && attack.targetA.Thing == marker,
            "ranged attackers shoot the flame instead of running Wait_Combat's generic target scan");
        Check(attack.expiryInterval == 487 && attack.checkOverrideOnExpire
            && attack.endIfCantShootTargetFromCurPos,
            "ranged attack retains reevaluation timing and replans when firing is obstructed");
        vanillaCombatJob = new Job(new JobDef { defName = "Goto" });
        Check(probe.GiveJob(pawn) == vanillaCombatJob, "vanilla movement to weapon range is preserved");
        vanillaCombatJob = new Job(new JobDef { defName = "AttackMelee" }, marker);
        Check(probe.GiveJob(pawn) == vanillaCombatJob, "vanilla melee attack against the flame is preserved");
        vanillaCombatJob = null;
        Check(probe.GiveJob(pawn) == null, "unreachable attack lets the duty use combat/sapper fallback");
        harmony.Unpatch(baseGive, HarmonyPatchType.Prefix, harmony.Id);
        harmony.Unpatch(makeJob, HarmonyPatchType.Prefix, harmony.Id);

        string defs = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(GameComponent_MingyuanWhiteFlameQuest).Assembly.Location), "../Defs"));
        var xml = new XmlDocument();
        xml.Load(Path.Combine(defs, "ThingDefs/MiliraXian_Mingyuan_Quest.xml"));
        Check(xml.SelectSingleNode("/Defs/ThingDef[defName='MX_Mingyuan_QuestRebirthFlame']/building/isInert").InnerText == "false",
            "the flame is not inert, so vanilla TrashJob can also attack it");
        xml.Load(Path.Combine(defs, "DutyDefs/MiliraXian_Mingyuan_Quest.xml"));
        Check(xml.SelectSingleNode("/Defs/DutyDef/thinkNode/subNodes/li[1]").Attributes["Class"].Value
            == typeof(JobGiver_MingyuanAttackFlame).FullName,
            "flame attack has priority over nearby-enemy combat in the duty tree");
    }

    private static GameComponent_MingyuanWhiteFlameQuest Setup()
    {
        AccessTools.Field(typeof(DefOfHelper), "bindingNow").SetValue(null, true);
        var game = Bare<Game>();
        game.components = new List<GameComponent>();
        game.tickManager = Bare<TickManager>();
        Set(game.tickManager, "ticksGameInt", 1000);
        game.questManager = new QuestManager();
        game.uniqueIDsManager = new UniqueIDsManager();
        Current.Game = game;
        Current.ProgramState = ProgramState.Playing;
        var component = new GameComponent_MingyuanWhiteFlameQuest(game);
        game.components.Add(component);
        return component;
    }

    private static Quest MakeQuest(Map map, bool accepted)
    {
        var quest = new Quest {
            root = new QuestScriptDef { defName = QuestName },
            acceptanceTick = accepted ? 1000 : -1
        };
        quest.AddPart(new QuestPart_MingyuanWhiteFlame {
            map = map, inSignal = quest.InitiateSignal,
            signalListenMode = QuestPart.SignalListenMode.OngoingOrNotYetAccepted
        });
        return quest;
    }

    private sealed class SpawnBoundaryReached : Exception { }
    private static void StopBeforeSpawning(Map preferredMap)
    {
        requestedMap = preferredMap;
        spawnAttempts++;
        throw new SpawnBoundaryReached();
    }

    private static void ExpectSpawn(Action action)
    {
        int before = spawnAttempts;
        try { action(); }
        catch (SpawnBoundaryReached) { }
        catch (TargetInvocationException ex) { if (!(ex.InnerException is SpawnBoundaryReached)) throw; }
        Check(spawnAttempts == before + 1, "accepted quest reaches the existing defense spawn path exactly once");
    }

    private static object Call(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args); }
    private static object Get(object target, string field)
    { return target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target); }
    private static void Set(object target, string field, object value)
    { target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value); }
    private static string State(object target) { return Get(target, "stage").ToString(); }
    private static void SetStage(object target, string stage)
    { Set(target, "stage", Enum.Parse(Get(target, "stage").GetType(), stage)); }
    private static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("FAIL: " + message);
    }
}
