using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
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
        public int stunTicks = 60;
        public float knockbackDistance = 3f;
        public float flowerDecreeCost = 1f;
        public int impactDelayTicks = 30;

        public string disabledReason = "MX_QH_FlowerDanceNotLearned";
        public string noLineOfSightToLandingMessage = "MX_QH_FlowerDanceLandingNoLineOfSight";
        public string noLineOfSightToDirectionMessage = "MX_QH_FlowerDanceDirectionNoLineOfSight";
        public string invalidLandingMessage = "MX_QH_FlowerDanceInvalidLanding";
        public string chooseLandingLabel = "MX_QH_FlowerDanceChooseLanding";
        public string chooseDirectionLabel = "MX_QH_FlowerDanceChooseDirection";

        public string entryEffecter = "Skip_EntryNoDelay";
        public string exitEffecter = "Skip_ExitNoDelay";
        public string impactEffecter = "ImpactSmallDustCloud";
        public string entryFleck = "PsycastSkipFlashEntry";
        public string exitFleck = "PsycastSkipFlashExit";
        public string impactFleck = "ExplosionFlash";
        public string hitFleck = "PsycastAreaEffect";
        public SoundDef castSound;

        public CompProperties_AbilityAscentSlash()
        {
            compClass = typeof(CompAbilityEffect_AscentSlash);
        }
    }

    public class CompAbilityEffect_AscentSlash : CompAbilityEffect, ICompAbilityEffectOnJumpCompleted
    {
        private static readonly Color LandingRangeColor = new Color(0.3f, 0.8f, 1f, 0.45f);
        private static readonly Color ConePreviewColor = new Color(1f, 0.45f, 0.65f, 0.55f);
        private const int AscentSlashArcDurationTicks = 36;

        private readonly List<IntVec3> tmpPreviewCells = new List<IntVec3>();
        private readonly List<Pawn> tmpPreviewPawns = new List<Pawn>();
        private readonly HashSet<Pawn> tmpPreviewPawnSet = new HashSet<Pawn>();
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

            if (!ValidateLanding(caster, target, true) || !ValidateDirection(caster.MapHeld, target.Cell, dest, true))
            {
                return;
            }

            IntVec3 origin = caster.Position;
            IntVec3 landing = target.Cell;
            IntVec3 directionCell = dest.Cell;
            Map map = caster.MapHeld;

            PlayVisuals(map, origin, landing, Props.entryEffecter, Props.entryFleck, 1f);

            if (Props.flyerDef != null)
            {
                PawnFlyer flyer = PawnFlyer.MakeFlyer(Props.flyerDef, caster, landing, null, Props.castSound, triggeringAbility: parent, target: dest);
                GenSpawn.Spawn(flyer, landing, map);
                RestoreCasterSelectionDuringFlight(caster);
                return;
            }

            ResolveLandingImpact(caster, origin, landing, directionCell);
            RestoreCasterSelectionIfNeeded(caster);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
        }

        public void OnJumpCompleted(IntVec3 origin, LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || caster.MapHeld == null || !target.IsValid)
            {
                return;
            }

            ResolveLandingImpact(caster, origin, caster.Position, target.Cell);
            RestoreCasterSelectionIfNeeded(caster);
        }

        public bool ValidateLandingForCommand(LocalTargetInfo target, bool showMessages)
        {
            return ValidateLanding(parent?.pawn, target, showMessages);
        }

        public bool ValidateDirectionForCommand(LocalTargetInfo landing, LocalTargetInfo direction, bool showMessages)
        {
            Pawn caster = parent?.pawn;
            return caster?.MapHeld != null && ValidateDirection(caster.MapHeld, landing.Cell, direction, showMessages);
        }

        public bool CanLandForCommand(LocalTargetInfo target, out string reason)
        {
            return CanLand(parent?.pawn, target, out reason);
        }

        public bool CanChooseDirectionForCommand(LocalTargetInfo landing, LocalTargetInfo direction, out string reason)
        {
            Pawn caster = parent?.pawn;
            return CanChooseDirection(caster?.MapHeld, landing.Cell, direction, out reason);
        }

        public void DrawLandingPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned)
            {
                return;
            }

            GenDraw.DrawRadiusRing(caster.Position, Props.range, LandingRangeColor, cell => LandingCellVisible(caster, cell));
            string reason;
            if (!CanLandForCommand(target, out reason))
            {
                return;
            }

            GenDraw.DrawTargetHighlightWithLayer(target.CenterVector3, AltitudeLayer.MetaOverlays);
        }

        public void DrawDirectionPreview(LocalTargetInfo landing, LocalTargetInfo direction)
        {
            string reason;
            if (!landing.IsValid || !CanChooseDirectionForCommand(landing, direction, out reason))
            {
                return;
            }

            GenDraw.DrawTargetHighlightWithLayer(landing.CenterVector3, AltitudeLayer.MetaOverlays);
            GenDraw.DrawTargetHighlight(direction);
            BuildConeCells(parent?.pawn?.MapHeld, landing.Cell, direction.Cell, tmpPreviewCells);
            GenDraw.DrawFieldEdges(tmpPreviewCells, ConePreviewColor);
            DrawAffectedPawnHighlights(landing.Cell, direction.Cell);
        }

        public void NotifyQueuedFromSelectedCommand()
        {
            Pawn caster = parent?.pawn;
            reselectCasterOnLanding = caster != null && Find.Selector != null && Find.Selector.IsSelected(caster);
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

        private bool ValidateDirection(Map map, IntVec3 landing, LocalTargetInfo direction, bool showMessages)
        {
            Pawn caster = parent?.pawn;
            string reason;
            if (!CanChooseDirection(map, landing, direction, out reason))
            {
                return Reject(reason, caster, direction, showMessages);
            }

            return true;
        }

        private bool CanChooseDirection(Map map, IntVec3 landing, LocalTargetInfo direction, out string reason)
        {
            if (map == null || !landing.IsValid || !landing.InBounds(map) || !direction.IsValid || !direction.Cell.InBounds(map))
            {
                reason = Props.noLineOfSightToDirectionMessage.Translate();
                return false;
            }

            if (direction.Cell == landing)
            {
                reason = Props.noLineOfSightToDirectionMessage.Translate();
                return false;
            }

            if (!GenSight.LineOfSight(landing, direction.Cell, map))
            {
                reason = Props.noLineOfSightToDirectionMessage.Translate();
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

        private bool LandingCellVisible(Pawn caster, IntVec3 cell)
        {
            Map map = caster?.MapHeld;
            return map != null
                && cell.InBounds(map)
                && GenSight.LineOfSight(caster.Position, cell, map)
                && JumpUtility.ValidJumpTarget(caster, map, cell);
        }

        private static bool HasLearnedJueying(Pawn pawn)
        {
            return FlowerCourtUtility.EnsureSkillTreeState(pawn)?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Jueying) == true;
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
            GraphicsUtility.Fx(map, landing, Props.impactEffecter, 1f);
            GraphicsUtility.Fleck(map, landing, Props.impactFleck, Mathf.Max(0.8f, Props.coneRadius * 0.18f));
            Props.castSound?.PlayOneShot(new TargetInfo(landing, map));

            Vector3 forward = ComputeForward(landing, directionCell);
            map.GetComponent<MapComponent_QingheAscentSlashVisuals>()?.AddArc(landing, forward, Props.coneRadius, Props.coneAngleDegrees, AscentSlashArcDurationTicks);
            map.GetComponent<MapComponent_QingheAscentSlashVisuals>()?.AddDelayedImpact(caster, landing, directionCell, Props.impactDelayTicks, Props);
            FlowerCourtUtility.GetFlowerDecree(caster)?.TryConsumeDecree(Props.flowerDecreeCost);
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
            List<Pawn> victims = CollectHostilePawnsInCone(map, landing, caster, props.coneRadius, forward, halfAngle);

            for (int i = 0; i < victims.Count; i++)
            {
                Pawn victim = victims[i];
                victim.TakeDamage(new DamageInfo(damageDef, props.damageAmount, props.armorPenetration, -1f, caster));
                if (props.stunTicks > 0 && !victim.Dead && !victim.Destroyed)
                {
                    victim.stances?.stunner?.StunFor(props.stunTicks, caster);
                }

                TryKnockback(victim, landing, props.knockbackDistance, props.knockbackFlyerDef);
                if (victim.Spawned && victim.MapHeld == map)
                {
                    GraphicsUtility.Fleck(map, victim.Position, props.hitFleck, 0.7f);
                }
            }
        }

        private static List<Pawn> CollectHostilePawnsInCone(Map map, IntVec3 center, Pawn caster, float radius, Vector3 forward, float halfAngle)
        {
            List<Pawn> result = new List<Pawn>();
            HashSet<Pawn> unique = new HashSet<Pawn>();
            CollectHostilePawnsInCone(map, center, caster, radius, forward, halfAngle, result, unique);
            return result;
        }

        private static void CollectHostilePawnsInCone(Map map, IntVec3 center, Pawn caster, float radius, Vector3 forward, float halfAngle, List<Pawn> outPawns, HashSet<Pawn> unique)
        {
            outPawns.Clear();
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
                    Pawn pawn = things[i] as Pawn;
                    if (pawn == null || pawn == caster || pawn.Dead || pawn.Destroyed || !GenHostility.HostileTo(caster, pawn))
                    {
                        continue;
                    }

                    if (unique.Add(pawn))
                    {
                        outPawns.Add(pawn);
                    }
                }
            }
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

        private void DrawAffectedPawnHighlights(IntVec3 landing, IntVec3 directionCell)
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
            CollectHostilePawnsInCone(map, landing, caster, Props.coneRadius, forward, halfAngle, tmpPreviewPawns, tmpPreviewPawnSet);
            for (int i = 0; i < tmpPreviewPawns.Count; i++)
            {
                GenDraw.DrawTargetHighlight(tmpPreviewPawns[i]);
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
            GraphicsUtility.Fx(map, cell, effecter, scale);
            GraphicsUtility.Fleck(map, cell, fleck, scale);
            if (source.IsValid && source.InBounds(map) && source != cell)
            {
                GenDraw.DrawLineBetween(source.ToVector3Shifted(), cell.ToVector3Shifted());
            }
        }
    }

    public class Command_AbilityAscentSlash : Command_Ability
    {
        public Command_AbilityAscentSlash(Ability ability, Pawn pawn) : base(ability, pawn)
        {
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
            if (!Ability.CanCast)
            {
                return;
            }

            CompAbilityEffect_AscentSlash comp = Ability.CompOfType<CompAbilityEffect_AscentSlash>();
            if (comp == null)
            {
                return;
            }

            Find.DesignatorManager.Deselect();
            BeginLandingTargeting(comp);
        }

        private void BeginLandingTargeting(CompAbilityEffect_AscentSlash comp)
        {
            TargetingParameters parameters = TargetingParameters.ForCell();
            Find.Targeter.BeginTargeting(
                parameters,
                landing =>
                {
                    if (!comp.ValidateLandingForCommand(landing, true))
                    {
                        BeginLandingTargeting(comp);
                        return;
                    }

                    BeginDirectionTargeting(comp, landing);
                },
                landing => comp.DrawLandingPreview(landing),
                null,
                Pawn,
                null,
                null,
                playSoundOnAction: true,
                onGuiAction: landing => DrawLandingMouseLabel(comp, landing, Ability.def.uiIcon));
        }

        private void BeginDirectionTargeting(CompAbilityEffect_AscentSlash comp, LocalTargetInfo landing)
        {
            TargetingParameters parameters = TargetingParameters.ForCell();
            Find.Targeter.BeginTargeting(
                parameters,
                direction =>
                {
                    if (!comp.ValidateDirectionForCommand(landing, direction, true))
                    {
                        BeginDirectionTargeting(comp, landing);
                        return;
                    }

                    comp.NotifyQueuedFromSelectedCommand();
                    ability.QueueCastingJob(landing, direction);
                },
                direction => comp.DrawDirectionPreview(landing, direction),
                null,
                Pawn,
                null,
                null,
                playSoundOnAction: true,
                onGuiAction: direction => DrawDirectionMouseLabel(comp, landing, direction, Ability.def.uiIcon));
        }

        private static void DrawLandingMouseLabel(CompAbilityEffect_AscentSlash comp, LocalTargetInfo target, Texture2D validIcon)
        {
            string reason;
            DrawMouseLabel(comp.Props.chooseLandingLabel.Translate(), comp.CanLandForCommand(target, out reason) ? null : reason, validIcon);
        }

        private static void DrawDirectionMouseLabel(CompAbilityEffect_AscentSlash comp, LocalTargetInfo landing, LocalTargetInfo direction, Texture2D validIcon)
        {
            string reason;
            DrawMouseLabel(comp.Props.chooseDirectionLabel.Translate(), comp.CanChooseDirectionForCommand(landing, direction, out reason) ? null : reason, validIcon);
        }

        private static void DrawMouseLabel(string label, string rejectReason, Texture2D validIcon)
        {
            if (!rejectReason.NullOrEmpty())
            {
                GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
                Widgets.MouseAttachedLabel(rejectReason, 0f, 0f, ColorLibrary.RedReadable);
                return;
            }

            GenUI.DrawMouseAttachment(validIcon);
            if (!label.NullOrEmpty())
            {
                Widgets.MouseAttachedLabel(label, 0f, 0f, null);
            }
        }
    }
}
