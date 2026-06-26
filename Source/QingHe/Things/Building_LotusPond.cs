using System.Collections.Generic;
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
                yield return new FloatMenuOption("打开花神庭（需要清荷本人）", null);
                yield break;
            }

            yield return new FloatMenuOption(
                "打开花神庭",
                delegate
                {
                    if (!interactor.CanReserveAndReach(clickedThing, PathEndMode.InteractionCell, Danger.Deadly))
                    {
                        Messages.Message("清荷现在无法接近荷池。", interactor, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(interactor);
                    FlowerCourtUtility.EnsureFlowerResources(interactor);
                    if (state == null)
                    {
                        Messages.Message("清荷尚未建立花神庭。", interactor, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Find.WindowStack.Add(new Dialog_QH_SkillTree(interactor, state, FlowerCourtUtility.EnsureFlowerChoices(interactor)));
                });

            yield return new FloatMenuOption(
                "在荷池旁冥想",
                delegate
                {
                    HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(interactor);
                    if (state == null)
                    {
                        Messages.Message("清荷尚未建立花神庭。", interactor, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    if (!interactor.CanReserveAndReach(clickedThing, PathEndMode.InteractionCell, Danger.Deadly))
                    {
                        Messages.Message("清荷现在无法接近荷池。", interactor, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Verse.AI.Job job = JobMaker.MakeJob(MX_QHDefOf.MX_QH_MeditateAtFlowerCourt, clickedThing);
                    interactor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
        }
    }
}
