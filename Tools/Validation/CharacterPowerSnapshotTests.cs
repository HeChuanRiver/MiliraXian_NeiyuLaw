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
            Assembly assembly = Assembly.LoadFrom(Path.Combine(root, "1.6/Assemblies/MiliraXian_NeiyuLaw.dll"));
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
            Console.WriteLine("PASS: production snapshot engine; 100 complete tier cycles, exact scalar/reference restoration, type conversion, independent profiles, invalid-setting fallback.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
