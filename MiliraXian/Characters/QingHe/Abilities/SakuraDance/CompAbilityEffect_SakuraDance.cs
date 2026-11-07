using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>Starts the Sakura Dance job toward the aimed direction; the job runs the timeline.</summary>
    public class CompAbilityEffect_SakuraDance : CompAbilityEffect
    {
        private static readonly Color PreviewColor = new(1f, 0.62f, 0.75f, 0.6f);

        public new CompProperties_AbilitySakuraDance Props => (CompProperties_AbilitySakuraDance)props;

        private bool LacksPressure =>
            Props.swordPressureCost > 0f
            && (Hediffs.MX_QH_HediffUtility.GetSwordPressure(parent.pawn)?.CurrentValue ?? 0f) < Props.swordPressureCost - 1e-4f;

        public override bool GizmoDisabled(out string reason)
        {
            if (LacksPressure)
            {
                reason = "MX_QH_SwordPressureNotEnough".Translate(Props.swordPressureCost.ToString("0.#"));
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            return caster != null
                && target.IsValid
                && target.Cell != caster.Position
                && base.Valid(target, throwMessages);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || Props.cuts.NullOrEmpty())
            {
                return;
            }
            float r = 0f;
            foreach (SakuraCutProps cut in Props.cuts)
            {
                r = Mathf.Max(r, cut.radius);
            }
            GenDraw.DrawRadiusRing(caster.Position, r, PreviewColor);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            if (caster?.jobs == null || MX_QHDefOf.MX_QH_SakuraDance == null || !target.IsValid || target.Cell == caster.Position)
            {
                return;
            }

            caster.rotationTracker?.FaceCell(target.Cell);
            Job job = JobMaker.MakeJob(MX_QHDefOf.MX_QH_SakuraDance);
            job.ability = parent;
            job.SetTarget(TargetIndex.A, target.Cell);
            job.playerForced = true;
            if (SakuraThunderReturn.Casting)
            {
                job.count = SakuraThunderReturn.JobMarker;
            }
            caster.jobs.StartJob(job, JobCondition.InterruptForced, cancelBusyStances: false, tag: JobTag.Misc);
        }
    }
}
