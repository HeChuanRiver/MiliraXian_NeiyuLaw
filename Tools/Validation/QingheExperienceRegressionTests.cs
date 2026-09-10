using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;

internal static class QingheExperienceRegressionTests
{
    private static int checks;

    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, evt) => {
            string fileName = new AssemblyName(evt.Name).Name + ".dll";
            foreach (string directory in args)
            {
                string file = Path.Combine(directory, fileName);
                if (File.Exists(file)) return Assembly.LoadFrom(file);
            }
            return null;
        };
        try
        {
            Run();
            Console.WriteLine("PASS: " + checks + " Qinghe crafting experience checks.");
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
        // Permit the recipe DefOf comparison without logging through an absent Unity runtime.
        typeof(DefOfHelper).GetField("bindingNow", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        // Fixed market values isolate the production XP method from Unity and live stat caches.
        for (int quality = 0; quality <= 6; quality++)
        {
            var product = Product("SculptureLarge", 295f, 1, (QualityCategory)quality);
            var wrapped = Wrap(product);
            Near(Experience(product), Experience(wrapped), "packaging preserves quality " + quality);
        }
        Near(561.47f, Experience(Wrap(Product("SculptureLarge", 295f, 1, QualityCategory.Excellent))), "excellent sculpture");
        Near(1479.66f, Experience(Wrap(Product("Piano", 1010f, 1, QualityCategory.Excellent)), 50000f), "excellent piano");
        Near(86.97962f, Experience(Product("Apparel_CowboyHat", 65.97f, 1, QualityCategory.Excellent), 1800f), "short recipe reward");
        Near(62.64f, Experience(Product("ComponentIndustrial", 32f, 1, null), 5000f), "qualityless component");
        Near(140.4f, Experience(Product("ComponentIndustrial", 32f, 10, null), 5000f), "stack shares one work reward");
        Near(62.64f, Experience(Wrap(Product("QualitylessBuilding", 32f, 1, null)), 5000f), "qualityless wrapper fallback");
        Near(48813.8f, Experience(Wrap(Product("ExpensiveArt", 100000f, 1, QualityCategory.Legendary)), 1000000f), "work and value caps");

        var component = Product("ComponentIndustrial", 32f, 1, null);
        Near(90f, Experience(component, 15000f) - Experience(component, 5000f), "labor distinguishes equal-value products");
        var byproduct = Product("OtherProduct", 50f, 1, null);
        var recipe = Recipe(component, 5000f);
        recipe.products.Add(new ThingDefCountClass { thingDef = byproduct.def, count = 1 });
        Near(76.14f, Experience(component, recipe) + Experience(byproduct, recipe), "multiple outputs do not duplicate work");
        recipe.products.Clear();
        Near(8.64f, Experience(component, recipe), "dynamic output does not duplicate work");

        float cumulative = 0f;
        float previous = 0f;
        for (int level = 0; level < HediffComp_QingheGraceSync.MaxGraceLevel; level++)
        {
            float required = HediffComp_QingheGraceSync.GetRequiredProgress(level);
            if (required <= previous) throw new InvalidOperationException("requirements must increase at level " + level);
            previous = required;
            cumulative += required;
            switch (level + 1)
            {
                case 6: Near(800f, cumulative, "early unlock budget"); break;
                case 12: Near(5000f, cumulative, "small colony budget"); break;
                case 18: Near(30000f, cumulative, "advanced colony budget"); break;
                case 24: Near(240000f, cumulative, "endgame budget"); break;
            }
        }
        Near(60f, HediffComp_QingheGraceSync.GetRequiredProgress(-1), "negative level clamps to start");
        Near(0f, HediffComp_QingheGraceSync.GetRequiredProgress(24), "maximum level has no requirement");
        Near(0f, HediffComp_QingheGraceSync.GetRequiredProgress(25), "above maximum has no requirement");
    }

    private static RecipeDef Recipe(Thing product, float work)
    {
        var recipe = (RecipeDef)FormatterServices.GetUninitializedObject(typeof(RecipeDef));
        recipe.workAmount = work;
        recipe.products = new List<ThingDefCountClass> {
            new ThingDefCountClass { thingDef = product.GetInnerIfMinified().def, count = product.stackCount }
        };
        return recipe;
    }

    private static float Experience(Thing product, float work = 21000f)
    {
        return Experience(product, Recipe(product, work));
    }

    private static float Experience(Thing product, RecipeDef recipe)
    {
        MethodInfo calculate = typeof(MX_QH_HediffUtility).GetMethod(
            "CalculateDivineGraceProgressFromCraft", BindingFlags.Static | BindingFlags.NonPublic);
        return (float)calculate.Invoke(null, new object[] { recipe, product });
    }

    private static FixedValueProduct Product(string name, float value, int count, QualityCategory? quality)
    {
        var product = new FixedValueProduct { def = Definition(name), Value = value, stackCount = count };
        if (quality.HasValue)
        {
            var comp = new CompQuality { parent = product };
            typeof(CompQuality).GetField("qualityInt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(comp, quality.Value);
            typeof(ThingWithComps).GetField("comps", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(product, new List<ThingComp> { comp });
        }
        return product;
    }

    private static FixedValueMinified Wrap(Thing product)
    {
        var wrapper = new FixedValueMinified { def = Definition("MinifiedThing"), stackCount = 1 };
        // Populate the fixture without invoking world notifications that require a running game.
        ((ThingOwner<Thing>)wrapper.GetDirectlyHeldThings()).InnerListForReading.Add(product);
        return wrapper;
    }

    private static ThingDef Definition(string name)
    {
        var def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
        def.defName = name;
        def.category = ThingCategory.Item;
        def.stackLimit = 75;
        return def;
    }

    private static void Near(float expected, float actual, string label)
    {
        checks++;
        if (Math.Abs(expected - actual) > Math.Max(0.001f, Math.Abs(expected) * 0.000001f))
            throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual);
    }

    private sealed class FixedValueProduct : ThingWithComps
    {
        public float Value;
        public override float MarketValue { get { return Value; } }
    }

    private sealed class FixedValueMinified : MinifiedThing
    {
        public override float MarketValue { get { return InnerThing.MarketValue; } }
    }
}
