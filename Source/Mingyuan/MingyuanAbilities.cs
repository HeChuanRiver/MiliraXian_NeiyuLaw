using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public class CompProperties_AbilityMingyuanAscendantFlameDash : CompProperties_AbilityEffect
    {
        public int maxDistance = 150;
        public float pathDamage = 10f;
        public float lifeBurnLayers = 100f;
        public float selfLifeBurnLayers = 30f;
        public int stunTicks = 180;

        public CompProperties_AbilityMingyuanAscendantFlameDash()
        {
            compClass = typeof(CompAbilityEffect_MingyuanAscendantFlameDash);
        }
    }

    public class CompAbilityEffect_MingyuanAscendantFlameDash : CompAbilityEffect
    {
        public new CompProperties_AbilityMingyuanAscendantFlameDash Props => (CompProperties_AbilityMingyuanAscendantFlameDash)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null || !caster.Spawned || !target.Cell.IsValid)
            {
                return;
            }

            Map map = caster.Map;
            List<IntVec3> cells = GenSight.BresenhamCellsBetween(caster.Position, target.Cell);
            IntVec3 destination = caster.Position;
            int traveled = 0;
            for (int i = 0; i < cells.Count && traveled < Props.maxDistance; i++)
            {
                IntVec3 cell = cells[i];
                if (!cell.InBounds(map))
                {
                    break;
                }

                destination = cell;
                traveled++;
                AffectDashCell(caster, map, cell);
            }

            destination = MingyuanUtility.FindStandableCellNear(destination, map, 5);
            caster.DeSpawn();
            GenSpawn.Spawn(caster, destination, map);
            MingyuanUtility.AddLifeBurn(caster, caster, Props.selfLifeBurnLayers);
        }

        private void AffectDashCell(Pawn caster, Map map, IntVec3 cell)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Pawn pawn;
                if (!MingyuanUtility.IsHostilePawn(things[i], caster, out pawn))
                {
                    continue;
                }

                MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, Props.pathDamage, caster);
                MingyuanUtility.AddLifeBurn(pawn, caster, Props.lifeBurnLayers);
                pawn.stances?.stunner?.StunFor(Props.stunTicks, caster, false, true, false);
                KnockbackPawn(caster, pawn, map, 3);
            }
        }

        private void KnockbackPawn(Pawn caster, Pawn pawn, Map map, int maxCells)
        {
            if (caster == null || pawn == null || map == null || !pawn.Spawned)
            {
                return;
            }

            int dx = System.Math.Sign(pawn.Position.x - caster.Position.x);
            int dz = System.Math.Sign(pawn.Position.z - caster.Position.z);
            if (dx == 0 && dz == 0)
            {
                dz = 1;
            }

            IntVec3 destination = pawn.Position;
            for (int step = 1; step <= maxCells; step++)
            {
                IntVec3 candidate = new IntVec3(pawn.Position.x + dx * step, pawn.Position.y, pawn.Position.z + dz * step);
                if (!candidate.InBounds(map) || !candidate.Standable(map))
                {
                    break;
                }

                destination = candidate;
            }

            if (destination != pawn.Position)
            {
                pawn.DeSpawn();
                GenSpawn.Spawn(pawn, destination, map);
            }
        }
    }

    public class CompProperties_AbilityMingyuanInstantCombustion : CompProperties_AbilityEffect
    {
        public float radius = 30f;
        public float partDamage = 10f;
        public int stunTicks = 720;

        public CompProperties_AbilityMingyuanInstantCombustion()
        {
            compClass = typeof(CompAbilityEffect_MingyuanInstantCombustion);
        }
    }

    public class CompAbilityEffect_MingyuanInstantCombustion : CompAbilityEffect
    {
        public new CompProperties_AbilityMingyuanInstantCombustion Props => (CompProperties_AbilityMingyuanInstantCombustion)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null || !caster.Spawned)
            {
                return;
            }

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(caster.Position, caster.Map, Props.radius, true))
            {
                Pawn pawn;
                if (!MingyuanUtility.IsHostilePawn(thing, caster, out pawn))
                {
                    continue;
                }

                DamageBrainAndEyes(pawn, caster);
                float currentLayers = MingyuanUtility.GetLifeBurnLayers(pawn);
                MingyuanUtility.AddLifeBurn(pawn, caster, Mathf.Max(1f, currentLayers));
                pawn.stances?.stunner?.StunFor(Props.stunTicks, caster, false, true, false);
            }
        }

        private void DamageBrainAndEyes(Pawn pawn, Pawn caster)
        {
            BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
            if (brain != null)
            {
                MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, Props.partDamage, caster, brain);
            }

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def == BodyPartDefOf.Eye || part.def.defName.IndexOf("Eye", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, Props.partDamage, caster, part);
                }
            }
        }
    }

    public class CompProperties_AbilityMingyuanBurningPillar : CompProperties_AbilityEffect
    {
        public ThingDef fieldDef;

        public CompProperties_AbilityMingyuanBurningPillar()
        {
            compClass = typeof(CompAbilityEffect_MingyuanBurningPillar);
        }
    }

    public class CompAbilityEffect_MingyuanBurningPillar : CompAbilityEffect
    {
        public new CompProperties_AbilityMingyuanBurningPillar Props => (CompProperties_AbilityMingyuanBurningPillar)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null || Props.fieldDef == null || !target.Cell.IsValid)
            {
                return;
            }

            Thing field = ThingMaker.MakeThing(Props.fieldDef);
            GenSpawn.Spawn(field, target.Cell, caster.Map);
            field.TryGetComp<CompMingyuanBurningField>()?.Init(caster);
        }
    }

    public class CompProperties_AbilityMingyuanTimeBurn : CompProperties_AbilityEffect
    {
        public int durationTicks = 60000;

        public CompProperties_AbilityMingyuanTimeBurn()
        {
            compClass = typeof(CompAbilityEffect_MingyuanTimeBurn);
        }
    }

    public class CompAbilityEffect_MingyuanTimeBurn : CompAbilityEffect
    {
        public new CompProperties_AbilityMingyuanTimeBurn Props => (CompProperties_AbilityMingyuanTimeBurn)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null)
            {
                return;
            }

            Pawn targetPawn = target.Pawn;
            if (targetPawn != null && !targetPawn.Dead)
            {
                MingyuanUtility.EnsureHediff(targetPawn, MingyuanUtility.TimeBurnFrozenDef);
                if (targetPawn.ageTracker != null)
                {
                    targetPawn.ageTracker.AgeBiologicalTicks = 0;
                }

                MingyuanTimeLockUtility.RegisterLock(targetPawn, Props.durationTicks, MingyuanUtility.TimeBurnFrozenDef, false);
                return;
            }

            Thing targetThing = target.Thing;
            if (targetThing != null && targetThing.def.category == ThingCategory.Building)
            {
                targetThing.Destroy(DestroyMode.Deconstruct);
            }
        }
    }

    public class CompProperties_AbilityMingyuanAshesOfSelf : CompProperties_AbilityEffect
    {
        public ThingDef fieldDef;
        public float selfBurnLayers = 100f;
        public float shieldEnergyCost = 40f;
        public float healthCostFraction = 0.2f;
        public int fieldDurationTicks = 900;

        public CompProperties_AbilityMingyuanAshesOfSelf()
        {
            compClass = typeof(CompAbilityEffect_MingyuanAshesOfSelf);
        }
    }

    public class CompAbilityEffect_MingyuanAshesOfSelf : CompAbilityEffect
    {
        public new CompProperties_AbilityMingyuanAshesOfSelf Props => (CompProperties_AbilityMingyuanAshesOfSelf)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null || !caster.Spawned)
            {
                return;
            }

            if (!ConsumeCost(caster))
            {
                Messages.Message("Mingyuan has insufficient shield energy or blood to ignite Ashes of Self.", caster, MessageTypeDefOf.RejectInput, false);
                return;
            }

            MingyuanUtility.AddSelfBurn(caster, Props.selfBurnLayers);

            if (Props.fieldDef == null)
            {
                return;
            }

            Thing field = ThingMaker.MakeThing(Props.fieldDef);
            GenSpawn.Spawn(field, caster.Position, caster.Map);
            field.TryGetComp<CompMingyuanBurningField>()?.Init(caster, Props.fieldDurationTicks);
        }

        private bool ConsumeCost(Pawn caster)
        {
            HediffComp_MingyuanProtectiveFlameShield shield = (caster.health?.hediffSet?.GetFirstHediffOfDef(MingyuanUtility.ShieldDef) as HediffWithComps)?.GetComp<HediffComp_MingyuanProtectiveFlameShield>();
            if (shield != null && shield.TryConsumeEnergy(Props.shieldEnergyCost))
            {
                return true;
            }

            float damage = Mathf.Max(1f, caster.health.LethalDamageThreshold * Props.healthCostFraction);
            DamageWorker.DamageResult result = MingyuanUtility.ApplyTrueDamage(caster, DamageDefOf.Cut, damage, caster);
            return result != null;
        }
    }
}
