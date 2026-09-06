using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

// Executes the production snapshot engine, without starting Unity or loading a save.
internal static class CharacterPowerSnapshotTests
{
    private sealed class Sample
    {
        public int count = 73;
        public float factor = 1.73f;
        public List<int> stages = new List<int> { 17, 23 };
    }

    private sealed class ConservativeSample
    {
        public int charges = 108;
        public int cooldown = 4800;
        public float damage = 80f;
        public float bonus = 1.5f;
        public float offset = -.2f;
        public bool mechanic = true;
        public object range;
    }

    private static int Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(args[0]);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, evt) => {
                string name = new AssemblyName(evt.Name).Name + ".dll";
                foreach (string directory in new[] {
                    Path.Combine(root, "1.6/Assemblies"), Path.GetFullPath(Path.Combine(root, "../../RimWorldWin64_Data/Managed")),
                    Path.Combine(root, "packages/Lib.Harmony.2.4.2/lib/net48"),
                    @"E:\SteamLibrary\steamapps\workshop\content\294100\3665997350\1.6\Assemblies" })
                {
                    string file = Path.Combine(directory, name);
                    if (File.Exists(file)) return Assembly.LoadFrom(file);
                }
                return null;
            };
            Assembly assembly = Assembly.LoadFrom(args.Length > 1 ? Path.GetFullPath(args[1])
                : Path.Combine(root, "1.6/Assemblies/MiliraXian_NeiyuLaw.dll"));
            Type profileType = assembly.GetType("MiliraXian.Characters.CharacterPowerProfile", true);
            Type levelType = assembly.GetType("MiliraXian.Characters.Neiyu.CharacterPowerLevel", true);
            object first = Activator.CreateInstance(profileType, true), second = Activator.CreateInstance(profileType, true);
            Sample a = new Sample(), b = new Sample();
            var originalStages = a.stages;
            var balancedStages = new List<int> { 2 };
            var decorativeStages = new List<int>();
            MethodInfo field = profileType.GetMethod("Field"), set = profileType.GetMethod("SetLevel");
            field.Invoke(first, new object[] { a, "count", 24f, 16f }); // float literals -> int field.
            field.Invoke(first, new object[] { a, "factor", 1.12f, 1f });
            field.Invoke(first, new object[] { a, "stages", balancedStages, decorativeStages });
            field.Invoke(second, new object[] { b, "count", 31, 18 });
            Check(a.count == 73 && a.factor == 1.73f && a.stages == originalStages, "initial mode changed baseline");
            for (int i = 0; i < 100; i++)
            {
                set.Invoke(first, new[] { Enum.ToObject(levelType, 1) });
                Check(a.count == 24 && a.factor == 1.12f && a.stages == balancedStages, "balanced values");
                Check(b.count == 73, "cross-character leakage");
                set.Invoke(second, new[] { Enum.ToObject(levelType, 2) });
                Check(b.count == 18 && a.count == 24, "independent settings");
                set.Invoke(first, new[] { Enum.ToObject(levelType, 2) });
                Check(a.count == 16 && a.factor == 1f && a.stages == decorativeStages, "decorative values");
                set.Invoke(first, new[] { Enum.ToObject(levelType, 0) });
                set.Invoke(second, new[] { Enum.ToObject(levelType, 0) });
                Check(a.count == 73 && a.factor == 1.73f && a.stages == originalStages && b.count == 73, "baseline restore/drift");
            }
            set.Invoke(first, new[] { Enum.ToObject(levelType, 1) });
            set.Invoke(first, new[] { Enum.ToObject(levelType, 99) });
            Check(a.count == 73, "invalid enum fallback");
            TestConservativeTuning(assembly, profileType, levelType);
            TestNeiyuTunings(assembly, levelType);
            Console.WriteLine("PASS: production snapshot engines; 100 complete tier cycles each, exact restoration, conservative scaling, intact mechanics, typed ranges, independent profiles, invalid-setting fallback.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void TestConservativeTuning(Assembly assembly, Type profileType, Type levelType)
    {
        object profile = Activator.CreateInstance(profileType, true);
        MethodInfo scale = profileType.GetMethod("ScaleField"), keep = profileType.GetMethod("KeepField"), set = profileType.GetMethod("SetLevel");
        Type rangeType = Assembly.Load("Assembly-CSharp").GetType("Verse.IntRange", true);
        var sample = new ConservativeSample();
        sample.range = Activator.CreateInstance(rangeType, new object[] { 3, 5 });
        object originalRange = sample.range;
        object sealedRange = Activator.CreateInstance(rangeType, new object[] { 1, 1 });
        scale.Invoke(profile, new object[] { sample, "charges", .9f, 0, 0f });
        scale.Invoke(profile, new object[] { sample, "cooldown", 1.2f, 18000, 0f });
        scale.Invoke(profile, new object[] { sample, "damage", .85f, 10f, 0f });
        scale.Invoke(profile, new object[] { sample, "bonus", .85f, 1f, 1f });
        scale.Invoke(profile, new object[] { sample, "offset", .85f, 0f, 0f });
        scale.Invoke(profile, new object[] { sample, "range", 1f, sealedRange, 0f });
        keep.Invoke(profile, new object[] { sample, "mechanic", false });
        for (int i = 0; i < 100; i++)
        {
            set.Invoke(profile, new[] { Enum.ToObject(levelType, 1) });
            Check(sample.charges == 97 && sample.cooldown == 5760, "integer rounding/cooldown scaling");
            Check(Near(sample.damage, 68f) && Near(sample.bonus, 1.425f) && Near(sample.offset, -.17f), "damage versus bonus scaling");
            Check(sample.mechanic && sample.range.Equals(originalRange), "tier two disabled a mechanic or changed a count range");
            set.Invoke(profile, new[] { Enum.ToObject(levelType, 2) });
            Check(!sample.mechanic && sample.charges == 0 && sample.cooldown == 18000
                && sample.damage == 10f && sample.bonus == 1f && sample.offset == 0f && sample.range.Equals(sealedRange), "tier three changed");
            set.Invoke(profile, new[] { Enum.ToObject(levelType, 0) });
            Check(sample.mechanic && sample.charges == 108 && sample.cooldown == 4800
                && sample.damage == 80f && sample.bonus == 1.5f && sample.offset == -.2f && sample.range == originalRange, "tier one drift");
        }

        Type mingyuan = assembly.GetType("MiliraXian.Characters.Mingyuan.MingyuanPowerBalance", true);
        MethodInfo factor = mingyuan.GetMethod("StageFactor", BindingFlags.NonPublic | BindingFlags.Static);
        Check(Near((float)factor.Invoke(null, new object[] { "BurningBody", "IncomingDamageFactor", .01f }), .015f), "extreme damage resistance was flattened");
        Check(Near((float)factor.Invoke(null, new object[] { "SelfBurn", "MeleeCooldownFactor", .25f }), 1f / 3.55f), "attack speed was weakened twice");
        Check((float)factor.Invoke(null, new object[] { "SelfBurn", "IncomingDamageFactor", 0f }) == 0f, "immunity was removed");
        Type numbers = assembly.GetType("MiliraXian.Characters.ConservativePowerTuning", true);
        MethodInfo cooldown = numbers.GetMethod("RemapCooldown");
        Check((int)cooldown.Invoke(null, new object[] { 30000, 60000, 5760 }) == 2880, "legacy cooldown migration lost progress");
        Check((int)cooldown.Invoke(null, new object[] { 1440, 5760, 4800 }) == 1200, "reverse cooldown conversion restarted the cooldown");
        Check((int)cooldown.Invoke(null, new object[] { 0, 4800, 5760 }) == 0, "expired cooldown restarted");
        Type transition = assembly.GetType("MiliraXian.Characters.GameComponent_CharacterPowerTransition", true);
        Check(transition.GetField("CooldownDuration", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) != null,
            "game cooldown field accessor could not initialize");
    }

    private static bool Near(float actual, float expected) { return Math.Abs(actual - expected) < .00001f; }

    private static void TestNeiyuTunings(Assembly assembly, Type levelType)
    {
        Type neiyu = assembly.GetType("MiliraXian.Characters.Neiyu.NeiyuPowerBalance", true);
        MethodInfo add = neiyu.GetMethod("AddScaled", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo set = neiyu.GetMethod("SetLevel");
        var sample = new ConservativeSample();
        add.MakeGenericMethod(typeof(ConservativeSample), typeof(int)).Invoke(null, new object[] {
            sample, new Func<int>(() => sample.charges), new Action<int>(value => sample.charges = value), .9f, 0, 0f });
        add.MakeGenericMethod(typeof(ConservativeSample), typeof(float)).Invoke(null, new object[] {
            sample, new Func<float>(() => sample.damage), new Action<float>(value => sample.damage = value), .85f, 10f, 0f });
        // The harness has no Def loader. Enable only the two registered production tunings.
        neiyu.GetField("defsInitialized", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, true);
        for (int i = 0; i < 100; i++)
        {
            set.Invoke(null, new[] { Enum.ToObject(levelType, 1) });
            Check(sample.charges == 97 && Near(sample.damage, 68f), "Neiyu conservative tuning");
            set.Invoke(null, new[] { Enum.ToObject(levelType, 2) });
            Check(sample.charges == 0 && sample.damage == 10f, "Neiyu tier three changed");
            set.Invoke(null, new[] { Enum.ToObject(levelType, 0) });
            Check(sample.charges == 108 && sample.damage == 80f, "Neiyu baseline restoration drift");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
