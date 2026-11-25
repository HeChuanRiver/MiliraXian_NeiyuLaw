using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.Common.Vfx.Emitter;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    internal enum DuanzhanSequenceStage
    {
        None,
        Charging,
        Sweeping,
        Recovering
    }

    internal sealed class DuanzhanVisualState
    {
        public int weaponThingId;
        public int sequenceStartTick;
        public int chargeTicks;
        public int sweepTicks;
        public int recoverTicks;
        public int sequenceEndTick;
        public float baseAimAngle;
        public float readyWeaponScale;
        public float slashWeaponScale;
        public float slashStartAngleOffset;
        public float slashEndAngleOffset;
        public float lungeDistanceCells;

        internal VfxEmitterHandle trail;
        internal bool restoreTrail;

        internal void EndTrail()
        {
            if (trail.Host?.Resolve(trail) is ZhaoliDuanzhanTrail live && live.Owner == this)
            {
                live.Owner = null;
                trail.Host.Destroy(trail);
            }
            trail = default;
        }

        internal bool Pose(int now, out float weaponScale, out float drawAimAngle, out Vector3 drawOffset, out bool drawAfterimages)
        {
            weaponScale = 1f;
            drawAimAngle = 0f;
            drawOffset = Vector3.zero;
            drawAfterimages = false;
            int chargeEndTick = sequenceStartTick + chargeTicks;
            int sweepEndTick = chargeEndTick + sweepTicks;
            int recoverEndTick = sequenceEndTick;
            Vector3 forward = ForwardFromAimAngle(baseAimAngle);

            if (now < sequenceStartTick || now >= recoverEndTick)
            {
                return false;
            }

            if (now < chargeEndTick)
            {
                float progress = Mathf.Clamp01((now - sequenceStartTick) / (float)chargeTicks);
                weaponScale = Mathf.Lerp(1f, readyWeaponScale, progress);
                drawAimAngle = baseAimAngle + slashStartAngleOffset;
                drawOffset = -forward * (lungeDistanceCells * 0.25f * progress);
                return true;
            }

            if (now < sweepEndTick)
            {
                float progress = Mathf.Clamp01((now - chargeEndTick) / (float)sweepTicks);
                weaponScale = slashWeaponScale;
                drawAimAngle = baseAimAngle + Mathf.Lerp(slashStartAngleOffset, slashEndAngleOffset, progress);
                drawOffset = forward * (Mathf.Sin(progress * Mathf.PI) * lungeDistanceCells);
                drawAfterimages = true;
                return true;
            }

            if (recoverTicks <= 0)
            {
                return false;
            }

            float recoverProgress = Mathf.Clamp01((now - sweepEndTick) / (float)recoverTicks);
            weaponScale = Mathf.Lerp(slashWeaponScale, readyWeaponScale, recoverProgress);
            drawAimAngle = baseAimAngle + slashEndAngleOffset;
            drawOffset = forward * (lungeDistanceCells * (1f - recoverProgress) * 0.35f);
            return true;
        }

        private static Vector3 ForwardFromAimAngle(float aimAngle)
        {
            float radians = aimAngle * 0.0174532924f;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }
    }


    internal static class DuanzhanVisualTracker
    {
        private static readonly Dictionary<int, DuanzhanVisualState> States = new();

        internal static void ClearRuntimeState()
        {
            foreach (DuanzhanVisualState state in States.Values) state.EndTrail();
            States.Clear();
        }

        public static void SetState(
            Pawn pawn,
            ThingWithComps weapon,
            int sequenceStartTick,
            int chargeTicks,
            int sweepTicks,
            int recoverTicks,
            float baseAimAngle,
            float readyWeaponScale,
            float slashWeaponScale,
            float slashStartAngleOffset,
            float slashEndAngleOffset,
            float lungeDistanceCells,
            bool restored)
        {
            if (pawn == null || weapon == null)
            {
                return;
            }

            if (States.TryGetValue(pawn.thingIDNumber, out var existing))
            {
                if (existing.sequenceStartTick == sequenceStartTick && existing.weaponThingId == weapon.thingIDNumber) return;
                existing.EndTrail();
            }
            int safeChargeTicks = Mathf.Max(1, chargeTicks);
            int safeSweepTicks = Mathf.Max(1, sweepTicks);
            int safeRecoverTicks = Mathf.Max(0, recoverTicks);
            States[pawn.thingIDNumber] = new DuanzhanVisualState
            {
                weaponThingId = weapon.thingIDNumber,
                restoreTrail = restored,
                sequenceStartTick = sequenceStartTick,
                chargeTicks = safeChargeTicks,
                sweepTicks = safeSweepTicks,
                recoverTicks = safeRecoverTicks,
                sequenceEndTick = sequenceStartTick + safeChargeTicks + safeSweepTicks + safeRecoverTicks,
                baseAimAngle = baseAimAngle,
                readyWeaponScale = Mathf.Max(0.1f, readyWeaponScale),
                slashWeaponScale = Mathf.Max(0.1f, slashWeaponScale),
                slashStartAngleOffset = slashStartAngleOffset,
                slashEndAngleOffset = slashEndAngleOffset,
                lungeDistanceCells = Mathf.Max(0f, lungeDistanceCells)
            };
        }

        public static void Clear(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            if (States.TryGetValue(pawn.thingIDNumber, out DuanzhanVisualState state))
            {
                state.EndTrail();
                States.Remove(pawn.thingIDNumber);
            }
        }

        internal static bool TryGet(Thing weapon, out DuanzhanVisualState state, out Pawn pawn)
        {
            pawn = (weapon?.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            state = null;
            return pawn != null && States.TryGetValue(pawn.thingIDNumber, out state) && state.weaponThingId == weapon.thingIDNumber;
        }
    }

    public class CompProperties_AbilityDuanzhan : CompProperties_AbilityEffect
    {
        public float impactRadius = 4.2f;
        public float lineLengthCells = 6.2f;
        public float lineWidthCells = 0.85f;
        public float damageAmount = 72f;
        public float lineDamageMultiplier = 1.25f;
        public float armorPenetration = 0.65f;
        public float coneDotThreshold = -0.1f;
        public int chargeTicks = 42;
        public int sweepTicks = 18;
        public int recoverTicks = 24;
        public int backswingTicks = 42;
        public float minghuoFlameDamageFactor = 0.25f;
        public float readyWeaponScale = 1.55f;
        public float slashWeaponScale = 2.85f;
        public float slashStartAngleOffset = -78f;
        public float slashEndAngleOffset = 78f;
        public float lungeDistanceCells = 0.65f;

        public CompProperties_AbilityDuanzhan()
        {
            compClass = typeof(CompAbilityEffect_Duanzhan);
        }
    }

    public class CompAbilityEffect_Duanzhan : CompAbilityEffect_ZhaoliPowerLimited
    {
        private const string RequiredWeaponDefName = "MX_Zhaoli_DuanzhanBlade";

        private new CompProperties_AbilityDuanzhan Props => (CompProperties_AbilityDuanzhan)props;

        private DuanzhanSequenceStage stage;
        private IntVec3 originCell;
        private IntVec3 targetCell;
        private int sequenceStartTick;
        private int sweepStartTick;
        private int sweepEndTick;
        private int sequenceEndTick;
        private bool damageApplied;
        private bool chargeFxPlayed;
        private bool sweepFxPlayed;
        private bool finishFxPlayed;

        internal bool SequenceActive => stage != DuanzhanSequenceStage.None;

        internal bool CanHitCell(IntVec3 center, IntVec3 aimCell, IntVec3 victimCell)
        {
            return CanHitCell(center, ComputeForward(center, aimCell), victimCell);
        }

        private bool CanHitCell(IntVec3 center, Vector3 forward, IntVec3 victimCell)
        {
            return IsInsideSlashArea(center, victimCell, forward,
                Props.impactRadius, Props.coneDotThreshold, Props.lineLengthCells, Props.lineWidthCells);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !target.IsValid)
            {
                return false;
            }

            if (!target.Cell.InBounds(map))
            {
                if (throwMessages)
                {
                    Messages.Message("MX_ZL_DuanzhanInvalidTargetCell".Translate(), MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon == null || weapon.def == null || weapon.def.defName != RequiredWeaponDefName)
            {
                if (throwMessages)
                {
                    Messages.Message("MX_ZL_DuanzhanNeedWeapon".Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (ZhaoliPowerBalance.Sealed) return;
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !target.IsValid)
            {
                return;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            originCell = caster.Position;
            targetCell = target.Cell;
            sequenceStartTick = now;
            sweepStartTick = now + Mathf.Max(1, Props.chargeTicks);
            sweepEndTick = sweepStartTick + Mathf.Max(1, Props.sweepTicks);
            sequenceEndTick = sweepEndTick + Mathf.Max(0, Props.recoverTicks);
            stage = DuanzhanSequenceStage.Charging;
            damageApplied = false;
            chargeFxPlayed = false;
            sweepFxPlayed = false;
            finishFxPlayed = false;

            int totalLockTicks = Mathf.Max(1, Props.chargeTicks + Props.sweepTicks + Props.recoverTicks + Props.backswingTicks);
            if (caster.stances?.stunner != null)
            {
                caster.stances.stunner.StunFor(totalLockTicks, caster, addBattleLog: false, showMote: false);
            }

            caster.rotationTracker?.FaceCell(targetCell);
            RefreshVisualState(caster);
            PlayChargeEffects(map, originCell);
            chargeFxPlayed = true;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (stage == DuanzhanSequenceStage.None)
            {
                return;
            }

            Pawn caster = parent?.pawn;
            Map map = caster?.MapHeld;
            if (ZhaoliPowerBalance.Sealed) { ClearSequence(caster); return; }
            if (caster == null || map == null)
            {
                ClearSequence(caster);
                return;
            }

            caster.rotationTracker?.FaceCell(targetCell);
            RefreshVisualState(caster);

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : sequenceEndTick;
            if (stage == DuanzhanSequenceStage.Charging && now >= sweepStartTick)
            {
                stage = DuanzhanSequenceStage.Sweeping;
            }

            if (!chargeFxPlayed)
            {
                PlayChargeEffects(map, originCell);
                chargeFxPlayed = true;
            }

            if (!sweepFxPlayed && now >= sweepStartTick)
            {
                PlaySweepStartEffects(map, originCell, ComputeForward(originCell, targetCell));
                sweepFxPlayed = true;
            }

            if (stage == DuanzhanSequenceStage.Sweeping && !damageApplied)
            {
                int triggerTick = sweepStartTick + Mathf.Max(1, (sweepEndTick - sweepStartTick) / 2);
                if (now >= triggerTick)
                {
                    DoDuanzhan(caster, map, originCell, ComputeForward(originCell, targetCell));
                    damageApplied = true;
                }
            }

            if (stage == DuanzhanSequenceStage.Sweeping && now >= sweepEndTick)
            {
                stage = DuanzhanSequenceStage.Recovering;
            }

            if (!finishFxPlayed && now >= sweepEndTick)
            {
                PlayFinishEffects(map, originCell, ComputeForward(originCell, targetCell));
                finishFxPlayed = true;
            }

            if (now >= sequenceEndTick)
            {
                if (!damageApplied)
                {
                    DoDuanzhan(caster, map, originCell, ComputeForward(originCell, targetCell));
                }

                ClearSequence(caster);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            int stageInt = (int)stage;
            Scribe_Values.Look(ref stageInt, "mxnl_zhaoli_duanzhan_stage", 0);
            Scribe_Values.Look(ref originCell, "mxnl_zhaoli_duanzhan_originCell");
            Scribe_Values.Look(ref targetCell, "mxnl_zhaoli_duanzhan_targetCell");
            Scribe_Values.Look(ref sequenceStartTick, "mxnl_zhaoli_duanzhan_sequenceStartTick", -1);
            Scribe_Values.Look(ref sweepStartTick, "mxnl_zhaoli_duanzhan_sweepStartTick", -1);
            Scribe_Values.Look(ref sweepEndTick, "mxnl_zhaoli_duanzhan_sweepEndTick", -1);
            Scribe_Values.Look(ref sequenceEndTick, "mxnl_zhaoli_duanzhan_sequenceEndTick", -1);
            Scribe_Values.Look(ref damageApplied, "mxnl_zhaoli_duanzhan_damageApplied", false);
            Scribe_Values.Look(ref chargeFxPlayed, "mxnl_zhaoli_duanzhan_chargeFxPlayed", false);
            Scribe_Values.Look(ref sweepFxPlayed, "mxnl_zhaoli_duanzhan_sweepFxPlayed", false);
            Scribe_Values.Look(ref finishFxPlayed, "mxnl_zhaoli_duanzhan_finishFxPlayed", false);
            stage = (DuanzhanSequenceStage)stageInt;
            if (stage != DuanzhanSequenceStage.None && Scribe.mode == LoadSaveMode.PostLoadInit)
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    if (parent?.pawn?.Spawned == true && stage != DuanzhanSequenceStage.None)
                        RefreshVisualState(parent.pawn, restored: parent.pawn.Position == originCell);
                });
        }

        private void RefreshVisualState(Pawn caster, bool restored = false)
        {
            ThingWithComps weapon = caster?.equipment?.Primary;
            if (caster == null || weapon == null || stage == DuanzhanSequenceStage.None)
            {
                DuanzhanVisualTracker.Clear(caster);
                return;
            }

            DuanzhanVisualTracker.SetState(
                caster,
                weapon,
                sequenceStartTick,
                Props.chargeTicks,
                Props.sweepTicks,
                Props.recoverTicks,
                ComputeAimAngle(originCell, targetCell),
                Props.readyWeaponScale,
                Props.slashWeaponScale,
                Props.slashStartAngleOffset,
                Props.slashEndAngleOffset,
                Props.lungeDistanceCells, restored);
        }

        private void ClearSequence(Pawn caster)
        {
            stage = DuanzhanSequenceStage.None;
            damageApplied = false;
            chargeFxPlayed = false;
            sweepFxPlayed = false;
            finishFxPlayed = false;
            DuanzhanVisualTracker.Clear(caster);
        }

        private static Vector3 ComputeForward(IntVec3 sourceCell, IntVec3 targetCell)
        {
            Vector3 forward = (targetCell - sourceCell).ToVector3();
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            forward.y = 0f;
            forward.Normalize();
            return forward;
        }

        private static float ComputeAimAngle(IntVec3 sourceCell, IntVec3 targetCell)
        {
            Vector3 forward = ComputeForward(sourceCell, targetCell);
            return Mathf.Atan2(forward.x, forward.z) * 57.29578f;
        }

        private void PlayChargeEffects(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return;
            }

            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipInnerExit, 0.9f);
            FleckMaker.Static(cell, map, FleckDefOf.MicroSparksFast, 0.8f);
        }

        private void PlaySweepStartEffects(Map map, IntVec3 cell, Vector3 forward)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return;
            }

            FleckMaker.Static(cell, map, FleckDefOf.ShotFlash, 1.45f);
            FleckMaker.Static(cell, map, FleckDefOf.FeedbackMelee, 1.5f);
            PlaySlashPathFlecks(map, cell, forward, 0.45f);
        }

        private void PlayFinishEffects(Map map, IntVec3 cell, Vector3 forward)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return;
            }

            IntVec3 endCell = (cell.ToVector3Shifted() + forward * Mathf.Max(1f, Props.lineLengthCells)).ToIntVec3();
            if (endCell.InBounds(map))
            {
                FleckMaker.Static(endCell, map, FleckDefOf.PsycastSkipFlashEntry, 1.2f);
            }

            FleckMaker.Static(cell, map, FleckDefOf.ExplosionFlash, 0.85f);
        }

        private void PlaySlashPathFlecks(Map map, IntVec3 center, Vector3 forward, float chance)
        {
            int spawned = 0;
            float searchRadius = Mathf.Max(Props.impactRadius, Props.lineLengthCells + Props.lineWidthCells);
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, searchRadius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                if (!IsInsideSlashArea(center, cell, forward, Props.impactRadius, Props.coneDotThreshold, Props.lineLengthCells, Props.lineWidthCells))
                {
                    continue;
                }

                if (Rand.Chance(chance))
                {
                    FleckMaker.Static(cell, map, FleckDefOf.MicroSparksFast, Rand.Range(0.55f, 0.9f));
                    spawned++;
                    if (spawned >= 12)
                    {
                        break;
                    }
                }
            }
        }

        private void DoDuanzhan(Pawn caster, Map map, IntVec3 center, Vector3 forward)
        {
            if (ZhaoliPowerBalance.Sealed) return;
            ThingWithComps weapon = caster.equipment?.Primary;
            HediffComp_ZhaoliMinghuo minghuoComp = GetActiveMinghuoComp(caster, weapon);
            float searchRadius = Mathf.Max(Props.impactRadius, Props.lineLengthCells + Props.lineWidthCells);
            HashSet<Pawn> targets = NeiyuFlowerSwordSkillUtility.CollectPawnsInRadius(map, center, searchRadius);

            foreach (Pawn pawn in targets)
            {
                if (pawn == null || pawn == caster || pawn.Dead || !pawn.Spawned)
                {
                    continue;
                }

                if (!NeiyuFlowerSwordSkillUtility.IsHostile(caster, pawn))
                {
                    continue;
                }

                if (!CanHitCell(center, forward, pawn.Position))
                {
                    continue;
                }

                bool directLine = IsInsideDirectLine(center, pawn.Position, forward, Props.lineLengthCells, Props.lineWidthCells);
                float damageAmount = Props.damageAmount * (directLine ? Mathf.Max(1f, Props.lineDamageMultiplier) : 1f);
                ApplyPrimaryDamage(caster, pawn, weapon, damageAmount);
                ApplyMinghuoDamage(caster, pawn, weapon, minghuoComp, damageAmount);
                FleckMaker.Static(pawn.Position, map, FleckDefOf.FeedbackMelee, directLine ? 1.35f : 1.05f);

                if (caster.Spawned && pawn.Spawned)
                {
                    caster.Drawer.Notify_MeleeAttackOn(pawn);
                }
            }
        }

        private static bool IsInsideSlashArea(IntVec3 center, IntVec3 targetCell, Vector3 forward, float radius, float dotThreshold, float lineLength, float lineWidth)
        {
            Vector3 offset = (targetCell - center).ToVector3();
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.01f)
            {
                return true;
            }

            float distance = offset.magnitude;
            Vector3 direction = offset / distance;
            if (distance <= radius && Vector3.Dot(forward, direction) >= dotThreshold)
            {
                return true;
            }

            return IsInsideDirectLine(center, targetCell, forward, lineLength, lineWidth);
        }

        private static bool IsInsideDirectLine(IntVec3 center, IntVec3 targetCell, Vector3 forward, float lineLength, float lineWidth)
        {
            Vector3 offset = (targetCell - center).ToVector3();
            offset.y = 0f;
            float projection = Vector3.Dot(offset, forward);
            if (projection < 0f || projection > lineLength)
            {
                return false;
            }

            Vector3 side = offset - forward * projection;
            return side.sqrMagnitude <= lineWidth * lineWidth;
        }

        private void ApplyPrimaryDamage(Pawn caster, Pawn target, ThingWithComps weapon, float damageAmount)
        {
            IEnumerable<BodyPartRecord> parts = target.health?.hediffSet?.GetNotMissingParts();
            BodyPartRecord torso = parts != null ? parts.FirstOrDefault(part => part.def == BodyPartDefOf.Torso) : null;
            if (ZhaoliPowerBalance.Sealed) return;
            DamageInfo damageInfo = new(DamageDefOf.Cut, damageAmount, Props.armorPenetration, -1f, caster, torso, weapon != null ? weapon.def : null);
            damageInfo.SetBodyRegion(BodyPartHeight.Middle, BodyPartDepth.Outside);
            target.TakeDamage(damageInfo);
        }

        private void ApplyMinghuoDamage(Pawn caster, Pawn target, ThingWithComps weapon, HediffComp_ZhaoliMinghuo minghuoComp, float baseDamageAmount)
        {
            if (minghuoComp == null)
            {
                return;
            }

            IEnumerable<BodyPartRecord> parts = target.health?.hediffSet?.GetNotMissingParts();
            BodyPartRecord torso = parts != null ? parts.FirstOrDefault(part => part.def == BodyPartDefOf.Torso) : null;
            float flameDamage = Mathf.Max(1f, baseDamageAmount * minghuoComp.PropsMinghuo.fireDamageFactor * Props.minghuoFlameDamageFactor);
            DamageInfo damageInfo = new(DamageDefOf.Flame, flameDamage, 0f, -1f, caster, torso, weapon != null ? weapon.def : null);
            damageInfo.SetBodyRegion(BodyPartHeight.Middle, BodyPartDepth.Outside);
            target.TakeDamage(damageInfo);
            if (target.Spawned)
            {
                FleckMaker.Static(target.Position, target.Map, FleckDefOf.FireGlow, 0.9f);
            }
        }

        private static HediffComp_ZhaoliMinghuo GetActiveMinghuoComp(Pawn caster, ThingWithComps weapon)
        {
            if (caster?.health?.hediffSet == null || weapon == null)
            {
                return null;
            }

            HediffDef hediffDef = ZhaoliEffectUtility.MinghuoHediffDef;
            if (hediffDef == null)
            {
                return null;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            HediffComp_ZhaoliMinghuo comp = hediff?.TryGetComp<HediffComp_ZhaoliMinghuo>();
            return comp != null && comp.IsActiveFor(caster, weapon) ? comp : null;
        }
    }

    [StaticConstructorOnStartup]
    internal static class Patch_ZhaoliDuanzhan_DrawEquipmentAiming
    {
        private const string BladePath = "MiliraXianZhaoli/Items/Zhaoli_DuanzhanBlade_OnHand";
        internal const float BladeScale = 3f;
        private static readonly Material blade = VfxMaterialCache.FromTexture(BladePath, "Cutout");

        internal static bool DrawWeapon(Thing eq, Vector3 drawLoc, float aimAngle)
        {
            if (!DuanzhanVisualTracker.TryGet(eq, out var state, out Pawn pawn) || blade?.mainTexture == null) return true;
            int now = Find.TickManager.TicksGame;
            if (!state.Pose(now, out float scale, out float angle, out Vector3 offset, out bool sweeping)) return true;
            // The equipment callback supplies the actual hand pivot. Only the held blade draws here.
            scale *= BladeScale;
            Vector3 grip = drawLoc + offset;
            if (sweeping || !state.trail.IsNone || state.restoreTrail)
                ZhaoliDuanzhanTrail.Capture(state, pawn, drawLoc, grip, angle, scale, sweeping, now);
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 center = grip + rotation * Vector3.forward * (.5f * scale);
            float aspect = blade.mainTexture.width / (float)Mathf.Max(1, blade.mainTexture.height);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(center, rotation, new Vector3(aspect * scale, 0f, scale)), blade, 0);
            return false;
        }
    }
}
