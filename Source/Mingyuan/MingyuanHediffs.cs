using System.Collections.Generic;
using System.Text;
using MiliraXian.Characters.QingHe;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public class HediffCompProperties_MingyuanLifeBurn : HediffCompProperties
    {
        public int tickInterval = 120;
        public float baseDamage = 0.5f;
        public float damagePer100Layers = 0.25f;
        public float needDrainPer100Layers = 0.01f;
        public float ageTicksPerLayer = 60f;
        public float transferRadius = 30f;
        public float transferFraction = 0.5f;
        public float executeHealthScaleMultiplier = 10f;
        public float burstDamageMultiplier = 1f;
        public float burnSelfStackFraction = 0.05f;
        public int decayDelayTicks = 1800;
        public float decayFraction = 0.1f;
        public float removeBelowLayers = 0.5f;
        public int transferVisualLimit = 8;

        public HediffCompProperties_MingyuanLifeBurn()
        {
            compClass = typeof(HediffComp_MingyuanLifeBurn);
        }
    }

    public class HediffComp_MingyuanLifeBurn : HediffComp
    {
        private static readonly List<Pawn> TransferTargets = new List<Pawn>(64);

        private Pawn instigator;
        private int ticksToNextDamage;
        private int lastExternalStackTick;
        private Mote lifeBurnMark;

        public HediffCompProperties_MingyuanLifeBurn PropsLifeBurn => (HediffCompProperties_MingyuanLifeBurn)props;

        public float CurrentLayers => Mathf.Max(0f, parent?.Severity ?? 0f);

        public float ExecuteThreshold
        {
            get
            {
                float lethalThreshold = Pawn?.health?.LethalDamageThreshold ?? ((Pawn?.HealthScale ?? 1f) * 150f);
                return Mathf.Max(1f, lethalThreshold * Mathf.Max(0.01f, PropsLifeBurn.executeHealthScaleMultiplier));
            }
        }

        public float RemainingToExecute => Mathf.Max(0f, ExecuteThreshold - CurrentLayers);

        public float ExecuteProgress => Mathf.Clamp01(CurrentLayers / ExecuteThreshold);

        public float BurstDamage => Mathf.Max(Pawn?.health?.LethalDamageThreshold ?? 1f, ExecuteThreshold * Mathf.Max(0.01f, PropsLifeBurn.burstDamageMultiplier));

        public override string CompLabelInBracketsExtra => Mathf.RoundToInt(CurrentLayers) + "/" + Mathf.RoundToInt(ExecuteThreshold);

        public override string CompTipStringExtra
        {
            get
            {
                if (Pawn == null || CurrentLayers <= 0f)
                {
                    return null;
                }

                float layers = CurrentLayers;
                float per100Percent = Mathf.Min(95f, layers / 100f);
                float increasedPercent = Mathf.Min(95f, layers / 100f);
                float periodicDamage = PeriodicDamageFor(layers);
                float selfStack = layers * Mathf.Max(0f, PropsLifeBurn.burnSelfStackFraction);
                float needDrainPercent = PropsLifeBurn.needDrainPer100Layers * (layers / 100f) * 100f;
                int ageTicks = Mathf.RoundToInt(layers * PropsLifeBurn.ageTicksPerLayer);
                int equipmentLoss = HitPointLossFor(layers);
                int decaySeconds = Mathf.CeilToInt(Mathf.Max(0, PropsLifeBurn.decayDelayTicks) / 60f);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipCurrentLayers".Translate(FormatNumber(layers)));
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipExecuteThreshold".Translate(FormatNumber(ExecuteThreshold)));
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipRemaining".Translate(FormatNumber(RemainingToExecute)));
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipProgress".Translate(FormatPercent(ExecuteProgress * 100f)));
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipBurstDamage".Translate(FormatNumber(BurstDamage)));
                builder.AppendLine();
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipPeriodicDamage".Translate(FormatNumber(periodicDamage)));
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipSelfStack".Translate(FormatNumber(selfStack)));
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipDecay".Translate(decaySeconds.ToString(), FormatPercent(PropsLifeBurn.decayFraction * 100f)));
                builder.AppendLine();
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipDebuffDown".Translate(FormatPercent(per100Percent)));
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipDebuffUp".Translate(FormatPercent(increasedPercent)));
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipNeedsAgeGear".Translate(FormatPercent(needDrainPercent), ageTicks.ToString(), equipmentLoss.ToString()));
                builder.AppendLine("MX_Mingyuan_LifeBurn_TipTransfer".Translate(FormatPercent(PropsLifeBurn.transferFraction * 100f), PropsLifeBurn.transferRadius.ToString("F0"), Mathf.Max(0, PropsLifeBurn.transferVisualLimit).ToString()));
                return builder.ToString().TrimEnd('\r', '\n');
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref instigator, "instigator", false);
            Scribe_Values.Look(ref ticksToNextDamage, "ticksToNextDamage", 0);
            Scribe_Values.Look(ref lastExternalStackTick, "lastExternalStackTick", 0);
        }

        public void SetInstigator(Pawn pawn)
        {
            if (pawn != null)
            {
                instigator = pawn;
            }
        }

        public void NotifyLifeBurnStack(Pawn pawn, bool refreshDecayTimer)
        {
            SetInstigator(pawn);
            int tick = CurrentTick;
            if (refreshDecayTimer || lastExternalStackTick <= 0)
            {
                lastExternalStackTick = tick;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            if (MingyuanUtility.IsLifeBurnImmunePawn(Pawn))
            {
                Pawn.health.RemoveHediff(parent);
                return;
            }

            if (lastExternalStackTick <= 0)
            {
                lastExternalStackTick = CurrentTick;
            }

            MaintainLifeBurnMark();

            ticksToNextDamage--;
            if (ticksToNextDamage > 0)
            {
                return;
            }

            ticksToNextDamage = Mathf.Max(1, PropsLifeBurn.tickInterval);
            ApplyPeriodicEffects();
        }

        private void ApplyPeriodicEffects()
        {
            float layers = CurrentLayers;
            if (layers <= 0f)
            {
                Pawn.health.RemoveHediff(parent);
                return;
            }

            DrainNeeds(layers);
            AgePawn(layers);
            DamageEquipment(layers);

            float damage = PeriodicDamageFor(layers);
            MingyuanUtility.ApplyTrueDamage(Pawn, DamageDefOf.Burn, damage, instigator);
            if (Pawn.Dead)
            {
                return;
            }

            AddInternalLayers(layers * Mathf.Max(0f, PropsLifeBurn.burnSelfStackFraction));
            ApplyDecayIfNeeded();
            if (Pawn.Dead || parent == null || CurrentLayers <= 0f)
            {
                return;
            }

            if (CurrentLayers >= ExecuteThreshold)
            {
                TriggerBurstDamage();
            }
        }

        private void DrainNeeds(float layers)
        {
            if (Pawn.needs == null || Pawn.needs.AllNeeds == null)
            {
                return;
            }

            float amount = PropsLifeBurn.needDrainPer100Layers * (layers / 100f);
            for (int i = 0; i < Pawn.needs.AllNeeds.Count; i++)
            {
                Need need = Pawn.needs.AllNeeds[i];
                if (need == null || need.def?.defName == "Mood")
                {
                    continue;
                }

                need.CurLevel = Mathf.Max(0f, need.CurLevel - amount);
            }
        }

        private void AgePawn(float layers)
        {
            if (Pawn.ageTracker == null)
            {
                return;
            }

            long addedTicks = Mathf.RoundToInt(layers * PropsLifeBurn.ageTicksPerLayer);
            if (addedTicks > 0)
            {
                Pawn.ageTracker.AgeBiologicalTicks += addedTicks;
            }
        }

        private void DamageEquipment(float layers)
        {
            int hitPointLoss = HitPointLossFor(layers);
            if (Pawn.apparel?.WornApparel != null)
            {
                for (int i = 0; i < Pawn.apparel.WornApparel.Count; i++)
                {
                    DamageThingHitPoints(Pawn.apparel.WornApparel[i], hitPointLoss);
                }
            }

            if (Pawn.equipment?.AllEquipmentListForReading != null)
            {
                List<ThingWithComps> equipment = Pawn.equipment.AllEquipmentListForReading;
                for (int i = 0; i < equipment.Count; i++)
                {
                    DamageThingHitPoints(equipment[i], hitPointLoss);
                }
            }
        }

        private void DamageThingHitPoints(Thing thing, int amount)
        {
            if (thing == null || thing.Destroyed || thing.def.useHitPoints == false)
            {
                return;
            }

            thing.HitPoints = Mathf.Max(1, thing.HitPoints - amount);
        }

        private void AddInternalLayers(float layers)
        {
            if (layers <= 0f || Pawn == null || Pawn.Dead)
            {
                return;
            }

            parent.Severity = Mathf.Clamp(parent.Severity + layers, 0f, parent.def.maxSeverity);
            Pawn.health.Notify_HediffChanged(parent);
        }

        private void ApplyDecayIfNeeded()
        {
            if (PropsLifeBurn.decayDelayTicks <= 0 || PropsLifeBurn.decayFraction <= 0f)
            {
                return;
            }

            if (CurrentTick - lastExternalStackTick < PropsLifeBurn.decayDelayTicks)
            {
                return;
            }

            parent.Severity = Mathf.Max(0f, parent.Severity - parent.Severity * Mathf.Clamp01(PropsLifeBurn.decayFraction));
            if (parent.Severity <= Mathf.Max(0f, PropsLifeBurn.removeBelowLayers))
            {
                Pawn.health.RemoveHediff(parent);
            }
            else
            {
                Pawn.health.Notify_HediffChanged(parent);
            }
        }

        private void TriggerBurstDamage()
        {
            SpawnBurstMote(Pawn.MapHeld, Pawn.PositionHeld);
            MingyuanUtility.ApplyTrueDamage(Pawn, DamageDefOf.Burn, BurstDamage, instigator);
            if (!Pawn.Dead && Pawn.health?.hediffSet?.hediffs?.Contains(parent) == true)
            {
                Pawn.health.RemoveHediff(parent);
            }
        }

        private void MaintainLifeBurnMark()
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.MapHeld == null)
            {
                return;
            }

            ThingDef markDef = MX_MingyuanDefOf.MX_Mingyuan_Mote_LifeBurnMark ?? DefDatabase<ThingDef>.GetNamedSilentFail("MX_Mingyuan_Mote_LifeBurnMark");
            if (markDef == null)
            {
                return;
            }

            if (lifeBurnMark == null || lifeBurnMark.Destroyed)
            {
                lifeBurnMark = MoteMaker.MakeAttachedOverlay(Pawn, markDef, Vector3.zero, 1f);
            }

            lifeBurnMark?.Maintain();
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            Map map = Pawn?.MapHeld;
            if (map == null || parent.Severity <= 0f)
            {
                return;
            }

            float transferred = parent.Severity * PropsLifeBurn.transferFraction;
            if (transferred <= 0f)
            {
                return;
            }

            IntVec3 originCell = Pawn.PositionHeld;
            int radiusSquared = Mathf.CeilToInt(PropsLifeBurn.transferRadius * PropsLifeBurn.transferRadius);
            IReadOnlyList<Pawn> spawnedPawns = map.mapPawns.AllPawnsSpawned;
            TransferTargets.Clear();

            for (int i = 0; i < spawnedPawns.Count; i++)
            {
                Pawn target = spawnedPawns[i];
                if (target == null || target == Pawn || target.Dead || !target.Spawned)
                {
                    continue;
                }

                if (target.PositionHeld.DistanceToSquared(originCell) <= radiusSquared
                    && MingyuanUtility.IsHostilePawn(target, instigator, out Pawn hostileTarget))
                {
                    TransferTargets.Add(hostileTarget);
                }
            }

            if (TransferTargets.Count == 0)
            {
                return;
            }

            SpawnBurstMote(map, originCell);
            TransferTargets.Sort(delegate(Pawn left, Pawn right)
            {
                int leftDistance = left.PositionHeld.DistanceToSquared(originCell);
                int rightDistance = right.PositionHeld.DistanceToSquared(originCell);
                return leftDistance.CompareTo(rightDistance);
            });

            int visualCount = Mathf.Min(TransferTargets.Count, Mathf.Max(0, PropsLifeBurn.transferVisualLimit));
            for (int i = 0; i < TransferTargets.Count; i++)
            {
                Pawn target = TransferTargets[i];
                MingyuanUtility.AddLifeBurn(target, instigator, transferred);
                if (i < visualCount)
                {
                    SpawnTransferTrail(map, originCell, target);
                }
            }

            TransferTargets.Clear();
        }

        private void SpawnBurstMote(Map map, IntVec3 cell)
        {
            ThingDef burstDef = MX_MingyuanDefOf.MX_Mingyuan_Mote_LifeBurnBurst ?? DefDatabase<ThingDef>.GetNamedSilentFail("MX_Mingyuan_Mote_LifeBurnBurst");
            if (map != null && burstDef != null && cell.IsValid)
            {
                MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, burstDef, 1f);
            }
        }

        private void SpawnTransferTrail(Map map, IntVec3 originCell, Pawn target)
        {
            ThingDef trailDef = MX_MingyuanDefOf.MX_Mingyuan_Mote_LifeBurnTransferTrail ?? DefDatabase<ThingDef>.GetNamedSilentFail("MX_Mingyuan_Mote_LifeBurnTransferTrail");
            FleckDef lineDef = MX_MingyuanDefOf.MX_Mingyuan_Fleck_LifeBurnTransferLine ?? DefDatabase<FleckDef>.GetNamedSilentFail("MX_Mingyuan_Fleck_LifeBurnTransferLine");
            FleckDef distortDef = MX_MingyuanDefOf.MX_Mingyuan_Fleck_LifeBurnTransferDistort ?? DefDatabase<FleckDef>.GetNamedSilentFail("MX_Mingyuan_Fleck_LifeBurnTransferDistort");
            if (map == null || trailDef == null || lineDef == null || target == null || !target.Spawned)
            {
                return;
            }

            Mote_QHCurvedDistortionTrail trail = ThingMaker.MakeThing(trailDef) as Mote_QHCurvedDistortionTrail;
            if (trail == null)
            {
                return;
            }

            trail.Setup(
                new TargetInfo(originCell, map),
                new TargetInfo(target),
                lineDef,
                distortDef,
                0.045f,
                2.0f,
                0.24f,
                1.45f,
                4.0f,
                5.2f,
                40,
                12,
                1.15f,
                1,
                3,
                0.4f,
                8,
                48,
                5);

            GenSpawn.Spawn(trail, originCell, map);
            trail.exactPosition = originCell.ToVector3Shifted();
        }

        private float PeriodicDamageFor(float layers)
        {
            return Mathf.Max(0f, PropsLifeBurn.baseDamage + (layers / 100f) * PropsLifeBurn.damagePer100Layers);
        }

        private static int HitPointLossFor(float layers)
        {
            return Mathf.Max(1, Mathf.RoundToInt(layers / 100f));
        }

        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        private static string FormatNumber(float value)
        {
            return value >= 10f ? value.ToString("F0") : value.ToString("F1");
        }

        private static string FormatPercent(float value)
        {
            return value.ToString("F1") + "%";
        }
    }

    public class HediffCompProperties_MingyuanSelfBurn : HediffCompProperties
    {
        public int decayIntervalTicks = 120;
        public float decayLayers = 10f;
        public ThingDef gainMoteDef;
        public float gainMoteScale = 1f;
        public int gainMoteCooldownTicks = 60;
        public float effectiveBonusCap = MingyuanUtility.DefaultSelfBurnEffectiveCap;
        public float overburnThreshold = MingyuanUtility.DefaultSelfBurnEffectiveCap;
        public float overburnDamageFactor = 2f;
        public float overburnLifeBurnFactor = 2f;
        public int combatRegenIntervalTicks = 60;
        public float combatRegenLayers = 1f;
        public int nonCombatDecayIntervalTicks = 60;
        public float nonCombatDecayLayers = 1f;
        public float overburnDecayLayers = 1f;
        public ThingDef overburnMoteDef;
        public float overburnMoteScale = 1.35f;

        public HediffCompProperties_MingyuanSelfBurn()
        {
            compClass = typeof(HediffComp_MingyuanSelfBurn);
        }
    }

    public class HediffComp_MingyuanSelfBurn : HediffComp
    {
        private int ticksToDecay;
        private int nextGainMoteTick;
        private bool wasOverburning;
        private Mote overburnMote;

        public HediffCompProperties_MingyuanSelfBurn PropsSelfBurn => (HediffCompProperties_MingyuanSelfBurn)props;

        public override string CompLabelInBracketsExtra
        {
            get
            {
                string layers = Mathf.FloorToInt(parent?.Severity ?? 0f).ToStringCached();
                return IsOverburningNow()
                    ? "MX_Mingyuan_SelfBurn_LabelExtraOverburn".Translate(layers).ToString()
                    : "MX_Mingyuan_SelfBurn_LabelExtra".Translate(layers).ToString();
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                float layers = Mathf.Max(0f, parent?.Severity ?? 0f);
                int wholeLayers = Mathf.FloorToInt(layers);
                float effectiveLayers = Pawn != null ? MingyuanUtility.GetSelfBurnEffectiveLayers(Pawn) : Mathf.Min(layers, PropsSelfBurn.effectiveBonusCap);
                float overburnLayers = Mathf.Max(0f, layers - PropsSelfBurn.overburnThreshold);
                bool overburning = overburnLayers > 0f;
                int per100 = Mathf.FloorToInt(effectiveLayers / 100f);
                float damage = effectiveLayers;
                float move = effectiveLayers * 0.5f;
                float attackSpeed = effectiveLayers;
                float work = effectiveLayers;
                float meleeLifeBurn = per100 * 10f;
                float rangedLifeBurn = per100 * 2f;
                int ticksRemaining = Mathf.Max(0, ticksToDecay);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("MX_Mingyuan_SelfBurn_TipCurrentLayers".Translate(wholeLayers.ToStringCached()));
                builder.AppendLine("MX_Mingyuan_SelfBurn_TipEffectiveLayers".Translate(FormatNumber(effectiveLayers), FormatNumber(PropsSelfBurn.effectiveBonusCap)));
                builder.AppendLine("MX_Mingyuan_SelfBurn_TipOverburn".Translate(FormatNumber(overburnLayers), FormatNumber(PropsSelfBurn.overburnThreshold), overburning ? "MX_Mingyuan_Enabled".Translate() : "MX_Mingyuan_Disabled".Translate()));
                builder.AppendLine("MX_Mingyuan_SelfBurn_TipBonuses".Translate(FormatPercent(damage), FormatPercent(move), FormatPercent(attackSpeed), FormatPercent(work)));
                builder.AppendLine("MX_Mingyuan_SelfBurn_TipLifeBurnBonus".Translate(FormatNumber(meleeLifeBurn), FormatNumber(rangedLifeBurn)));
                builder.AppendLine("MX_Mingyuan_SelfBurn_TipOverburnEffect".Translate(FormatPercent((PropsSelfBurn.overburnDamageFactor - 1f) * 100f), FormatPercent((PropsSelfBurn.overburnLifeBurnFactor - 1f) * 100f)));
                builder.AppendLine("MX_Mingyuan_SelfBurn_TipRhythm".Translate(
                    PropsSelfBurn.combatRegenIntervalTicks.ToStringCached(),
                    FormatNumber(PropsSelfBurn.combatRegenLayers),
                    FormatNumber(PropsSelfBurn.overburnThreshold),
                    PropsSelfBurn.nonCombatDecayIntervalTicks.ToStringCached(),
                    FormatNumber(PropsSelfBurn.nonCombatDecayLayers),
                    FormatNumber(PropsSelfBurn.overburnDecayLayers),
                    ticksRemaining.ToStringCached()));
                return builder.ToString().TrimEnd((char)13, (char)10);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksToDecay, "ticksToDecay", 0);
            Scribe_Values.Look(ref nextGainMoteTick, "nextGainMoteTick", 0);
            Scribe_Values.Look(ref wasOverburning, "wasOverburning", false);
        }

        public void NotifySelfBurnStack(bool refreshDecayTimer = true, bool showMote = true)
        {
            if (refreshDecayTimer)
            {
                ticksToDecay = CurrentChangeIntervalTicks();
            }

            if (showMote)
            {
                TrySpawnGainMote();
            }

            UpdateOverburnVisuals();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            UpdateOverburnVisuals();
            ticksToDecay--;
            if (ticksToDecay > 0)
            {
                return;
            }

            ticksToDecay = CurrentChangeIntervalTicks();
            ApplyNaturalChange();
        }

        private void ApplyNaturalChange()
        {
            if (parent == null || Pawn == null)
            {
                return;
            }

            if (IsOverburningNow())
            {
                parent.Severity = Mathf.Max(PropsSelfBurn.overburnThreshold, parent.Severity - Mathf.Max(0f, PropsSelfBurn.overburnDecayLayers));
                return;
            }

            if (Pawn.Drafted)
            {
                parent.Severity = Mathf.Min(PropsSelfBurn.overburnThreshold, parent.Severity + Mathf.Max(0f, PropsSelfBurn.combatRegenLayers));
                return;
            }

            parent.Severity = Mathf.Max(0f, parent.Severity - Mathf.Max(0f, PropsSelfBurn.nonCombatDecayLayers));
            if (parent.Severity <= 0f && !MingyuanUtility.IsMingyuan(Pawn))
            {
                Pawn.health.RemoveHediff(parent);
            }
        }

        private int CurrentChangeIntervalTicks()
        {
            if (IsOverburningNow())
            {
                return Mathf.Max(1, PropsSelfBurn.nonCombatDecayIntervalTicks);
            }

            return Mathf.Max(1, Pawn?.Drafted == true ? PropsSelfBurn.combatRegenIntervalTicks : PropsSelfBurn.nonCombatDecayIntervalTicks);
        }

        private bool IsOverburningNow()
        {
            return (parent?.Severity ?? 0f) > PropsSelfBurn.overburnThreshold;
        }

        private void UpdateOverburnVisuals()
        {
            bool overburning = IsOverburningNow();
            if (overburning && !wasOverburning)
            {
                TrySpawnGainMote(PropsSelfBurn.overburnMoteDef ?? PropsSelfBurn.gainMoteDef, PropsSelfBurn.overburnMoteScale);
            }

            wasOverburning = overburning;
            if (overburning)
            {
                MaintainOverburnMote();
            }
        }

        private void MaintainOverburnMote()
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.MapHeld == null)
            {
                return;
            }

            ThingDef moteDef = PropsSelfBurn.overburnMoteDef ?? PropsSelfBurn.gainMoteDef ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_SelfBurnOverburn ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_SelfBurnGain;
            if (moteDef == null)
            {
                return;
            }

            if (overburnMote == null || overburnMote.Destroyed)
            {
                overburnMote = MoteMaker.MakeAttachedOverlay(Pawn, moteDef, Vector3.zero, Mathf.Max(0.1f, PropsSelfBurn.overburnMoteScale));
            }

            overburnMote?.Maintain();
        }

        private void TrySpawnGainMote()
        {
            TrySpawnGainMote(PropsSelfBurn.gainMoteDef ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_SelfBurnGain, PropsSelfBurn.gainMoteScale);
        }

        private void TrySpawnGainMote(ThingDef moteDef, float scale)
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.MapHeld == null)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            if (tick < nextGainMoteTick)
            {
                return;
            }

            if (MingyuanUtility.TryMakeAttachedMote(Pawn, moteDef, scale))
            {
                nextGainMoteTick = tick + Mathf.Max(1, PropsSelfBurn.gainMoteCooldownTicks);
            }
        }

        private static string FormatNumber(float value)
        {
            return value >= 10f ? value.ToString("F0") : value.ToString("F1");
        }

        private static string FormatPercent(float value)
        {
            return value.ToString("F1") + "%";
        }
    }

    public class HediffCompProperties_MingyuanBurningBody : HediffCompProperties
    {
        public int restoreIntervalTicks = 1800;
        public int invulnerableTicks = 90;
        public float reflectLifeBurnLayers = 20f;
        public float selfBurnOnHit = 2f;
        public float heatShieldEnergyFactor = 0.25f;

        public HediffCompProperties_MingyuanBurningBody()
        {
            compClass = typeof(HediffComp_MingyuanBurningBody);
        }
    }

    public class HediffComp_MingyuanBurningBody : HediffComp
    {
        private int invulnerableUntilTick;
        private int ticksToRestore;

        public HediffCompProperties_MingyuanBurningBody PropsBody => (HediffCompProperties_MingyuanBurningBody)props;

        public bool Invulnerable => Find.TickManager != null && Find.TickManager.TicksGame < invulnerableUntilTick;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref invulnerableUntilTick, "invulnerableUntilTick", 0);
            Scribe_Values.Look(ref ticksToRestore, "ticksToRestore", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            if (Pawn.Drafted && Pawn.health?.hediffSet?.GetFirstHediffOfDef(MingyuanUtility.SelfBurnDef) == null)
            {
                MingyuanUtility.EnsureSelfBurnTracker(Pawn);
            }

            ticksToRestore--;
            if (ticksToRestore > 0)
            {
                return;
            }

            ticksToRestore = Mathf.Max(1, PropsBody.restoreIntervalTicks);
            MingyuanUtility.RestorePawnToBestCondition(Pawn, true);
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (Pawn == null || Pawn.Dead || totalDamageDealt <= 0f)
            {
                return;
            }

            invulnerableUntilTick = Find.TickManager.TicksGame + Mathf.Max(1, PropsBody.invulnerableTicks);
            MingyuanUtility.AddSelfBurn(Pawn, PropsBody.selfBurnOnHit);

            Pawn attacker = dinfo.Instigator as Pawn;
            if (attacker != null && attacker != Pawn && attacker.HostileTo(Pawn) && !attacker.Dead)
            {
                MingyuanUtility.ApplyTrueDamage(attacker, dinfo.Def ?? DamageDefOf.Burn, Mathf.Max(1f, dinfo.Amount), Pawn);
                MingyuanUtility.AddLifeBurn(attacker, Pawn, PropsBody.reflectLifeBurnLayers);
            }
        }
    }

    public class HediffCompProperties_MingyuanProtectiveFlameShield : HediffCompProperties
    {
        public float maxEnergy = 100f;
        public int repairIntervalTicks = 60;
        public int selfBurnRefillIntervalTicks = 300;
        public int selfBurnRefillCooldownTicks = 1800;
        public float selfBurnPerEnergy = 3f;
        public float selfBurnRefillMaxFractionOfCap = 0.3f;
        public int overburnDrainIntervalTicks = 300;
        public float overburnDrainEnergy = 1f;
        public float lowIgnoreDamage = 20f;
        public float highIgnoreDamage = 100f;
        public int breakRecoverTicks = 480;
        public float hitEnergyCost = 10f;
        public float selfBurnNoCostThreshold = 300f;
        public float selfBurnOnNoCostHit = 10f;
        public ThingDef shieldSelfBurnMoteDef;
        public float shieldSelfBurnMoteScale = 1f;
        public int shieldSelfBurnMoteCooldownTicks = 60;

        public HediffCompProperties_MingyuanProtectiveFlameShield()
        {
            compClass = typeof(HediffComp_MingyuanProtectiveFlameShield);
        }
    }

    public class HediffComp_MingyuanProtectiveFlameShield : HediffComp
    {
        private float energy = -1f;
        private int brokenUntilTick;
        private int nextShieldSelfBurnMoteTick;
        private int ticksToRegenSettlement;
        private float pendingRegenEnergy;
        private int ticksToRepair;
        private int ticksToSelfBurnRefill;
        private int ticksToOverburnDrain;
        private int selfBurnRefillCooldownTicksLeft;

        public HediffCompProperties_MingyuanProtectiveFlameShield PropsShield => (HediffCompProperties_MingyuanProtectiveFlameShield)props;

        public bool Broken => false;
        public float Energy => Mathf.Clamp(energy < 0f ? PropsShield.maxEnergy : energy, 0f, PropsShield.maxEnergy);

        public override string CompLabelInBracketsExtra => Mathf.RoundToInt(Energy) + "/" + Mathf.RoundToInt(PropsShield.maxEnergy);

        public override string CompTipStringExtra
        {
            get
            {
                if (Pawn == null)
                {
                    return null;
                }

                bool overburning = MingyuanUtility.IsOverburning(Pawn);
                bool canRefill = !overburning && MingyuanUtility.GetSelfBurnLayers(Pawn) >= PropsShield.selfBurnPerEnergy && Energy < PropsShield.maxEnergy;

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("MX_Mingyuan_Shield_TipStatus".Translate(FormatNumber(Energy), FormatNumber(PropsShield.maxEnergy)));
                builder.AppendLine("MX_Mingyuan_Shield_TipRepair".Translate(TicksToSeconds(ticksToRepair).ToString(), FormatNumber(PropsShield.overburnDrainEnergy)));
                builder.AppendLine("MX_Mingyuan_Shield_TipRefill".Translate(
                    TicksToSeconds(ticksToSelfBurnRefill).ToString(),
                    FormatNumber(PropsShield.selfBurnPerEnergy),
                    FormatNumber(MaxSelfBurnToSpend()),
                    TicksToSeconds(selfBurnRefillCooldownTicksLeft).ToString(),
                    canRefill ? "MX_Mingyuan_Enabled".Translate() : "MX_Mingyuan_Disabled".Translate()));
                builder.AppendLine("MX_Mingyuan_Shield_TipOverburnDrain".Translate(
                    overburning ? "MX_Mingyuan_Enabled".Translate() : "MX_Mingyuan_Disabled".Translate(),
                    FormatNumber(PropsShield.overburnDrainEnergy),
                    TicksToSeconds(ticksToOverburnDrain).ToString()));
                return builder.ToString().TrimEnd((char)13, (char)10);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref energy, "energy", -1f);
            Scribe_Values.Look(ref brokenUntilTick, "brokenUntilTick", 0);
            Scribe_Values.Look(ref nextShieldSelfBurnMoteTick, "nextShieldSelfBurnMoteTick", 0);
            Scribe_Values.Look(ref ticksToRegenSettlement, "ticksToRegenSettlement", 0);
            Scribe_Values.Look(ref pendingRegenEnergy, "pendingRegenEnergy", 0f);
            Scribe_Values.Look(ref ticksToRepair, "ticksToRepair", 0);
            Scribe_Values.Look(ref ticksToSelfBurnRefill, "ticksToSelfBurnRefill", 0);
            Scribe_Values.Look(ref ticksToOverburnDrain, "ticksToOverburnDrain", 0);
            Scribe_Values.Look(ref selfBurnRefillCooldownTicksLeft, "selfBurnRefillCooldownTicksLeft", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            if (energy < 0f)
            {
                energy = PropsShield.maxEnergy;
            }
            energy = Mathf.Clamp(energy, 0f, PropsShield.maxEnergy);

            TickRepairReserve();
            TickSelfBurnRefillOrOverburnDrain();
        }

        private void TickRepairReserve()
        {
            if (ticksToRepair <= 0)
            {
                ticksToRepair = RepairIntervalTicks;
            }

            ticksToRepair--;
            if (ticksToRepair > 0 || Energy < 1f)
            {
                return;
            }

            if (TryRepairOneBodyPart())
            {
                energy = Mathf.Max(0f, energy - 1f);
                TrySpawnShieldSelfBurnMote();
            }
        }

        private void TickSelfBurnRefillOrOverburnDrain()
        {
            if (selfBurnRefillCooldownTicksLeft > 0)
            {
                selfBurnRefillCooldownTicksLeft--;
            }

            if (MingyuanUtility.IsOverburning(Pawn))
            {
                ticksToSelfBurnRefill = SelfBurnRefillIntervalTicks;
                TickOverburnDrain();
                return;
            }

            ticksToOverburnDrain = OverburnDrainIntervalTicks;
            if (ticksToSelfBurnRefill <= 0)
            {
                ticksToSelfBurnRefill = SelfBurnRefillIntervalTicks;
            }

            ticksToSelfBurnRefill--;
            if (ticksToSelfBurnRefill > 0 || selfBurnRefillCooldownTicksLeft > 0 || Energy >= PropsShield.maxEnergy)
            {
                return;
            }

            ticksToSelfBurnRefill = SelfBurnRefillIntervalTicks;
            if (TryConvertSelfBurnToShield())
            {
                selfBurnRefillCooldownTicksLeft = Mathf.Max(1, PropsShield.selfBurnRefillCooldownTicks);
                TrySpawnShieldSelfBurnMote();
            }
        }

        private void TickOverburnDrain()
        {
            if (ticksToOverburnDrain <= 0)
            {
                ticksToOverburnDrain = OverburnDrainIntervalTicks;
            }

            ticksToOverburnDrain--;
            if (ticksToOverburnDrain <= 0 && energy > 0f)
            {
                energy = Mathf.Max(0f, energy - Mathf.Max(0f, PropsShield.overburnDrainEnergy));
                ticksToOverburnDrain = OverburnDrainIntervalTicks;
            }
        }

        private int RepairIntervalTicks => Mathf.Max(1, PropsShield.repairIntervalTicks);

        private int SelfBurnRefillIntervalTicks => Mathf.Max(1, PropsShield.selfBurnRefillIntervalTicks);

        private int OverburnDrainIntervalTicks => Mathf.Max(1, PropsShield.overburnDrainIntervalTicks);

        public bool TryConsumeEnergy(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (energy < 0f)
            {
                energy = PropsShield.maxEnergy;
            }

            if (energy < amount)
            {
                return false;
            }

            energy -= amount;
            return true;
        }

        public bool TryConsumeAllEnergy(float minimumEnergy = 0.01f)
        {
            if (Pawn == null || Pawn.Dead)
            {
                return false;
            }

            if (energy < 0f)
            {
                energy = PropsShield.maxEnergy;
            }

            if (Broken || energy <= minimumEnergy)
            {
                return false;
            }

            energy = 0f;
            return true;
        }

        public void AddEnergy(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            if (energy < 0f)
            {
                energy = PropsShield.maxEnergy;
            }

            energy = Mathf.Min(PropsShield.maxEnergy, energy + amount);
        }

        public bool TryAbsorb(ref DamageInfo dinfo, ref bool absorbed)
        {
            return false;
        }

        private bool TryConvertSelfBurnToShield()
        {
            float missingEnergy = PropsShield.maxEnergy - Energy;
            if (missingEnergy <= 0f)
            {
                return false;
            }

            float maxSpend = MaxSelfBurnToSpend();
            float available = Mathf.Min(MingyuanUtility.GetSelfBurnLayers(Pawn), maxSpend);
            float restore = Mathf.Min(missingEnergy, available / Mathf.Max(0.001f, PropsShield.selfBurnPerEnergy));
            if (restore <= 0f)
            {
                return false;
            }

            float spent = MingyuanUtility.ReduceSelfBurn(Pawn, restore * PropsShield.selfBurnPerEnergy);
            if (spent <= 0f)
            {
                return false;
            }

            energy = Mathf.Min(PropsShield.maxEnergy, energy + spent / Mathf.Max(0.001f, PropsShield.selfBurnPerEnergy));
            return true;
        }

        private float MaxSelfBurnToSpend()
        {
            return Mathf.Max(0f, MingyuanUtility.DefaultSelfBurnEffectiveCap * Mathf.Clamp01(PropsShield.selfBurnRefillMaxFractionOfCap));
        }

        private bool TryRepairOneBodyPart()
        {
            if (Pawn?.health?.hediffSet == null || Pawn.Dead)
            {
                return false;
            }

            List<Hediff_MissingPart> missingParts = Pawn.health.hediffSet.GetMissingPartsCommonAncestors();
            if (!missingParts.NullOrEmpty())
            {
                for (int i = 0; i < missingParts.Count; i++)
                {
                    BodyPartRecord part = missingParts[i]?.Part;
                    if (part != null)
                    {
                        Pawn.health.RestorePart(part);
                        return true;
                    }
                }
            }

            BodyPartRecord bestPart = null;
            float bestSeverity = 0f;
            List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_Injury injury = hediffs[i] as Hediff_Injury;
                if (injury == null || injury.IsPermanent() || injury.Severity <= 0f)
                {
                    continue;
                }

                float total = TotalNonPermanentInjurySeverityOnPart(injury.Part);
                if (total > bestSeverity)
                {
                    bestSeverity = total;
                    bestPart = injury.Part;
                }
            }

            if (bestSeverity <= 0f)
            {
                return false;
            }

            bool healed = false;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                Hediff_Injury injury = hediffs[i] as Hediff_Injury;
                if (injury == null || injury.IsPermanent() || injury.Severity <= 0f || injury.Part != bestPart)
                {
                    continue;
                }

                injury.Heal(injury.Severity);
                healed = true;
            }

            return healed;
        }

        private float TotalNonPermanentInjurySeverityOnPart(BodyPartRecord part)
        {
            float total = 0f;
            List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_Injury injury = hediffs[i] as Hediff_Injury;
                if (injury != null && !injury.IsPermanent() && injury.Severity > 0f && injury.Part == part)
                {
                    total += injury.Severity;
                }
            }

            return total;
        }

        private void TrySpawnShieldSelfBurnMote()
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.MapHeld == null)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            if (tick < nextShieldSelfBurnMoteTick)
            {
                return;
            }

            ThingDef moteDef = PropsShield.shieldSelfBurnMoteDef ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_ShieldSelfBurnLink;
            if (MingyuanUtility.TryMakeAttachedMote(Pawn, moteDef, PropsShield.shieldSelfBurnMoteScale))
            {
                nextShieldSelfBurnMoteTick = tick + Mathf.Max(1, PropsShield.shieldSelfBurnMoteCooldownTicks);
            }
        }

        private static int TicksToSeconds(int ticks)
        {
            return Mathf.CeilToInt(Mathf.Max(0, ticks) / 60f);
        }

        private static string FormatNumber(float value)
        {
            return value >= 10f ? value.ToString("F0") : value.ToString("F1");
        }
    }

    public class HediffCompProperties_MingyuanRebirthFlame : HediffCompProperties
    {
        public HediffCompProperties_MingyuanRebirthFlame()
        {
            compClass = typeof(HediffComp_MingyuanRebirthFlame);
        }
    }

    public class HediffComp_MingyuanRebirthFlame : HediffComp
    {
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            MingyuanRebirthUtility.TryScheduleRebirth(Pawn);
        }
    }
}
