using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using MiliraXian.Characters.Mingyuan;
using RimWorld;
using Verse;

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
            Console.WriteLine("PASS: " + checks + " Mingyuan quest registration/acceptance checks; Unity spawning is not exercised.");
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
    }

    private static GameComponent_MingyuanWhiteFlameQuest Setup()
    {
        var game = Bare<Game>();
        game.components = new List<GameComponent>();
        game.tickManager = Bare<TickManager>();
        Set(game.tickManager, "ticksGameInt", 1000);
        game.questManager = new QuestManager();
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
