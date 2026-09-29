using HarmonyLib;
using Verse;

namespace MiliraXian.Characters.Common
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

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class Patch_UnbreakableEquipment_TakeDamage
    {
        private static bool Prefix(Thing __instance, ref DamageWorker.DamageResult __result)
        {
            ThingWithComps thingWithComps = __instance as ThingWithComps;
            if (thingWithComps == null)
            {
                return true;
            }

            // Check the damaged equipment itself; apparel damage-absorption callbacks also protect its wearer.
            if (thingWithComps.GetComp<CompUnbreakableEquipment>() == null
                && thingWithComps.GetComp<CompPawnKindEquipmentDurability>()?.PreventsDurabilityLoss != true)
            {
                return true;
            }

            __result = new DamageWorker.DamageResult();
            return false;
        }
    }
}
