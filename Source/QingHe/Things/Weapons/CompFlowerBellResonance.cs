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
        public List<FlowerBellResonanceProjectileSet> settings = new();

        public CompProperties_FlowerBellResonance()
        {
            compClass = typeof(CompFlowerBellResonance);
        }
    }

    public class CompFlowerBellResonance : ThingComp
    {
        private Pawn cachedHolder;
        private HediffComp_QingheCombatState cachedCombatState;

        public CompProperties_FlowerBellResonance Props => (CompProperties_FlowerBellResonance)props;

        public ThingDef CurrentProjectileFor(Pawn pawn)
        {
            HediffComp_QingheCombatState state = GetCombatState(pawn, true);
            FlowerBellResonanceProjectileSet set = SetFor(state?.Resonance ?? FlowerBellResonance.Spring);
            return state?.ExtraBuildingDamage == true ? set?.buildingProjectile ?? set?.projectile : set?.projectile;
        }

        private HediffComp_QingheCombatState GetCombatState(Pawn pawn, bool ensure)
        {
            if (cachedHolder != pawn)
            {
                cachedHolder = pawn;
                cachedCombatState = null;
            }

            if (cachedCombatState == null || cachedCombatState.Pawn != pawn)
            {
                cachedCombatState = ensure
                    ? MX_QH_HediffUtility.EnsureCombatState(pawn)
                    : MX_QH_HediffUtility.GetCombatState(pawn);
            }

            return cachedCombatState;
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
                isActive = () => GetCombatState(pawn, true)?.ExtraBuildingDamage == true,
                toggleAction = delegate
                {
                    GetCombatState(pawn, true)?.ToggleExtraBuildingDamage();
                }
            };
        }

        public static string LabelFor(FlowerBellResonance value)
        {
            return value switch
            {
                FlowerBellResonance.Summer => (string)"MX_QH_FlowerBellResonanceSummer".Translate(),
                FlowerBellResonance.Autumn => (string)"MX_QH_FlowerBellResonanceAutumn".Translate(),
                FlowerBellResonance.Winter => (string)"MX_QH_FlowerBellResonanceWinter".Translate(),
                _ => (string)"MX_QH_FlowerBellResonanceSpring".Translate(),
            };
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
