using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MiliraXian.Characters.Neiyu;
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

    internal struct DuanzhanVisualState
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
    }

    internal static class DuanzhanVisualTracker
    {
        private static readonly Dictionary<int, DuanzhanVisualState> States = new Dictionary<int, DuanzhanVisualState>();

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
            float lungeDistanceCells)
        {
            if (pawn == null || weapon == null)
            {
                return;
            }

            int safeChargeTicks = Mathf.Max(1, chargeTicks);
            int safeSweepTicks = Mathf.Max(1, sweepTicks);
            int safeRecoverTicks = Mathf.Max(0, recoverTicks);
            States[pawn.thingIDNumber] = new DuanzhanVisualState
            {
                weaponThingId = weapon.thingIDNumber,
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

            States.Remove(pawn.thingIDNumber);
        }

        public static bool TryGetWeaponVisual(Thing eq, out float weaponScale, out float drawAimAngle, out Vector3 drawOffset, out bool drawAfterimages)
        {
            weaponScale = 1f;
            drawAimAngle = 0f;
            drawOffset = Vector3.zero;
            drawAfterimages = false;

            Pawn_EquipmentTracker equipmentTracker = eq?.ParentHolder as Pawn_EquipmentTracker;
            Pawn pawn = equipmentTracker?.pawn;
            if (pawn == null || !States.TryGetValue(pawn.thingIDNumber, out DuanzhanVisualState state))
            {
                return false;
            }

            if (eq.thingIDNumber != state.weaponThingId)
            {
                return false;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : state.sequenceEndTick;
            int chargeEndTick = state.sequenceStartTick + state.chargeTicks;
            int sweepEndTick = chargeEndTick + state.sweepTicks;
            int recoverEndTick = state.sequenceEndTick;
            Vector3 forward = ForwardFromAimAngle(state.baseAimAngle);

            if (now < state.sequenceStartTick || now >= recoverEndTick)
            {
                return false;
            }

            if (now < chargeEndTick)
            {
                float progress = Mathf.Clamp01((now - state.sequenceStartTick) / (float)state.chargeTicks);
                weaponScale = Mathf.Lerp(1f, state.readyWeaponScale, progress);
                drawAimAngle = state.baseAimAngle + state.slashStartAngleOffset;
                drawOffset = -forward * (state.lungeDistanceCells * 0.25f * progress);
                return true;
            }

            if (now < sweepEndTick)
            {
                float progress = Mathf.Clamp01((now - chargeEndTick) / (float)state.sweepTicks);
                weaponScale = state.slashWeaponScale;
                drawAimAngle = state.baseAimAngle + Mathf.Lerp(state.slashStartAngleOffset, state.slashEndAngleOffset, progress);
                drawOffset = forward * (Mathf.Sin(progress * Mathf.PI) * state.lungeDistanceCells);
                drawAfterimages = true;
                return true;
            }

            if (state.recoverTicks <= 0)
            {
                return false;
            }

            float recoverProgress = Mathf.Clamp01((now - sweepEndTick) / (float)state.recoverTicks);
            weaponScale = Mathf.Lerp(state.slashWeaponScale, state.readyWeaponScale, recoverProgress);
            drawAimAngle = state.baseAimAngle + state.slashEndAngleOffset;
            drawOffset = forward * (state.lungeDistanceCells * (1f - recoverProgress) * 0.35f);
            return true;
        }

        private static Vector3 ForwardFromAimAngle(float aimAngle)
        {
            float radians = aimAngle * 0.0174532924f;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
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

    public class CompAbilityEffect_Duanzhan : CompAbilityEffect
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
                    Messages.Message("断斩的目标位置无效。", MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon == null || weapon.def == null || weapon.def.defName != RequiredWeaponDefName)
            {
                if (throwMessages)
                {
                    Messages.Message("断斩只能由昭离装备离断时施放。", caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
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
        }

        private void RefreshVisualState(Pawn caster)
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
                Props.lungeDistanceCells);
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

                if (!IsInsideSlashArea(center, pawn.Position, forward, Props.impactRadius, Props.coneDotThreshold, Props.lineLengthCells, Props.lineWidthCells))
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
            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Cut, damageAmount, Props.armorPenetration, -1f, caster, torso, weapon != null ? weapon.def : null);
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
            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Flame, flameDamage, 0f, -1f, caster, torso, weapon != null ? weapon.def : null);
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

    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    internal static class Patch_ZhaoliDuanzhan_DrawEquipmentAiming
    {
        private const string SlashTexturePath = "MiliraXianZhaoli/Items/Zhaoli_DuanzhanBlade_OnHand";
        private const string SlashGlowTexturePath = "MiliraXianZhaoli/Effect/Duanzhan/Zhaoli_DuanzhanTrailGlow";
        private const float SlashTextureScaleFactor = 3f;
        private const int TrailLifetimeTicks = 22;
        private const int TrailMaxSamples = 18;
        private const int TrailInterpolationSubdivisions = 3;
        private const float TrailMinPointDistance = 0.012f;
        private const float TrailMinAngleDelta = 1.75f;
        private const float CurrentGlowAlpha = 0.88f;
        private const float CurrentGlowScaleMultiplier = 1.06f;
        private const float TrailGlowScaleStart = 1.04f;
        private const float TrailGlowScaleStep = 0.035f;
        private const float TrailGlowBackOffsetStep = 0.045f;
        private const float TrailGlowAltitudeStep = 0.006f;

        private struct SlashTrailSample
        {
            public Vector3 drawLoc;
            public float aimAngle;
            public float scaleMultiplier;
            public int tick;
        }

        private static readonly Dictionary<int, List<SlashTrailSample>> TrailSamplesByWeapon = new Dictionary<int, List<SlashTrailSample>>();
        private static Material slashMaterial;
        private static Material slashGlowMaterial;
        private static Vector2 slashBaseDrawSize = Vector2.one;
        private static Vector2 slashGlowBaseDrawSize = Vector2.one;
        private static bool triedLoadMaterials;

        [HarmonyPrefix]
        private static bool Prefix(Thing eq, Vector3 drawLoc, float aimAngle)
        {
            if (eq == null || !DuanzhanVisualTracker.TryGetWeaponVisual(eq, out float weaponScale, out float drawAimAngle, out Vector3 drawOffset, out bool drawAfterimages))
            {
                return true;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            Vector3 weaponDrawLoc = drawLoc + drawOffset;
            TrimExpiredTrailSamples(eq.thingIDNumber, now);
            if (drawAfterimages)
            {
                RecordTrailSample(eq.thingIDNumber, weaponDrawLoc, drawAimAngle, weaponScale * SlashTextureScaleFactor, now);
            }

            return !DrawScaledEquipment(eq.thingIDNumber, weaponDrawLoc, drawAimAngle, weaponScale * SlashTextureScaleFactor, drawAfterimages, now);
        }

        private static void EnsureMaterialsLoaded()
        {
            if (triedLoadMaterials)
            {
                return;
            }

            triedLoadMaterials = true;

            Texture2D slashTexture = ContentFinder<Texture2D>.Get(SlashTexturePath, reportFailure: false);
            if (slashTexture != null)
            {
                slashMaterial = MaterialPool.MatFrom(slashTexture, ShaderDatabase.Cutout, Color.white);
                slashBaseDrawSize = new Vector2(Mathf.Max(0.01f, slashTexture.width / (float)Mathf.Max(1, slashTexture.height)), 1f);
            }

            Texture2D glowTexture = ContentFinder<Texture2D>.Get(SlashGlowTexturePath, reportFailure: false);
            if (glowTexture != null)
            {
                slashGlowMaterial = MaterialPool.MatFrom(glowTexture, ShaderDatabase.MoteGlow, Color.white);
                slashGlowBaseDrawSize = new Vector2(Mathf.Max(0.01f, glowTexture.width / (float)Mathf.Max(1, glowTexture.height)), 1f);
            }
        }

        private static bool DrawScaledEquipment(int weaponThingId, Vector3 drawLoc, float aimAngle, float scaleMultiplier, bool drawAfterimages, int now)
        {
            EnsureMaterialsLoaded();
            if (slashMaterial == null)
            {
                return false;
            }

            DrawTrailSamples(weaponThingId, now);

            if (drawAfterimages && slashGlowMaterial != null)
            {
                Material activeGlowMaterial = FadedMaterialPool.FadedVersionOf(slashGlowMaterial, CurrentGlowAlpha);
                DrawMesh(drawLoc + Altitudes.AltIncVect * 0.015f, aimAngle, scaleMultiplier * CurrentGlowScaleMultiplier, activeGlowMaterial, slashGlowBaseDrawSize);
            }

            DrawMesh(drawLoc, aimAngle, scaleMultiplier, slashMaterial, slashBaseDrawSize);
            return true;
        }

        private static void RecordTrailSample(int weaponThingId, Vector3 drawLoc, float aimAngle, float scaleMultiplier, int now)
        {
            if (!TrailSamplesByWeapon.TryGetValue(weaponThingId, out List<SlashTrailSample> samples))
            {
                samples = new List<SlashTrailSample>();
                TrailSamplesByWeapon.Add(weaponThingId, samples);
            }

            if (samples.Count > 0)
            {
                SlashTrailSample last = samples[samples.Count - 1];
                Vector3 delta = drawLoc - last.drawLoc;
                float angleDelta = Mathf.Abs(Mathf.DeltaAngle(last.aimAngle, aimAngle));
                if (delta.sqrMagnitude < TrailMinPointDistance * TrailMinPointDistance && angleDelta < TrailMinAngleDelta)
                {
                    return;
                }
            }

            samples.Add(new SlashTrailSample
            {
                drawLoc = drawLoc,
                aimAngle = aimAngle,
                scaleMultiplier = scaleMultiplier,
                tick = now
            });

            if (samples.Count > TrailMaxSamples)
            {
                samples.RemoveAt(0);
            }
        }

        private static void TrimExpiredTrailSamples(int weaponThingId, int now)
        {
            if (!TrailSamplesByWeapon.TryGetValue(weaponThingId, out List<SlashTrailSample> samples))
            {
                return;
            }

            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (now - samples[i].tick > TrailLifetimeTicks)
                {
                    samples.RemoveAt(i);
                }
            }

            if (samples.Count == 0)
            {
                TrailSamplesByWeapon.Remove(weaponThingId);
            }
        }

        private static void DrawTrailSamples(int weaponThingId, int now)
        {
            if (slashGlowMaterial == null || !TrailSamplesByWeapon.TryGetValue(weaponThingId, out List<SlashTrailSample> samples) || samples.Count == 0)
            {
                return;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                DrawTrailSample(samples[i], samples.Count - i, now);

                if (i >= samples.Count - 1)
                {
                    continue;
                }

                SlashTrailSample from = samples[i];
                SlashTrailSample to = samples[i + 1];
                for (int subdivision = 1; subdivision <= TrailInterpolationSubdivisions; subdivision++)
                {
                    float t = subdivision / (float)(TrailInterpolationSubdivisions + 1);
                    SlashTrailSample interpolated = new SlashTrailSample
                    {
                        drawLoc = Vector3.Lerp(from.drawLoc, to.drawLoc, t),
                        aimAngle = Mathf.LerpAngle(from.aimAngle, to.aimAngle, t),
                        scaleMultiplier = Mathf.Lerp(from.scaleMultiplier, to.scaleMultiplier, t),
                        tick = Mathf.RoundToInt(Mathf.Lerp(from.tick, to.tick, t))
                    };
                    DrawTrailSample(interpolated, samples.Count - i, now);
                }
            }
        }

        private static void DrawTrailSample(SlashTrailSample sample, int trailIndex, int now)
        {
            int ageTicks = now - sample.tick;
            if (ageTicks < 0 || ageTicks > TrailLifetimeTicks)
            {
                return;
            }

            float ageRatio = ageTicks / (float)Mathf.Max(1, TrailLifetimeTicks);
            float alpha = Mathf.Lerp(0.72f, 0.04f, ageRatio);
            if (alpha <= 0.01f)
            {
                return;
            }

            float scale = sample.scaleMultiplier * (TrailGlowScaleStart + TrailGlowScaleStep * trailIndex);
            Vector3 backOffset = -AimOffset(sample.aimAngle) * (TrailGlowBackOffsetStep * trailIndex);
            Vector3 altitudeOffset = Altitudes.AltIncVect * (TrailGlowAltitudeStep * trailIndex);
            Material fadedGlow = FadedMaterialPool.FadedVersionOf(slashGlowMaterial, alpha);
            DrawMesh(sample.drawLoc + backOffset + altitudeOffset, sample.aimAngle, scale, fadedGlow, slashGlowBaseDrawSize);
        }

        private static void DrawMesh(Vector3 drawLoc, float aimAngle, float scaleMultiplier, Material material, Vector2 baseDrawSize)
        {
            if (material == null)
            {
                return;
            }

            float rotation = aimAngle % 360f;
            Vector2 drawSize = baseDrawSize * Mathf.Max(0.01f, scaleMultiplier);
            Quaternion quaternion = Quaternion.AngleAxis(rotation, Vector3.up);
            Vector3 pivotToCenter = quaternion * new Vector3(0f, 0f, drawSize.y * 0.5f);
            Vector3 meshCenter = drawLoc + pivotToCenter;
            Matrix4x4 matrix = Matrix4x4.TRS(meshCenter, quaternion, new Vector3(drawSize.x, 0f, drawSize.y));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }

        private static Vector3 AimOffset(float aimAngle)
        {
            float radians = aimAngle * 0.0174532924f;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }
    }
}
