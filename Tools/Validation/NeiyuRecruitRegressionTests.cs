using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

// Executes the production recruitment gates against actual RimWorld types without Unity.
internal static class NeiyuRecruitRegressionTests
{
    private const string RecruitQuest = "MXNL_NeiyuProjectionRecruitQuest";
    private static int checks;
    private static MethodInfo isScenario;
    private static MethodInfo canOffer;

    private static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, evt) => {
                string filename = new AssemblyName(evt.Name).Name + ".dll";
                foreach (string directory in args)
                {
                    string file = Path.Combine(directory, filename);
                    if (File.Exists(file)) return Assembly.LoadFrom(file);
                }
                return null;
            };
            Run();
            Console.WriteLine("PASS: " + checks + " Neiyu recruitment regression checks against production DLL.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Run()
    {
        Type utility = typeof(GameComponent_NeiyuProjectionRecruit).Assembly.GetType(
            "MiliraXian.Characters.Neiyu.NeiyuRecruitUtility", true);
        isScenario = utility.GetMethod("IsNeiyuStartingScenario");
        canOffer = utility.GetMethod("CanOfferRecruitment");

        Check(!Blocked(null), "no scenario");
        Scenario ordinary = ScenarioWithKind("Colonist", 1, true);
        ordinary.name = "界外羽痕";
        Check(!Blocked(ordinary), "a matching translated name does not block ordinary starts");
        Check(!Blocked(ScenarioWithKind("MiliraXian_Neiyu", 0, true)), "zero starting pawns");
        Check(!Blocked(ScenarioWithKind("MiliraXian_Neiyu", 1, false)), "optional unselected candidates");
        Check(!Blocked(ScenarioWithKind("MiliraXian_Qinghe", 1, true)), "other special-character start");

        Scenario original = ScenarioWithKind("MiliraXian_Neiyu", 1, true);
        Check(Blocked(original), "required Neiyu start");
        Scenario copy = original.CopyForEditing();
        copy.name = "Renamed custom scenario";
        Check(!ReferenceEquals(original, copy), "actual game scenario copy");
        Check(Blocked(copy), "copied/renamed scenario remains blocked");
        Scenario loaded = ScenarioWithKind("MiliraXian_Neiyu", 1, true);
        loaded.name = "保存后切换语言的剧本";
        Check(Blocked(loaded), "independently reconstructed persisted parts need no ScenarioDef identity");

        Game game = Bare<Game>();
        game.Scenario = copy;
        game.components = new List<GameComponent>();
        game.tickManager = Bare<TickManager>();
        game.questManager = new QuestManager();
        game.letterStack = new LetterStack();
        Current.Game = game;
        Current.ProgramState = ProgramState.Playing;
        var component = new GameComponent_NeiyuProjectionRecruit(game);
        game.components.Add(component);

        Check(component.IsBlockedByScenario(), "public component gate recognizes copied start");
        Check(!Offerable(), "quest gate blocks before first component tick, without any pawn present");
        var slate = new Slate();
        Check(!TestRoot(new QuestNode_Root_NeiyuProjectionRecruit_AvailableQuest(), slate), "available quest root blocked");
        Check(!TestRoot(new QuestNode_Root_NeiyuProjectionRecruit(), slate), "legacy quest root blocked");
        var worker = new IncidentWorker_NeiyuProjectionRecruit();
        MethodInfo execute = worker.GetType().GetMethod("TryExecuteWorker", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(!(bool)execute.Invoke(worker, new object[] { new IncidentParms { forced = true } }), "forced/queued execution blocked");
        component.GameComponentTick();
        Check(component.EventAlreadyTriggered, "first playable tick permanently consumes scenario recruitment");

        game.Scenario = ordinary;
        Check(!Offerable(), "saved consumed state remains blocked after scenario edit");
        component = new GameComponent_NeiyuProjectionRecruit(game);
        game.components.Clear();
        game.components.Add(component);
        Check(Offerable(), "ordinary scenario with no prior event or pawn stays eligible");

        // Simulate an old save where an offer already set eventTriggered. Quest cleanup is
        // pre-completed to avoid global faction/ideology services; End/state and letter removal
        // still execute on the real Quest/LetterStack types in the production component.
        game.Scenario = loaded;
        component.MarkTriggered();
        Quest pending = QuestFixture(RecruitQuest, -1);
        Quest accepted = QuestFixture(RecruitQuest, 0);
        Quest other = QuestFixture("UnrelatedQuest", -1);
        Quest historical = QuestFixture(RecruitQuest, -1);
        historical.End(QuestEndOutcome.Success, false, false);
        var quests = game.questManager.QuestsListForReading;
        quests.Add(pending);
        quests.Add(accepted);
        quests.Add(other);
        quests.Add(historical);
        var obsoleteLetter = new ChoiceLetter_AcceptJoiner { quest = pending };
        var unrelatedLetter = new ChoiceLetter_AcceptJoiner { quest = other };
        game.letterStack.LettersListForReading.Add(obsoleteLetter);
        game.letterStack.LettersListForReading.Add(unrelatedLetter);
        component.GameComponentTick();
        Check(pending.State == QuestState.EndedUnknownOutcome, "old unaccepted offer withdrawn even when event already triggered");
        Check(!game.letterStack.LettersListForReading.Contains(obsoleteLetter), "obsolete offer letter removed");
        Check(game.letterStack.LettersListForReading.Contains(unrelatedLetter), "unrelated letter preserved");
        Check(accepted.State == QuestState.Ongoing, "accepted quest untouched");
        Check(other.State == QuestState.NotYetAccepted, "unrelated offer untouched");
        Check(historical.State == QuestState.EndedSuccess, "historical outcome untouched");
        component.GameComponentTick();
        Check(pending.State == QuestState.EndedUnknownOutcome, "cleanup is idempotent");
        Current.Game = null;
    }

    private static Scenario ScenarioWithKind(string name, int count, bool required)
    {
        var scenario = new Scenario();
        var kind = Bare<PawnKindDef>();
        kind.defName = name;
        var part = new ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs();
        part.kindCounts.Add(new PawnKindCount { kindDef = kind, count = count, requiredAtStart = required });
        SetField(scenario, "parts", new List<ScenPart> { part });
        SetField(scenario, "playerFaction", new ScenPart_PlayerFaction());
        SetField(scenario, "surfaceLayer", new ScenPart_PlanetLayer());
        return scenario;
    }

    private static Quest QuestFixture(string name, int acceptanceTick)
    {
        var quest = new Quest { root = new QuestScriptDef { defName = name }, acceptanceTick = acceptanceTick };
        SetField(quest, "cleanedUp", true);
        return quest;
    }

    private static bool TestRoot(QuestNode node, Slate slate)
    {
        return (bool)node.GetType().GetMethod("TestRunInt", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(node, new object[] { slate });
    }

    private static bool Blocked(Scenario scenario) { return (bool)isScenario.Invoke(null, new object[] { scenario }); }
    private static bool Offerable() { return (bool)canOffer.Invoke(null, null); }
    private static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("FAIL: " + message);
    }
}
