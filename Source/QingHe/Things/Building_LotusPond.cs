using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.UI;
using RimWorld;
using Verse.AI;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    public class Building_LotusPond : Building
    {
    }

    public class CompAssignableToPawn_QingheMeditationSpot : CompAssignableToPawn_MeditationSpot
    {
        public override IEnumerable<Pawn> AssigningCandidates
        {
            get
            {
                if (!parent.Spawned)
                {
                    return Enumerable.Empty<Pawn>();
                }

                return parent.Map.mapPawns.FreeColonists
                    .Where(MX_QHUtility.IsQinghe)
                    .OrderByDescending(pawn => CanAssignTo(pawn).Accepted);
            }
        }

        public override AcceptanceReport CanAssignTo(Pawn pawn)
        {
            if (!MX_QHUtility.IsQinghe(pawn))
            {
                return "MX_QH_LotusPondAssignQingheOnly".Translate();
            }

            return base.CanAssignTo(pawn);
        }

        public override void TryAssignPawn(Pawn pawn)
        {
            if (!MX_QHUtility.IsQinghe(pawn))
            {
                return;
            }

            base.TryAssignPawn(pawn);
        }
    }

    public class FloatMenuOptionProvider_LotusPondInteraction : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => false;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!(clickedThing is Building_LotusPond))
            {
                yield break;
            }

            Pawn interactor = context.FirstSelectedPawn;
            if (interactor == null || interactor.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (!MX_QHUtility.IsQinghe(interactor))
            {
                yield return new FloatMenuOption("MX_QH_OpenFlowerCourtRequiresQinghe".Translate(), null);
                yield break;
            }

            yield return new FloatMenuOption(
                "MX_QH_OpenFlowerCourt".Translate(),
                delegate
                {
                    if (!interactor.CanReserveAndReach(clickedThing, PathEndMode.InteractionCell, Danger.Deadly))
                    {
                        Messages.Message("MX_QH_LotusPondCannotReach".Translate(), interactor, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(interactor);
                    FlowerCourtUtility.EnsureFlowerResources(interactor);
                    if (state == null)
                    {
                        Messages.Message("MX_QH_FlowerCourtMissing".Translate(), interactor, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Find.WindowStack.Add(new Dialog_QH_SkillTree(interactor, state));
                });

            ThingWithComps flowerBell = interactor.equipment?.Primary;
            CompFlowerBellResonance resonanceComp = flowerBell?.TryGetComp<CompFlowerBellResonance>();
            if (resonanceComp == null)
            {
                yield return new FloatMenuOption("MX_QH_TuneFlowerBellRequiresWeapon".Translate(), null);
                yield break;
            }

            foreach (FlowerBellResonance resonance in System.Enum.GetValues(typeof(FlowerBellResonance)))
            {
                FlowerBellResonance targetResonance = resonance;
                string label = "MX_QH_TuneFlowerBellOption".Translate(CompFlowerBellResonance.LabelFor(targetResonance));
                if (resonanceComp.Resonance == targetResonance)
                {
                    yield return new FloatMenuOption(label + "MX_QH_CurrentSuffix".Translate(), null);
                    continue;
                }

                yield return new FloatMenuOption(label, delegate
                {
                    if (!interactor.CanReserveAndReach(clickedThing, PathEndMode.InteractionCell, Danger.Deadly))
                    {
                        Messages.Message("MX_QH_LotusPondCannotReach".Translate(), interactor, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Job job = JobMaker.MakeJob(MX_QHDefOf.MX_QH_TuneBell, clickedThing, flowerBell);
                    job.count = (int)targetResonance;
                    interactor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
        }
    }
}
