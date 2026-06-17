using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_QingheMusicScore : CompProperties
    {
        public QingheMusicScoreDef score;

        public CompProperties_QingheMusicScore()
        {
            compClass = typeof(Comp_QingheMusicScore);
        }
    }

    public class Comp_QingheMusicScore : ThingComp
    {
        public CompProperties_QingheMusicScore Props => (CompProperties_QingheMusicScore)props;

        public QingheMusicScoreDef ScoreDef => Props?.score;

        public QingheSkillTreeDef UnlocksTree => ScoreDef?.unlocksTree;

        public float ExperienceGain => ScoreDef?.experienceGain ?? 0f;

        public string UnlocksTreeLabel => UnlocksTree?.LabelCap ?? parent.LabelNoCount;

        public bool CanStudy(Pawn pawn, out string disabledReason)
        {
            if (!MX_QHUtility.IsQinghe(pawn))
            {
                disabledReason = "需要清荷本人研读。";
                return false;
            }

            HediffComp_QingheSkillTreeState state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            if (state == null)
            {
                disabledReason = "清荷尚未建立花神庭。";
                return false;
            }

            if (UnlocksTree == null)
            {
                disabledReason = "曲谱数据缺失。";
                return false;
            }

            if (state.IsTreeUnlocked(UnlocksTree))
            {
                disabledReason = "清荷已经读过这份曲谱。";
                return false;
            }

            disabledReason = null;
            return true;
        }
    }

    public class FloatMenuOptionProvider_QingheMusicScore : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => true;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing, FloatMenuContext context)
        {
            Comp_QingheMusicScore scoreComp = clickedThing?.TryGetComp<Comp_QingheMusicScore>();
            if (scoreComp == null)
            {
                yield break;
            }

            Pawn pawn = context.FirstSelectedPawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            string label = "带到荷池研读《" + clickedThing.LabelNoCount + "》";
            if (!scoreComp.CanStudy(pawn, out string disabledReason))
            {
                yield return new FloatMenuOption(label + "（" + disabledReason + "）", null);
                yield break;
            }

            if (!TryFindReachableLotusPond(pawn, out Building_LotusPond lotusPond))
            {
                yield return new FloatMenuOption(label + "（需要可到达的荷池）", null);
                yield break;
            }

            if (!pawn.CanReserveAndReach(clickedThing, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                yield return new FloatMenuOption(label + "（无法接近曲谱）", null);
                yield break;
            }

            if (!pawn.CanReserveAndReach(lotusPond, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption(label + "（无法接近荷池）", null);
                yield break;
            }

            yield return new FloatMenuOption(label, delegate
            {
                Job job = JobMaker.MakeJob(MX_QHDefOf.MX_QH_StudyMusicScoreAtLotusPond, lotusPond, clickedThing, lotusPond.Position);
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });
        }

        private static bool TryFindReachableLotusPond(Pawn pawn, out Building_LotusPond lotusPond)
        {
            lotusPond = null;
            if (pawn?.Map == null || MX_QHDefOf.MX_QH_LotusPond == null)
            {
                return false;
            }

            foreach (Thing thing in pawn.Map.listerThings.ThingsOfDef(MX_QHDefOf.MX_QH_LotusPond))
            {
                Building_LotusPond candidate = thing as Building_LotusPond;
                if (candidate != null && pawn.CanReserveAndReach(candidate, PathEndMode.InteractionCell, Danger.Deadly))
                {
                    lotusPond = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
