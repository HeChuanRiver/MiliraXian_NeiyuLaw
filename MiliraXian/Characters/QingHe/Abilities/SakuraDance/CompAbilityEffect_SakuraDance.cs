using System.Collections.Generic;
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
        private readonly List<IntVec3> previewCells = new();

        public new CompProperties_AbilitySakuraDance Props => (CompProperties_AbilitySakuraDance)props;

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
            if (caster == null || !caster.Spawned || Props.Cuts.NullOrEmpty())
            {
                return;
            }
            float r = 0f;
            foreach (SakuraCutProps cut in Props.Cuts)
            {
                r = Mathf.Max(r, cut.radius);
            }
            previewCells.Clear();
            previewCells.AddRange(GenRadial.RadialCellsAround(caster.Position, r, useCenter: true));
            GenDraw.DrawFieldEdges(previewCells, PreviewColor);
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
