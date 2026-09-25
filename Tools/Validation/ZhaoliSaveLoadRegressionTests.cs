using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using HarmonyLib;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using RimWorld.Planet;
using Verse;

// Uses the production component serializers and RimWorld's XML/reference/post-load
// pipeline. World-owned pawns/maps are registered separately; Unity spawning is a boundary.
internal static class ZhaoliSaveLoadTestRunner
{
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
            Assembly.GetExecutingAssembly().GetType("ZhaoliSaveLoadRegressionTests")
                .GetMethod("Run").Invoke(null, null);
            return 0;
        }
        catch (Exception ex)
        {
            for (Exception error = ex; error != null; error = error.InnerException)
            {
                Console.Error.WriteLine(error.GetType().FullName + ": " + error.Message);
                Console.Error.WriteLine(error.StackTrace);
            }
            return 1;
        }
    }
}

internal static class ZhaoliSaveLoadRegressionTests
{
    private static int checks;
    private static int rebirthAttempts;
    private static int returnOffers;
    private static int scribeErrors;
    private static Map home;
    private static bool testTargetHostile;

    public static void Run()
    {
        AccessTools.Field(typeof(GenTypes), "allTypesCached").SetValue(null, new List<Type> {
            typeof(GameComponent_ZhaoliScenario), typeof(GameComponent_ZhaoliKarma),
            typeof(GameComponent_ZhaoliRebirth), typeof(ZhaoliPendingRebirth)
        });
        Game game = Bare<Game>();
        game.components = new List<GameComponent>();
        game.tickManager = Bare<TickManager>();
        game.questManager = new QuestManager();
        Current.Game = game;
        Current.ProgramState = ProgramState.Playing;
        Set(game, "maps", new List<Map>());
        game.World = Bare<World>();
        game.World.factionManager = new FactionManager();
        game.World.worldObjects = new WorldObjectsHolder();
        game.World.worldPawns = new WorldPawns();
        home = Bare<Map>();
        home.uniqueID = 17;
        SetTick(1000);

        var harmony = new Harmony("MiliraXian.Tests.ZhaoliSaveLoad");
        Patch(harmony, AccessTools.Method(typeof(Log), "Error", new[] { typeof(string) }), "PrintError");
        Patch(harmony, AccessTools.Method(typeof(DeepProfiler), "Start"), "SkipUnity");
        Patch(harmony, AccessTools.Method(typeof(DeepProfiler), "End"), "SkipUnity");
        Patch(harmony, AccessTools.Method(typeof(GenTypes), "GetTypeInAnyAssembly"), "ResolveModType");
        Type utility = typeof(GameComponent_ZhaoliScenario).Assembly.GetType(
            "MiliraXian.Characters.Zhaoli.ZhaoliScenarioUtility", true);
        Patch(harmony, utility.GetMethod("ResolveBestHomeMap"), "ResolveHome");
        Patch(harmony, utility.GetMethod("PlayerHasZhaoli"), "NoPlayerZhaoli");
        Patch(harmony, typeof(Map).GetProperty("IsPlayerHome").GetGetMethod(), "IsHome");
        Patch(harmony, AccessTools.Method(typeof(GameComponent_ZhaoliScenario), "RefreshScenarioStateCache"), "RefreshScenario");
        Patch(harmony, AccessTools.Method(typeof(GameComponent_ZhaoliScenario), "TryExecuteScheduledReturn"), "OfferReturn");
        Patch(harmony, typeof(ZhaoliRebirthUtility).GetMethod("PreparePawnForPendingRebirth"), "SkipUnity");
        Patch(harmony, typeof(ZhaoliRebirthUtility).GetMethod("TryFindRebirthLocation"), "ObserveRebirth");

        TestMedicineReturn(game);
        TestRebirth(game);
        TestLegacyRebirth(game);
        TestAnimalGuiyi(harmony);
        TestScenarioPawnDiscovery(harmony, game, utility);
        Check(scribeErrors == 0, "no Scribe errors were suppressed during round trips");
        Current.Game = null;
        Console.WriteLine("PASS: " + checks + " Zhaoli save/load checks; Unity spawning is not exercised.");
    }

