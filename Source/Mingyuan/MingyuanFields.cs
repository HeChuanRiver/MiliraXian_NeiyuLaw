using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Mingyuan
{
    public class Thing_MingyuanBurningPillarField : Building, IAttackTarget, ILoadReferenceable, IAttackTargetSearcher
    {
        private CompMingyuanBurningPillarTornado TornadoComp => GetComp<CompMingyuanBurningPillarTornado>();

        Thing IAttackTarget.Thing => this;

        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

        public float TargetPriorityFactor => TornadoComp?.TargetPriorityFactor ?? 0.65f;

        Thing IAttackTargetSearcher.Thing => this;

        public Verb CurrentEffectiveVerb => null;

        public LocalTargetInfo LastAttackedTarget => LocalTargetInfo.Invalid;

        public int LastAttackTargetTick => 0;

        public float VisualScale => TornadoComp?.VisualScale ?? 1f;

        public float VisualRotation => TornadoComp?.VisualRotation ?? 0f;

        public float VisualAlpha => TornadoComp?.VisualAlpha ?? 1f;

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor)
        {
            return !Spawned || Destroyed || TornadoComp == null || TornadoComp.ThreatDisabled;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(TornadoComp?.SmoothedDrawLoc(drawLoc) ?? drawLoc, flip);
        }
    }

    public class Thing_MingyuanAshesField : ThingWithComps
    {
        private CompMingyuanBurningField FieldComp => GetComp<CompMingyuanBurningField>();

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(FieldComp?.DrawLoc ?? drawLoc, flip);
        }
    }

    public class CompProperties_MingyuanBurningPillarTornado : CompProperties
    {
        public float startRadius = 1f;
        public float maxRadius = 15f;
        public int durationTicks = MingyuanUtility.TicksPerHour;
        public int radiusGrowTicks = 417;
        public int controlUnlockTicks = MingyuanUtility.TicksPerHour / 2;
        public int coreGrowTicks = MingyuanUtility.TicksPerHour / 2;
        public int pulseIntervalTicks = 60;
        public int moveIntervalTicks = 60;
        public int initialHitPoints = 500;
        public int maxHitPoints = 2000;
        public float centerBurnDamage = 30f;
        public float edgeBurnDamage = 5f;
        public float centerCutDamage = 5f;
        public float edgeCutDamage = 30f;
        public float lifeBurnLayersPerBurnDamage = 1f;
        public float buildingDamageFraction = 0.33f;
        public float targetPriorityFactor = 0.65f;
        public float visualRotationDegreesPerTick = 2.25f;
        public int visualFadeTicks = 120;
        public ThingDef coreMoteDef;
        public float coreMoteScale = 1f;

        public CompProperties_MingyuanBurningPillarTornado()
        {
            compClass = typeof(CompMingyuanBurningPillarTornado);
        }
    }

    public class CompMingyuanBurningPillarTornado : ThingComp
    {
        private Pawn caster;
        private int spawnTick;
        private int expireTick;
        private int ticksToPulse;
        private int ticksToMove;
        private IntVec3 destinationCell = IntVec3.Invalid;
        private IntVec3 drawFromCell = IntVec3.Invalid;
        private IntVec3 drawToCell = IntVec3.Invalid;
        private int drawMoveStartTick;
        private int grantedCoreHitPoints;
        private Mote coreMote;

        public CompProperties_MingyuanBurningPillarTornado PropsTornado => (CompProperties_MingyuanBurningPillarTornado)props;

        public float CurrentRadius
        {
            get
            {
                EnsureInitialized();
                float progress = Mathf.Clamp01(AgeTicks / (float)Mathf.Max(1, PropsTornado.radiusGrowTicks));
                return Mathf.Lerp(Mathf.Max(0.1f, PropsTornado.startRadius), Mathf.Max(PropsTornado.startRadius, PropsTornado.maxRadius), progress);
            }
        }

        public int CurrentCoreCapacity
        {
            get
            {
                EnsureInitialized();
                return ComputeCoreCapacity();
            }
        }

        public float TargetPriorityFactor => Mathf.Max(0.05f, PropsTornado.targetPriorityFactor);

        public bool ThreatDisabled => caster == null || caster.Destroyed || caster.Dead || parent.Destroyed || parent.Map == null;

        public float VisualScale => Mathf.Clamp(CurrentRadius / Mathf.Max(0.1f, PropsTornado.maxRadius), 0.05f, 1.25f);

        public float VisualRotation => (Find.TickManager.TicksGame + parent.HashOffset()) * PropsTornado.visualRotationDegreesPerTick;

        public float VisualAlpha
        {
            get
            {
                EnsureInitialized();
                int currentTick = Find.TickManager.TicksGame;
                int fadeTicks = Mathf.Max(0, PropsTornado.visualFadeTicks);
                if (fadeTicks <= 0)
                {
                    return 1f;
                }

                float fadeIn = Mathf.Clamp01((currentTick - spawnTick) / (float)fadeTicks);
                float fadeOut = Mathf.Clamp01((expireTick - currentTick) / (float)fadeTicks);
                return Mathf.Min(fadeIn, fadeOut);
            }
        }

        private int AgeTicks => Mathf.Max(0, Find.TickManager.TicksGame - spawnTick);

        private bool ControlsUnlocked => AgeTicks >= Mathf.Max(0, PropsTornado.controlUnlockTicks);

        private bool HasDestination => destinationCell.IsValid;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster", false);
            Scribe_Values.Look(ref spawnTick, "spawnTick", 0);
            Scribe_Values.Look(ref expireTick, "expireTick", 0);
            Scribe_Values.Look(ref ticksToPulse, "ticksToPulse", 0);
            Scribe_Values.Look(ref ticksToMove, "ticksToMove", 0);
            Scribe_Values.Look(ref destinationCell, "destinationCell", IntVec3.Invalid);
            Scribe_Values.Look(ref drawFromCell, "drawFromCell", IntVec3.Invalid);
            Scribe_Values.Look(ref drawToCell, "drawToCell", IntVec3.Invalid);
            Scribe_Values.Look(ref drawMoveStartTick, "drawMoveStartTick", 0);
            Scribe_Values.Look(ref grantedCoreHitPoints, "grantedCoreHitPoints", 0);
        }

        public void Init(Pawn newCaster)
        {
            caster = newCaster;
            spawnTick = Find.TickManager.TicksGame;
            expireTick = spawnTick + Mathf.Max(1, PropsTornado.durationTicks);
            ticksToPulse = 1;
            ticksToMove = Mathf.Max(1, PropsTornado.moveIntervalTicks);
            grantedCoreHitPoints = Mathf.Clamp(PropsTornado.initialHitPoints, 1, parent.MaxHitPoints);
            parent.HitPoints = grantedCoreHitPoints;
            if (caster?.Faction != null && parent.def.CanHaveFaction && parent.Faction != caster.Faction)
            {
                parent.SetFaction(caster.Faction);
            }

            MaintainCoreMote();
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
            if (currentTick >= expireTick || caster == null || caster.Destroyed || caster.Dead)
            {
                parent.Destroy(DestroyMode.Vanish);
                return;
            }

            GrowCoreHitPoints();
            MaintainCoreMote();
            TickDamagePulse();
            TickMovement();
        }

        public override string CompInspectStringExtra()
        {
            EnsureInitialized();
            int remainingSeconds = TicksToSeconds(Mathf.Max(0, expireTick - Find.TickManager.TicksGame));
            return "MX_Mingyuan_BurningPillar_Inspect".Translate(
                CurrentRadius.ToString("F1"),
                parent.HitPoints.ToStringCached(),
                CurrentCoreCapacity.ToStringCached(),
                remainingSeconds.ToString(),
                ControlStateLabel()).ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (!ControlsUnlocked || parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            yield return MakeMoveCommand();
            yield return new Command_Action
            {
                defaultLabel = "MX_Mingyuan_BurningPillar_StopLabel".Translate().ToString(),
                defaultDesc = "MX_Mingyuan_BurningPillar_StopDesc".Translate().ToString(),
                icon = TexCommand.CannotShoot,
                action = ClearDestination
            };
        }

        public Vector3 SmoothedDrawLoc(Vector3 fallback)
        {
            if (!drawFromCell.IsValid || !drawToCell.IsValid)
            {
                return fallback;
            }

            int interval = Mathf.Max(1, PropsTornado.moveIntervalTicks);
            float progress = Mathf.Clamp01((Find.TickManager.TicksGame - drawMoveStartTick) / (float)interval);
            if (progress >= 1f)
            {
                drawFromCell = IntVec3.Invalid;
                drawToCell = IntVec3.Invalid;
                return fallback;
            }

            Vector3 from = drawFromCell.ToVector3Shifted();
            Vector3 to = drawToCell.ToVector3Shifted();
            from.y = fallback.y;
            to.y = fallback.y;
            return Vector3.Lerp(from, to, progress);
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
                expireTick = spawnTick + Mathf.Max(1, PropsTornado.durationTicks);
            }

            if (ticksToPulse <= 0)
            {
                ticksToPulse = Rand.RangeInclusive(1, Mathf.Max(1, PropsTornado.pulseIntervalTicks));
            }

            if (ticksToMove <= 0)
            {
                ticksToMove = Mathf.Max(1, PropsTornado.moveIntervalTicks);
            }

            if (grantedCoreHitPoints <= 0)
            {
                grantedCoreHitPoints = Mathf.Clamp(Mathf.Min(parent.HitPoints, ComputeCoreCapacity()), 1, parent.MaxHitPoints);
                if (parent.HitPoints > grantedCoreHitPoints)
                {
                    parent.HitPoints = grantedCoreHitPoints;
                }
            }
        }

        private int ComputeCoreCapacity()
        {
            float progress = Mathf.Clamp01(AgeTicks / (float)Mathf.Max(1, PropsTornado.coreGrowTicks));
            int configuredMax = Mathf.Max(1, PropsTornado.maxHitPoints);
            int statMax = parent?.MaxHitPoints ?? configuredMax;
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(Mathf.Max(1, PropsTornado.initialHitPoints), configuredMax, progress)), 1, statMax);
        }

        private void GrowCoreHitPoints()
        {
            int capacity = CurrentCoreCapacity;
            if (capacity <= grantedCoreHitPoints)
            {
                return;
            }

            int delta = capacity - grantedCoreHitPoints;
            grantedCoreHitPoints = capacity;
            parent.HitPoints = Mathf.Min(parent.MaxHitPoints, parent.HitPoints + delta);
        }

        private void MaintainCoreMote()
        {
            ThingDef moteDef = PropsTornado.coreMoteDef ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_BurningPillarCore;
            if (moteDef == null || parent.MapHeld == null || !parent.Spawned)
            {
                return;
            }

            if (coreMote == null || coreMote.Destroyed)
            {
                coreMote = MoteMaker.MakeAttachedOverlay(parent, moteDef, Vector3.zero, Mathf.Max(0.1f, PropsTornado.coreMoteScale));
            }

            coreMote.Maintain();
            SyncCoreMotePosition();
        }

        private void SyncCoreMotePosition()
        {
            if (coreMote != null && !coreMote.Destroyed)
            {
                coreMote.exactPosition = SmoothedDrawLoc(parent.DrawPos);
            }
        }

        private void TickDamagePulse()
        {
            ticksToPulse--;
            if (ticksToPulse > 0)
            {
                return;
            }

            ticksToPulse = Mathf.Max(1, PropsTornado.pulseIntervalTicks);
            Pulse();
        }

        private void TickMovement()
        {
            if (!ControlsUnlocked || !HasDestination)
            {
                ticksToMove = Mathf.Max(1, PropsTornado.moveIntervalTicks);
                return;
            }

            ticksToMove--;
            if (ticksToMove > 0)
            {
                return;
            }

            ticksToMove = Mathf.Max(1, PropsTornado.moveIntervalTicks);
            TryMoveOneCell();
        }

        private void Pulse()
        {
            float radius = CurrentRadius;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, radius, true))
            {
                if (thing == parent || thing.Destroyed || !thing.Spawned || thing.def.category == ThingCategory.Mote)
                {
                    continue;
                }

                Pawn pawn;
                if (MingyuanUtility.IsHostilePawn(thing, caster, out pawn))
                {
                    HandlePawn(pawn, radius);
                    continue;
                }

                if (thing.def.category == ThingCategory.Building)
                {
                    HandleBuilding(thing);
                }
            }
        }

        private void HandlePawn(Pawn pawn, float radius)
        {
            float edgeFactor = Mathf.Clamp01((pawn.Position - parent.Position).LengthHorizontal / Mathf.Max(0.1f, radius));
            float burnDamage = Mathf.Lerp(PropsTornado.centerBurnDamage, PropsTornado.edgeBurnDamage, edgeFactor);
            float cutDamage = Mathf.Lerp(PropsTornado.centerCutDamage, PropsTornado.edgeCutDamage, edgeFactor);

            MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, burnDamage, caster, scaleWithSelfBurn: true);
            MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Cut, cutDamage, caster, scaleWithSelfBurn: true);
            MingyuanUtility.AddLifeBurn(pawn, caster, Mathf.Max(0f, burnDamage * PropsTornado.lifeBurnLayersPerBurnDamage), scaleWithOverburn: true);
        }

        private void HandleBuilding(Thing building)
        {
            if (building == null || building.Destroyed || !building.def.useHitPoints || building.MaxHitPoints <= 0)
            {
                return;
            }

            float desiredDamage = Mathf.Max(1f, Mathf.Ceil(building.MaxHitPoints * Mathf.Clamp01(PropsTornado.buildingDamageFraction)));
            MingyuanUtility.ApplyTrueDamage(building, DamageDefOf.Burn, AdjustBurnDamageForBuilding(building, desiredDamage), caster, scaleWithSelfBurn: false);
        }

        private float AdjustBurnDamageForBuilding(Thing building, float desiredDamage)
        {
            DamageDef burn = DamageDefOf.Burn;
            float multiplier = burn.buildingDamageFactor;
            multiplier *= building.def.passability != Traversability.Impassable ? burn.buildingDamageFactorPassable : burn.buildingDamageFactorImpassable;
            if (burn.scaleDamageToBuildingsBasedOnFlammability)
            {
                multiplier *= Mathf.Max(0.05f, building.GetStatValue(StatDefOf.Flammability));
            }

            return multiplier > 0.0001f ? desiredDamage / multiplier : desiredDamage;
        }

        private void TryMoveOneCell()
        {
            Map map = parent.Map;
            if (!IsValidDestination(destinationCell, map))
            {
                ClearDestination();
                return;
            }

            IntVec3 current = parent.Position;
            if (current == destinationCell)
            {
                ClearDestination();
                return;
            }

            IntVec3 destination = NextStraightStep(current, destinationCell);
            if (!CanOccupy(destination, map))
            {
                ClearDestination();
                return;
            }

            bool wasSelected = Find.Selector.IsSelected(parent);
            Rot4 rotation = parent.Rotation;
            parent.DeSpawn(DestroyMode.Vanish);
            GenSpawn.Spawn(parent, destination, map, rotation, WipeMode.Vanish);
            drawFromCell = current;
            drawToCell = destination;
            drawMoveStartTick = Find.TickManager.TicksGame;
            SyncCoreMotePosition();
            if (wasSelected)
            {
                Find.Selector.Select(parent, false, false);
            }
        }

        private static bool CanOccupy(IntVec3 cell, Map map)
        {
            return map != null && cell.InBounds(map) && cell.GetTerrain(map).passability != Traversability.Impassable && cell.GetFirstBuilding(map) == null;
        }

        private static bool IsValidDestination(IntVec3 cell, Map map)
        {
            return cell.IsValid && CanOccupy(cell, map);
        }

        private static IntVec3 NextStraightStep(IntVec3 current, IntVec3 destination)
        {
            IntVec3 delta = destination - current;
            return current + new IntVec3(Mathf.Clamp(delta.x, -1, 1), 0, Mathf.Clamp(delta.z, -1, 1));
        }

        private string ControlStateLabel()
        {
            if (!ControlsUnlocked)
            {
                return "MX_Mingyuan_BurningPillar_ControlLocked".Translate().ToString();
            }

            if (HasDestination)
            {
                return "MX_Mingyuan_BurningPillar_ControlMoving".Translate(destinationCell.ToString()).ToString();
            }

            return "MX_Mingyuan_BurningPillar_ControlStopped".Translate().ToString();
        }

        private static int TicksToSeconds(int ticks)
        {
            return Mathf.CeilToInt(Mathf.Max(0, ticks) / 60f);
        }

        private Command_Target MakeMoveCommand()
        {
            TargetingParameters targetingParameters = TargetingParameters.ForCell();
            targetingParameters.validator = target => target.IsValid && target.Map == parent.Map && target.Cell != parent.Position && IsValidDestination(target.Cell, parent.Map);
            return new Command_Target
            {
                defaultLabel = "MX_Mingyuan_BurningPillar_MoveLabel".Translate().ToString(),
                defaultDesc = "MX_Mingyuan_BurningPillar_MoveDesc".Translate().ToString(),
                icon = TexCommand.GatherSpotActive,
                targetingParams = targetingParameters,
                action = delegate(LocalTargetInfo target)
                {
                    TrySetDestination(target.Cell);
                },
                onUpdate = delegate(LocalTargetInfo target)
                {
                    DrawMovePreview(target);
                }
            };
        }

        private void TrySetDestination(IntVec3 cell)
        {
            if (!IsValidDestination(cell, parent.Map) || cell == parent.Position)
            {
                Messages.Message("MX_Mingyuan_BurningPillar_InvalidDestination".Translate(), parent, MessageTypeDefOf.RejectInput, false);
                return;
            }

            destinationCell = cell;
            ticksToMove = Mathf.Max(1, PropsTornado.moveIntervalTicks);
        }

        private void ClearDestination()
        {
            destinationCell = IntVec3.Invalid;
        }

        private void DrawMovePreview(LocalTargetInfo target)
        {
            if (parent.Map == null)
            {
                return;
            }

            GenDraw.DrawRadiusRing(parent.Position, CurrentRadius, new Color(1f, 0.76f, 0.32f, 0.42f));
            if (target.IsValid && target.Cell.InBounds(parent.Map))
            {
                GenDraw.DrawRadiusRing(target.Cell, CurrentRadius, IsValidDestination(target.Cell, parent.Map) ? new Color(1f, 0.82f, 0.38f, 0.55f) : new Color(1f, 0.25f, 0.18f, 0.45f));
                GenDraw.DrawLineBetween(parent.DrawPos, target.Cell.ToVector3Shifted(), SimpleColor.Yellow, 0.12f);
            }
        }
    }

    public class Graphic_MingyuanBurningPillarTornado : Graphic_Single
    {
        private static readonly MaterialPropertyBlock TornadoPropertyBlock = new MaterialPropertyBlock();

        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            Thing_MingyuanBurningPillarField pillar = thing as Thing_MingyuanBurningPillarField;
            float alpha = pillar?.VisualAlpha ?? 1f;
            if (alpha <= 0.001f)
            {
                return;
            }

            float scale = pillar?.VisualScale ?? 1f;
            float drawRotation = pillar?.VisualRotation ?? extraRotation;
            Vector3 offset = data != null ? data.drawOffset : Vector3.zero;
            Vector3 drawScale = new Vector3(drawSize.x * scale, 1f, drawSize.y * scale);
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(loc + offset, Quaternion.AngleAxis(drawRotation, Vector3.up), drawScale);
            Color drawColor = Color;
            drawColor.a *= alpha;
            TornadoPropertyBlock.SetColor(ShaderPropertyIDs.Color, drawColor);
            Graphics.DrawMesh(MeshPool.plane10, matrix, MatSingle, 0, null, 0, TornadoPropertyBlock);
        }

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_MingyuanBurningPillarTornado>(path, newShader, drawSize, newColor, newColorTwo, data);
        }
    }

    public class CompProperties_MingyuanBurningField : CompProperties
    {
        public float radius = 15f;
        public int durationTicks = 10000;
        public int pulseIntervalTicks = 15;
        public float damageAmount = 100f;
        public float armorPenetration = 999f;
        public float lifeBurnLayers = 100f;
        public bool destroyBuildings;
        public bool destroyAnimals;
        public bool scalesWithSelfBurn;
        public float selfBurnLifeBurnPer100 = 20f;
        public float selfBurnDamagePerLayer = 0.01f;
        public float selfHealAmount = 1f;
        public float maxSelfBurnGainPerPulse = 20f;
        public ThingDef pulseMoteDef;
        public ThingDef hitMoteDef;
        public ThingDef selfHealMoteDef;
        public float pulseMoteScale = 1f;
        public float hitMoteScale = 1f;
        public float selfHealMoteScale = 1f;
        public int maxHitMotesPerPulse = 12;
        public Color previewRingColor = new Color(1f, 0.75f, 0.34f, 0.64f);

        public CompProperties_MingyuanBurningField()
        {
            compClass = typeof(CompMingyuanBurningField);
        }
    }

    public class CompMingyuanBurningField : ThingComp
    {
        private Pawn caster;
        private int expireTick;
        private int ticksToPulse;
        private float selfBurnGainedThisPulse;

        public CompProperties_MingyuanBurningField PropsField => (CompProperties_MingyuanBurningField)props;

        public IntVec3 CenterCell
        {
            get
            {
                if (caster != null && !caster.Destroyed && !caster.Dead && caster.Spawned && caster.MapHeld == parent.MapHeld)
                {
                    return caster.PositionHeld;
                }

                return parent.Position;
            }
        }

        public Vector3 DrawLoc
        {
            get
            {
                if (caster != null && !caster.Destroyed && !caster.Dead && caster.Spawned && caster.MapHeld == parent.MapHeld)
                {
                    return caster.DrawPos;
                }

                return parent.DrawPos;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster", false);
            Scribe_Values.Look(ref expireTick, "expireTick", 0);
            Scribe_Values.Look(ref ticksToPulse, "ticksToPulse", 0);
        }

        public void Init(Pawn newCaster, int durationOverride = -1)
        {
            caster = newCaster;
            expireTick = Find.TickManager.TicksGame + (durationOverride > 0 ? durationOverride : PropsField.durationTicks);
            ticksToPulse = 1;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Destroyed || parent.Map == null)
            {
                return;
            }

            if (Find.TickManager.TicksGame >= expireTick || caster == null || caster.Destroyed || caster.Dead)
            {
                parent.Destroy(DestroyMode.Vanish);
                return;
            }

            ticksToPulse--;
            if (ticksToPulse > 0)
            {
                return;
            }

            ticksToPulse = Mathf.Max(1, PropsField.pulseIntervalTicks);
            Pulse();
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (caster != null && Find.Selector.IsSelected(caster))
            {
                GenDraw.DrawRadiusRing(CenterCell, PropsField.radius, PropsField.previewRingColor);
            }
        }

        public override string CompInspectStringExtra()
        {
            int remainingSeconds = Mathf.CeilToInt(Mathf.Max(0, expireTick - Find.TickManager.TicksGame) / 60f);
            return "MX_Mingyuan_AshesField_Inspect".Translate(
                PropsField.radius.ToString("F1"),
                PropsField.damageAmount.ToString("0.#"),
                PropsField.lifeBurnLayers.ToString("0.#"),
                remainingSeconds.ToString()).ToString();
        }

        private void Pulse()
        {
            SpawnPulseMote();
            int spawnedHitMotes = 0;
            selfBurnGainedThisPulse = 0f;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(CenterCell, parent.Map, PropsField.radius, true))
            {
                if (thing == parent || thing.Destroyed)
                {
                    continue;
                }

                Pawn pawn = thing as Pawn;
                if (pawn != null)
                {
                    HandlePawn(pawn, ref spawnedHitMotes);
                    continue;
                }

                if (PropsField.destroyBuildings && thing.def.category == ThingCategory.Building && thing.Spawned)
                {
                    thing.Destroy(DestroyMode.Deconstruct);
                }
            }
        }

        private void HandlePawn(Pawn pawn, ref int spawnedHitMotes)
        {
            if (pawn == null || pawn.Dead)
            {
                return;
            }

            if (pawn == caster)
            {
                HandleCasterPulse();
                return;
            }

            if (PropsField.destroyAnimals && pawn.RaceProps != null && pawn.RaceProps.Animal)
            {
                DamageInfo killInfo = new DamageInfo(DamageDefOf.Burn, 99999f, 999f, -1f, caster);
                killInfo.SetIgnoreArmor(true);
                killInfo.SetIgnoreInstantKillProtection(true);
                killInfo.SetApplyAllDamage(true);
                pawn.Kill(killInfo);
                return;
            }

            if (!pawn.HostileTo(caster))
            {
                return;
            }

            float selfBurn = PropsField.scalesWithSelfBurn ? MingyuanUtility.GetSelfBurnEffectiveLayers(caster) : 0f;
            float damage = PropsField.damageAmount;
            float layers = PropsField.lifeBurnLayers + (selfBurn / 100f) * PropsField.selfBurnLifeBurnPer100;

            MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, damage, caster);
            MingyuanUtility.AddLifeBurn(pawn, caster, layers, scaleWithOverburn: true);
            TrySpawnHitMote(pawn, ref spawnedHitMotes);
            if (PropsField.scalesWithSelfBurn)
            {
                float gain = Mathf.Max(1f, layers / 20f);
                float remainingGain = Mathf.Max(0f, PropsField.maxSelfBurnGainPerPulse - selfBurnGainedThisPulse);
                if (remainingGain > 0f)
                {
                    float appliedGain = Mathf.Min(gain, remainingGain);
                    MingyuanUtility.AddSelfBurn(caster, appliedGain);
                    selfBurnGainedThisPulse += appliedGain;
                }
            }
        }

        private void HandleCasterPulse()
        {
            if (caster == null || caster.Dead || !MingyuanUtility.HasHediff(caster, MingyuanUtility.BurningBodyDef))
            {
                return;
            }

            MingyuanUtility.HealInjuriesIncludingScars(caster, Mathf.Max(0f, PropsField.selfHealAmount));
            ThingDef moteDef = PropsField.selfHealMoteDef ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_SelfBurnGain;
            MingyuanUtility.TryMakeAttachedMote(caster, moteDef, PropsField.selfHealMoteScale);
        }

        private void SpawnPulseMote()
        {
            ThingDef moteDef = PropsField.pulseMoteDef ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_AshesPulse;
            MingyuanUtility.TryMakeStaticMote(CenterCell, parent.Map, moteDef, PropsField.pulseMoteScale);
        }

        private void TrySpawnHitMote(Pawn pawn, ref int spawnedHitMotes)
        {
            if (spawnedHitMotes >= Mathf.Max(0, PropsField.maxHitMotesPerPulse))
            {
                return;
            }

            ThingDef moteDef = PropsField.hitMoteDef ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_AshesHit;
            if (MingyuanUtility.TryMakeAttachedMote(pawn, moteDef, PropsField.hitMoteScale))
            {
                spawnedHitMotes++;
            }
        }
    }
}
