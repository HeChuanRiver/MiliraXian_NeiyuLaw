using System.Collections.Generic;
using RimWorld;
using MiliraXian.Characters.QingHe.Hediffs;
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
        public CompProperties_FlowerBellResonance Props => (CompProperties_FlowerBellResonance)props;

        public ThingDef CurrentProjectileFor(Pawn pawn)
        {
            HediffComp_QingheCombatState state = MX_QH_HediffUtility.EnsureCombatState(pawn);
            FlowerBellResonanceProjectileSet set = SetFor(state?.Resonance ?? FlowerBellResonance.Spring);
            return state?.ExtraBuildingDamage == true ? set?.buildingProjectile ?? set?.projectile : set?.projectile;
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

            return Props.settings.Count > 0 ? Props.settings[0] : null;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield break;
        }

        public IEnumerable<Gizmo> EquippedGizmos(Pawn pawn)
        {
            yield return BuildingDamageToggleGizmo(pawn);
        }

        private Gizmo BuildingDamageToggleGizmo(Pawn pawn)
        {
            return new Command_Toggle
            {
                defaultLabel = "MX_QH_FlowerBellBuildingDamageLabel".Translate(),
                defaultDesc = "MX_QH_FlowerBellBuildingDamageDesc".Translate(),
                isActive = () => MX_QH_HediffUtility.GetCombatState(pawn)?.ExtraBuildingDamage == true,
                toggleAction = delegate
                {
                    MX_QH_HediffUtility.EnsureCombatState(pawn)?.ToggleExtraBuildingDamage();
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

            Pawn pawn = PrimaryVerb?.CasterPawn;
            foreach (Gizmo gizmo in resonanceComp.EquippedGizmos(pawn))
            {
                yield return gizmo;
            }
        }
    }
}
