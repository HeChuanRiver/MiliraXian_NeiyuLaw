using RimWorld;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Mingyuan
{
    public class CompProperties_AbilityMingyuanAshesOfSelf : CompProperties_AbilityEffect
    {
        public ThingDef fieldDef;
        public float selfBurnLayers = 150f;
        public float bloodLossCost = 0.15f;
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

        public override bool GizmoDisabled(out string reason)
        {
            if (base.GizmoDisabled(out reason)) return true;
            if (CanPay(parent.pawn)) return false;
            reason = "MX_Mingyuan_AshesOfSelf_InsufficientCost".Translate();
            return true;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) =>
            base.CanApplyOn(target, dest) && CanPay(parent.pawn);

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster?.MapHeld == null)
            {
                return;
            }

            if (Props.fieldDef?.GetCompProperties<CompProperties_MingyuanBurningField>() is { } field)
                GenDraw.DrawRadiusRing(caster.Position, field.radius, field.previewRingColor);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = parent.pawn;
            if (MingyuanPowerBalance.Sealed || caster?.Spawned != true || caster.Dead
                || Props.fieldDef == null || !CanPay(caster)) return;

            base.Apply(target, dest);
            ThingWithComps field = (ThingWithComps)ThingMaker.MakeThing(Props.fieldDef);
            field.GetComp<CompMingyuanBurningField>().Init(caster);
            GenSpawn.Spawn(field, caster.Position, caster.Map);

            HealthUtility.AdjustSeverity(caster, HediffDefOf.BloodLoss, Props.bloodLossCost);
            MingyuanUtility.AddSelfBurn(caster, Props.selfBurnLayers);
            PlayCastVisuals(caster);
        }

        private bool CanPay(Pawn caster) =>
            caster?.health != null && !caster.Dead && caster.RaceProps.IsFlesh
            && MingyuanSurvival.BloodLoss(caster) + Props.bloodLossCost < MingyuanSurvival.DeathBloodLoss;

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
