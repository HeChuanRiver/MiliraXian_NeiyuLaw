using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public class HediffCompProperties_MingyuanLifeBurn : HediffCompProperties
    {
        public int tickInterval = 120;
        public float baseDamage = 2f;
        public float damagePer100Layers = 1f;
        public float needDrainPer100Layers = 0.01f;
        public float ageTicksPerLayer = 60f;
        public float transferRadius = 30f;
        public float transferFraction = 0.5f;
        public float executeHealthScaleMultiplier = 100f;

        public HediffCompProperties_MingyuanLifeBurn()
        {
            compClass = typeof(HediffComp_MingyuanLifeBurn);
        }
    }

    public class HediffComp_MingyuanLifeBurn : HediffComp
    {
        private Pawn instigator;
        private int ticksToNextDamage;

        public HediffCompProperties_MingyuanLifeBurn PropsLifeBurn => (HediffCompProperties_MingyuanLifeBurn)props;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref instigator, "instigator", false);
            Scribe_Values.Look(ref ticksToNextDamage, "ticksToNextDamage", 0);
        }

        public void SetInstigator(Pawn pawn)
        {
            if (pawn != null)
            {
                instigator = pawn;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

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
            float layers = Mathf.Max(0f, parent.Severity);
            if (layers <= 0f)
            {
                Pawn.health.RemoveHediff(parent);
                return;
            }

            DrainNeeds(layers);
            AgePawn(layers);
            DamageEquipment(layers);

            float damage = PropsLifeBurn.baseDamage + (layers / 100f) * PropsLifeBurn.damagePer100Layers;
            MingyuanUtility.ApplyTrueDamage(Pawn, DamageDefOf.Burn, damage, instigator);

            if (!Pawn.Dead && layers >= Pawn.HealthScale * PropsLifeBurn.executeHealthScaleMultiplier)
            {
                DamageInfo dinfo = new DamageInfo(DamageDefOf.Burn, 99999f, 999f, -1f, instigator);
                dinfo.SetIgnoreArmor(true);
                dinfo.SetIgnoreInstantKillProtection(true);
                dinfo.SetApplyAllDamage(true);
                Pawn.Kill(dinfo);
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
            int hitPointLoss = Mathf.Max(1, Mathf.RoundToInt(layers / 100f));
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

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            if (Pawn?.MapHeld == null || parent.Severity <= 0f)
            {
                return;
            }

            float transferred = parent.Severity * PropsLifeBurn.transferFraction;
            if (transferred <= 0f)
            {
                return;
            }

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(Pawn.PositionHeld, Pawn.MapHeld, PropsLifeBurn.transferRadius, true))
            {
                Pawn target;
                if (MingyuanUtility.IsHostilePawn(thing, instigator, out target))
                {
                    MingyuanUtility.AddLifeBurn(target, instigator, transferred);
                }
            }
        }
    }

    public class HediffCompProperties_MingyuanSelfBurn : HediffCompProperties
    {
        public int decayIntervalTicks = 120;
        public float decayLayers = 10f;

        public HediffCompProperties_MingyuanSelfBurn()
        {
            compClass = typeof(HediffComp_MingyuanSelfBurn);
        }
    }

    public class HediffComp_MingyuanSelfBurn : HediffComp
    {
        private int ticksToDecay;

        public HediffCompProperties_MingyuanSelfBurn PropsSelfBurn => (HediffCompProperties_MingyuanSelfBurn)props;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksToDecay, "ticksToDecay", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            ticksToDecay--;
            if (ticksToDecay > 0)
            {
                return;
            }

            ticksToDecay = Mathf.Max(1, PropsSelfBurn.decayIntervalTicks);
            parent.Severity = Mathf.Max(0f, parent.Severity - PropsSelfBurn.decayLayers);
            if (parent.Severity <= 0f)
            {
                Pawn.health.RemoveHediff(parent);
            }
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
        public float maxEnergy = 200f;
        public float regenPerSecond = 2f;
        public float lowIgnoreDamage = 20f;
        public float highIgnoreDamage = 100f;
        public int breakRecoverTicks = 480;
        public float hitEnergyCost = 1f;
        public float selfBurnNoCostThreshold = 300f;
        public float selfBurnOnNoCostHit = 10f;

        public HediffCompProperties_MingyuanProtectiveFlameShield()
        {
            compClass = typeof(HediffComp_MingyuanProtectiveFlameShield);
        }
    }

    public class HediffComp_MingyuanProtectiveFlameShield : HediffComp
    {
        private float energy = -1f;
        private int brokenUntilTick;

        public HediffCompProperties_MingyuanProtectiveFlameShield PropsShield => (HediffCompProperties_MingyuanProtectiveFlameShield)props;

        public bool Broken => Find.TickManager != null && Find.TickManager.TicksGame < brokenUntilTick;
        public float Energy => energy < 0f ? PropsShield.maxEnergy : energy;

        public override string CompLabelInBracketsExtra => Mathf.RoundToInt(Energy) + "/" + Mathf.RoundToInt(PropsShield.maxEnergy);

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref energy, "energy", -1f);
            Scribe_Values.Look(ref brokenUntilTick, "brokenUntilTick", 0);
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

            int tick = Find.TickManager.TicksGame;
            if (Broken)
            {
                return;
            }

            if (brokenUntilTick > 0 && tick >= brokenUntilTick)
            {
                energy = PropsShield.maxEnergy;
                brokenUntilTick = 0;
            }

            float regen = PropsShield.regenPerSecond / 60f;
            float selfBurnFactor = 1f + MingyuanUtility.GetSelfBurnLayers(Pawn) * 0.02f;
            energy = Mathf.Min(PropsShield.maxEnergy, energy + regen * selfBurnFactor);
        }

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
            if (energy > 0f && !Broken)
            {
                brokenUntilTick = 0;
            }
        }

        public bool TryAbsorb(ref DamageInfo dinfo, ref bool absorbed)
        {
            if (absorbed || Pawn == null || Pawn.Dead)
            {
                return false;
            }

            if (energy < 0f)
            {
                energy = PropsShield.maxEnergy;
            }

            if (Broken)
            {
                return false;
            }

            bool shouldAbsorb = dinfo.Amount <= PropsShield.lowIgnoreDamage
                                || dinfo.Amount >= PropsShield.highIgnoreDamage
                                || IsPotentiallyLethal(dinfo);
            if (!shouldAbsorb)
            {
                return false;
            }

            bool noCost = MingyuanUtility.GetSelfBurnLayers(Pawn) >= PropsShield.selfBurnNoCostThreshold;
            if (!noCost)
            {
                energy = Mathf.Max(0f, energy - PropsShield.hitEnergyCost);
            }
            else
            {
                MingyuanUtility.AddSelfBurn(Pawn, PropsShield.selfBurnOnNoCostHit);
            }

            MingyuanUtility.ClearControlStates(Pawn);
            absorbed = true;

            if (energy <= 0f && !noCost)
            {
                brokenUntilTick = Find.TickManager.TicksGame + Mathf.Max(1, PropsShield.breakRecoverTicks);
            }

            return true;
        }

        private bool IsPotentiallyLethal(DamageInfo dinfo)
        {
            return Pawn != null && dinfo.Amount >= Pawn.health.LethalDamageThreshold;
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
