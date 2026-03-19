using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MiliraXian.Characters.Neiyu
{
    public delegate void NeiyuSwordVisualPhaseHandler(Pawn pawn, string phaseId, IntVec3 focusCell, float weaponScale, float angleDeg);

    public static class NeiyuWeaponVisualHooks
    {

        public static event NeiyuSwordVisualPhaseHandler OnSwordVisualPhase;

        public static void Notify(Pawn pawn, string phaseId, IntVec3 focusCell, float weaponScale, float angleDeg)
        {
            NeiyuSwordVisualPhaseHandler handler = OnSwordVisualPhase;
            if (handler != null)
            {
                handler(pawn, phaseId, focusCell, weaponScale, angleDeg);
            }
        }
    }

    internal static class NeiyuFlowerSwordSkillUtility
    {
        public static bool HasRequiredWeapon(Pawn pawn, ThingDef requiredWeapon)
        {
            if (pawn == null || pawn.equipment == null || pawn.equipment.Primary == null)
            {
                return false;
            }

            if (requiredWeapon == null)
            {
                return true;
            }

            return pawn.equipment.Primary.def == requiredWeapon;
        }

        public static HashSet<Pawn> CollectPawnsInRadius(Map map, IntVec3 center, float radius)
        {
            HashSet<Pawn> result = new HashSet<Pawn>();
            if (map == null || !center.IsValid)
            {
                return result;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Pawn pawn = things[i] as Pawn;
                    if (pawn != null && !pawn.Destroyed && !pawn.Dead)
                    {
                        result.Add(pawn);
                    }
                }
            }

            return result;
        }

        public static void PlayEffecterAt(Map map, IntVec3 cell, string effecterDefName, float scale = 1f)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map) || effecterDefName.NullOrEmpty())
            {
                return;
            }

            EffecterDef effecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail(effecterDefName);
            if (effecterDef == null)
            {
                return;
            }

            Effecter effecter = effecterDef.Spawn(cell, map, Mathf.Max(0.01f, scale));
            if (effecter != null)
            {
                effecter.Cleanup();
            }
        }

        public static void PlayFleckAt(Map map, IntVec3 cell, string fleckDefName, float scale = 1f)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map) || fleckDefName.NullOrEmpty())
            {
                return;
            }

            FleckDef fleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(fleckDefName);
            if (fleckDef == null)
            {
                return;
            }

            FleckMaker.Static(cell, map, fleckDef, Mathf.Max(0.01f, scale));
        }

        public static void TrySetHediffDuration(Hediff hediff, int ticks)
        {
            if (hediff == null || ticks <= 0)
            {
                return;
            }

            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                disappears.SetDuration(ticks);
            }
        }

        public static void HealAllInjuries(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                Hediff_Injury injury = hediffs[i] as Hediff_Injury;
                if (injury != null && injury.Severity > 0f)
                {
                    injury.Heal(injury.Severity);
                }
            }
        }

        public static bool IsHostile(Pawn caster, Pawn target)
        {
            if (caster == null || target == null)
            {
                return false;
            }

            return GenHostility.HostileTo(caster, target);
        }

        public static IntVec3 ClampToMap(IntVec3 cell, Map map)
        {
            if (map == null)
            {
                return cell;
            }

            int x = Mathf.Clamp(cell.x, 0, map.Size.x - 1);
            int z = Mathf.Clamp(cell.z, 0, map.Size.z - 1);
            return new IntVec3(x, 0, z);
        }

        public static IntVec3 StepDirectionAwayFrom(IntVec3 from, IntVec3 threat)
        {
            IntVec3 delta = from - threat;
            if (delta.x == 0 && delta.z == 0)
            {
                return new IntVec3(0, 0, -1);
            }

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
            {
                return new IntVec3(Math.Sign(delta.x), 0, 0);
            }

            return new IntVec3(0, 0, Math.Sign(delta.z));
        }

        public static void PlayFleckAt(Map map, Vector3 worldPos, string fleckDefName, float scale = 1f)
        {
            if (map == null || fleckDefName.NullOrEmpty())
            {
                return;
            }

            FleckDef fleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(fleckDefName);
            if (fleckDef == null)
            {
                return;
            }

            FleckMaker.Static(worldPos, map, fleckDef, Mathf.Max(0.01f, scale));
        }
    }

    public class PawnFlyerWorker_NeiyuAscent : PawnFlyerWorker
    {
        public PawnFlyerWorker_NeiyuAscent(PawnFlyerProperties properties) : base(properties)
        {
        }

        public override float GetHeight(float t)
        {
            return Mathf.Clamp01(t);
        }
    }

    public class PawnFlyerWorker_NeiyuDescent : PawnFlyerWorker
    {
        public PawnFlyerWorker_NeiyuDescent(PawnFlyerProperties properties) : base(properties)
        {
        }

        public override float GetHeight(float t)
        {
            return Mathf.Clamp01(1f - t);
        }
    }

    internal enum NeiyuSkyfallVisualStage
    {
        None,
        Ascending,
        Warning,
        Descending
    }

    internal struct NeiyuSkyfallVisualState
    {
        public NeiyuSkyfallVisualStage stage;
        public int stageStartTick;
        public int stageEndTick;
        public float maxAltitudeLayers;
        public float maxForwardOffset;
    }

    internal static class NeiyuSkyfallVisualTracker
    {
        private static readonly Dictionary<int, NeiyuSkyfallVisualState> states = new Dictionary<int, NeiyuSkyfallVisualState>();

        public static void BeginAscent(Pawn pawn, int startTick, int endTick)
        {
            SetState(pawn, NeiyuSkyfallVisualStage.Ascending, startTick, endTick, 74f, 30f);
        }

        public static void BeginWarning(Pawn pawn, int startTick, int endTick)
        {
            SetState(pawn, NeiyuSkyfallVisualStage.Warning, startTick, endTick, 74f, 30f);
        }

        public static void BeginDescending(Pawn pawn, int startTick, int endTick)
        {
            SetState(pawn, NeiyuSkyfallVisualStage.Descending, startTick, endTick, 74f, 30f);
        }

        public static void Clear(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            states.Remove(pawn.thingIDNumber);
        }

        public static bool TryGetOffset(Pawn pawn, out Vector3 offset)
        {
            offset = Vector3.zero;
            if (pawn == null)
            {
                return false;
            }

            if (!states.TryGetValue(pawn.thingIDNumber, out NeiyuSkyfallVisualState state))
            {
                return false;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : state.stageEndTick;
            float t = 1f;
            if (state.stageEndTick > state.stageStartTick)
            {
                t = Mathf.Clamp01((now - state.stageStartTick) / (float)(state.stageEndTick - state.stageStartTick));
            }

            float h;
            switch (state.stage)
            {
                case NeiyuSkyfallVisualStage.Ascending:
                    h = Mathf.Pow(t, 0.22f);
                    break;
                case NeiyuSkyfallVisualStage.Warning:
                    h = 1f;
                    break;
                case NeiyuSkyfallVisualStage.Descending:
                    h = 1f - Mathf.Pow(t, 1.2f);
                    break;
                default:
                    h = 0f;
                    break;
            }

            if (h <= 0.001f)
            {
                if (now > state.stageEndTick + 5)
                {
                    states.Remove(pawn.thingIDNumber);
                }
                return false;
            }

            offset = Altitudes.AltIncVect * (state.maxAltitudeLayers * h) + Vector3.forward * (state.maxForwardOffset * h);
            return true;
        }

        private static void SetState(Pawn pawn, NeiyuSkyfallVisualStage stage, int startTick, int endTick, float maxAltitudeLayers, float maxForwardOffset)
        {
            if (pawn == null)
            {
                return;
            }

            states[pawn.thingIDNumber] = new NeiyuSkyfallVisualState
            {
                stage = stage,
                stageStartTick = startTick,
                stageEndTick = Mathf.Max(startTick + 1, endTick),
                maxAltitudeLayers = Mathf.Max(0f, maxAltitudeLayers),
                maxForwardOffset = Mathf.Max(0f, maxForwardOffset)
            };
        }
    }

    public class CompProperties_AbilityNeiyuFlowerBless : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;
        public float radius = 6f;
        public HediffDef buffHediff;
        public ThoughtDef moodThought;
        public int buffDurationTicks = 18000;
        public bool includeCaster = true;
        public string areaEffecterDefName = "MXNL_ForFeatherCastingCircle";
        public string areaFleckDefName = "MXNL_ForFeatherCircle";
        public float areaFleckScale = 1f;

        public CompProperties_AbilityNeiyuFlowerBless()
        {
            compClass = typeof(CompAbilityEffect_NeiyuFlowerBless);
        }
    }

    public class CompAbilityEffect_NeiyuFlowerBless : CompAbilityEffect
    {
        private new CompProperties_AbilityNeiyuFlowerBless Props => (CompProperties_AbilityNeiyuFlowerBless)props;

        public override bool ShouldHideGizmo
        {
            get
            {
                Pawn pawn = parent != null ? parent.pawn : null;
                return !NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(pawn, Props.requiredWeapon);
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn caster = parent != null ? parent.pawn : null;
            if (!NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(caster, Props.requiredWeapon))
            {
                reason = "[Neiyu] Need flower form weapon.";
                return true;
            }

            reason = null;
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent != null ? parent.pawn : null;
            Map map = caster != null ? caster.MapHeld : null;
            if (caster == null || map == null || !target.IsValid)
            {
                return;
            }

            IntVec3 center = target.Cell;
            NeiyuFlowerSwordSkillUtility.PlayEffecterAt(map, center, Props.areaEffecterDefName, 1f);
            NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, center, Props.areaFleckDefName, Props.areaFleckScale);

            int affected = 0;
            HashSet<Pawn> pawns = NeiyuFlowerSwordSkillUtility.CollectPawnsInRadius(map, center, Props.radius);
            foreach (Pawn pawn in pawns)
            {
                if (!Props.includeCaster && pawn == caster)
                {
                    continue;
                }

                if (pawn != caster && NeiyuFlowerSwordSkillUtility.IsHostile(caster, pawn))
                {
                    continue;
                }

                ApplyBlessToPawn(caster, pawn);
                affected++;
            }

            if (affected <= 0)
            {
                Messages.Message("[Neiyu] No valid ally in range.", caster, MessageTypeDefOf.RejectInput);
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return;
            }

            GenDraw.DrawRadiusRing(target.Cell, Props.radius, Color.green);
        }

        private void ApplyBlessToPawn(Pawn caster, Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return;
            }

            if (Props.buffHediff != null)
            {
                Hediff buff = pawn.health.hediffSet.GetFirstHediffOfDef(Props.buffHediff);
                if (buff == null)
                {
                    buff = HediffMaker.MakeHediff(Props.buffHediff, pawn);
                    pawn.health.AddHediff(buff);
                }

                NeiyuFlowerSwordSkillUtility.TrySetHediffDuration(buff, Props.buffDurationTicks);
            }

            NeiyuFlowerSwordSkillUtility.HealAllInjuries(pawn);

            if (Props.moodThought != null && pawn.needs != null && pawn.needs.mood != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemoryFast(Props.moodThought);
            }
        }
    }

    public class CompProperties_AbilityNeiyuFlowerToxinField : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;
        public float radius = 10f;
        public float severeFoodPoisoningSeverity = 0.25f;
        public float berserkChance = 0.10f;
        public string areaEffecterDefName = "MXNL_ForFeatherCastingCircle";
        public string areaFleckDefName = "MXNL_ForFeatherCircle";
        public float areaFleckScale = 1f;

        public CompProperties_AbilityNeiyuFlowerToxinField()
        {
            compClass = typeof(CompAbilityEffect_NeiyuFlowerToxinField);
        }
    }

    public class CompAbilityEffect_NeiyuFlowerToxinField : CompAbilityEffect
    {
        private new CompProperties_AbilityNeiyuFlowerToxinField Props => (CompProperties_AbilityNeiyuFlowerToxinField)props;

        public override bool ShouldHideGizmo
        {
            get
            {
                Pawn pawn = parent != null ? parent.pawn : null;
                return !NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(pawn, Props.requiredWeapon);
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn caster = parent != null ? parent.pawn : null;
            if (!NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(caster, Props.requiredWeapon))
            {
                reason = "[Neiyu] Need flower form weapon.";
                return true;
            }

            reason = null;
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent != null ? parent.pawn : null;
            Map map = caster != null ? caster.MapHeld : null;
            if (caster == null || map == null || !target.IsValid)
            {
                return;
            }

            IntVec3 center = target.Cell;
            NeiyuFlowerSwordSkillUtility.PlayEffecterAt(map, center, Props.areaEffecterDefName, 1f);
            NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, center, Props.areaFleckDefName, Props.areaFleckScale);

            int affected = 0;
            HashSet<Pawn> pawns = NeiyuFlowerSwordSkillUtility.CollectPawnsInRadius(map, center, Props.radius);
            foreach (Pawn pawn in pawns)
            {
                if (!NeiyuFlowerSwordSkillUtility.IsHostile(caster, pawn))
                {
                    continue;
                }

                ApplyFoodPoisoningSevere(pawn, Props.severeFoodPoisoningSeverity);
                if (pawn.mindState != null && pawn.mindState.mentalStateHandler != null && Rand.Chance(Props.berserkChance))
                {
                    pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, "Neiyu flower toxin", false, false, false, null, false, false, false);
                }

                affected++;
            }

            if (affected <= 0)
            {
                Messages.Message("[Neiyu] No hostile target in range.", caster, MessageTypeDefOf.RejectInput);
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return;
            }

            GenDraw.DrawRadiusRing(target.Cell, Props.radius, Color.magenta);
        }

        private static void ApplyFoodPoisoningSevere(Pawn pawn, float targetSeverity)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            Hediff foodPoisoning = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.FoodPoisoning);
            if (foodPoisoning == null)
            {
                foodPoisoning = HediffMaker.MakeHediff(HediffDefOf.FoodPoisoning, pawn);
                pawn.health.AddHediff(foodPoisoning);
            }

            if (foodPoisoning.Severity < targetSeverity)
            {
                foodPoisoning.Severity = targetSeverity;
            }
        }
    }

    public class CompProperties_AbilityNeiyuSwordSkyfall : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;
        public ThingDef ascendFlyerDef;
        public ThingDef descendFlyerDef;
        public DamageDef impactDamageDef;
        public float impactRadius = 3f;
        public int impactDamage = 180;
        public float impactArmorPen = 999f;
        public HediffDef vulnerabilityHediff;
        public int vulnerabilityDurationTicks = 9000;
        public int warningDelayTicks = 36;
        public int ascendVisualTicks = 8;
        public int descendVisualTicks = 16;
        public int ascentPulseIntervalTicks = 2;
        public int warningPulseIntervalTicks = 3;
        public int descendPulseIntervalTicks = 2;
        public bool lockCasterDuringSkyfall = true;
        public string warningEffectDefName = "Skip_EntryNoDelay";
        public string warningFleckDefName = "PsycastAreaEffect";
        public float warningFleckScale = 1.1f;
        public string effectBDefName = "ImpactSmallDustCloud";
        public string effectCDefName = "Skip_ExitNoDelay";
        public string launchFleckDefName = "PsycastSkipFlashEntry";
        public string landFleckDefName = "ExplosionFlash";
        public float launchWeaponScale = 2f;
        public float landWeaponScale = 2.6f;
        public float launchWeaponAngle = 90f;
        public float landWeaponAngle = -90f;
        public string takeoffGroundEffectDefName = "MXNL_Effecter_Skyfall_FlyBeginGround";
        public string ascentTrailFleckDefName = "MXNL_Skyfall_FlyBegin_F";
        public float ascentTrailFleckScale = 3.2f;
        public float ascentTrailOffsetX = 0f;
        public float ascentTrailOffsetZ = 8f;
        public string impactSoundDefName = "Thunder_OnMap";
        public float landingScreenShake = 0.16f;
        public int landingScreenShakeTicks = 20;
        public int landingDustBurstCount = 16;
        public float landingDustRadius = 2.8f;
        public float landingDustScale = 2.2f;

        public CompProperties_AbilityNeiyuSwordSkyfall()
        {
            compClass = typeof(CompAbilityEffect_NeiyuSwordSkyfall);
        }
    }

    public class CompAbilityEffect_NeiyuSwordSkyfall : CompAbilityEffect
    {
        private enum SkyfallStage
        {
            None,
            Ascending,
            Warning,
            Descending
        }

        private new CompProperties_AbilityNeiyuSwordSkyfall Props => (CompProperties_AbilityNeiyuSwordSkyfall)props;

        private SkyfallStage stage;
        private IntVec3 originCell;
        private IntVec3 targetCell;
        private IntVec3 landingCell;
        private int stageStartTick = -1;
        private int stageEndTick = -1;
        private int lastPulseTick = -99999;

        public override bool ShouldHideGizmo
        {
            get
            {
                Pawn pawn = parent != null ? parent.pawn : null;
                return !NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(pawn, Props.requiredWeapon);
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn caster = parent != null ? parent.pawn : null;
            if (!NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(caster, Props.requiredWeapon))
            {
                reason = "[Neiyu] Need sword form weapon.";
                return true;
            }

            if (stage != SkyfallStage.None)
            {
                reason = "[Neiyu] Skyfall is already in progress.";
                return true;
            }

            reason = null;
            return false;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent != null ? parent.pawn : null;
            if (!NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(caster, Props.requiredWeapon))
            {
                if (throwMessages)
                {
                    Messages.Message("[Neiyu] Need sword form weapon.", MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (stage != SkyfallStage.None)
            {
                if (throwMessages)
                {
                    Messages.Message("[Neiyu] Skyfall is already in progress.", MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            int stageInt = (int)stage;
            Scribe_Values.Look(ref stageInt, "mxnl_skyfall_stage", 0);
            Scribe_Values.Look(ref originCell, "mxnl_skyfall_originCell");
            Scribe_Values.Look(ref targetCell, "mxnl_skyfall_targetCell");
            Scribe_Values.Look(ref landingCell, "mxnl_skyfall_landingCell");
            Scribe_Values.Look(ref stageStartTick, "mxnl_skyfall_stageStartTick", -1);
            Scribe_Values.Look(ref stageEndTick, "mxnl_skyfall_stageEndTick", -1);
            Scribe_Values.Look(ref lastPulseTick, "mxnl_skyfall_lastPulseTick", -99999);
            stage = (SkyfallStage)stageInt;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent != null ? parent.pawn : null;
            Map map = caster != null ? caster.MapHeld : null;
            if (caster == null || map == null || !target.IsValid)
            {
                return;
            }

            originCell = caster.Position;
            targetCell = target.Cell;
            if (!targetCell.InBounds(map))
            {
                targetCell = NeiyuFlowerSwordSkillUtility.ClampToMap(targetCell, map);
            }

            landingCell = FindLandingCell(map, targetCell, caster);
            BeginStage(SkyfallStage.Ascending, Props.ascendVisualTicks);
            NeiyuSkyfallVisualTracker.BeginAscent(caster, stageStartTick, stageEndTick);

            if (Props.lockCasterDuringSkyfall && caster.stances != null && caster.stances.stunner != null)
            {
                int lockTicks = Mathf.Max(1, Props.ascendVisualTicks + Props.warningDelayTicks + Props.descendVisualTicks + 30);
                caster.stances.stunner.StunFor(lockTicks, caster, addBattleLog: false, showMote: false);
            }

            NeiyuFlowerSwordSkillUtility.PlayEffecterAt(map, originCell, Props.takeoffGroundEffectDefName, 1f);
            if (!Props.ascentTrailFleckDefName.NullOrEmpty())
            {
                Vector3 originPos = originCell.ToVector3Shifted();
                Vector3 trailPos = originPos + new Vector3(Props.ascentTrailOffsetX, 0f, Props.ascentTrailOffsetZ);
                NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, trailPos, Props.ascentTrailFleckDefName, Props.ascentTrailFleckScale);
            }
            NeiyuWeaponVisualHooks.Notify(caster, "SwordSkyfall_TakeoffTipUp", originCell, Props.launchWeaponScale, Props.launchWeaponAngle);
        }

        public override void CompTick()
        {
            base.CompTick();

            if (stage == SkyfallStage.None)
            {
                return;
            }

            Pawn caster = parent != null ? parent.pawn : null;
            Map map = caster != null ? caster.MapHeld : null;
            if (caster == null || map == null || !originCell.IsValid || !landingCell.IsValid)
            {
                ClearStage();
                return;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;

            switch (stage)
            {
                case SkyfallStage.Ascending:
                    TickAscending(caster, map, now);
                    break;
                case SkyfallStage.Warning:
                    TickWarning(caster, map, now);
                    break;
                case SkyfallStage.Descending:
                    TickDescending(caster, map, now);
                    break;
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return;
            }

            GenDraw.DrawRadiusRing(target.Cell, Props.impactRadius, Color.red);
        }

        private void TickAscending(Pawn caster, Map map, int now)
        {
            int interval = Mathf.Max(1, Props.ascentPulseIntervalTicks);
            if (now - lastPulseTick >= interval)
            {
                float progress = StageProgress(now);
                NeiyuWeaponVisualHooks.Notify(caster, "SwordSkyfall_AscentLoop", originCell, Mathf.Lerp(Props.launchWeaponScale, Props.launchWeaponScale + 0.45f, progress), Props.launchWeaponAngle);
                lastPulseTick = now;
            }

            if (now >= stageEndTick)
            {
                BeginStage(SkyfallStage.Warning, Props.warningDelayTicks);
                NeiyuSkyfallVisualTracker.BeginWarning(caster, stageStartTick, stageEndTick);
                NeiyuFlowerSwordSkillUtility.PlayEffecterAt(map, landingCell, Props.warningEffectDefName, 1f);
                NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, landingCell, Props.warningFleckDefName, Props.warningFleckScale);
                NeiyuWeaponVisualHooks.Notify(caster, "SwordSkyfall_TargetWarning", landingCell, Props.landWeaponScale, Props.landWeaponAngle);
            }
        }

        private void TickWarning(Pawn caster, Map map, int now)
        {
            int interval = Mathf.Max(1, Props.warningPulseIntervalTicks);
            if (now - lastPulseTick >= interval)
            {
                float pulse = 0.95f + 0.55f * Mathf.Abs(Mathf.Sin((now - stageStartTick) * 0.35f));
                NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, landingCell, Props.warningFleckDefName, Props.warningFleckScale * pulse);
                if ((now - stageStartTick) % (interval * 2) == 0)
                {
                    NeiyuFlowerSwordSkillUtility.PlayEffecterAt(map, landingCell, Props.warningEffectDefName, 1f);
                }
                lastPulseTick = now;
            }

            if (now >= stageEndTick)
            {
                BeginStage(SkyfallStage.Descending, Props.descendVisualTicks);
                NeiyuSkyfallVisualTracker.BeginDescending(caster, stageStartTick, stageEndTick);

                IntVec3 dropCell = FindLandingCell(map, landingCell, caster);
                if (caster.Spawned && caster.MapHeld == map && dropCell.IsValid && dropCell.InBounds(map) && dropCell != caster.Position)
                {
                    caster.Position = dropCell;
                    caster.Notify_Teleported();
                }
                landingCell = dropCell;
            }
        }

        private void TickDescending(Pawn caster, Map map, int now)
        {
            int interval = Mathf.Max(1, Props.descendPulseIntervalTicks);
            if (now - lastPulseTick >= interval)
            {
                float progress = StageProgress(now);
                float scale = Mathf.Lerp(1.6f, 0.95f, progress);
                NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, landingCell, Props.landFleckDefName, scale);
                NeiyuWeaponVisualHooks.Notify(caster, "SwordSkyfall_DescendLoop", landingCell, Mathf.Lerp(Props.landWeaponScale + 0.5f, Props.landWeaponScale, progress), Props.landWeaponAngle);
                lastPulseTick = now;
            }

            if (now >= stageEndTick)
            {
                IntVec3 impactCell = landingCell.IsValid ? landingCell : caster.Position;
                DoImpact(caster, map, impactCell);
                ClearStage();
            }
        }

        private float StageProgress(int now)
        {
            if (stageEndTick <= stageStartTick)
            {
                return 1f;
            }

            return Mathf.Clamp01((now - stageStartTick) / (float)(stageEndTick - stageStartTick));
        }

        private void BeginStage(SkyfallStage nextStage, int durationTicks)
        {
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            stage = nextStage;
            stageStartTick = now;
            stageEndTick = now + Mathf.Max(1, durationTicks);
            lastPulseTick = -99999;
        }

        private void ClearStage()
        {
            Pawn caster = parent != null ? parent.pawn : null;
            if (caster != null)
            {
                NeiyuSkyfallVisualTracker.Clear(caster);
            }

            stage = SkyfallStage.None;
            stageStartTick = -1;
            stageEndTick = -1;
            lastPulseTick = -99999;
        }

        private IntVec3 FindLandingCell(Map map, IntVec3 desired, Pawn caster)
        {
            if (map == null)
            {
                return desired;
            }

            IntVec3 cell = desired;
            if (!cell.IsValid || !cell.InBounds(map))
            {
                cell = NeiyuFlowerSwordSkillUtility.ClampToMap(cell, map);
            }

            if (JumpUtility.ValidJumpTarget(caster, map, cell))
            {
                return cell;
            }

            int count = GenRadial.NumCellsInRadius(3.9f);
            for (int i = 0; i < count; i++)
            {
                IntVec3 c = cell + GenRadial.RadialPattern[i];
                if (JumpUtility.ValidJumpTarget(caster, map, c))
                {
                    return c;
                }
            }

            return caster != null ? caster.Position : cell;
        }

        private void DoImpact(Pawn caster, Map map, IntVec3 center)
        {
            NeiyuFlowerSwordSkillUtility.PlayEffecterAt(map, center, Props.effectBDefName, 1f);
            NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, center, Props.landFleckDefName, 1.4f);
            NeiyuFlowerSwordSkillUtility.PlayEffecterAt(map, center, Props.effectCDefName, 1f);
            NeiyuWeaponVisualHooks.Notify(caster, "SwordSkyfall_LandTipDown", center, Props.landWeaponScale, Props.landWeaponAngle);

            if (!Props.impactSoundDefName.NullOrEmpty())
            {
                SoundDef impactSound = DefDatabase<SoundDef>.GetNamedSilentFail(Props.impactSoundDefName);
                impactSound?.PlayOneShot(new TargetInfo(center, map));
            }

            if (Find.CurrentMap == map && Props.landingScreenShake > 0f && Find.CameraDriver != null && Find.CameraDriver.shaker != null)
            {
                if (Props.landingScreenShakeTicks > 0)
                {
                    Find.CameraDriver.shaker.DoShake(Props.landingScreenShake, Props.landingScreenShakeTicks);
                }
                else
                {
                    Find.CameraDriver.shaker.DoShake(Props.landingScreenShake);
                }
            }

            int dustCount = Mathf.Max(0, Props.landingDustBurstCount);
            if (dustCount > 0)
            {
                Vector3 centerPos = center.ToVector3Shifted();
                for (int i = 0; i < dustCount; i++)
                {
                    float angleDeg = Rand.Range(0f, 360f);
                    float radius = Props.landingDustRadius * Mathf.Sqrt(Rand.Value);
                    float rad = angleDeg * 0.0174532924f;
                    Vector3 puffPos = centerPos + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
                    FleckMaker.ThrowDustPuff(puffPos, map, Props.landingDustScale * Rand.Range(0.8f, 1.25f));
                }
            }

            DamageDef damageDef = Props.impactDamageDef ?? DamageDefOf.Bomb;
            HashSet<Pawn> pawns = NeiyuFlowerSwordSkillUtility.CollectPawnsInRadius(map, center, Props.impactRadius);
            foreach (Pawn pawn in pawns)
            {
                if (!NeiyuFlowerSwordSkillUtility.IsHostile(caster, pawn))
                {
                    continue;
                }

                DamageInfo dinfo = new DamageInfo(damageDef, Props.impactDamage, Props.impactArmorPen, -1f, caster);
                dinfo.SetIgnoreArmor(true);
                pawn.TakeDamage(dinfo);

                if (Props.vulnerabilityHediff != null)
                {
                    Hediff v = pawn.health.hediffSet.GetFirstHediffOfDef(Props.vulnerabilityHediff);
                    if (v == null)
                    {
                        v = HediffMaker.MakeHediff(Props.vulnerabilityHediff, pawn);
                        pawn.health.AddHediff(v);
                    }
                    NeiyuFlowerSwordSkillUtility.TrySetHediffDuration(v, Props.vulnerabilityDurationTicks);
                }
            }
        }
    }
    public class CompProperties_AbilityNeiyuSwordExecution : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;
        public ThingDef flyerDef;
        public int backstepCells = 2;
        public int dashDamage = 320;
        public string effectADefName = "Skip_EntryNoDelay";
        public string effectBDefName = "ImpactSmallDustCloud";
        public string effectCDefName = "Skip_ExitNoDelay";
        public string launchFleckDefName = "PsycastSkipFlashEntry";
        public string hitFleckDefName = "ExplosionFlash";
        public float backstepWeaponScale = 1.8f;
        public float backstepWeaponAngle = 180f;
        public float dashWeaponScale = 2.4f;
        public float dashWeaponAngle = 0f;
        public float uppercutWeaponScale = 2.6f;
        public float uppercutWeaponAngle = 25f;

        public CompProperties_AbilityNeiyuSwordExecution()
        {
            compClass = typeof(CompAbilityEffect_NeiyuSwordExecution);
        }
    }

    public class CompAbilityEffect_NeiyuSwordExecution : CompAbilityEffect
    {
        private new CompProperties_AbilityNeiyuSwordExecution Props => (CompProperties_AbilityNeiyuSwordExecution)props;

        public override bool ShouldHideGizmo
        {
            get
            {
                Pawn pawn = parent != null ? parent.pawn : null;
                return !NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(pawn, Props.requiredWeapon);
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn caster = parent != null ? parent.pawn : null;
            if (!NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(caster, Props.requiredWeapon))
            {
                reason = "[Neiyu] Need sword form weapon.";
                return true;
            }

            reason = null;
            return false;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent != null ? parent.pawn : null;
            if (!NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(caster, Props.requiredWeapon))
            {
                if (throwMessages)
                {
                    Messages.Message("[Neiyu] Need sword form weapon.", MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            Pawn targetPawn = target.Pawn;
            if (targetPawn == null)
            {
                if (throwMessages)
                {
                    Messages.Message("[Neiyu] Must target a pawn.", MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (!NeiyuFlowerSwordSkillUtility.IsHostile(caster, targetPawn))
            {
                if (throwMessages)
                {
                    Messages.Message("[Neiyu] Target must be hostile.", MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent != null ? parent.pawn : null;
            Pawn victim = target.Pawn;
            Map map = caster != null ? caster.MapHeld : null;
            if (caster == null || victim == null || map == null || !victim.Spawned || victim.MapHeld != map)
            {
                return;
            }

            IntVec3 step = NeiyuFlowerSwordSkillUtility.StepDirectionAwayFrom(caster.Position, victim.Position);
            IntVec3 backCell = caster.Position + step * Mathf.Max(1, Props.backstepCells);
            backCell = NeiyuFlowerSwordSkillUtility.ClampToMap(backCell, map);
            if (backCell != caster.Position && backCell.Standable(map))
            {
                caster.Position = backCell;
                caster.Notify_Teleported();
            }

            NeiyuFlowerSwordSkillUtility.PlayEffecterAt(map, caster.Position, Props.effectADefName, 1f);
            NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, caster.Position, Props.launchFleckDefName, 1f);
            NeiyuWeaponVisualHooks.Notify(caster, "SwordExecute_BackstepTipAway", caster.Position, Props.backstepWeaponScale, Props.backstepWeaponAngle);

            ExecuteDash(caster, victim, map, target);
        }

        private void ExecuteDash(Pawn caster, Pawn victim, Map map, LocalTargetInfo target)
        {
            if (caster == null || victim == null || map == null || victim.Destroyed || victim.Dead || !victim.Spawned)
            {
                return;
            }

            IntVec3 landingCell = FindLandingCell(caster, victim, map);
            if (landingCell.IsValid && landingCell != caster.Position)
            {
                for (int index = 1; index <= 3; index++)
                {
                    Vector3 trailPos = Vector3.Lerp(caster.DrawPos, landingCell.ToVector3Shifted(), index / 3f);
                    NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, trailPos, Props.launchFleckDefName, Mathf.Lerp(0.4f, 0.9f, index / 3f));
                }

                caster.Position = landingCell;
                caster.Notify_Teleported();
            }

            OnJumpCompleted(landingCell.IsValid ? landingCell : caster.Position, target);
        }

        public void OnJumpCompleted(IntVec3 origin, LocalTargetInfo target)
        {
            Pawn caster = parent != null ? parent.pawn : null;
            Pawn victim = target.Pawn;
            Map map = caster != null ? caster.MapHeld : null;
            if (caster == null || map == null || victim == null || victim.Destroyed || victim.Dead)
            {
                return;
            }

            IntVec3 hitCell = victim.Position;
            NeiyuFlowerSwordSkillUtility.PlayEffecterAt(map, hitCell, Props.effectBDefName, 1f);
            NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, hitCell, Props.hitFleckDefName, 1.2f);
            NeiyuWeaponVisualHooks.Notify(caster, "SwordExecute_DashTipForward", hitCell, Props.dashWeaponScale, Props.dashWeaponAngle);

            if (caster.Spawned && victim.Spawned)
            {
                caster.rotationTracker?.Face(victim.DrawPos);
                caster.Drawer.Notify_MeleeAttackOn(victim);
            }

            DecapitateTarget(caster, victim);

            NeiyuFlowerSwordSkillUtility.PlayEffecterAt(map, hitCell, Props.effectCDefName, 1f);
            NeiyuWeaponVisualHooks.Notify(caster, "SwordExecute_UppercutSlash", hitCell, Props.uppercutWeaponScale, Props.uppercutWeaponAngle);
        }

        private void DecapitateTarget(Pawn caster, Pawn victim)
        {
            if (victim == null || victim.health == null || victim.health.hediffSet == null)
            {
                return;
            }

            bool headRemoved = false;
            BodyPartRecord head = victim.health.hediffSet.GetNotMissingParts().FirstOrDefault(p => p.def == BodyPartDefOf.Head);
            if (head != null)
            {
                victim.health.AddHediff(HediffDefOf.MissingBodyPart, head);
                headRemoved = true;
            }

            if (!headRemoved && !victim.Dead && Props.dashDamage > 0)
            {
                DamageInfo finisher = new DamageInfo(DamageDefOf.Cut, Props.dashDamage, 999f, -1f, caster);
                finisher.SetIgnoreArmor(true);
                victim.TakeDamage(finisher);
            }
        }

        private IntVec3 FindLandingCell(Pawn caster, Pawn victim, Map map)
        {
            if (caster == null || victim == null || map == null)
            {
                return IntVec3.Invalid;
            }

            IntVec3 preferred = victim.Position + NeiyuFlowerSwordSkillUtility.StepDirectionAwayFrom(victim.Position, caster.Position);
            preferred = NeiyuFlowerSwordSkillUtility.ClampToMap(preferred, map);

            bool found = false;
            IntVec3 bestCell = IntVec3.Invalid;
            int bestScore = int.MaxValue;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(victim.Position, 1.9f, true))
            {
                if (!cell.InBounds(map) || cell == victim.Position || !cell.Standable(map))
                {
                    continue;
                }

                bool blockedByPawn = false;
                List<Thing> things = cell.GetThingList(map);
                for (int index = 0; index < things.Count; index++)
                {
                    Pawn pawn = things[index] as Pawn;
                    if (pawn != null && pawn != caster && pawn != victim)
                    {
                        blockedByPawn = true;
                        break;
                    }
                }

                if (blockedByPawn)
                {
                    continue;
                }

                int dx = cell.x - preferred.x;
                int dz = cell.z - preferred.z;
                int score = dx * dx + dz * dz;
                if (!found || score < bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestCell = cell;
                }
            }

            return found ? bestCell : caster.Position;
        }
    }

    [HarmonyPatch(typeof(Pawn_DrawTracker), "DrawPos", MethodType.Getter)]
    public static class Patch_MXNeiyuSkyfall_DrawPos
    {
        private static readonly FieldInfo TrackerPawnField = AccessTools.Field(typeof(Pawn_DrawTracker), "pawn");

        [HarmonyPostfix]
        public static void Postfix(Pawn_DrawTracker __instance, ref Vector3 __result)
        {
            Pawn pawn = TrackerPawnField != null ? TrackerPawnField.GetValue(__instance) as Pawn : null;
            if (pawn == null || pawn.Destroyed || !pawn.Spawned)
            {
                return;
            }

            if (NeiyuSkyfallVisualTracker.TryGetOffset(pawn, out Vector3 offset))
            {
                __result += offset;
            }
        }
    }

    [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.NeedInterval))]
    public static class Patch_MXNeiyuSword_HungerFloor
    {
        private const float HungerFloorPercent = 0.20f;
        private static readonly FieldInfo NeedPawnField = AccessTools.Field(typeof(Need), "pawn");
        private static ThingDef cachedSwordDef;
        private static bool swordDefResolved;

        private static ThingDef SwordDef
        {
            get
            {
                if (!swordDefResolved)
                {
                    cachedSwordDef = DefDatabase<ThingDef>.GetNamedSilentFail("MX_Neiyu_Form_Weapon");
                    swordDefResolved = true;
                }

                return cachedSwordDef;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Need_Food __instance)
        {
            if (__instance == null)
            {
                return;
            }

            Pawn pawn = NeedPawnField != null ? NeedPawnField.GetValue(__instance) as Pawn : null;
            if (pawn == null || pawn.equipment == null || pawn.equipment.Primary == null)
            {
                return;
            }

            ThingDef swordDef = SwordDef;
            if (swordDef == null || pawn.equipment.Primary.def != swordDef)
            {
                return;
            }

            if (__instance.CurLevelPercentage < HungerFloorPercent)
            {
                __instance.CurLevelPercentage = HungerFloorPercent;
            }
        }
    }
}
