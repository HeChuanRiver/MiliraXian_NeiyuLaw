using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Rituals
{
    public class Precept_QixiRitual : Precept_Ritual
    {
        public static Precept_Ritual EnsureFor(Ideo ideo)
        {
            if (ideo == null || MX_QHDefOf.MX_QH_QixiRitual == null)
            {
                return null;
            }

            Precept_Ritual ritual = ideo.GetPrecept(MX_QHDefOf.MX_QH_QixiRitual) as Precept_Ritual;
            if (ritual == null)
            {
                ritual = PreceptMaker.MakePrecept(MX_QHDefOf.MX_QH_QixiRitual) as Precept_Ritual;
                ideo.AddPrecept(ritual, init: true, null, MX_QHDefOf.MX_QH_QixiRitualPattern);
                return ritual;
            }

            EnsureFilled(ritual);
            return ritual;
        }

        private static void EnsureFilled(Precept_Ritual ritual)
        {
            RitualPatternDef pattern = MX_QHDefOf.MX_QH_QixiRitualPattern;
            if (ritual == null || pattern == null)
            {
                return;
            }

            if (ritual.sourcePattern == null
                || ritual.behavior == null
                || ritual.outcomeEffect == null
                || ritual.obligationTargetFilter == null)
            {
                pattern.Fill(ritual);
                ritual.RegenerateName();
            }
        }
    }

    public class RitualBehaviorWorker_Qixi : RitualBehaviorWorker
    {
        public RitualBehaviorWorker_Qixi()
        {
        }

        public RitualBehaviorWorker_Qixi(RitualBehaviorDef def)
            : base(def)
        {
        }

        public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null, Dictionary<string, Pawn> forcedForRole = null)
        {
            string reason = base.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);
            if (!reason.NullOrEmpty())
            {
                return reason;
            }

            return Current.Game?.GetComponent<GameComponent_QingheQixiRitual>()?.CooldownReason;
        }
    }

    public class GameComponent_QingheQixiRitual : GameComponent
    {
        private int nextAllowedTick;

        public string CooldownReason
        {
            get
            {
                int ticksLeft = nextAllowedTick - (Find.TickManager?.TicksGame ?? 0);
                return ticksLeft > 0 ? "MX_QH_QixiCooldown".Translate(ticksLeft.ToStringTicksToPeriod()) : null;
            }
        }

        public GameComponent_QingheQixiRitual(Game game)
        {
        }

        public void NotifyRitualCompleted()
        {
            nextAllowedTick = (Find.TickManager?.TicksGame ?? 0) + GenDate.TicksPerYear;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextAllowedTick, "mx_qh_qixi_nextAllowedTick", 0);
        }
    }

    public class RitualObligationTargetWorker_QixiLotusPond : RitualObligationTargetFilter
    {
        public RitualObligationTargetWorker_QixiLotusPond()
        {
        }

        public RitualObligationTargetWorker_QixiLotusPond(RitualObligationTargetFilterDef def)
            : base(def)
        {
        }

        public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation, Map map)
        {
            if (map == null || MX_QHDefOf.MX_QH_LotusPond == null)
            {
                yield break;
            }

            foreach (Thing thing in map.listerThings.ThingsOfDef(MX_QHDefOf.MX_QH_LotusPond))
            {
                if (CanUseTarget(thing, obligation).canUse)
                {
                    yield return thing;
                }
            }
        }

        protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target, RitualObligation obligation)
        {
            if (!target.HasThing || target.Thing.def != MX_QHDefOf.MX_QH_LotusPond)
            {
                return false;
            }

            if (target.Thing.Faction == null || !target.Thing.Faction.IsPlayer)
            {
                return false;
            }

            return true;
        }

        public override IEnumerable<string> GetTargetInfos(RitualObligation obligation)
        {
            yield return MX_QHDefOf.MX_QH_LotusPond?.label ?? "MX_QH_LotusPond".Translate();
        }
    }

    public class RitualRole_Qinghe : RitualRoleColonist
    {
        public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            if (!base.AppliesToPawn(p, out reason, selectedTarget, ritual, assignments, precept, skipReason))
            {
                return false;
            }

            if (MX_QHCharacterUtility.IsQinghe(p))
            {
                return true;
            }

            if (!skipReason)
            {
                reason = "MX_QH_QixiRequiresQinghe".Translate();
            }

            return false;
        }
    }

    public class RitualOutcomeEffectWorker_Qixi : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_Qixi()
        {
        }

        public RitualOutcomeEffectWorker_Qixi(RitualOutcomeEffectDef def)
            : base(def)
        {
        }

        protected override void ApplyExtraOutcome(Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual, RitualOutcomePossibility outcome, out string extraOutcomeDesc, ref LookTargets letterLookTargets)
        {
            Current.Game?.GetComponent<GameComponent_QingheQixiRitual>()?.NotifyRitualCompleted();
            int fragmentCount = SpawnFragments(jobRitual, ref letterLookTargets);
            List<Pawn> inspiredPawns = outcome.Positive ? GiveInspirations(totalPresence, outcome.BestPositiveOutcome(jobRitual) ? 2 : 1) : new List<Pawn>();

            extraOutcomeDesc = "MX_QH_QixiOutcomeFragments".Translate(fragmentCount);
            if (inspiredPawns.Count > 0)
            {
                extraOutcomeDesc += "\n" + "MX_QH_QixiOutcomeInspirations".Translate(inspiredPawns.Select(pawn => pawn.LabelShortCap).ToCommaList());
            }
        }

        private int SpawnFragments(LordJob_Ritual jobRitual, ref LookTargets letterLookTargets)
        {
            if (jobRitual?.Map == null || MX_QHDefOf.MX_QH_LostMusicScoreFragment == null)
            {
                return 0;
            }

            int count = Rand.RangeInclusive(2, 4);
            Thing fragments = ThingMaker.MakeThing(MX_QHDefOf.MX_QH_LostMusicScoreFragment);
            fragments.stackCount = count;
            GenPlace.TryPlaceThing(fragments, jobRitual.selectedTarget.Cell, jobRitual.Map, ThingPlaceMode.Near);
            if (fragments.Spawned)
            {
                letterLookTargets = fragments;
            }

            return count;
        }

        private List<Pawn> GiveInspirations(Dictionary<Pawn, int> totalPresence, int count)
        {
            List<Pawn> candidates = totalPresence.Keys
                .Where(pawn => pawn?.mindState?.inspirationHandler != null && !pawn.Inspired)
                .InRandomOrder()
                .ToList();
            List<Pawn> inspiredPawns = new();

            for (int i = 0; i < candidates.Count && inspiredPawns.Count < count; i++)
            {
                Pawn pawn = candidates[i];
                if (TryStartQixiInspiration(pawn))
                {
                    inspiredPawns.Add(pawn);
                }
            }

            return inspiredPawns;
        }

        private bool TryStartQixiInspiration(Pawn pawn)
        {
            InspirationDef first = Rand.Bool ? MX_QHDefOf.Inspired_Creativity : MX_QHDefOf.Frenzy_Work;
            InspirationDef second = first == MX_QHDefOf.Inspired_Creativity ? MX_QHDefOf.Frenzy_Work : MX_QHDefOf.Inspired_Creativity;
            string reason = "MX_QH_QixiInspirationReason".Translate();

            return TryStartInspiration(pawn, first, reason) || TryStartInspiration(pawn, second, reason);
        }

        private bool TryStartInspiration(Pawn pawn, InspirationDef inspirationDef, string reason)
        {
            return inspirationDef != null
                && inspirationDef.Worker.InspirationCanOccur(pawn)
                && pawn.mindState.inspirationHandler.TryStartInspiration(inspirationDef, reason, sendLetter: false);
        }
    }
}