    private static void TestMedicineReturn(Game game)
    {
        SetTick(1000);
        var component = new GameComponent_ZhaoliScenario(game);
        component.MarkMurmurOffered(home);
        component.NotifyHideoutMedicineAccepted(home);
        Check((int)Get(component, "scheduledReturnTick") == 61000, "herb hand-in schedules one day later");
        component = RoundTrip(component, home);
        Check(Get(component, "scenarioHomeMap") == home, "home-map reference resolves after load");
        Check((int)Get(component, "scheduledReturnTick") == 61000, "herb hand-in deadline survives XML round trip");
        SetTick(60999);
        Check(!component.CanOfferReturn(home), "quest is not offered before its saved deadline");
        SetTick(61000);
        Check(component.CanOfferReturn(home), "quest becomes available at its saved deadline");
        component.GameComponentTick();
        Check(returnOffers == 1 && (int)Get(component, "scheduledReturnTick") == -1,
            "first due tick reaches return-incident boundary and consumes the schedule");
        component = RoundTrip(component, home);
        SetTick(91000);
        component.GameComponentTick();
        Check(returnOffers == 1, "save/load after offering does not duplicate the return");
        Console.WriteLine("Herb hand-in save/load path passed.");
    }

    private static void TestRebirth(Game game)
    {
        Pawn dead = DeadPawn(101);
        Check(dead.Dead && dead.Destroyed && !dead.Discarded, "fixture matches a killed, retained world pawn");
        var component = new GameComponent_ZhaoliKarma(game);
        component.RegisterPendingRebirth(dead, 601000);
        string xml = Scribe.saver.DebugOutputFor(component);
        Check(xml.Contains("<pawn>" + dead.GetUniqueLoadID() + "</pawn>"), "dead pawn is saved by load ID, not null");
        component = Load<GameComponent_ZhaoliKarma>(xml, dead);
        Check(component.IsPending(dead), "dead pawn remains scheduled after reference resolution and post-load cleanup");
        Check((int)Get(component, "nextRebirthCheckTick") == 601000, "cached wake-up tick is rebuilt");
        SetTick(600999);
        component.GameComponentTick();
        Check(rebirthAttempts == 0, "rebirth cannot run early");
        SetTick(901000);
        component.GameComponentTick();
        Check(rebirthAttempts == 1, "advancing fifteen days after load reaches rebirth location selection");
        Check(component.IsPending(dead), "missing home map retains the rebirth for retry");
        component = RoundTrip(component, dead);
        component.GameComponentTick();
        Check(rebirthAttempts == 2, "an overdue rebirth retries after a second load");
    }

    private static void TestLegacyRebirth(Game game)
    {
        Pawn dead = DeadPawn(102);
        var component = new GameComponent_ZhaoliRebirth(game);
        component.RegisterPendingRebirth(dead, 601000);
        component = RoundTrip(component, dead);
        Check(component.IsPending(dead), "legacy rebirth component also retains killed pawns");
        int before = rebirthAttempts;
        SetTick(901000);
        component.GameComponentTick();
        Check(rebirthAttempts == before + 1, "legacy due tick reaches the rebirth boundary");
        Check(component.IsPending(dead), "legacy retry does not discard a dead pawn");
        component = Load<GameComponent_ZhaoliRebirth>("<saveable />");
        component.GameComponentTick();
        Check(!component.IsPending(dead), "legacy missing queue is initialized safely");
    }

    private static T RoundTrip<T>(T component, params ILoadReferenceable[] references) where T : GameComponent
    {
        return Load<T>(Scribe.saver.DebugOutputFor(component), references);
    }

