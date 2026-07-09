using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityAscentSlash : CompProperties_AbilityEffect
    {
        public ThingDef flyerDef;
        public ThingDef knockbackFlyerDef;
        public float range = 22f;
        public float coneRadius = 5.5f;
        public float coneAngleDegrees = 80f;
        public DamageDef damageDef;
        public float damageAmount = 32f;
        public float armorPenetration = 0.35f;
        public DamageDef accumulationDamageDef = MX_StatusEffectsDefOf.MX_StatusEffectBleedAccumulation;
        public float accumulationDamageAmount = 0.18f;
        public float accumulationArmorPenetration = 2.1f;
        public float buildingDamageMultiplier = 2f;
        public int stunTicks = 60;
        public float knockbackDistance = 3f;
        public float flowerDecreeCost = 1f;
        public int impactDelayTicks = 30;

        public string disabledReason = "MX_QH_AscentSlashNotLearned";
        public string noLineOfSightToLandingMessage = "MX_QH_FlowerDanceLandingNoLineOfSight";
        public string invalidLandingMessage = "MX_QH_FlowerDanceInvalidLanding";

        public string entryEffecter;
        public string exitEffecter;
        public string impactEffecter = "ImpactSmallDustCloud";
        public string takeoffGroundEffecter = "MXNL_Effecter_Skyfall_FlyBeginGround";
        public string entryFleck;
        public string exitFleck;
        public string impactFleck = "ExplosionFlash";
        public string ascentTrailFleck = "MXNL_Skyfall_FlyBegin_F";
        public float ascentTrailFleckScale = 1.15f;
        public float ascentTrailOffsetX = 0f;
        public float ascentTrailOffsetZ = 8f;
        public string hitFleck = "PsycastAreaEffect";
        public SoundDef castSound;

        public CompProperties_AbilityAscentSlash()
        {
            compClass = typeof(CompAbilityEffect_AscentSlash);
        }
    }

    public class CompAbilityEffect_AscentSlash : CompAbilityEffect, ICompAbilityEffectOnJumpCompleted
    {
        private static readonly Color ConePreviewColor = new Color(1f, 0.45f, 0.65f, 0.55f);
        private const int AscentSlashArcDurationTicks = 36;

        private readonly List<IntVec3> tmpPreviewCells = new List<IntVec3>();
        private readonly List<Thing> tmpPreviewTargets = new List<Thing>();
        private readonly HashSet<Thing> tmpPreviewTargetSet = new HashSet<Thing>();
        private bool reselectCasterOnLanding;

        public new CompProperties_AbilityAscentSlash Props => (CompProperties_AbilityAscentSlash)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (!HasLearnedJueying(parent?.pawn))
            {
                reason = Props.disabledReason.Translate();
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            Pawn caster = parent?.pawn;
            if (!HasLearnedJueying(caster))
            {
                if (throwMessages)
                {
                    Messages.Message(Props.disabledReason.Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return ValidateLanding(caster, target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || caster.MapHeld == null)
            {
                return;
            }

            if (!ValidateLanding(caster, target, true))
            {
                return;
            }

            IntVec3 origin = caster.Position;
            IntVec3 landing = target.Cell;
            IntVec3 directionCell = ComputeDirectionCell(origin, landing);
            Map map = caster.MapHeld;

            RememberSelectionForFlight(caster);
            AddAscentSlashInvulnerability(caster);
            PlayTakeoffVisuals(map, origin);

            if (Props.flyerDef != null)
            {
                PawnFlyer flyer = PawnFlyer.MakeFlyer(Props.flyerDef, caster, landing, null, Props.castSound, triggeringAbility: parent, target: target);
                GenSpawn.Spawn(flyer, landing, map);
                RestoreCasterSelectionDuringFlight(caster);
                return;
            }

            ResolveLandingImpact(caster, origin, landing, directionCell);
            RemoveAscentSlashInvulnerability(caster);
            RestoreCasterSelectionIfNeeded(caster);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            DrawLandingPreview(target);
        }

        public void OnJumpCompleted(IntVec3 origin, LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || caster.MapHeld == null || !target.IsValid)
            {
                return;
            }

            IntVec3 landing = caster.Position;
            ResolveLandingImpact(caster, origin, landing, ComputeDirectionCell(origin, landing));
            RemoveAscentSlashInvulnerability(caster);
            RestoreCasterSelectionIfNeeded(caster);
        }

        public void DrawLandingPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned)
            {
                return;
            }

            string reason;
            if (!CanLand(caster, target, out reason))
            {
                return;
            }

            IntVec3 directionCell = ComputeDirectionCell(caster.Position, target.Cell);
            BuildConeCells(caster.MapHeld, target.Cell, directionCell, tmpPreviewCells);
            GenDraw.DrawFieldEdges(tmpPreviewCells, ConePreviewColor);
            DrawAffectedTargetHighlights(target.Cell, directionCell);
        }

        private bool ValidateLanding(Pawn caster, LocalTargetInfo target, bool showMessages)
        {
            string reason;
            if (!CanLand(caster, target, out reason))
            {
                return Reject(reason, caster, target, showMessages);
            }

            return true;
        }

        private bool CanLand(Pawn caster, LocalTargetInfo target, out string reason)
        {
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !target.IsValid || !target.Cell.InBounds(map))
            {
                reason = Props.invalidLandingMessage.Translate();
                return false;
            }

            if (Props.range > 0f && caster.Position.DistanceTo(target.Cell) > Props.range)
            {
                reason = "AbilityOutOfRange".Translate();
                return false;
            }

            if (!GenSight.LineOfSight(caster.Position, target.Cell, map))
            {
                reason = Props.noLineOfSightToLandingMessage.Translate();
                return false;
            }

            if (!JumpUtility.ValidJumpTarget(caster, map, target.Cell))
            {
                reason = Props.invalidLandingMessage.Translate();
                return false;
            }

            reason = null;
            return true;
        }

        private bool Reject(string message, Pawn caster, LocalTargetInfo target, bool showMessages)
        {
            if (showMessages)
            {
                LookTargets lookTargets = caster != null && target.IsValid && caster.MapHeld != null
                    ? new LookTargets(caster, target.ToTargetInfo(caster.MapHeld))
                    : null;
                Messages.Message(message, lookTargets, MessageTypeDefOf.RejectInput, historical: false);
            }

            return false;
        }

        private static IntVec3 ComputeDirectionCell(IntVec3 origin, IntVec3 landing)
        {
            IntVec3 offset = landing - origin;
            offset.y = 0;
            if (offset.x == 0 && offset.z == 0)
            {
                return landing + IntVec3.North;
            }

            return landing + offset;
        }

        private static bool HasLearnedJueying(Pawn pawn)
        {
            return MX_QH_HediffUtility.EnsureFlowerResonance(pawn)?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Jueying) == true;
        }

        private void ResolveLandingImpact(Pawn caster, IntVec3 origin, IntVec3 landing, IntVec3 directionCell)
        {
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !landing.IsValid || !directionCell.IsValid)
            {
                return;
            }

            caster.rotationTracker?.FaceCell(directionCell);
            PlayVisuals(map, landing, landing, Props.exitEffecter, Props.exitFleck, 1.15f);
            MX_QHGraphicsUtility.Fx(map, landing, Props.impactEffecter, 1f);
            MX_QHGraphicsUtility.Fleck(map, landing, Props.impactFleck, Mathf.Max(0.8f, Props.coneRadius * 0.18f));
            Props.castSound?.PlayOneShot(new TargetInfo(landing, map));

            Vector3 forward = ComputeForward(landing, directionCell);
            map.GetComponent<MapComponent_QingheAscentSlashVisuals>()?.AddArc(landing, forward, Props.coneRadius, Props.coneAngleDegrees, AscentSlashArcDurationTicks);
            map.GetComponent<MapComponent_QingheAscentSlashVisuals>()?.AddDelayedImpact(caster, landing, directionCell, Props.impactDelayTicks, Props);
            MX_QH_HediffUtility.GetFlowerDecree(caster)?.TryConsumeDecree(Props.flowerDecreeCost);
        }

        private void RememberSelectionForFlight(Pawn caster)
        {
            reselectCasterOnLanding = caster != null && Find.Selector != null && Find.Selector.IsSelected(caster);
        }

        private static void AddAscentSlashInvulnerability(Pawn caster)
        {
            if (caster?.health == null || MX_QHDefOf.MX_QH_AscentSlashInvulnerable == null)
            {
                return;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AscentSlashInvulnerable);
            if (hediff == null)
            {
                hediff = caster.health.AddHediff(MX_QHDefOf.MX_QH_AscentSlashInvulnerable);
            }

            hediff?.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
        }

        private static void RemoveAscentSlashInvulnerability(Pawn caster)
        {
            if (caster?.health == null || MX_QHDefOf.MX_QH_AscentSlashInvulnerable == null)
            {
                return;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AscentSlashInvulnerable);
            if (hediff != null)
            {
                caster.health.RemoveHediff(hediff);
            }
        }

        public static void ResolveDelayedConeImpact(Pawn caster, Map map, IntVec3 landing, IntVec3 directionCell, CompProperties_AbilityAscentSlash props)
        {
            if (props == null)
            {
                return;
            }

            ResolveCone(caster, map, landing, directionCell, props);
        }

        private static void ResolveCone(Pawn caster, Map map, IntVec3 landing, IntVec3 directionCell, CompProperties_AbilityAscentSlash props)
        {
            Vector3 forward = ComputeForward(landing, directionCell);
            if (forward.sqrMagnitude < 0.001f)
            {
                return;
            }

            float halfAngle = Mathf.Clamp(props.coneAngleDegrees, 1f, 360f) * 0.5f;
            DamageDef damageDef = props.damageDef ?? MX_QHDefOf.MX_QH_NoteImpact ?? DamageDefOf.Blunt;
            List<Thing> victims = CollectHostileTargetsInCone(map, landing, caster, props.coneRadius, forward, halfAngle);
            float specialFactor = MX_QHSkillUtility.GetSpecialAbilityEffectFactor(caster);

            for (int i = 0; i < victims.Count; i++)
            {
                Thing victim = victims[i];
                float damageAmount = props.damageAmount * specialFactor;
                if (victim is Building)
                {
                    damageAmount *= props.buildingDamageMultiplier;
                }
                victim.TakeDamage(new DamageInfo(damageDef, damageAmount, props.armorPenetration, -1f, caster));

                Pawn victimPawn = victim as Pawn;
                ApplyAccumulation(victimPawn, caster, props, specialFactor);
                if (props.stunTicks > 0 && victimPawn != null && !victimPawn.Dead && !victimPawn.Destroyed)
                {
                    victimPawn.stances?.stunner?.StunFor(props.stunTicks, caster);
                }

                TryKnockback(victimPawn, landing, props.knockbackDistance, props.knockbackFlyerDef);
                if (victim.Spawned && victim.MapHeld == map)
                {
                    MX_QHGraphicsUtility.Fleck(map, victim.Position, props.hitFleck, 0.7f);
                }
            }
        }

        private static void ApplyAccumulation(Pawn victim, Pawn caster, CompProperties_AbilityAscentSlash props, float specialFactor)
        {
            if (victim == null || victim.Dead || victim.Destroyed || props.accumulationDamageDef == null || props.accumulationDamageAmount <= 0f)
            {
                return;
            }

            victim.TakeDamage(new DamageInfo(
                props.accumulationDamageDef,
                props.accumulationDamageAmount * specialFactor,
                props.accumulationArmorPenetration,
                -1f,
                caster));
        }

        private static List<Thing> CollectHostileTargetsInCone(Map map, IntVec3 center, Pawn caster, float radius, Vector3 forward, float halfAngle)
        {
            List<Thing> result = new List<Thing>();
            HashSet<Thing> unique = new HashSet<Thing>();
            CollectHostileTargetsInCone(map, center, caster, radius, forward, halfAngle, result, unique);
            return result;
        }

        private static void CollectHostileTargetsInCone(Map map, IntVec3 center, Pawn caster, float radius, Vector3 forward, float halfAngle, List<Thing> outTargets, HashSet<Thing> unique)
        {
            outTargets.Clear();
            unique.Clear();
            if (map == null || caster == null)
            {
                return;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map) || cell == center)
                {
                    continue;
                }

                if (!GenSight.LineOfSightToEdges(center, cell, map, skipFirstCell: true))
                {
                    continue;
                }

                Vector3 toCell = (cell - center).ToVector3();
                toCell.y = 0f;
                if (toCell.sqrMagnitude < 0.001f || Vector3.Angle(forward, toCell.normalized) > halfAngle)
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (!CanHitWithAscentSlash(caster, thing))
                    {
                        continue;
                    }

                    if (unique.Add(thing))
                    {
                        outTargets.Add(thing);
                    }
                }
            }
        }

        private static bool CanHitWithAscentSlash(Pawn caster, Thing thing)
        {
            if (thing == null || thing == caster || thing.Destroyed || !thing.Spawned)
            {
                return false;
            }

            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                return !pawn.Dead && GenHostility.HostileTo(caster, pawn);
            }

            Building building = thing as Building;
            return building != null && thing.HostileTo(caster);
        }

        private static Vector3 ComputeForward(IntVec3 source, IntVec3 target)
        {
            Vector3 forward = (target - source).ToVector3();
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                return Vector3.zero;
            }

            forward.Normalize();
            return forward;
        }

        private static void TryKnockback(Pawn pawn, IntVec3 center, float distance, ThingDef flyerDef)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || !pawn.Spawned || pawn.MapHeld == null || distance <= 0f)
            {
                return;
            }

            Map map = pawn.MapHeld;
            IntVec3 start = pawn.Position;
            Vector3 direction = (start - center).ToVector3();
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
            }
            direction.Normalize();

            IntVec3 best = start;
            int steps = Mathf.Max(1, Mathf.RoundToInt(distance));
            for (int i = 1; i <= steps; i++)
            {
                IntVec3 next = start + (direction * i).ToIntVec3();
                if (!ValidKnockbackCell(map, next, pawn))
                {
                    break;
                }
                best = next;
            }

            if (best == start)
            {
                return;
            }

            pawn.pather?.StopDead();
            pawn.jobs?.StopAll(false, true);
            pawn.stances?.CancelBusyStanceHard();

            if (flyerDef != null)
            {
                PawnFlyer flyer = PawnFlyer.MakeFlyer(flyerDef, pawn, best, null, null);
                GenSpawn.Spawn(flyer, best, map);
                return;
            }

            pawn.Position = best;
            pawn.pather?.StopDead();
            pawn.jobs?.StopAll(false, true);
        }

        private static bool ValidKnockbackCell(Map map, IntVec3 cell, Pawn movingPawn)
        {
            if (!cell.IsValid || !cell.InBounds(map) || !cell.Walkable(map) || cell.Impassable(map) || cell.Fogged(map))
            {
                return false;
            }

            Building_Door door = cell.GetEdifice(map) as Building_Door;
            if (door != null && !door.Open)
            {
                return false;
            }

            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Pawn other = things[i] as Pawn;
                if (other != null && other != movingPawn && other.Spawned && !other.Dead)
                {
                    return false;
                }
            }

            return true;
        }

        private void BuildConeCells(Map map, IntVec3 landing, IntVec3 directionCell, List<IntVec3> outCells)
        {
            outCells.Clear();
            if (map == null || !landing.IsValid || !landing.InBounds(map) || !directionCell.IsValid)
            {
                return;
            }

            Vector3 forward = (directionCell - landing).ToVector3();
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                return;
            }

            forward.Normalize();
            float halfAngle = Mathf.Clamp(Props.coneAngleDegrees, 1f, 360f) * 0.5f;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(landing, Props.coneRadius, true))
            {
                if (!cell.InBounds(map) || cell == landing)
                {
                    continue;
                }

                if (!GenSight.LineOfSightToEdges(landing, cell, map, skipFirstCell: true))
                {
                    continue;
                }

                Vector3 toCell = (cell - landing).ToVector3();
                toCell.y = 0f;
                if (toCell.sqrMagnitude < 0.001f || Vector3.Angle(forward, toCell.normalized) > halfAngle)
                {
                    continue;
                }

                outCells.Add(cell);
            }
        }

        private void DrawAffectedTargetHighlights(IntVec3 landing, IntVec3 directionCell)
        {
            Pawn caster = parent?.pawn;
            Map map = caster?.MapHeld;
            if (map == null)
            {
                return;
            }

            Vector3 forward = ComputeForward(landing, directionCell);
            if (forward.sqrMagnitude < 0.001f)
            {
                return;
            }

            float halfAngle = Mathf.Clamp(Props.coneAngleDegrees, 1f, 360f) * 0.5f;
            CollectHostileTargetsInCone(map, landing, caster, Props.coneRadius, forward, halfAngle, tmpPreviewTargets, tmpPreviewTargetSet);
            for (int i = 0; i < tmpPreviewTargets.Count; i++)
            {
                GenDraw.DrawTargetHighlight(tmpPreviewTargets[i]);
            }
        }

        private void RestoreCasterSelectionIfNeeded(Pawn caster)
        {
            if (!reselectCasterOnLanding)
            {
                return;
            }

            reselectCasterOnLanding = false;
            if (caster == null || caster.Destroyed || !caster.Spawned || caster.MapHeld != Find.CurrentMap || Find.Selector == null || Find.Selector.IsSelected(caster))
            {
                return;
            }

            Find.Selector.Select(caster, playSound: false, forceDesignatorDeselect: false);
        }

        private void RestoreCasterSelectionDuringFlight(Pawn caster)
        {
            if (!reselectCasterOnLanding || caster == null || caster.Destroyed || Find.Selector == null || Find.Selector.IsSelected(caster))
            {
                return;
            }

            Map heldMap = caster.MapHeld;
            if (heldMap == null || heldMap != Find.CurrentMap)
            {
                return;
            }

            Find.Selector.Select(caster, playSound: false, forceDesignatorDeselect: false);
        }

        private static void PlayVisuals(Map map, IntVec3 source, IntVec3 cell, string effecter, string fleck, float scale)
        {
            MX_QHGraphicsUtility.Fx(map, cell, effecter, scale);
            MX_QHGraphicsUtility.Fleck(map, cell, fleck, scale);
            if (source.IsValid && source.InBounds(map) && source != cell)
            {
                GenDraw.DrawLineBetween(source.ToVector3Shifted(), cell.ToVector3Shifted());
            }
        }

        private void PlayTakeoffVisuals(Map map, IntVec3 origin)
        {
            MX_QHGraphicsUtility.Fx(map, origin, Props.takeoffGroundEffecter, 1f);
            Vector3 trailPos = origin.ToVector3Shifted() + new Vector3(Props.ascentTrailOffsetX, 0f, Props.ascentTrailOffsetZ);
            MX_QHGraphicsUtility.Fleck(map, trailPos, Props.ascentTrailFleck, Props.ascentTrailFleckScale);
            MX_QHGraphicsUtility.Fx(map, origin, Props.entryEffecter, 1f);
            MX_QHGraphicsUtility.Fleck(map, origin, Props.entryFleck, 1f);
        }
    }

}



