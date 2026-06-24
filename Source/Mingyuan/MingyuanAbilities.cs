using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

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

            MingyuanUtility.AddSelfBurn(caster, Props.selfLifeBurnLayers);
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

                MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, Props.pathDamage, caster, scaleWithSelfBurn: true);
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

                    Thing thing = GenSpawn.Spawn(scorchDef, cell, map, WipeMode.Vanish);
                    Thing_MingyuanAscendantFlameScorch scorch = thing as Thing_MingyuanAscendantFlameScorch;
                    if (scorch != null)
                    {
                        scorch.Init(caster, Mathf.Max(0.1f, Props.scorchMoteScale), Rand.Range(0f, 360f));
                    }
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

    public class Thing_MingyuanAscendantFlameScorch : ThingWithComps
    {
        private CompMingyuanAscendantFlameScorch ScorchComp => GetComp<CompMingyuanAscendantFlameScorch>();

        public float VisualAlpha => ScorchComp?.VisualAlpha ?? 1f;

        public float VisualScale => ScorchComp?.VisualScale ?? 1f;

        public float VisualRotation => ScorchComp?.VisualRotation ?? 0f;

        public void Init(Pawn caster, float scale, float rotation)
        {
            ScorchComp?.Init(caster, scale, rotation);
        }
    }

    public class CompProperties_MingyuanAscendantFlameScorch : CompProperties
    {
        public int durationTicks = 900;
        public int pulseIntervalTicks = 60;
        public float lifeBurnLayers = 20f;
        public int fadeInTicks = 15;
        public int fadeOutTicks = 60;

        public CompProperties_MingyuanAscendantFlameScorch()
        {
            compClass = typeof(CompMingyuanAscendantFlameScorch);
        }
    }

    public class CompMingyuanAscendantFlameScorch : ThingComp
    {
        private Pawn caster;
        private int spawnTick;
        private int expireTick;
        private int ticksToPulse;
        private float visualScale = 1f;
        private float visualRotation;

        public CompProperties_MingyuanAscendantFlameScorch PropsScorch => (CompProperties_MingyuanAscendantFlameScorch)props;

        public float VisualScale => Mathf.Max(0.1f, visualScale);

        public float VisualRotation => visualRotation;

        public float VisualAlpha
        {
            get
            {
                EnsureInitialized();
                int currentTick = Find.TickManager.TicksGame;
                float alpha = 1f;
                int fadeInTicks = Mathf.Max(0, PropsScorch.fadeInTicks);
                if (fadeInTicks > 0)
                {
                    alpha = Mathf.Min(alpha, Mathf.Clamp01((currentTick - spawnTick) / (float)fadeInTicks));
                }

                int fadeOutTicks = Mathf.Max(0, PropsScorch.fadeOutTicks);
                if (fadeOutTicks > 0)
                {
                    alpha = Mathf.Min(alpha, Mathf.Clamp01((expireTick - currentTick) / (float)fadeOutTicks));
                }

                return alpha;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster", false);
            Scribe_Values.Look(ref spawnTick, "spawnTick", 0);
            Scribe_Values.Look(ref expireTick, "expireTick", 0);
            Scribe_Values.Look(ref ticksToPulse, "ticksToPulse", 0);
            Scribe_Values.Look(ref visualScale, "visualScale", 1f);
            Scribe_Values.Look(ref visualRotation, "visualRotation", 0f);
        }

        public void Init(Pawn newCaster, float scale, float rotation)
        {
            caster = newCaster;
            spawnTick = Find.TickManager.TicksGame;
            expireTick = spawnTick + Mathf.Max(1, PropsScorch.durationTicks);
            ticksToPulse = Rand.RangeInclusive(1, Mathf.Max(1, PropsScorch.pulseIntervalTicks));
            visualScale = Mathf.Max(0.1f, scale);
            visualRotation = rotation;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Destroyed || parent.Map == null)
            {
                return;
            }

            EnsureInitialized();
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick >= expireTick)
            {
                parent.Destroy(DestroyMode.Vanish);
                return;
            }

            ticksToPulse--;
            if (ticksToPulse > 0)
            {
                return;
            }

            ticksToPulse = Mathf.Max(1, PropsScorch.pulseIntervalTicks);
            PulseStandingPawns();
        }

        private void EnsureInitialized()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (spawnTick <= 0)
            {
                spawnTick = parent.TickSpawned > 0 ? parent.TickSpawned : currentTick;
            }

            if (expireTick <= 0)
            {
                expireTick = spawnTick + Mathf.Max(1, PropsScorch.durationTicks);
            }

            if (ticksToPulse <= 0)
            {
                ticksToPulse = Rand.RangeInclusive(1, Mathf.Max(1, PropsScorch.pulseIntervalTicks));
            }

            if (visualScale <= 0f)
            {
                visualScale = 1f;
            }
        }

        private void PulseStandingPawns()
        {
            if (caster == null || caster.Destroyed || caster.Dead || parent.Map == null || PropsScorch.lifeBurnLayers <= 0f)
            {
                return;
            }

            List<Thing> things = parent.Position.GetThingList(parent.Map);
            for (int i = 0; i < things.Count; i++)
            {
                Pawn pawn;
                if (MingyuanUtility.IsHostilePawn(things[i], caster, out pawn))
                {
                    MingyuanUtility.AddLifeBurn(pawn, caster, PropsScorch.lifeBurnLayers);
                }
            }
        }
    }

    public class Graphic_MingyuanScorchFlicker : Graphic_MoteRandom
    {
        private const int TicksPerFrameChange = 15;
        private static readonly MaterialPropertyBlock ScorchPropertyBlock = new MaterialPropertyBlock();

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            if (subGraphics == null || subGraphics.Length == 0)
            {
                base.DrawWorker(loc, rot, thingDef, thing, extraRotation);
                return;
            }

            Mote mote = thing as Mote;
            int offset = mote != null ? mote.offsetRandom : thing?.HashOffset() ?? 0;
            int frame = Mathf.Abs((Find.TickManager.TicksGame + offset) / TicksPerFrameChange) % subGraphics.Length;
            Material material = subGraphics[frame].MatSingle;
            if (mote != null)
            {
                Graphic_Mote.DrawMote(data, material, base.Color, loc, rot, thingDef, thing, 0, ForcePropertyBlock);
                return;
            }

            Thing_MingyuanAscendantFlameScorch scorch = thing as Thing_MingyuanAscendantFlameScorch;
            float alpha = scorch?.VisualAlpha ?? 1f;
            if (alpha <= 0.001f)
            {
                return;
            }

            float scale = scorch?.VisualScale ?? 1f;
            float drawRotation = scorch?.VisualRotation ?? extraRotation;
            Vector3 drawScale = new Vector3(data.drawSize.x * scale, 1f, data.drawSize.y * scale);
            Color drawColor = base.Color;
            drawColor.a *= alpha;

            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(loc, Quaternion.AngleAxis(drawRotation, Vector3.up), drawScale);
            if (!ForcePropertyBlock && drawColor.IndistinguishableFrom(material.color))
            {
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                return;
            }

            ScorchPropertyBlock.SetColor(ShaderPropertyIDs.Color, drawColor);
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0, null, 0, ScorchPropertyBlock);
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
        public ThingDef flashMoteDef;
        public ThingDef targetMoteDef;
        public float flashMoteScale = 1f;
        public float targetMoteScale = 1f;
        public int maxTargetMotes = 48;
        public float minimumLifeBurnLayers = 1f;

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

            SpawnFlashMote(caster);
            int spawnedTargetMotes = 0;
            int maxTargetMotes = Mathf.Max(0, Props.maxTargetMotes);
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(caster.Position, caster.Map, Props.radius, true))
            {
                Pawn pawn;
                if (!MingyuanUtility.IsHostilePawn(thing, caster, out pawn))
                {
                    continue;
                }

                DamageBrainAndEyes(pawn, caster);
                float currentLayers = MingyuanUtility.GetLifeBurnLayers(pawn);
                float layersToAdd = Mathf.Max(Props.minimumLifeBurnLayers, currentLayers);
                if (layersToAdd > 0f)
                {
                    MingyuanUtility.AddLifeBurn(pawn, caster, layersToAdd);
                }

                pawn.stances?.stunner?.StunFor(Props.stunTicks, caster, false, true, false);
                if (spawnedTargetMotes < maxTargetMotes && SpawnTargetMote(pawn))
                {
                    spawnedTargetMotes++;
                }
            }
        }

        private void SpawnFlashMote(Pawn caster)
        {
            ThingDef flashDef = Props.flashMoteDef
                                ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_InstantCombustionFlash
                                ?? DefDatabase<ThingDef>.GetNamedSilentFail("MX_Mingyuan_Mote_InstantCombustionFlash");
            if (caster?.Map == null || flashDef == null)
            {
                return;
            }

            Mote mote = MoteMaker.MakeStaticMote(
                caster.DrawPos,
                caster.Map,
                flashDef,
                Mathf.Max(0.1f, Props.flashMoteScale),
                false,
                Rand.Range(0f, 360f));
            if (mote != null)
            {
                mote.exactPosition = caster.DrawPos;
            }
        }

        private bool SpawnTargetMote(Pawn pawn)
        {
            ThingDef targetDef = Props.targetMoteDef
                                 ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_InstantCombustionMark
                                 ?? DefDatabase<ThingDef>.GetNamedSilentFail("MX_Mingyuan_Mote_InstantCombustionMark");
            if (pawn == null || !pawn.Spawned || pawn.MapHeld == null || targetDef == null)
            {
                return false;
            }

            Mote mote = MoteMaker.MakeAttachedOverlay(pawn, targetDef, Vector3.zero, Mathf.Max(0.1f, Props.targetMoteScale));
            if (mote != null)
            {
                mote.exactRotation = Rand.Range(0f, 360f);
                return true;
            }

            return false;
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

            IntVec3 spawnCell;
            if (!TryFindBurningPillarSpawnCell(target.Cell, caster.Map, out spawnCell))
            {
                Messages.Message("MX_Mingyuan_BurningPillar_NoValidCell".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Thing field = ThingMaker.MakeThing(Props.fieldDef);
            if (field.def.CanHaveFaction && caster.Faction != null)
            {
                field.SetFactionDirect(caster.Faction);
            }

            GenSpawn.Spawn(field, spawnCell, caster.Map);
            field.TryGetComp<CompMingyuanBurningPillarTornado>()?.Init(caster);
            field.TryGetComp<CompMingyuanBurningField>()?.Init(caster);
        }

        private static bool TryFindBurningPillarSpawnCell(IntVec3 center, Map map, out IntVec3 result)
        {
            if (CanSpawnBurningPillarAt(center, map))
            {
                result = center;
                return true;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 6f, false))
            {
                if (CanSpawnBurningPillarAt(cell, map))
                {
                    result = cell;
                    return true;
                }
            }

            result = IntVec3.Invalid;
            return false;
        }

        private static bool CanSpawnBurningPillarAt(IntVec3 cell, Map map)
        {
            return map != null && cell.InBounds(map) && cell.Standable(map) && cell.GetFirstBuilding(map) == null;
        }
    }

    public class CompProperties_AbilityMingyuanTimeBurn : CompProperties_AbilityEffect
    {
        public int durationTicks = MingyuanUtility.TicksPerHour;
        public int tickIntervalTicks = 60;
        public int moteIntervalTicks = 120;
        public ThingDef startMoteDef;
        public ThingDef targetMoteDef;
        public ThingDef collapseMoteDef;
        public SoundDef effectSoundDef;
        public float startMoteScale = 2.2f;
        public float targetMoteScale = 1.15f;
        public float collapseMoteScale = 2.4f;
        public int mechSteelCount = 75;
        public int mechPlasteelCount = 25;

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
                MingyuanTimeBurnUtility.PlayEffectSound(Props.effectSoundDef, targetPawn.PositionHeld, targetPawn.MapHeld);
                MingyuanTimeBurnUtility.TryMakeStaticMote(targetPawn.PositionHeld, targetPawn.MapHeld, Props.startMoteDef, Props.startMoteScale);
                MingyuanTimeBurnUtility.Register(targetPawn, caster, Props);
                return;
            }

            Thing targetThing = target.Thing;
            if (targetThing != null && targetThing.def.category == ThingCategory.Building)
            {
                MingyuanTimeBurnUtility.DissolveBuilding(targetThing, caster, Props);
            }
        }
    }

    public class CompProperties_AbilityMingyuanAshesOfSelf : CompProperties_AbilityEffect
    {
        public ThingDef fieldDef;
        public float selfBurnLayers = 100f;
        public float bloodLossCost = 0.5f;
        public int fieldDurationTicks = 900;
        public float fieldPreviewRadius = 2.4f;
        public ThingDef castMoteDef;
        public float castMoteScale = 1f;
        public SoundDef effectSoundDef;

        public CompProperties_AbilityMingyuanAshesOfSelf()
        {
            compClass = typeof(CompAbilityEffect_MingyuanAshesOfSelf);
        }
    }

    public class CompAbilityEffect_MingyuanAshesOfSelf : CompAbilityEffect
    {
        public new CompProperties_AbilityMingyuanAshesOfSelf Props => (CompProperties_AbilityMingyuanAshesOfSelf)props;

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster?.MapHeld == null)
            {
                return;
            }

            GenDraw.DrawRadiusRing(caster.Position, Mathf.Max(0.1f, Props.fieldPreviewRadius), new Color(1f, 0.74f, 0.34f, 0.72f));
        }

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
                Messages.Message("MX_Mingyuan_AshesOfSelf_InsufficientCost".Translate(), caster, MessageTypeDefOf.RejectInput, false);
                return;
            }

            MingyuanUtility.AddSelfBurn(caster, Props.selfBurnLayers);
            PlayCastVisuals(caster);

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
            if (shield != null && shield.TryConsumeAllEnergy())
            {
                return true;
            }

            if (caster.RaceProps?.IsFlesh != true || caster.WouldDieFromAdditionalBloodLoss(Props.bloodLossCost))
            {
                return false;
            }

            HealthUtility.AdjustSeverity(caster, HediffDefOf.BloodLoss, Mathf.Max(0f, Props.bloodLossCost));
            return true;
        }

        private void PlayCastVisuals(Pawn caster)
        {
            if (caster?.Map == null)
            {
                return;
            }

            Props.effectSoundDef?.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
            ThingDef moteDef = Props.castMoteDef ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_AshesCast;
            MingyuanUtility.TryMakeStaticMote(caster.Position, caster.Map, moteDef, Props.castMoteScale);
        }
    }
}
