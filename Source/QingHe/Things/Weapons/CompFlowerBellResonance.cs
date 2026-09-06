using System.Collections.Generic;
using RimWorld;
using MiliraXian.Characters.QingHe.Hediffs;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    public enum FlowerBellResonance
    {
        None = -1,
        Spring = 0,
        Summer,
        Autumn,
        Winter
    }

    public class FlowerBellResonanceProjectileSet
    {
        public FlowerBellResonance resonance;
        public ThingDef projectile;
    }

    public class CompProperties_FlowerBellResonance : CompProperties
    {
        public List<FlowerBellResonanceProjectileSet> settings = new();

        public CompProperties_FlowerBellResonance()
        {
            compClass = typeof(CompFlowerBellResonance);
        }
    }

    public class CompFlowerBellResonance : ThingComp
    {
        public CompProperties_FlowerBellResonance Props => (CompProperties_FlowerBellResonance)props;

        public ThingDef CurrentProjectileFor(Pawn pawn)
        {
            FlowerBellResonance resonance = MX_QH_HediffUtility.GetSeasonalResonance(pawn)?.Resonance ?? FlowerBellResonance.None;
            return resonance == FlowerBellResonance.None ? null : SetFor(resonance)?.projectile;
        }

        private FlowerBellResonanceProjectileSet SetFor(FlowerBellResonance resonance)
        {
            if (Props?.settings == null)
            {
                return null;
            }

            for (int i = 0; i < Props.settings.Count; i++)
            {
                FlowerBellResonanceProjectileSet set = Props.settings[i];
                if (set != null && set.resonance == resonance)
                {
                    return set;
                }
            }

            return null;
        }

        public static string LabelFor(FlowerBellResonance value)
        {
            return value switch
            {
                FlowerBellResonance.None => string.Empty,
                FlowerBellResonance.Summer => (string)"MX_QH_FlowerBellResonanceSummer".Translate(),
                FlowerBellResonance.Autumn => (string)"MX_QH_FlowerBellResonanceAutumn".Translate(),
                FlowerBellResonance.Winter => (string)"MX_QH_FlowerBellResonanceWinter".Translate(),
                _ => (string)"MX_QH_FlowerBellResonanceSpring".Translate(),
            };
        }
    }
}
