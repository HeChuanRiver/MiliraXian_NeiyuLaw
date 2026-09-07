using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityAscentSlash : CompProperties_AbilityEffect
    {
        public float dashSpeedCellsPerSecond = 60f;
        public int dashDurationMinTicks = 8;
        public float dashCollisionRadius = 1.35f;
        public float dashImpactRadius = 1.8f;
        public float dashDamageAmount = 20f;
        public int ascentTicks = 16;
        public int hoverTicks = 12;
        public int descentTicks = 12;
        public int takeoffDelayTicks = 8;
        public float ascentDecelerationPower = 2.2f;
        public float descentAccelerationPower = 2.4f;
        public float secondStageTrackingRange = 8f;
        public float secondStageLimitedFollowDistance = 2.5f;
        public float secondStageMaxForwardOffset = 12f;
        public float secondStageMaxAltitudeLayers = 48f;
        public float secondImpactRadius = 2f;
        public float damageAmount = 32f;
        public float armorPenetration = 0.35f;
        public float buildingDamageMultiplier = 2f;
        public float empoweredDamagePerPressurePoint = 0.25f;
        public int empoweredSlashCount = 7;
        public int empoweredSlashIntervalTicks = 6;
        public float empoweredSlashRadius = 1f;
        public FleckDef empoweredSlashFleck;
        public float empoweredSlashVisualScale = 3.2f;
        public float empoweredSlashVisualAngleJitter = 12f;
        public int normalSlashCount = 3;
        public float postHitRecoveryPoints = 1f;
        public int postHitRecoveryTicks = 300;
        public int impactDelayTicks = 30;

        public string invalidLandingMessage = "MX_QH_AscentSlashInvalidLanding";

        public string entryEffecter;
        public string exitEffecter;
        public string impactEffecter = "ImpactSmallDustCloud";
        public string takeoffGroundFleck = "MXNL_Skyfall_FlyBegin_G";
        public Vector2 takeoffGroundFleckSize = new(9.6f, 3.8f);
        public Vector2 takeoffGroundFleckOffset = Vector2.zero;
        public string entryFleck;
        public string exitFleck;
        public string impactFleck = "ExplosionFlash";
        public string ascentTrailFleck = "MXNL_Skyfall_FlyBegin_F";
        public Vector2 ascentTrailFleckSize = new(2.2f, 20.2f);
        public Vector2 ascentTrailFleckOffset = new(0f, 8f);
        public SoundDef castSound;
        public SoundDef dropSound;
        public SoundDef slashSound;

        public CompProperties_AbilityAscentSlash()
        {
            compClass = typeof(CompAbilityEffect_AscentSlash);
        }
    }

    public class CompAbilityEffect_AscentSlash : CompAbilityEffect
    {
        private sealed class ActiveManagerHolder
        {
            public CompAbilityEffect_AscentSlash manager;
        }

        private static readonly Color PreviewColor = new(1f, 0.45f, 0.65f, 0.55f);
        private static ConditionalWeakTable<Pawn, ActiveManagerHolder> activeManagers = new();

        private ASDash dashAction;
        private ASSlash slashAction;

        public new CompProperties_AbilityAscentSlash Props => (CompProperties_AbilityAscentSlash)props;

        private float AbilityRange => Mathf.Max(0f, parent?.def?.verbProperties?.range ?? 0f);

        private bool ActionInProgress => dashAction?.Active == true || slashAction?.Active == true;

        public override bool GizmoDisabled(out string reason)
        {
            if (ActionInProgress)
            {
                reason = "MX_QH_AscentSlashInProgress".Translate();
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
            return !ActionInProgress && ValidateAim(caster, target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || caster.MapHeld == null || !ValidateAim(caster, target, true))
            {
                return;
            }

            EnsureActions();
            IntVec3 dashEndCell = ComputeDashEndCell(caster.Position, target.Cell, caster.MapHeld, AbilityRange);
            dashAction.Start(caster, dashEndCell);
            SyncActiveManager(caster);
        }

        public override void CompTick()
        {
            base.CompTick();
            EnsureActions();
            if (!ActionInProgress)
            {
                return;
            }

            Pawn caster = parent?.pawn;
            Map map = caster?.MapHeld;
            if (caster == null || caster.Destroyed || !caster.Spawned || map == null)
            {
                dashAction.Cancel(caster);
                slashAction.Cancel(caster);
                Unregister(caster);
                return;
            }

            if (dashAction.Active)
            {
                if (dashAction.Tick(caster, map, out ASDashResult result) && result.StartsSlash)
                {
                    slashAction.Start(caster, result);
                }
                SyncActiveManager(caster);
                return;
            }

            slashAction.Tick(caster, map);
            SyncActiveManager(caster);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            EnsureActions();
            dashAction.ExposeData();
            slashAction.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Pawn caster = parent?.pawn;
                dashAction.RestoreAfterLoad();
                slashAction.RestoreAfterLoad(caster);
                SyncActiveManager(caster);
            }
        }

        internal static void ApplyActiveActionDrawPos(Pawn pawn, ref Vector3 drawPos)
        {
            if (pawn == null || !activeManagers.TryGetValue(pawn, out ActiveManagerHolder holder))
            {
                return;
            }

            CompAbilityEffect_AscentSlash manager = holder.manager;
            if (manager?.dashAction?.TryApplyDrawPos(ref drawPos) == true)
            {
                return;
            }

            manager?.slashAction?.TryApplyDrawPos(ref drawPos);
        }

        internal static void NotifyPawnUnavailable(Pawn pawn)
        {
            if (pawn == null || !activeManagers.TryGetValue(pawn, out ActiveManagerHolder holder))
            {
                return;
            }

            holder.manager?.dashAction?.Cancel(pawn);
            holder.manager?.slashAction?.Cancel(pawn);
            activeManagers.Remove(pawn);
        }

        internal static void ClearActiveManagers()
        {
            activeManagers = new ConditionalWeakTable<Pawn, ActiveManagerHolder>();
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            DrawLandingPreview(target);
        }

        public void DrawLandingPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || !CanAim(caster, target, out _))
            {
                return;
            }

            IntVec3 endCell = ComputeDashEndCell(caster.Position, target.Cell, caster.MapHeld, AbilityRange);
            GenDraw.DrawLineBetween(caster.Position.ToVector3Shifted(), endCell.ToVector3Shifted());
            GenDraw.DrawRadiusRing(endCell, Props.dashImpactRadius, PreviewColor);
        }

        private void EnsureActions()
        {
            dashAction ??= new ASDash(Props);
            slashAction ??= new ASSlash(Props);
        }

        private void SyncActiveManager(Pawn caster)
        {
            if (caster == null)
            {
                return;
            }

            if (!ActionInProgress)
            {
                Unregister(caster);
                return;
            }

            if (activeManagers.TryGetValue(caster, out ActiveManagerHolder holder))
            {
                holder.manager = this;
                return;
            }

            activeManagers.Add(caster, new ActiveManagerHolder { manager = this });
        }

        private static void Unregister(Pawn caster)
        {
            if (caster != null)
            {
                activeManagers.Remove(caster);
            }
        }

        private bool ValidateAim(Pawn caster, LocalTargetInfo target, bool showMessages)
        {
            if (CanAim(caster, target, out string reason))
            {
                return true;
            }

            if (showMessages)
            {
                LookTargets lookTargets = caster != null && target.IsValid && caster.MapHeld != null
                    ? new LookTargets(caster, target.ToTargetInfo(caster.MapHeld))
                    : null;
                Messages.Message(reason, lookTargets, MessageTypeDefOf.RejectInput, historical: false);
            }
            return false;
        }

        private bool CanAim(Pawn caster, LocalTargetInfo target, out string reason)
        {
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !target.IsValid || !target.Cell.InBounds(map))
            {
                reason = Props.invalidLandingMessage.Translate();
                return false;
            }

            float range = AbilityRange;
            if (range > 0f && caster.Position.DistanceTo(target.Cell) > range)
            {
                reason = "AbilityOutOfRange".Translate();
                return false;
            }

            if (caster.Position == target.Cell)
            {
                reason = Props.invalidLandingMessage.Translate();
                return false;
            }

            reason = null;
            return true;
        }

        private static IntVec3 ComputeDashEndCell(IntVec3 origin, IntVec3 aim, Map map, float range)
        {
            Vector3 direction = (aim - origin).ToVector3().Yto0();
            if (direction.sqrMagnitude < 0.001f)
            {
                return origin;
            }

            Vector3 desired = origin.ToVector3Shifted() + direction.normalized * Mathf.Max(1f, range);
            return AscentSlashActionUtility.ClampToMap(desired.ToIntVec3(), map);
        }

    }
}