    private static T Load<T>(string xml, params ILoadReferenceable[] references) where T : GameComponent
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        Scribe.mode = LoadSaveMode.LoadingVars;
        Scribe.loader.curXmlParent = document.DocumentElement;
        T component = ScribeExtractor.SaveableFromNode<T>(document.DocumentElement, new object[] { Current.Game });
        var directory = (LoadedObjectDirectory)Get(Scribe.loader.crossRefs, "loadedObjectDirectory");
        foreach (ILoadReferenceable reference in references) directory.RegisterLoaded(reference);
        Scribe.loader.FinalizeLoading();
        Check(component != null && Scribe.mode == LoadSaveMode.Inactive, "actual Scribe load pipeline completes");
        return component;
    }

    private static Pawn DeadPawn(int id)
    {
        Pawn pawn = Bare<Pawn>();
        pawn.def = Bare<ThingDef>();
        pawn.def.defName = "Milira_Race";
        pawn.thingIDNumber = id;
        pawn.health = Bare<Pawn_HealthTracker>();
        Set(pawn.health, "healthState", PawnHealthState.Dead);
        Set(pawn, "mapIndexOrState", (sbyte)-2);
        return pawn;
    }

    private static void TestAnimalGuiyi(Harmony harmony)
    {
        // Unity presentation and body-cache construction are outside this managed fixture.
        Patch(harmony, AccessTools.Method(typeof(HediffSet), "GetMissingPartsCommonAncestors"), "NoMissingParts");
        Patch(harmony, AccessTools.Method(typeof(Pawn_HealthTracker), "RemoveHediff"), "RemoveTestHediff");
        Patch(harmony, AccessTools.Method(typeof(FleckMaker), "AttachedOverlay"), "SkipUnity");
        Patch(harmony, AccessTools.PropertyGetter(typeof(RaceProperties), "IsAnomalyEntity"), "NoPlayerZhaoli");
        Patch(harmony, AccessTools.Method(typeof(GenHostility), "HostileTo", new[] { typeof(Thing), typeof(Thing) }), "TestHostility");
        AccessTools.Field(typeof(DefOfHelper), "bindingNow").SetValue(null, true);
        var language = Bare<LoadedLanguage>();
        language.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
        Set(language, "dataIsLoaded", true);
        foreach (string key in new[] { "MX_ZL_LinkTargetInvalid", "MX_ZL_LinkLimitReached" })
            language.keyedReplacements[key] = new LoadedLanguage.KeyedReplacement { value = key };
        LanguageDatabase.activeLanguage = language;

        Pawn caster = LivePawn(701, Intelligence.Humanlike), animal = LivePawn(702, Intelligence.Animal), human = LivePawn(703, Intelligence.Humanlike);
        caster.kindDef = new PawnKindDef { defName = ZhaoliKarmaUtility.ZhaoliPawnKindDefName };
        var karmaDef = new HediffDef { defName = ZhaoliKarmaUtility.KarmaHediffDefName };
        DefDatabase<HediffDef>.Add(karmaDef);
        var karma = new HediffWithComps { def = karmaDef, pawn = caster };
        var links = new HediffComp_ZhaoliKarmaLinks { parent = karma, props = new HediffCompProperties_ZhaoliKarmaLinks { maxLinks = 0 } };
        var resource = new MiliraXian.Characters.HediffComp_PawnSpecialResource {
            parent = karma, props = new MiliraXian.Characters.HediffCompProperties_PawnSpecialResource { initialValue = 10f }
        };
        karma.comps = new List<HediffComp> { links, resource };
        caster.health.hediffSet.hediffs.Add(karma);
        var ability = Bare<Ability>(); ability.pawn = caster; ability.def = Bare<AbilityDef>();
        var guiYi = new CompAbilityEffect_ZhaoliGuiyi { parent = ability, props = new CompProperties_AbilityZhaoliGuiyi { karmaCost = 3f } };
        string reason;
        bool created;
        Check(!links.CanLinkTarget(animal, out reason) && !links.TryAddOrRefreshLink(animal, out created, out reason) && !created,
            "animals cannot enter a causal link through either entry point");
        Check(ZhaoliKarmaUtility.CanCarryKarmaLink(human), "human link eligibility is unchanged");
        var injury = new Hediff_Injury { pawn = animal, def = new HediffDef { isBad = true } };
        animal.health.hediffSet.hediffs.Add(injury);
        testTargetHostile = true;
        Check(!guiYi.Valid(new LocalTargetInfo(animal)), "hostile animals remain invalid treatment targets");
        testTargetHostile = false;
        Check(guiYi.Valid(new LocalTargetInfo(animal)), "injured animals remain valid with zero available link slots");
        guiYi.Apply(new LocalTargetInfo(animal), LocalTargetInfo.Invalid);
        Check(!animal.health.hediffSet.hediffs.Contains(injury) && resource.CurrentValue == 7f && links.ActiveLinkCount == 0,
            "actual animal treatment removes injury, spends karma, and creates no link");
        Check(!guiYi.Valid(new LocalTargetInfo(animal)), "healthy animals cannot waste treatment");

        var legacy = new HediffComp_ZhaoliKarmaLinkTarget { parent = new HediffWithComps { pawn = animal } };
        legacy.SetZhaoli(caster);
        Set(links, "linkedPawns", new List<Pawn> { animal });
        Check(legacy.CompShouldRemove && !ZhaoliKarmaUtility.HasLinkFrom(animal, caster), "old animal link markers expire and have no effect");
        Check(links.ActiveLinkCount == 0 && links.GetRandomLiveLinkedPawn() == null && !links.TryDistributeOverflow(1),
            "old animal links neither occupy slots nor supply sacrifice or overflow targets");
    }

    private static Pawn LivePawn(int id, Intelligence intelligence)
    {
        Pawn pawn = DeadPawn(id);
        pawn.def.race = new RaceProperties { intelligence = intelligence };
        Set(pawn.def.race, "fleshType", new FleshTypeDef { isOrganic = true });
        Set(pawn.health, "pawn", pawn);
        Set(pawn.health, "healthState", PawnHealthState.Mobile);
        pawn.health.hediffSet = new HediffSet(pawn);
        Set(pawn, "mapIndexOrState", (sbyte)-1);
        return pawn;
    }

    private static void TestScenarioPawnDiscovery(Harmony harmony, Game game, Type utility)
    {
        MethodInfo hasZhaoli = utility.GetMethod("PlayerHasZhaoli");
        MethodInfo refresh = AccessTools.Method(typeof(GameComponent_ZhaoliScenario), "RefreshScenarioStateCache");
        harmony.Unpatch(hasZhaoli, HarmonyPatchType.Prefix, harmony.Id);
        harmony.Unpatch(refresh, HarmonyPatchType.Prefix, harmony.Id);
        Func<bool> ownsZhaoli = () => (bool)hasZhaoli.Invoke(null, null);
        Check(!ownsZhaoli(), "missing player faction safely reports no recruited Zhaoli");
        var faction = Bare<Faction>(); faction.def = new FactionDef { isPlayer = true };
        Set(game.World.factionManager, "ofPlayer", faction);
        Pawn incompleteMech = Bare<Pawn>(); incompleteMech.kindDef = new PawnKindDef { defName = "TestIncompleteMech" };
        Pawn incompleteZhaoli = Bare<Pawn>(); incompleteZhaoli.kindDef = new PawnKindDef { defName = ZhaoliKarmaUtility.ZhaoliPawnKindDefName };
        Pawn zhaoli = LivePawn(704, Intelligence.Humanlike);
        zhaoli.kindDef = incompleteZhaoli.kindDef;
        Set(zhaoli, "factionInt", faction);
        var temporary = (List<List<Pawn>>)AccessTools.Field(typeof(PawnGroupKindWorker), "pawnsBeingGeneratedNow").GetValue(null);
        var generating = new List<Pawn> { null, incompleteMech, incompleteZhaoli };
        temporary.Add(generating);
        try
        {
            bool oldQueryThrew = false;
            try { var ignored = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead; }
            catch (NullReferenceException) { oldQueryThrew = true; }
            Check(oldQueryThrew, "reproduces the old global query's uninitialized-health exception");
            Check(!ownsZhaoli(), "unrelated and incomplete temporary pawns do not break ownership lookup");
            generating.Add(zhaoli);
            Check(ownsZhaoli(), "a valid temporary Zhaoli is still discovered after malformed entries");
            Set(zhaoli.health, "healthState", PawnHealthState.Dead);
            Check(!ownsZhaoli(), "dead Zhaoli does not count as a recruited living pawn");
            Set(zhaoli.health, "healthState", PawnHealthState.Mobile);
            generating.RemoveAt(generating.Count - 1);
            ((HashSet<Pawn>)Get(game.World.worldPawns, "pawnsAlive")).Add(zhaoli);
            Check(ownsZhaoli(), "world and caravan-owned Zhaoli remains discoverable");
            ((HashSet<Pawn>)Get(game.World.worldPawns, "pawnsAlive")).Clear();
            var map = Bare<Map>(); map.mapPawns = Bare<MapPawns>();
            Set(map.mapPawns, "pawnsSpawned", new List<Pawn> { incompleteMech, zhaoli });
            game.Maps.Add(map);
            Check(ownsZhaoli(), "spawned-map lookup filters identity before reading health");
            game.Maps.Clear();
            var component = new GameComponent_ZhaoliScenario(game);
            Set(component, "scenarioCompleted", true);
            for (int tick = 1000; tick <= 1500; tick += 250)
            {
                SetTick(tick);
                component.GameComponentTick();
                Check((int)Get(component, "nextScenarioStateCheckTick") > tick, "periodic scenario checks advance instead of repeating an exception");
            }
            AccessTools.Method(typeof(GameComponent_ZhaoliScenario), "BackfillTrackedPawnDeaths").Invoke(component, null);
            Check((int)Get(component, "qualifyingPawnDeathCount") == 0, "death backfill also tolerates incomplete temporary pawns");
            foreach (Pawn pawn in new[] { null, incompleteMech, incompleteZhaoli })
            {
                Patch_Pawn_Kill_ZhaoliSubstitute.Prefix(pawn);
                Patch_Pawn_Kill_ZhaoliSubstitute.Postfix(pawn);
                foreach (string patch in new[] { "Patch_Pawn_Kill_ZhaoliScenario", "Patch_Pawn_Kill_ZhaoliRebirthFallback" })
                    utility.Assembly.GetType("MiliraXian.Characters.Zhaoli." + patch).GetMethod("Postfix").Invoke(null, new object[] { pawn });
            }
            Check(true, "Zhaoli death hooks safely ignore unrelated or incomplete pawns");
            World world = game.World;
            game.World = null;
            Check(!ownsZhaoli(), "world initialization gap is handled without player-faction access");
            component.GameComponentTick();
            game.World = world;
            Current.Game = null;
            Check(!ownsZhaoli(), "main-menu or unloading state has no pawn lookup");
            Current.Game = game;
        }
        finally { temporary.Remove(generating); }
    }

    private static bool NoMissingParts(ref List<Hediff_MissingPart> __result) { __result = new List<Hediff_MissingPart>(); return false; }
    private static bool TestHostility(ref bool __result) { __result = testTargetHostile; return false; }
    private static bool RemoveTestHediff(Pawn_HealthTracker __instance, Hediff hediff)
    {
        __instance.hediffSet.hediffs.Remove(hediff);
        return false;
    }

    private static bool ResolveHome(ref Map __result) { __result = home; return false; }
    private static bool NoPlayerZhaoli(ref bool __result) { __result = false; return false; }
    private static bool ResolveModType(string typeName, ref Type __result)
    {
        __result = typeof(GameComponent_ZhaoliKarma).Assembly.GetType(typeName);
        return __result == null;
    }
    private static bool PrintError(string text) { scribeErrors++; Console.Error.WriteLine(text); return false; }
    private static bool IsHome(ref bool __result) { __result = true; return false; }
    private static bool SkipUnity() { return false; }
    private static bool ObserveRebirth(ref Map map, ref IntVec3 cell, ref bool __result)
    {
        rebirthAttempts++;
        map = null;
        cell = IntVec3.Invalid;
        __result = false;
        return false;
    }
    private static bool RefreshScenario(GameComponent_ZhaoliScenario __instance, int currentTick)
    {
        Set(__instance, "nextScenarioStateCheckTick", currentTick + 250);
        return false;
    }
    private static bool OfferReturn(GameComponent_ZhaoliScenario __instance, ref bool __result)
    {
        __result = __instance.CanOfferReturn(home);
        if (__result) { returnOffers++; __instance.MarkReturnOffered(home); }
        return false;
    }
    private static void Patch(Harmony harmony, MethodInfo target, string prefix)
    {
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(ZhaoliSaveLoadRegressionTests), prefix));
    }
    private static T Bare<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    private static object Get(object target, string field) { return AccessTools.Field(target.GetType(), field).GetValue(target); }
    private static void Set(object target, string field, object value) { AccessTools.Field(target.GetType(), field).SetValue(target, value); }
    private static void SetTick(int tick) { Set(Current.Game.tickManager, "ticksGameInt", tick); }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("FAIL: " + message);
    }
}
