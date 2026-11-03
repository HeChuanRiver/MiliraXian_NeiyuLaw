using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using MiliraXian.Characters.Common;

namespace MiliraXian.Characters.Mingyuan
{
    public class Thing_MingyuanAshesField : ThingWithComps
    {
        private CompMingyuanBurningField FieldComp => GetComp<CompMingyuanBurningField>();

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(FieldComp?.DrawLoc ?? drawLoc, flip);
        }
    }

    public class CompProperties_MingyuanBurningField : CompProperties
    {
        public float radius = 15f;
        public int durationTicks = 10000;
        public int pulseIntervalTicks = 15;
        public float damageAmount = 100f;
        public float lifeBurnLayers = 100f;
        public bool destroyBuildings;
        public bool destroyAnimals;
        public bool scalesWithSelfBurn;
        public float selfBurnLifeBurnPer100 = 20f;
        public float selfHealAmount = 1f;
        public float maxSelfBurnGainPerPulse = 20f;
        public Color previewRingColor = new(1f, 0.75f, 0.34f, 0.64f);

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

            if (MingyuanPowerBalance.Sealed || Find.TickManager.TicksGame >= expireTick || caster == null || caster.Destroyed || caster.Dead)
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
            selfBurnGainedThisPulse = 0f;
            Map map = parent.Map;
            List<Thing> targets = CombatTargetSnapshot.Rent(GenRadial.RadialDistinctThingsAround(CenterCell, map, PropsField.radius, true));
            try
            {
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    Thing thing = targets[targetIndex];
                    if (thing == parent || thing.Destroyed || !thing.Spawned || thing.Map != map)
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
            finally
            {
                CombatTargetSnapshot.Return(targets);
            }
        }

        private void HandlePawn(Pawn pawn)
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
                DamageInfo killInfo = new(DamageDefOf.Burn, 99999f, 999f, -1f, caster);
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
            float layers = PropsField.lifeBurnLayers + (selfBurn / 100f) * PropsField.selfBurnLifeBurnPer100 * MingyuanUtility.SelfBurnBonusScale;

            MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, damage, caster);
            MingyuanUtility.AddLifeBurn(pawn, caster, layers, scaleWithOverburn: true);
            if (PropsField.scalesWithSelfBurn)
            {
                float gain = Mathf.Max(1f, layers / 20f);
                float remainingGain = Mathf.Max(0f, PropsField.maxSelfBurnGainPerPulse - selfBurnGainedThisPulse);
                if (remainingGain > 0f)
                {
                    float appliedGain = Mathf.Min(gain, remainingGain);
                    MingyuanUtility.AddSelfBurn(caster, appliedGain, showMote: false);
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

            if (!MingyuanPowerBalance.Sealed) MingyuanUtility.HealInjuriesIncludingScars(caster, Mathf.Max(0f, PropsField.selfHealAmount));
        }
    }
}
