using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    public enum FlowerBellResonance
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    public class FlowerBellResonanceProjectileSet
    {
        public FlowerBellResonance resonance;
        public ThingDef projectile;
        public ThingDef buildingProjectile;
    }

    public class CompProperties_FlowerBellResonance : CompProperties
    {
        public List<FlowerBellResonanceProjectileSet> settings = new List<FlowerBellResonanceProjectileSet>();

        public CompProperties_FlowerBellResonance()
        {
            compClass = typeof(CompFlowerBellResonance);
        }
    }

    public class CompFlowerBellResonance : ThingComp
    {
        private FlowerBellResonance resonance = FlowerBellResonance.Spring;
        private bool extraBuildingDamage;

        public CompProperties_FlowerBellResonance Props => (CompProperties_FlowerBellResonance)props;

        public FlowerBellResonance Resonance => resonance;

        public bool ExtraBuildingDamage => extraBuildingDamage;

        public ThingDef CurrentProjectile
        {
            get
            {
                FlowerBellResonanceProjectileSet set = CurrentSet;
                return extraBuildingDamage ? set?.buildingProjectile ?? set?.projectile : set?.projectile;
            }
        }

        private FlowerBellResonanceProjectileSet CurrentSet
        {
            get
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

                return Props.settings.Count > 0 ? Props.settings[0] : null;
            }
        }

        public void SetResonance(FlowerBellResonance value)
        {
            resonance = value;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref resonance, "mx_qh_flowerBell_resonance", FlowerBellResonance.Spring);
            Scribe_Values.Look(ref extraBuildingDamage, "mx_qh_flowerBell_extraBuildingDamage", false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return BuildingDamageToggleGizmo();
        }

        public IEnumerable<Gizmo> EquippedGizmos()
        {
            yield return BuildingDamageToggleGizmo();
        }

        private Gizmo BuildingDamageToggleGizmo()
        {
            return new Command_Toggle
            {
                defaultLabel = "MX_QH_FlowerBellBuildingDamageLabel".Translate(),
                defaultDesc = "MX_QH_FlowerBellBuildingDamageDesc".Translate(),
                isActive = () => extraBuildingDamage,
                toggleAction = delegate
                {
                    extraBuildingDamage = !extraBuildingDamage;
                }
            };
        }

        public static string LabelFor(FlowerBellResonance value)
        {
            switch (value)
            {
                case FlowerBellResonance.Summer:
                    return "MX_QH_FlowerBellResonanceSummer".Translate();
                case FlowerBellResonance.Autumn:
                    return "MX_QH_FlowerBellResonanceAutumn".Translate();
                case FlowerBellResonance.Winter:
                    return "MX_QH_FlowerBellResonanceWinter".Translate();
                default:
                    return "MX_QH_FlowerBellResonanceSpring".Translate();
            }
        }
    }

    public class CompEquippable_FlowerBell : CompEquippable
    {
        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra())
            {
                yield return gizmo;
            }

            CompFlowerBellResonance resonanceComp = parent?.TryGetComp<CompFlowerBellResonance>();
            if (resonanceComp == null)
            {
                yield break;
            }

            foreach (Gizmo gizmo in resonanceComp.EquippedGizmos())
            {
                yield return gizmo;
            }
        }
    }
}
