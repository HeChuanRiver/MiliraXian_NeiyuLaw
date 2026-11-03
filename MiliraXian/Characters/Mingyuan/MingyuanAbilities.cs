using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using MiliraXian.Characters.Common;

namespace MiliraXian.Characters.Mingyuan
{
    public class CompProperties_AbilityMingyuanTimeBurn : CompProperties_AbilityEffect
    {
        public int durationTicks = MingyuanUtility.TicksPerHour;
        public int tickIntervalTicks = 60;
        public ThingDef startMoteDef;
        public ThingDef collapseMoteDef;
        public SoundDef effectSoundDef;
        public float startMoteScale = 2.2f;
        public float collapseMoteScale = 2.4f;
        public int mechSteelCount = 75;
        public int mechPlasteelCount = 25;

        public CompProperties_AbilityMingyuanTimeBurn()
        {
            compClass = typeof(CompAbilityEffect_MingyuanTimeBurn);
        }
    }

    public class CompAbilityEffect_MingyuanTimeBurn : CompAbilityEffect_MingyuanPowerLimited
    {
        public new CompProperties_AbilityMingyuanTimeBurn Props => (CompProperties_AbilityMingyuanTimeBurn)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (MingyuanPowerBalance.Sealed) return;
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null)
            {
                return;
            }

            Pawn targetPawn = target.Pawn;
            if (targetPawn != null && !targetPawn.Dead)
            {
                MingyuanTimeBurnUtility.PlayEffectSound(Props.effectSoundDef, targetPawn.PositionHeld, targetPawn.MapHeld);
                MingyuanTimeBurnUtility.TryMakeStaticMote(targetPawn.PositionHeld, targetPawn.MapHeld, Props.startMoteDef, Props.startMoteScale);
                MingyuanTimeBurnUtility.Register(targetPawn, caster, Props);
                return;
            }

            Thing targetThing = target.Thing;
            if (targetThing != null && targetThing.def.category == ThingCategory.Building)
            {
                MingyuanTimeBurnUtility.DissolveBuilding(targetThing, caster, Props);
            }
        }
    }

    public class CompProperties_AbilityMingyuanAshesOfSelf : CompProperties_AbilityEffect
    {
        public ThingDef fieldDef;
        public float selfBurnLayers = 100f;
        public float bloodLossCost = 0.5f;
        public int fieldDurationTicks = 900;
        public float fieldPreviewRadius = 2.4f;
        public ThingDef castMoteDef;
        public float castMoteScale = 1f;
        public SoundDef effectSoundDef;

        public CompProperties_AbilityMingyuanAshesOfSelf()
        {
            compClass = typeof(CompAbilityEffect_MingyuanAshesOfSelf);
        }
    }

    public class CompAbilityEffect_MingyuanAshesOfSelf : CompAbilityEffect_MingyuanPowerLimited
    {
        public new CompProperties_AbilityMingyuanAshesOfSelf Props => (CompProperties_AbilityMingyuanAshesOfSelf)props;

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster?.MapHeld == null)
            {
                return;
            }

            GenDraw.DrawRadiusRing(caster.Position, Mathf.Max(0.1f, Props.fieldPreviewRadius), new Color(1f, 0.74f, 0.34f, 0.72f));
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (MingyuanPowerBalance.Sealed) return;
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null || !caster.Spawned)
            {
                return;
            }

            if (!ConsumeCost(caster))
            {
                Messages.Message("MX_Mingyuan_AshesOfSelf_InsufficientCost".Translate(), caster, MessageTypeDefOf.RejectInput, false);
                return;
            }

            MingyuanUtility.AddSelfBurn(caster, Props.selfBurnLayers);
            PlayCastVisuals(caster);

            if (Props.fieldDef == null)
            {
                return;
            }

            Thing field = ThingMaker.MakeThing(Props.fieldDef);
            GenSpawn.Spawn(field, caster.Position, caster.Map);
            field.TryGetComp<CompMingyuanBurningField>()?.Init(caster, Props.fieldDurationTicks);
        }

        private bool ConsumeCost(Pawn caster)
        {
            if (caster.RaceProps?.IsFlesh != true || caster.WouldDieFromAdditionalBloodLoss(Props.bloodLossCost))
            {
                return false;
            }

            HealthUtility.AdjustSeverity(caster, HediffDefOf.BloodLoss, Mathf.Max(0f, Props.bloodLossCost));
            return true;
        }

        private void PlayCastVisuals(Pawn caster)
        {
            if (caster?.Map == null)
            {
                return;
            }

            Props.effectSoundDef?.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
            ThingDef moteDef = Props.castMoteDef ?? MX_MingyuanDefOf.MX_Mingyuan_Mote_AshesCast;
            MingyuanUtility.TryMakeStaticMote(caster.Position, caster.Map, moteDef, Props.castMoteScale);
        }
    }
}
