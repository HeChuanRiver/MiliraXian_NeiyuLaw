using System.Collections.Generic;
using MiliraXian.Characters;
using System.Linq;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Rituals;
using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.QingHe.UI;
using RimWorld;
using UnityEngine;
using Verse.AI;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Buildings
{
    public class RoomRoleWorker_QingheLotusRainPavilion : RoomRoleWorker
    {
        private const float LotusPondScore = 3000f;

        public override float GetScore(Room room)
        {
            return HasLotusPond(room) ? LotusPondScore : 0f;
        }

        public override float GetScoreDeltaIfBuildingPlaced(Room room, ThingDef buildingDef)
        {
            if (room?.Role?.Worker is RoomRoleWorker_QingheLotusRainPavilion)
            {
                return 0f;
            }

            return buildingDef == MX_QHDefOf.MX_QH_LotusPond ? LotusPondScore : 0f;
        }

        private static bool HasLotusPond(Room room)
        {
            if (room == null || MX_QHDefOf.MX_QH_LotusPond == null)
            {
                return false;
            }

            List<Thing> things = room.ContainedAndAdjacentThings;
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i]?.def == MX_QHDefOf.MX_QH_LotusPond)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class PlaceWorker_QingheLotusPondDesign : PlaceWorker
    {
        public override bool IsBuildDesignatorVisible(BuildableDef def)
        {
            return Current.Game?.GetComponent<GameComponent_QingheFlowerCourtQuest>()?.LotusPondDesignUnlocked == true;
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (Current.Game?.GetComponent<GameComponent_QingheFlowerCourtQuest>()?.LotusPondDesignUnlocked == true)
            {
                return true;
            }

            return "MX_QH_FlowerCourtDesignLocked".Translate();
        }
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
                    .Where(MX_QHCharacterUtility.IsQinghe)
                    .OrderByDescending(pawn => CanAssignTo(pawn).Accepted);
            }
        }

        public override AcceptanceReport CanAssignTo(Pawn pawn)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn))
            {
                return "MX_QH_LotusPondAssignQingheOnly".Translate();
            }

            return base.CanAssignTo(pawn);
        }

        public override void TryAssignPawn(Pawn pawn)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn))
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
            if (clickedThing?.def != MX_QHDefOf.MX_QH_LotusPond)
            {
                yield break;
            }

            Pawn interactor = context.FirstSelectedPawn;
            if (interactor == null || interactor.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (!MX_QHCharacterUtility.IsQinghe(interactor))
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

                    HediffComp_SkillTreeState state = MX_QH_HediffUtility.EnsureFlowerResonance(interactor);
                    MX_QH_HediffUtility.EnsureFlowerDecree(interactor);
                    if (state == null)
                    {
                        Messages.Message("MX_QH_FlowerCourtMissing".Translate(), interactor, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Find.WindowStack.Add(new Dialog_QH_SkillTree(interactor, state));
                });

            if (ModsConfig.IdeologyActive)
            {
                Precept_Ritual qixiRitual = Precept_QixiRitual.EnsureFor(Faction.OfPlayer?.ideos?.PrimaryIdeo ?? interactor.Ideo);
                if (qixiRitual != null)
                {
                    string reason = qixiRitual.behavior?.CanStartRitualNow(clickedThing, qixiRitual, interactor);
                    RitualTargetUseReport targetReport = qixiRitual.CanUseTarget(clickedThing, null);
                    if (!targetReport.failReason.NullOrEmpty())
                    {
                        reason = targetReport.failReason;
                    }

                    System.Action action = null;
                    if (reason.NullOrEmpty())
                    {
                        action = delegate { qixiRitual.ShowRitualBeginWindow(clickedThing, null, interactor); };
                    }

                    yield return new FloatMenuOption(
                        reason.NullOrEmpty()
                            ? qixiRitual.GetBeginRitualText()
                            : qixiRitual.GetBeginRitualText() + " (" + reason + ")",
                        action,
                        qixiRitual.Icon,
                        Color.white);
                }
            }

            HediffComp_QingheCombatState combatState = MX_QH_HediffUtility.EnsureCombatState(interactor);

            foreach (FlowerBellResonance resonance in System.Enum.GetValues(typeof(FlowerBellResonance)))
            {
                FlowerBellResonance targetResonance = resonance;
                string label = "MX_QH_TuneFlowerBellOption".Translate(CompFlowerBellResonance.LabelFor(targetResonance));
                if (combatState?.Resonance == targetResonance)
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

                    Job job = JobMaker.MakeJob(MX_QHDefOf.MX_QH_TuneBell, clickedThing);
                    job.count = (int)targetResonance;
                    interactor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
        }
    }
}


