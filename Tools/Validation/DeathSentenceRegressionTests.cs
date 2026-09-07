using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using MiliraXian.Characters;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using Verse;

// Runs the compiled production code against real RimWorld/AL managed types.
// Unity and a live save are not started; immune targets deliberately have no
// health graph, so accidentally reaching injury, execution or rewards fails the test.
internal static class DeathSentenceRegressionTests
{
    private static readonly string[] ImmuneKinds = {
        "MiliraXian_Neiyu", "MiliraXian_Qinghe", "MiliraXian_Zhaoli", "MiliraXian_Mingyuan"
    };
    private static int checks;

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
            Console.WriteLine("PASS: " + checks + " death-sentence regression checks against production DLL and local AL.");
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
        Assembly mod = typeof(Hediff_ZhaoliDeathSentenceResult).Assembly;
        Type power = mod.GetType("MiliraXian.Characters.Zhaoli.ZhaoliPowerBalance", true);
        MethodInfo setLevel = power.GetMethod("SetLevel");
        Type levelType = setLevel.GetParameters()[0].ParameterType;
        Type utility = mod.GetType("MiliraXian.Characters.Zhaoli.ZhaoliDeathSentenceUtility", true);
        MethodInfo isImmune = utility.GetMethod("IsImmune");
        Check(!(bool)isImmune.Invoke(null, new object[] { null }), "null pawn is not a character");

        for (int tier = 0; tier < 3; tier++)
        {
            setLevel.Invoke(null, new[] { Enum.ToObject(levelType, tier) });
            foreach (string kind in ImmuneKinds)
            {
                Pawn pawn = BarePawn(kind);
                Check((bool)isImmune.Invoke(null, new object[] { pawn }), "immune kind: " + kind);
                float factor = 3f;
                new StatPart_ZhaoliDeathSentenceImmunity().TransformValue(StatRequest.For(pawn), ref factor);
                Check(factor == 0f, "accumulation blocked: " + kind);

                var comp = new HediffComp_ZhaoliDeathSentence {
                    parent = new Hediff_Abnormal { pawn = pawn },
                    props = new HediffCompProperties_ZhaoliDeathSentence()
                };
                Check(comp.CompShouldRemove, "legacy accumulation removable: " + kind);
                comp.NotifyApplied(null, 1f);
                comp.NotifyApplied(BarePawn("MiliraXian_Zhaoli"), 9f);
                Check(pawn.health.hediffSet == null, "no injury or tracker mutation: " + kind);

                var result = new Hediff_ZhaoliDeathSentenceResult { pawn = pawn, def = new HediffDef() };
                result.Initialize(BarePawn("MiliraXian_Zhaoli"), null);
                Check(result.ShouldRemove, "legacy result removable: " + kind);
                result.PostRemoved();
                result.PostRemoved();
                FieldInfo resolved = typeof(Hediff_ZhaoliDeathSentenceResult).GetField("resolved", BindingFlags.NonPublic | BindingFlags.Instance);
                Check((bool)resolved.GetValue(result), "result resolved without execution/rewards: " + kind);

                // A loaded legacy result has not necessarily run Initialize.
                var legacy = new Hediff_ZhaoliDeathSentenceResult { pawn = pawn, def = new HediffDef() };
                legacy.PostRemoved();
                Check((bool)resolved.GetValue(legacy), "uninitialized legacy result safe: " + kind);
            }
        }
        setLevel.Invoke(null, new[] { Enum.ToObject(levelType, 0) });

        foreach (string kind in new[] { "Colonist", "MiliraXian_Ordinary", "AL_OtherSpecialPawn" })
        {
            Pawn pawn = BarePawn(kind);
            Check(!(bool)isImmune.Invoke(null, new object[] { pawn }), "ordinary kind remains susceptible: " + kind);
            float factor = 2f;
            new StatPart_ZhaoliDeathSentenceImmunity().TransformValue(StatRequest.For(pawn), ref factor);
            Check(factor == 2f, "ordinary accumulation multiplier unchanged: " + kind);
            var comp = new HediffComp_ZhaoliDeathSentence { parent = new Hediff_Abnormal { pawn = pawn } };
            Check(!comp.CompShouldRemove, "ordinary accumulation retained: " + kind);
        }
        TestProtectedPawnCleanup();
    }

    private static void TestProtectedPawnCleanup()
    {
        MethodInfo discard = typeof(Hediff_ZhaoliDeathSentenceResult).GetMethod("DiscardExecutedPawn", BindingFlags.NonPublic | BindingFlags.Static);
        discard.Invoke(null, new object[] { null });
        Pawn survivor = BarePawn("AL_OtherSpecialPawn");
        Check(!survivor.Dead && !survivor.Spawned, "survivor fixture");
        discard.Invoke(null, new object[] { survivor });
        Check(!survivor.Discarded && !survivor.Destroyed, "cancelled death must not discard survivor");

        // Exercise the actual container type introduced by the installed AL,
        // without invoking its Unity-dependent StorePawn/NotifyAdded routines.
        Assembly al = Assembly.Load("AriandelLibrary");
        var holder = (IThingHolder)Activator.CreateInstance(al.GetType("AriandelLibrary.VirtualPawnContainer", true));
        survivor.holdingOwner = holder.GetDirectlyHeldThings();
        Check(survivor.ParentHolder == holder, "AL container ownership fixture");
        discard.Invoke(null, new object[] { survivor });
        Check(survivor.ParentHolder == holder && !survivor.Discarded, "AL survivor retains container ownership");

        FieldInfo state = typeof(Pawn_HealthTracker).GetField("healthState", BindingFlags.NonPublic | BindingFlags.Instance);
        state.SetValue(survivor.health, Enum.Parse(state.FieldType, "Dead"));
        discard.Invoke(null, new object[] { survivor });
        Check(survivor.ParentHolder == holder && !survivor.Discarded, "container-owned dead pawn is not stolen by cleanup");
    }

    private static Pawn BarePawn(string kind)
    {
        var pawn = (Pawn)FormatterServices.GetUninitializedObject(typeof(Pawn));
        pawn.stackCount = 1;
        pawn.kindDef = (PawnKindDef)FormatterServices.GetUninitializedObject(typeof(PawnKindDef));
        pawn.kindDef.defName = kind;
        pawn.health = (Pawn_HealthTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_HealthTracker));
        FieldInfo state = typeof(Pawn_HealthTracker).GetField("healthState", BindingFlags.NonPublic | BindingFlags.Instance);
        state.SetValue(pawn.health, Enum.Parse(state.FieldType, "Mobile"));
        typeof(Thing).GetField("mapIndexOrState", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pawn, (sbyte)-1);
        return pawn;
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("FAIL: " + message);
    }
}
