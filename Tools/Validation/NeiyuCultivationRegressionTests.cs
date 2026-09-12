using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using MiliraXian.Characters.Biography;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.Neiyu.Cultivation;
using RimWorld;
using UnityEngine;
using Verse;

internal static class NeiyuCultivationRegressionTests
{
    private static int checks;
    private static Type service, power;
    private static readonly BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, evt) => {
            string file = new AssemblyName(evt.Name).Name + ".dll";
            foreach (string directory in args)
                if (File.Exists(Path.Combine(directory, file))) return Assembly.LoadFrom(Path.Combine(directory, file));
            return null;
        };
        try
        {
            if (args[0] == "--xml") TestXmlCosts(args[1]); else Run();
            Console.WriteLine("PASS: " + checks + " cultivation production-DLL checks."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Run()
    {
        typeof(DefOfHelper).GetField("bindingNow", AnyStatic).SetValue(null, true);
        Assembly assembly = typeof(Dialog_NeiyuCultivation).Assembly;
        service = assembly.GetType("MiliraXian.Characters.Neiyu.Cultivation.CultivationService", true);
        power = assembly.GetType("MiliraXian.Characters.Neiyu.Cultivation.CultivationPower", true);
        Game game = Bare<Game>();
        game.tickManager = Bare<TickManager>();
        game.components = new List<GameComponent>();
        Current.Game = game;
        BiographyDefOf.MX_BiographyTracker = new HediffDef { defName = "MX_BiographyTracker" };

        Pawn a = Pawn("MiliraXian_Neiyu"), b = Pawn("MiliraXian_Neiyu"), other = Pawn("MiliraXian_Qinghe");
        Near(0, Rank(a, CultivationBranch.Wing), "old save without tracker starts at zero");
        var tracker = Attach(a);
        var second = Attach(b);
        tracker.SetProgress("legacy_story_counter", 123);
        for (int branch = 0; branch < 4; branch++)
        {
            Near(0, Rank(a, (CultivationBranch)branch), "old tracker missing new rank key");
            SetRank(tracker, (CultivationBranch)branch, branch % 3 + 1);
            Near(branch % 3 + 1, Rank(a, (CultivationBranch)branch), "independent branch state");
            Near(0, Rank(b, (CultivationBranch)branch), "no state leaks between Neiyu pawns");
        }
        Near(123, tracker.GetProgress("legacy_story_counter"), "legacy biography progress retained");
        Near(0, Rank(other, CultivationBranch.Wing), "other character does not gain cultivation");
        SetRank(tracker, CultivationBranch.Wing, float.NaN);
        Near(0, Rank(a, CultivationBranch.Wing), "corrupt NaN progress cannot unlock powers");
        SetRank(tracker, CultivationBranch.Wing, float.PositiveInfinity);
        Near(0, Rank(a, CultivationBranch.Wing), "corrupt infinite progress cannot unlock powers");
        SetRank(tracker, CultivationBranch.Wing, -1);
        Near(0, Rank(a, CultivationBranch.Wing), "negative progress");

        var tool = new Tool { power = 64f, armorPenetration = 1.2f };
        Call(power, "RegisterTool", tool, 10f, .12f);
        var balance = assembly.GetType("MiliraXian.Characters.Neiyu.NeiyuPowerBalance", true);
        foreach (CharacterPowerLevel level in new[] { CharacterPowerLevel.Original, CharacterPowerLevel.Balanced })
        {
            Call(balance, "SetLevel", level);
            tool.power = level == CharacterPowerLevel.Original ? 64f : 54.4f;
            tool.armorPenetration = level == CharacterPowerLevel.Original ? 1.2f : 1.14f;
            float previous = 0;
            for (int rank = 0; rank <= 3; rank++)
            {
                SetRank(tracker, CultivationBranch.Wing, rank);
                float damage = tool.power * (float)Call(power, "MeleeFactor", tool, a, false);
                Check(damage > previous, "damage grows monotonically in both power tiers");
                previous = damage;
                if (rank == 0) Near(10, damage, "minimal sword damage");
                if (rank == 3) Near(tool.power, damage, "full cultivation restores selected ceiling exactly");
                Near(10, tool.power * (float)Call(power, "MeleeFactor", tool, b, false), "shared tool remains minimal for another pawn");
            }
            Near(level == CharacterPowerLevel.Original ? 64 : 54.4f, tool.power, "progress never mutates shared tool Def");
        }
        SetRank(tracker, CultivationBranch.Wing, 3);
        Call(balance, "SetLevel", CharacterPowerLevel.Decorative);
        Near(10, tool.power * (float)Call(power, "MeleeFactor", tool, a, false), "decorative setting overrides unlocked powers");
        Near(3, Rank(a, CultivationBranch.Wing), "decorative setting preserves progression");
        Call(balance, "SetLevel", CharacterPowerLevel.Original);

        TestGear(a, tracker);
        TestShield(a, tracker);
        TestCounts();
        TestLayout(assembly);

        // Reconstruct the serialized dictionary, as Scribe_Collections does on load.
        var saved = new Dictionary<string, float>((Dictionary<string, float>)typeof(Hediff_BiographyTracker)
            .GetField("customProgress", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tracker));
        typeof(Hediff_BiographyTracker).GetField("customProgress", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(second, saved);
        for (int branch = 0; branch < 4; branch++) Near(Rank(a, (CultivationBranch)branch), Rank(b, (CultivationBranch)branch), "reconstructed persisted state");
        a.health.hediffSet.hediffs.Remove(tracker);
        Call(typeof(BiographyFrameworkUtility), "Invalidate", a);
        Near(0, Rank(a, CultivationBranch.Wing), "removed tracker cannot leave cached powers");
    }

    private static void TestGear(Pawn pawn, Hediff_BiographyTracker tracker)
    {
        var stat = new StatDef { defName = "TestArmor" };
        var offset = new StatDef { defName = "TestMove" };
        var def = Bare<ThingDef>();
        def.defName = "TestNeiyuApparel"; def.apparel = new ApparelProperties();
        def.statBases = new List<StatModifier> { new StatModifier { stat = stat, value = 1.7f } };
        def.equippedStatOffsets = new List<StatModifier> { new StatModifier { stat = offset, value = 1.2f } };
        Call(power, "RegisterStat", def, stat, .15f, false);
        Call(power, "RegisterStat", def, offset, 0f, true);
        var gear = Bare<Apparel>();
        gear.def = def;
        pawn.apparel = new Pawn_ApparelTracker(pawn);
        var holder = new ThingOwner<Apparel>(pawn.apparel);
        typeof(Thing).GetField("holdingOwner", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gear, holder);
        for (int rank = 0; rank <= 3; rank++)
        {
            SetRank(tracker, CultivationBranch.Law, rank);
            float fraction = rank == 0 ? 0 : rank == 1 ? .25f : rank == 2 ? .6f : 1;
            float value = 1.7f;
            stat.parts[0].TransformValue(StatRequest.For(gear), ref value);
            Near(.15f + 1.55f * fraction, value, "actual native StatPart armor value");
            value = 1.4f; // +0.2 from another source must remain.
            offset.parts[0].TransformValue(StatRequest.For(gear), ref value);
            Near(.2f + 1.2f * fraction, value, "gear offset preserves other contributions");
        }
        Near(1.7f, def.statBases[0].value, "gear Def ceiling unchanged");
        Near(1.2f, def.equippedStatOffsets[0].value, "gear offset ceiling unchanged");
    }

    private static void TestShield(Pawn pawn, Hediff_BiographyTracker tracker)
    {
        var parent = new HediffWithComps { pawn = pawn };
        var shield = new HediffComp_MXNeiyuCountShield { parent = parent, props = new HediffCompProperties_MXNeiyuCountShield() };
        Set(shield, "stage", 3); Set(shield, "phase2Charges", 108); Set(shield, "phase3StoredDamage", 9999f);
        Set(shield, "phase3AbsorbUntilTick", 10000); Set(shield, "phase3EndTick", 20000); Set(shield, "weakUntilTick", 30000);
        SetRank(tracker, CultivationBranch.Halo, 0);
        Normalize(shield);
        Check(shield.Stage == 1 && shield.Phase2Charges == 0 && shield.Phase3StoredDamage == 0 && shield.WeakUntilTick == -1,
            "legacy high shield migrates without a buff or weakness");
        SetRank(tracker, CultivationBranch.Halo, 1);
        Set(shield, "stage", 2); Set(shield, "phase2Charges", 108);
        Normalize(shield);
        Check(shield.Stage == 2 && shield.Phase2Charges == 6 && shield.Phase2MaxCharges == 6, "old layers clamp to learned ceiling");
        Set(shield, "phase2Charges", 2);
        SetRank(tracker, CultivationBranch.Halo, 2);
        Normalize(shield);
        Check(shield.Phase2Charges == 2 && shield.Phase2MaxCharges == 18, "rank advance does not refill damaged shield");
        SetRank(tracker, CultivationBranch.Halo, 3);
        Normalize(shield);
        Check(shield.Phase2Charges == 2 && shield.Phase2MaxCharges == 108, "full rank recovers ceiling without free layers");
        Check((bool)Call(power, "StageThreeEnabled", pawn), "full rank enables existing stage-three path");
    }

    private static void TestCounts()
    {
        int[] splits = { 1, 4, 8, 16 }, barrages = { 4, 24, 54, 108 };
        for (int rank = 0; rank <= 3; rank++)
        {
            Near(splits[rank], (int)Call(power, "ArrowCount", rank, 16, 1, 4, 8), "split arrows at each rank");
            Near(barrages[rank], (int)Call(power, "ArrowCount", rank, 108, 4, 24, 54), "barrage at each rank");
            Near(1, (int)Call(power, "ArrowCount", rank, 1, 1, 4, 8), "low configured ceiling respected");
        }
    }

    private static void TestXmlCosts(string path)
    {
        DeepProfiler.enabled = false; // No Unity preferences singleton in this managed fixture.
        var doc = new XmlDocument(); doc.Load(path);
        foreach (XmlNode node in doc.SelectNodes("/Defs/*/costs/*"))
        {
            Check(node.Name != "li", "material uses the native named-element format");
            var cost = new ThingDefCountClass();
            cost.LoadDataFromXmlCustom(node);
            Near(int.Parse(node.InnerText), cost.count, "native ThingDefCountClass XML reader parses material quantity");
        }
    }

    private static void TestLayout(Assembly assembly)
    {
        Type view = assembly.GetType("MiliraXian.Characters.Neiyu.Cultivation.CultivationView");
        foreach (Vector2 screen in new[] { new Vector2(640,360), new Vector2(854,480), new Vector2(1280,720), new Vector2(1920,1080), new Vector2(3440,1440) })
        {
            Vector2 size = (Vector2)Call(typeof(Dialog_NeiyuCultivation), "FitSize", screen.x, screen.y);
            Check(size.x < screen.x && size.y < screen.y, "window fits effective resolution " + screen);
            float width = size.x - 48;
            if (!(bool)Call(view, "SingleColumn", width))
            {
                float left = (float)Call(view, "ListWidth", width);
                Check(left >= 225 && width - left - 12 >= 350, "two-column reading width");
            }
        }
    }

    private static void Normalize(HediffComp_MXNeiyuCountShield shield) =>
        shield.GetType().GetMethod("NormalizeForPowerLevelChange", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(shield, new object[] { 100 });
    private static float Rank(Pawn pawn, CultivationBranch branch) => (int)Call(service, "Rank", pawn, branch);
    private static void SetRank(Hediff_BiographyTracker tracker, CultivationBranch branch, float value) =>
        tracker.SetProgress(new[] { "cultivation_wing", "cultivation_arrow", "cultivation_halo", "cultivation_law" }[(int)branch], value);
    private static Pawn Pawn(string kind)
    {
        var pawn = Bare<Pawn>();
        pawn.def = Bare<ThingDef>();
        pawn.def.race = new RaceProperties { intelligence = Intelligence.Humanlike };
        pawn.kindDef = new PawnKindDef { defName = kind };
        pawn.health = new Pawn_HealthTracker(pawn);
        return pawn;
    }
    private static Hediff_BiographyTracker Attach(Pawn pawn)
    {
        var tracker = new Hediff_BiographyTracker { pawn = pawn, def = BiographyDefOf.MX_BiographyTracker };
        pawn.health.hediffSet.hediffs.Add(tracker); return tracker;
    }
    private static object Call(Type type, string name, params object[] values)
    {
        foreach (var method in type.GetMethods(AnyStatic))
        {
            if (method.Name != name || method.GetParameters().Length != values.Length) continue;
            bool match = true; var parameters = method.GetParameters();
            for (int i = 0; i < values.Length; i++) if (values[i] != null && !parameters[i].ParameterType.IsInstanceOfType(values[i])) match = false;
            if (match) return method.Invoke(null, values);
        }
        throw new MissingMethodException(type.Name, name);
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static void Near(float expected, float actual, string message) => Check(Math.Abs(expected - actual) < .0001f, message + " (" + actual + ")");
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
}
