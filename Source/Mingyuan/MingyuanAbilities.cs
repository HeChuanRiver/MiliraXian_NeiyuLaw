using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public class CompProperties_AbilityMingyuanAscendantFlameDash : CompProperties_AbilityEffect
    {
        public int maxDistance = 150;
        public int pathWidth = 3;
        public float pathDamage = 10f;
        public float lifeBurnLayers = 100f;
        public float selfLifeBurnLayers = 30f;
        public int stunTicks = 180;
        public ThingDef flyerDef;
        public ThingDef scorchMoteDef;
        public float scorchMoteScale = 1f;
        public int maxScorchMotes = 225;

        public CompProperties_AbilityMingyuanAscendantFlameDash()
        {
            compClass = typeof(CompAbilityEffect_MingyuanAscendantFlameDash);
        }
    }

    public class CompAbilityEffect_MingyuanAscendantFlameDash : CompAbilityEffect, ICompAbilityEffectOnJumpCompleted
    {
        private static readonly Color PreviewColor = new Color(1f, 0.52f, 0.18f, 0.72f);

        private readonly List<IntVec3> tmpPathCells = new List<IntVec3>(512);
        private readonly HashSet<IntVec3> tmpPathCellSet = new HashSet<IntVec3>();
        private readonly HashSet<IntVec3> tmpScorchCellSet = new HashSet<IntVec3>();
        private readonly HashSet<Pawn> tmpAffectedPawns = new HashSet<Pawn>();

        public new CompProperties_AbilityMingyuanAscendantFlameDash Props => (CompProperties_AbilityMingyuanAscendantFlameDash)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            Pawn caster = parent?.pawn;
            Map map = caster?.MapHeld;
            bool invalid = caster == null
                           || map == null
                           || !target.Cell.IsValid
                           || !target.Cell.InBounds(map)
                           || target.Cell == caster.Position
                           || !TryBuildDashPath(caster, target.Cell, tmpPathCells, out IntVec3 destination)
                           || destination != target.Cell
                           || !destination.Standable(map);
            if (invalid)
            {
                if (throwMessages && caster != null)
                {
                    Messages.Message("MX_Mingyuan_AscendantFlameDash_InvalidTarget".Translate(), caster, MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            return true;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster?.MapHeld == null || !target.Cell.IsValid)
            {
                return;
            }

            if (TryBuildDashPath(caster, target.Cell, tmpPathCells, out IntVec3 _))
            {
                GenDraw.DrawFieldEdges(tmpPathCells, PreviewColor);
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null || !caster.Spawned || !target.Cell.IsValid || !Valid(target, false))
            {
                return;
            }

            Map map = caster.Map;
            if (!TryBuildDashPath(caster, target.Cell, tmpPathCells, out IntVec3 destination) || destination == caster.Position)
            {
                return;
            }

            base.Apply(target, dest);
            SpawnScorchMotes(caster, destination, map);
            AffectDashCells(caster, map, tmpPathCells);

            bool selected = Find.Selector.IsSelected(caster);
            ThingDef flyerDef = Props.flyerDef ?? MX_MingyuanDefOf.MX_Mingyuan_AscendantFlameDashFlyer ?? ThingDefOf.PawnFlyer;
            PawnFlyer flyer = PawnFlyer.MakeFlyer(
                flyerDef,
                caster,
                destination,
                parent.def.verbProperties.flightEffecterDef,
                parent.def.verbProperties.soundLanding,
                false,
                null,
                parent,
                target);

            if (flyer != null)
            {
                GenSpawn.Spawn(flyer, destination, map);
                if (selected)
                {
                    Find.Selector.Select(caster, false, false);
                }
            }
        }

        public void OnJumpCompleted(IntVec3 origin, LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || caster.Dead)
            {
                return;
            }

            MingyuanUtility.AddLifeBurn(caster, caster, Props.selfLifeBurnLayers);
        }

        private bool TryBuildDashPath(Pawn caster, IntVec3 targetCell, List<IntVec3> outCells, out IntVec3 destination)
        {
            outCells.Clear();
            tmpPathCellSet.Clear();
            destination = IntVec3.Invalid;
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !targetCell.IsValid || !targetCell.InBounds(map))
            {
                return false;
            }

            float dx = targetCell.x - caster.Position.x;
            float dz = targetCell.z - caster.Position.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance < 0.001f)
            {
                return false;
            }

            float clampedDistance = Mathf.Min(distance, Mathf.Max(1, Props.maxDistance));
            float dirX = dx / distance;
            float dirZ = dz / distance;
            destination = new IntVec3(
                Mathf.RoundToInt(caster.Position.x + dirX * clampedDistance),
                caster.Position.y,
                Mathf.RoundToInt(caster.Position.z + dirZ * clampedDistance));

            if (!destination.InBounds(map))
            {
                destination = destination.ClampInsideMap(map);
            }

            List<IntVec3> centerLine = GenSight.BresenhamCellsBetween(caster.Position, destination);
            float perpX = -dirZ;
            float perpZ = dirX;
            int halfWidth = Mathf.Max(0, Props.pathWidth / 2);
            for (int i = 0; i < centerLine.Count; i++)
            {
                IntVec3 center = centerLine[i];
                if (center == caster.Position)
                {
                    continue;
                }

                for (int offset = -halfWidth; offset <= halfWidth; offset++)
                {
                    IntVec3 cell = new IntVec3(
                        Mathf.RoundToInt(center.x + perpX * offset),
                        center.y,
                        Mathf.RoundToInt(center.z + perpZ * offset));
                    if (cell.InBounds(map) && tmpPathCellSet.Add(cell))
                    {
                        outCells.Add(cell);
                    }
                }
            }

            tmpPathCellSet.Clear();
            return outCells.Count > 0 && destination.IsValid;
        }

        private void AffectDashCells(Pawn caster, Map map, List<IntVec3> cells)
        {
            tmpAffectedPawns.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                AffectDashCell(caster, map, cells[i]);
            }

            tmpAffectedPawns.Clear();
        }

        private void AffectDashCell(Pawn caster, Map map, IntVec3 cell)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Pawn pawn;
                if (!MingyuanUtility.IsHostilePawn(things[i], caster, out pawn) || !tmpAffectedPawns.Add(pawn))
                {
                    continue;
                }

                MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, Props.pathDamage, caster);
                MingyuanUtility.AddLifeBurn(pawn, caster, Props.lifeBurnLayers);
                if (!pawn.Dead && pawn.Spawned)
                {
                    pawn.stances?.stunner?.StunFor(Props.stunTicks, caster, false, true, false);
                    KnockbackPawn(caster, pawn, map, 3);
                }
            }
        }

        private void SpawnScorchMotes(Pawn caster, IntVec3 destination, Map map)
        {
            ThingDef scorchDef = Props.scorchMoteDef ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_AscendantFlameScorch;
            if (caster == null || map == null || scorchDef == null || !destination.IsValid)
            {
                return;
            }

            float dx = destination.x - caster.Position.x;
            float dz = destination.z - caster.Position.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance < 0.001f)
            {
                return;
            }

            List<IntVec3> centerLine = GenSight.BresenhamCellsBetween(caster.Position, destination);
            if (centerLine.NullOrEmpty())
            {
                return;
            }

            float perpX = -dz / distance;
            float perpZ = dx / distance;
            int halfWidth = Mathf.Max(0, Props.pathWidth / 2);
            int cellsPerRow = Mathf.Max(1, halfWidth * 2 + 1);
            int maxMotes = Mathf.Max(cellsPerRow, Props.maxScorchMotes);
            int rowStride = Mathf.Max(1, Mathf.CeilToInt((float)Mathf.Max(1, centerLine.Count - 1) * cellsPerRow / maxMotes));

            tmpScorchCellSet.Clear();
            for (int i = 0; i < centerLine.Count; i++)
            {
                IntVec3 center = centerLine[i];
                if (center == caster.Position)
                {
                    continue;
                }

                bool forceEndpoint = center == destination;
                if (!forceEndpoint && (i - 1) % rowStride != 0)
                {
                    continue;
                }

                for (int offset = -halfWidth; offset <= halfWidth; offset++)
                {
                    IntVec3 cell = new IntVec3(
                        Mathf.RoundToInt(center.x + perpX * offset),
                        center.y,
                        Mathf.RoundToInt(center.z + perpZ * offset));
                    if (!cell.InBounds(map) || !tmpScorchCellSet.Add(cell) || HasScorchMote(map, cell, scorchDef))
                    {
                        continue;
                    }

                    MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, scorchDef, Mathf.Max(0.1f, Props.scorchMoteScale), true, Rand.Range(0f, 360f));
                }
            }

            tmpScorchCellSet.Clear();
        }

        private static bool HasScorchMote(Map map, IntVec3 cell, ThingDef scorchDef)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i]?.def == scorchDef)
                {
                    return true;
                }
            }

            return false;
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

    public class Graphic_MingyuanScorchFlicker : Graphic_MoteRandom
    {
        private const int TicksPerFrameChange = 15;

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            if (!(thing is Mote mote) || subGraphics == null || subGraphics.Length == 0)
            {
                base.DrawWorker(loc, rot, thingDef, thing, extraRotation);
                return;
            }

            int frame = Mathf.Abs((Find.TickManager.TicksGame + mote.offsetRandom) / TicksPerFrameChange) % subGraphics.Length;
            Graphic_Mote.DrawMote(data, subGraphics[frame].MatSingle, base.Color, loc, rot, thingDef, thing, 0, ForcePropertyBlock);
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            if (newColorTwo != Color.white)
            {
                Log.ErrorOnce("Cannot use Graphic_MingyuanScorchFlicker.GetColoredVersion with a non-white colorTwo.", 739114011);
            }

            return GraphicDatabase.Get<Graphic_MingyuanScorchFlicker>(path, newShader, drawSize, newColor, Color.white, data);
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
