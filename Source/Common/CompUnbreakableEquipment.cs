using HarmonyLib;
using Verse;

namespace MiliraXian.Characters
{
    public class CompProperties_UnbreakableEquipment : CompProperties
    {
        public CompProperties_UnbreakableEquipment()
        {
            compClass = typeof(CompUnbreakableEquipment);
        }
    }

    public class CompUnbreakableEquipment : ThingComp
    {
    }

    [StaticConstructorOnStartup]
    internal static class Patch_UnbreakableEquipment_TakeDamage
    {
        static Patch_UnbreakableEquipment_TakeDamage()
        {
            var harmony = new Harmony("HeChuanRiver.MiliraXian.Characters.UnbreakableEquipment");
            harmony.Patch(
                AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage)),
                prefix: new HarmonyMethod(typeof(Patch_UnbreakableEquipment_TakeDamage), nameof(Prefix)));
        }

        private static bool Prefix(Thing __instance, ref DamageWorker.DamageResult __result)
        {
            ThingWithComps thingWithComps = __instance as ThingWithComps;
            if (thingWithComps == null)
            {
                return true;
            }

            if (thingWithComps.GetComp<CompUnbreakableEquipment>() == null)
            {
                return true;
            }

            __result = new DamageWorker.DamageResult();
            return false;
        }
    }
}
