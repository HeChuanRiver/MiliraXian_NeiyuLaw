using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public class Thing_MingyuanBurningPillarField : ThingWithComps
    {
    }

    public class Thing_MingyuanAshesField : ThingWithComps
    {
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

        public CompProperties_MingyuanBurningField PropsField => (CompProperties_MingyuanBurningField)props;

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

        private void Pulse()
        {
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, PropsField.radius, true))
            {
                if (thing == parent || thing.Destroyed)
                {
                    continue;
                }

                Pawn pawn = thing as Pawn;
                if (pawn != null)
                {
                    HandlePawn(pawn);
                    continue;
                }

                if (PropsField.destroyBuildings && thing.def.category == ThingCategory.Building && thing.Spawned)
                {
                    thing.Destroy(DestroyMode.Deconstruct);
                }
            }
        }

        private void HandlePawn(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn == caster)
            {
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

            float selfBurn = PropsField.scalesWithSelfBurn ? MingyuanUtility.GetSelfBurnLayers(caster) : 0f;
            float damage = PropsField.damageAmount * (1f + selfBurn * PropsField.selfBurnDamagePerLayer);
            float layers = PropsField.lifeBurnLayers + (selfBurn / 100f) * PropsField.selfBurnLifeBurnPer100;

            MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, damage, caster);
            MingyuanUtility.AddLifeBurn(pawn, caster, layers);
            if (PropsField.scalesWithSelfBurn)
            {
                MingyuanUtility.AddSelfBurn(caster, Mathf.Max(1f, layers / 20f));
            }
        }
    }
}
