using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using MiliraXian.Characters.Mingyuan;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using Verse;
using Verse.AI;

// Executes production methods using the installed game's managed types. It does
// not start Unity or claim to validate graphics, full saves, or combat balance.
internal static class AuditSafetyRegressionTests
{
    private static int checks;
    private static Assembly mod;
    private static MethodInfo rent;
    private static MethodInfo giveBack;

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
            if (args[0] == "--field-baseline") TestFieldPulse(1);
            else if (args[0] == "--shot-baseline") TestShotReport(false);
            else Run(args[0] == "--snapshots");
            Console.WriteLine("PASS: " + checks + " audit safety regression checks (" + args[0] + ").");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Run(bool snapshotsOnly)
    {
        mod = typeof(HediffComp_MingyuanLifeBurn).Assembly;
        Type snapshot = mod.GetType("MiliraXian.Characters.CombatTargetSnapshot", true);
        rent = snapshot.GetMethod("Rent");
        giveBack = snapshot.GetMethod("Return");
        if (snapshotsOnly)
        {
            TestMutationAndNestedSnapshots();
            TestRadialSnapshot();
            TestFieldPulse(2);
            TestPillarBuildings();
        }
        else
        {
            TestShotReport();
            TestLifeBurnReset();
            TestSkyfallReset();
            TestLoadoutReset();
            TestRebirthGuards();
            TestMingyuanMechanics();
            TestRaidJobLogging();
        }
    }

    private static List<T> Rent<T>(IEnumerable<T> source)
    {
        return (List<T>)rent.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { source });
    }

    private static void Return<T>(List<T> values)
    {
        giveBack.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { values });
    }

    private static void TestMutationAndNestedSnapshots()
    {
        var live = new List<int> { 1, 2, 3, 4 };
        var outer = Rent(live);
        var visited = new List<int>();
        for (int i = 0; i < outer.Count; i++)
        {
            int target = outer[i];
            live.Remove(target);
            var inner = Rent(new[] { 91, 92 });
            Check(!object.ReferenceEquals(outer, inner), "nested effects own separate lists");
            Return(inner);
            visited.Add(target);
        }
        Check(string.Join(",", visited) == "1,2,3,4" && live.Count == 0, "despawning every target does not skip the next target");
        Return(outer);
        Check(outer.Count == 0, "returned snapshots release references");
        var reused = Rent(new[] { 7 });
        Check(reused.Count == 1 && reused[0] == 7, "pooled snapshot contains no stale entries");
        Return(reused);
        int freeBefore = SimplePool<List<object>>.FreeItemsCount;
        bool threw = false;
        try { Rent(ThrowingTargets()); }
        catch (TargetInvocationException ex) { threw = ex.InnerException is InvalidOperationException; }
        Check(threw, "enumeration failures propagate");
        Check(SimplePool<List<object>>.FreeItemsCount >= freeBefore, "failed collection returns rented list");
        var empty = Rent(new object[0]);
        Check(empty.Count == 0, "failed collection releases partial target references");
        Return(empty);
    }

    private static IEnumerable<object> ThrowingTargets()
    {
        yield return new object();
        throw new InvalidOperationException("Deliberate callback failure");
    }

    private static void TestRadialSnapshot()
    {
        Map map = (Map)FormatterServices.GetUninitializedObject(typeof(Map));
        map.info = new MapInfo { Size = new IntVec3(5, 1, 5) };
        map.cellIndices = new CellIndices(map);
        map.thingGrid = new ThingGrid(map);
        IntVec3 center = new IntVec3(2, 0, 2);
        var smallDef = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
        smallDef.size = new IntVec2(1, 1);
        var largeDef = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
        largeDef.size = new IntVec2(2, 2);
        Thing a = new Thing { def = smallDef };
        Thing b = new Thing { def = smallDef };
        Thing large = new Thing { def = largeDef };
        List<Thing> centerThings = map.thingGrid.ThingsListAt(center);
        centerThings.Add(a);
        centerThings.Add(b);
        centerThings.Add(large);
        map.thingGrid.ThingsListAt(center + new IntVec3(1, 0, 0)).Add(large);
        var actual = Rent(GenRadial.RadialDistinctThingsAround(center, map, 1.5f, true));
        Check(actual.Count == 3 && actual[0] == a && actual[1] == b && actual[2] == large,
            "actual game radial order and multi-cell deduplication are preserved");
        foreach (Thing target in actual) centerThings.Remove(target);
        Check(centerThings.Count == 0, "same-cell removals cannot skip snapshot targets");
        Return(actual);
    }

    private static void TestShotReport(bool requireNoAllocation = true)
    {
        Type reportType = typeof(ShotReport);
        FieldInfo targetField = reportType.GetField("target", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo sizeField = reportType.GetField("factorFromTargetSize", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(reportType.IsValueType && targetField.FieldType == typeof(TargetInfo) && sizeField.FieldType == typeof(float),
            "installed ShotReport is a struct with the expected private fields");
        object boxed = default(ShotReport);
        sizeField.SetValue(boxed, 1.25f);
        ShotReport report = (ShotReport)boxed;
        Patch_MXNeiyuShield_RangedDodge.Postfix(ref report);
        Check((float)sizeField.GetValue(report) == 1.25f, "non-pawn shot report is unchanged");

        Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
        Current.Game.tickManager = (TickManager)FormatterServices.GetUninitializedObject(typeof(TickManager));
        typeof(TickManager).GetField("ticksGameInt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Current.Game.tickManager, 200);
        Pawn pawn = BarePawn();
        var hediff = new HediffWithComps { pawn = pawn };
        var shield = new HediffComp_MXNeiyuCountShield { parent = hediff, props = new HediffCompProperties_MXNeiyuCountShield() };
        hediff.comps = new List<HediffComp> { shield };
        var shieldDef = (HediffDef)FormatterServices.GetUninitializedObject(typeof(HediffDef));
        shieldDef.defName = "MXNL_NeiyuShield";
        hediff.def = shieldDef;
        typeof(MXNeiyuShieldUtility).GetField("shieldHediffDef", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, shieldDef);
        pawn.health.hediffSet = new HediffSet(pawn);
        pawn.health.hediffSet.hediffs.Add(hediff);
        Set(shield, "stage", 3);
        Set(shield, "phase3StoredDamage", 300f);
        Set(shield, "phase3AbsorbUntilTick", 100);
        Set(shield, "phase3EndTick", 1000);
        targetField.SetValue(boxed, new TargetInfo(pawn));
        report = (ShotReport)boxed;
        MXNeiyuStage3Profile profile;
        Check(shield.TryGetStage3Profile(out profile) && profile.rangedDodgeBonusPct > 0f, "active shield fixture supplies dodge bonus");
        Patch_MXNeiyuShield_RangedDodge.Postfix(ref report);
        Check((float)sizeField.GetValue(report) == 1.25f * (1f - profile.rangedDodgeBonusPct), "struct mutation writes through to the returned shot report");
        shield.CompPostPostRemoved();
        pawn.health.hediffSet.hediffs.Clear();
        report = (ShotReport)boxed;
        Patch_MXNeiyuShield_RangedDodge.Postfix(ref report);
        Check((float)sizeField.GetValue(report) == 1.25f, "removed shield no longer changes hit chance");

        var allocationMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");
        if (allocationMethod != null)
        {
            var allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), allocationMethod);
            report = default(ShotReport);
            for (int i = 0; i < 1000; i++) Patch_MXNeiyuShield_RangedDodge.Postfix(ref report);
            long before = allocated();
            for (int i = 0; i < 10000; i++) Patch_MXNeiyuShield_RangedDodge.Postfix(ref report);
            long bytes = allocated() - before;
            Check(requireNoAllocation ? bytes == 0 : bytes > 0, "unaffected shot report allocation matches the selected baseline/current mode");
            Console.WriteLine("ShotReport direct-call allocation: " + bytes + " bytes / 10000 calls (desktop CLR, not Dubs PA).");
        }
    }

    private sealed class RemovableBuilding : Thing
    {
        public List<Thing> CellThings;
        public int DestroyCalls;

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            DestroyCalls++;
            CellThings.Remove(this);
            Set(this, "mapIndexOrState", (sbyte)-2, typeof(Thing));
        }
    }

    private static void TestFieldPulse(int expectedDestroyed)
    {
        // Real production Pulse + real GenRadial; only destruction's engine side
        // effects are replaced by a fixture that removes itself from the grid.
        Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
        var map = (Map)FormatterServices.GetUninitializedObject(typeof(Map));
        map.info = new MapInfo { Size = new IntVec3(5, 1, 5) };
        map.cellIndices = new CellIndices(map);
        map.thingGrid = new ThingGrid(map);
        Set(Current.Game, "maps", new List<Map> { map });
        IntVec3 center = new IntVec3(2, 0, 2);
        List<Thing> live = map.thingGrid.ThingsListAt(center);
        var def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
        def.size = new IntVec2(1, 1);
        def.category = ThingCategory.Building;
        var a = new RemovableBuilding { def = def, CellThings = live };
        var b = new RemovableBuilding { def = def, CellThings = live };
        live.Add(a);
        live.Add(b);
        Set(a, "mapIndexOrState", (sbyte)0, typeof(Thing));
        Set(b, "mapIndexOrState", (sbyte)0, typeof(Thing));
        var parent = new ThingWithComps { def = def };
        Set(parent, "mapIndexOrState", (sbyte)0, typeof(Thing));
        Set(parent, "positionInt", center, typeof(Thing));
        var field = new CompMingyuanBurningField {
            parent = parent,
            props = new CompProperties_MingyuanBurningField { radius = 1.5f, destroyBuildings = true }
        };
        typeof(CompMingyuanBurningField).GetMethod("Pulse", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(field, null);
        int destroyed = a.DestroyCalls + b.DestroyCalls;
        Check(destroyed == expectedDestroyed, "production field destroys expected same-cell targets");
        Check(a.DestroyCalls <= 1 && b.DestroyCalls <= 1, "production field does not double-process a target");
        Console.WriteLine("Production field same-cell destruction: " + destroyed + " / 2 targets.");
    }

    private static void TestLifeBurnReset()
    {
        Type type = typeof(HediffComp_MingyuanLifeBurn);
        var received = (IDictionary)type.GetField("DeathTransferLastReceivedTicks", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        received[77] = 900000;
        var comp = new HediffComp_MingyuanLifeBurn { props = new HediffCompProperties_MingyuanLifeBurn() };
        Pawn pawn = BarePawn();
        pawn.thingIDNumber = 77;
        MethodInfo canReceive = type.GetMethod("CanReceiveDeathTransfer", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(!(bool)canReceive.Invoke(comp, new object[] { pawn, 10 }), "old game cooldown would block reused pawn ID");
        type.GetField("burstBudgetTick", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 10);
        type.GetField("burstExecutionsThisTick", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 4);
        type.GetMethod("ClearRuntimeState", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        Check(received.Count == 0 && (bool)canReceive.Invoke(comp, new object[] { pawn, 10 }), "cross-game cooldown state is cleared");
        Check((int)type.GetField("burstBudgetTick", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) == -1
            && (int)type.GetField("burstExecutionsThisTick", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) == 0,
            "cross-game burst budget is cleared");
    }

    private static void TestSkyfallReset()
    {
        Type type = mod.GetType("MiliraXian.Characters.Neiyu.NeiyuSkyfallVisualTracker", true);
        IDictionary states = (IDictionary)type.GetField("states", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        Type state = states.GetType().GetGenericArguments()[1];
        states[77] = Activator.CreateInstance(state);
        type.GetMethod("ClearRuntimeState", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        Check(states.Count == 0, "cross-game skyfall state is cleared");
    }

    private static void TestLoadoutReset()
    {
        foreach (string name in new[] { "MiliraXian.Characters.Neiyu.NeiyuEquipmentUtility", "MiliraXian.Characters.Zhaoli.ZhaoliScenarioUtility" })
        {
            Type type = mod.GetType(name, true);
            var pending = (HashSet<int>)type.GetField("PendingLoadoutStabilizationPawnIds", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            pending.Add(77);
            type.GetMethod("ClearRuntimeState", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Check(pending.Count == 0, "loadout stabilization does not survive a game change: " + name);
        }
    }

    private static Pawn BarePawn()
    {
        var pawn = (Pawn)FormatterServices.GetUninitializedObject(typeof(Pawn));
        pawn.stackCount = 1;
        pawn.health = (Pawn_HealthTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_HealthTracker));
        Set(pawn, "mapIndexOrState", (sbyte)-1, typeof(Thing));
        return pawn;
    }

    private static void TestRebirthGuards()
    {
        Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
        var activeMaps = new List<Map>();
        Set(Current.Game, "maps", activeMaps);
        var map = (Map)FormatterServices.GetUninitializedObject(typeof(Map));
        map.info = new MapInfo { Size = new IntVec3(5, 1, 5) };
        Pawn pawn = BarePawn();
        IntVec3 cell = new IntVec3(2, 0, 2);
        Check(!MingyuanRebirthUtility.TryFinishRebirth(pawn, map, cell), "removed map is rejected before resurrection/world-pawn mutation");
        activeMaps.Add(map);
        Check(!MingyuanRebirthUtility.TryFinishRebirth(pawn, map, IntVec3.Invalid), "invalid return cell is rejected before mutation");
        Check(!MingyuanRebirthUtility.TryFinishRebirth(pawn, map, new IntVec3(99, 0, 99)), "out-of-bounds return cell is rejected before mutation");
        Assembly al = Assembly.Load("AriandelLibrary");
        var holder = (IThingHolder)Activator.CreateInstance(al.GetType("AriandelLibrary.VirtualPawnContainer", true));
        pawn.holdingOwner = holder.GetDirectlyHeldThings();
        Check(!MingyuanRebirthUtility.TryFinishRebirth(pawn, map, cell) && pawn.ParentHolder == holder,
            "pending return cannot steal a pawn from AL's recovery container");
    }

    private static void TestMingyuanMechanics()
    {
        float[] inputs = { -1f, 0f, 9.99f, 10f, 19.99f, 20f, 299.99f, 300f, 301f, 500f };
        float[] expected = { 0f, 0f, 0f, 10f, 10f, 20f, 290f, 300f, 300f, 300f };
        for (int i = 0; i < inputs.Length; i++)
            Check(MingyuanUtility.QuantizeSelfBurn(inputs[i]) == expected[i], "Self Burn decade boundary: " + inputs[i]);
        Check(MingyuanUtility.QuantizeSelfBurn(99, 25) == 20, "custom cap cannot award an incomplete decade");

        Pawn absorbed = BarePawn();
        Pawn other = BarePawn();
        // No health tracker side effects: this fixture checks the production queue
        // cancellation, including duplicate/legacy records and unrelated victims.
        absorbed.health = null;
        var timer = new GameComponent_MingyuanTimeBurn(null);
        FieldInfo recordsField = typeof(GameComponent_MingyuanTimeBurn).GetField("records", BindingFlags.NonPublic | BindingFlags.Instance);
        var records = (List<MingyuanTimeBurnRecord>)recordsField.GetValue(timer);
        records.Add(new MingyuanTimeBurnRecord { pawn = absorbed, endTick = 100 });
        records.Add(new MingyuanTimeBurnRecord { pawn = other, endTick = 150 });
        records.Add(new MingyuanTimeBurnRecord { pawn = absorbed, endTick = 200, reducedCast = true });
        HediffDef oldMarker = MX_MingyuanDefOf.MX_Mingyuan_TimeBurnFrozen;
        MX_MingyuanDefOf.MX_Mingyuan_TimeBurnFrozen = new HediffDef();
        try
        {
            Check(timer.Cancel(absorbed), "absorption cancels pending Time Burn");
            Check(records.Count == 1 && records[0].pawn == other, "all target records removed; unrelated target preserved");
            Check(!timer.Cancel(absorbed), "cancellation is idempotent and cannot schedule late damage");
            Check(!timer.Cancel(null), "missing absorption target cannot mutate the queue");
        }
        finally { MX_MingyuanDefOf.MX_Mingyuan_TimeBurnFrozen = oldMarker; }
        Check(!MingyuanRebirthUtility.TryInterceptALRecovery(null), "AL keeps handling null/noneligible recovery");

        var comp = new HediffComp_MingyuanSelfBurn { props = new HediffCompProperties_MingyuanSelfBurn() };
        FieldInfo release = comp.GetType().GetField("ticksToOverburnRelease", BindingFlags.NonPublic | BindingFlags.Instance);
        Check((int)release.GetValue(comp) == 600, "new Overburn timer starts at ten seconds, not immediate release");
    }

    private static void TestPillarBuildings()
    {
        Pawn caster = BarePawn();
        var own = (Faction)FormatterServices.GetUninitializedObject(typeof(Faction));
        own.def = new FactionDef();
        Set(caster, "factionInt", own, typeof(Thing));
        var comp = new CompMingyuanBurningPillarTornado();
        Set(comp, "caster", caster);
        MethodInfo eligible = comp.GetType().GetMethod("CanDamageBuilding", BindingFlags.NonPublic | BindingFlags.Instance);
        var def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
        def.category = ThingCategory.Building;
        def.useHitPoints = true;
        def.passability = Traversability.Impassable;
        var wall = new Thing { def = def, HitPoints = 100 };
        Check(!wall.HostileTo(own), "unclaimed ruins reproduce the original hostility-filter exclusion");
        Check((bool)eligible.Invoke(comp, new object[] { wall }), "production pillar filter accepts unclaimed walls");
        Set(wall, "factionInt", own, typeof(Thing));
        Check(!(bool)eligible.Invoke(comp, new object[] { wall }), "claimed colony walls remain protected");
        foreach (FactionRelationKind relation in new[] { FactionRelationKind.Ally, FactionRelationKind.Neutral, FactionRelationKind.Hostile })
        {
            var faction = (Faction)FormatterServices.GetUninitializedObject(typeof(Faction));
            faction.def = new FactionDef();
            Set(faction, "relations", new List<FactionRelation> { new FactionRelation { other = own, kind = relation } });
            Set(wall, "factionInt", faction, typeof(Thing));
            Check((bool)eligible.Invoke(comp, new object[] { wall }) == (relation == FactionRelationKind.Hostile),
                "production pillar building filter respects " + relation + " ownership");
        }
        Set(wall, "factionInt", null, typeof(Thing));
        def.useHitPoints = false;
        Check(!(bool)eligible.Invoke(comp, new object[] { wall }), "non-damageable ruins are skipped safely");
        def.useHitPoints = true;
        comp.parent = new ThingWithComps { def = def };
        Check(!(bool)eligible.Invoke(comp, new object[] { comp.parent }), "pillar never damages its own core");

    }

    private delegate void JobLogPrefix<TState>(Pawn pawn, Job job, JobCondition condition, ThinkNode giver,
        ThinkTreeDef tree, JobTag? tag, bool fromQueue, out TState state);

    private static void TestRaidJobLogging()
    {
        FieldInfo pawnField = typeof(Pawn_JobTracker).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(pawnField != null && pawnField.FieldType == typeof(Pawn), "installed job tracker supports Harmony pawn field injection");
        Type start = mod.GetType("MiliraXian.Characters.Zhaoli.Patch_Pawn_JobTracker_StartJob_ZhaoliRaidLog", true);
        Type end = mod.GetType("MiliraXian.Characters.Zhaoli.Patch_Pawn_JobTracker_EndCurrentJob_ZhaoliRaidLog", true);
        foreach (MethodInfo method in new[] { start.GetMethod("Prefix"), start.GetMethod("Postfix"), end.GetMethod("Prefix") })
        {
            ParameterInfo injected = method.GetParameters()[0];
            Check(injected.Name == "___pawn" && injected.ParameterType == typeof(Pawn), "job logging patch injects the verified pawn field");
        }
        Type stateType = start.GetNestedType("StartJobLogState", BindingFlags.NonPublic);
        typeof(AuditSafetyRegressionTests).GetMethod("MeasureJobLogging", BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(stateType).Invoke(null, new object[] { start.GetMethod("Prefix") });
        start.GetMethod("Postfix").Invoke(null, new object[] { BarePawn(), null });
        end.GetMethod("Prefix").Invoke(null, new object[] { BarePawn(), JobCondition.None, true });
        Check(true, "untracked job postfix and end hook accept an empty log state");
    }

    private static void MeasureJobLogging<TState>(MethodInfo method) where TState : class
    {
        var prefix = (JobLogPrefix<TState>)Delegate.CreateDelegate(typeof(JobLogPrefix<TState>), method);
        Pawn pawn = BarePawn();
        TState state;
        prefix(pawn, null, JobCondition.None, null, null, null, false, out state);
        Check(state == null, "untracked job starts do not construct a log state or inspect the current job");
        var allocationMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");
        if (allocationMethod == null) return;
        var allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), allocationMethod);
        for (int i = 0; i < 1000; i++) prefix(pawn, null, JobCondition.None, null, null, null, false, out state);
        long before = allocated();
        for (int i = 0; i < 10000; i++) prefix(pawn, null, JobCondition.None, null, null, null, false, out state);
        long bytes = allocated() - before;
        Check(bytes == 0, "rejected job log prefix has no per-call managed allocations");
        Console.WriteLine("Job log rejected-prefix allocation: " + bytes + " bytes / 10000 calls (desktop CLR, not Dubs PA).");
    }

    private static void Set(object obj, string field, object value, Type declaringType = null)
    {
        (declaringType ?? obj.GetType()).GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).SetValue(obj, value);
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("FAIL: " + message);
    }
}
