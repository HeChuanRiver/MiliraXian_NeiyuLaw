using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_QingheMusicScore : CompProperties
    {
        public QingheMusicScoreDef score;
        public List<QingheMusicScoreDef> possibleScores;
        public ThingDef plainBookDef;
        public int requiredReadingTicks = 5000;

        public CompProperties_QingheMusicScore()
        {
            compClass = typeof(Comp_QingheMusicScore);
        }
    }

    public class Comp_QingheMusicScore : ThingComp
    {
        private QingheMusicScoreDef score;
        private float readingProgress;
        private bool consumed;

        public CompProperties_QingheMusicScore Props => (CompProperties_QingheMusicScore)props;

        public QingheMusicScoreDef ScoreDef => score ?? Props.score;

        public IReadOnlyList<MX_QHSkillNodeDef> UnlocksNodes => ScoreDef?.unlocksNodes;

        public string UnlocksNodeLabel => ScoreDef?.LabelCap ?? parent.def.LabelCap;

        public string BookTitle => ScoreDef != null ? ScoreDef.LabelCap.ToString() : parent.def.LabelCap.ToString();

        public string BookContent => ScoreDef?.description ?? parent.def.description;

        public bool HasUnlockNodes => UnlocksNodes != null && UnlocksNodes.Count > 0;

        public bool IsMasteryScore => ScoreDef != null && ScoreDef.masteryGain > 0;

        public bool Consumed => consumed;

        public float ReadingProgress => readingProgress;

        public int RequiredReadingTicks => Mathf.Max(1, ScoreDef != null && ScoreDef.requiredReadingTicks > 0 ? ScoreDef.requiredReadingTicks : Props.requiredReadingTicks);

        public float ReadingProgressPercent => Mathf.Clamp01(readingProgress / RequiredReadingTicks);

        public ThingDef PlainBookDef => Props.plainBookDef ?? ThingDefOf.TextBook;

        public override void PostPostMake()
        {
            base.PostPostMake();
            EnsureInitialized();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref score, "mx_qh_score");
            Scribe_Values.Look(ref readingProgress, "mx_qh_readingProgress", 0f);
            Scribe_Values.Look(ref consumed, "mx_qh_consumed", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureInitialized();
            }
        }

        public void Initialize(QingheMusicScoreDef newScore)
        {
            score = newScore;
            readingProgress = 0f;
            consumed = false;
        }

        public void EnsureInitialized()
        {
            if (ScoreDef != null)
            {
                return;
            }

            List<QingheMusicScoreDef> candidates = Props.possibleScores;
            if (candidates == null || candidates.Count == 0)
            {
                candidates = DefDatabase<QingheMusicScoreDef>.AllDefsListForReading;
            }

            Initialize(candidates.Where(def => def != null).RandomElementWithFallback());
        }

        public bool CanStudy(Pawn pawn, out string disabledReason)
        {
            EnsureInitialized();
            if (consumed)
            {
                disabledReason = "MX_QH_SkillBookConsumed".Translate();
                return false;
            }

            if (!MX_QHCharacterUtility.IsQinghe(pawn))
            {
                disabledReason = "MX_QH_SkillBookRequiresQinghe".Translate();
                return false;
            }

            if (!BookUtility.CanReadEver(pawn))
            {
                disabledReason = "MX_QH_SkillBookCannotReadNow".Translate();
                return false;
            }

            HediffComp_FlowerResonance state = MX_QH_HediffUtility.EnsureFlowerResonance(pawn);
            if (state == null)
            {
                disabledReason = "MX_QH_FlowerCourtMissing".Translate();
                return false;
            }

            if (!HasUnlockNodes && !IsMasteryScore)
            {
                disabledReason = "MX_QH_SkillBookDataMissing".Translate();
                return false;
            }

            if (IsMasteryScore)
            {
                if (state.MusicMasteryLevel >= state.MaxMusicMasteryLevel)
                {
                    disabledReason = "MX_QH_MusicMasteryMaxed".Translate();
                    return false;
                }

                disabledReason = null;
                return true;
            }

            if (UnlocksNodes.All(node => node == null || state.HasNode(node)))
            {
                disabledReason = "MX_QH_SkillBookAlreadyRead".Translate();
                return false;
            }

            disabledReason = null;
            return true;
        }

        public void AddReadingProgress(Pawn pawn, int delta)
        {
            if (!CanStudy(pawn, out _))
            {
                return;
            }

            HediffComp_FlowerResonance state = MX_QH_HediffUtility.EnsureFlowerResonance(pawn);
            if (state == null || ScoreDef == null)
            {
                return;
            }

            float roomBonus = BookUtility.GetReadingBonus(parent);
            float progressTicks = pawn.GetStatValue(StatDefOf.ReadingSpeed) * roomBonus * Mathf.Max(1, delta);
            bool complete = state.AddMusicScoreReadingProgress(ScoreDef, progressTicks);
            readingProgress = state.GetMusicScoreReadingProgressTicks(ScoreDef);
            if (complete)
            {
                CompleteReading(pawn);
            }
        }

        public string GetBenefitsString(Pawn reader = null)
        {
            if (ScoreDef == null || consumed)
            {
                return null;
            }

            string effect = IsMasteryScore
                ? "MX_QH_SkillBookBenefitMastery".Translate(ScoreDef.masteryGain).ToString()
                : "MX_QH_SkillBookBenefitNodes".Translate(UnlocksNodeLabel).ToString();

            if (reader != null && !CanStudy(reader, out string reason))
            {
                effect += "MX_QH_Parentheses".Translate(reason);
            }

            return effect;
        }

        private void CompleteReading(Pawn reader)
        {
            HediffComp_FlowerResonance state = MX_QH_HediffUtility.EnsureFlowerResonance(reader);
            if (state == null)
            {
                return;
            }

            string bookTitle = BookTitle;
            if (IsMasteryScore)
            {
                if (!state.TryGainMusicMastery(ScoreDef.masteryGain, out string reason))
                {
                    Messages.Message(reason, reader, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                SendMusicMasteryLearnedLetter(reader, bookTitle, state.MusicMasteryLevel);
                state.ClearMusicScoreReadingProgress(ScoreDef);
            }
            else
            {
                List<MX_QHSkillNodeDef> newlyLearnedNodes = UnlocksNodes
                    .Where(node => node != null && !state.HasNode(node))
                    .ToList();
                int learnedCount = state.LearnNodes(UnlocksNodes);
                if (learnedCount <= 0)
                {
                    return;
                }

                SendSkillNodesLearnedLetter(reader, bookTitle, newlyLearnedNodes);
                state.ClearMusicScoreReadingProgress(ScoreDef);
            }

            consumed = true;
            reader.jobs?.EndCurrentJob(JobCondition.Succeeded);
            ReplaceWithPlainBook(reader);
        }

        private void ReplaceWithPlainBook(Pawn reader)
        {
            Thing skillBook = parent;
            if (skillBook == null || skillBook.Destroyed)
            {
                return;
            }

            Book plainBook = MakePlainBook(PlainBookDef);
            if (plainBook == null)
            {
                return;
            }

            if (skillBook.TryGetQuality(out QualityCategory quality) && plainBook.TryGetComp<CompQuality>() != null)
            {
                plainBook.TryGetComp<CompQuality>().SetQuality(quality, ArtGenerationContext.Outsider);
            }

            plainBook.HitPoints = Mathf.Clamp(skillBook.HitPoints, 1, plainBook.MaxHitPoints);
            Map map = skillBook.MapHeld ?? reader?.MapHeld;
            IntVec3 cell = skillBook.Spawned ? skillBook.Position : reader?.PositionHeld ?? IntVec3.Invalid;

            skillBook.Destroy(DestroyMode.Vanish);
            if (map != null && cell.IsValid)
            {
                GenPlace.TryPlaceThing(plainBook, cell, map, ThingPlaceMode.Near);
            }
        }

        private Book MakePlainBook(ThingDef plainBookDef)
        {
            ThingDef def = plainBookDef ?? ThingDefOf.TextBook;
            Thing thing = ThingMaker.MakeThing(def, GenStuff.RandomStuffFor(def));
            Book book = thing as Book;
            if (book == null)
            {
                return null;
            }

            CompQuality compQuality = book.TryGetComp<CompQuality>();
            if (compQuality != null)
            {
                compQuality.SetQuality(QualityUtility.GenerateQualityRandomEqualChance(), ArtGenerationContext.Outsider);
            }

            if (book is Thing_MX_CustomBook qingheBook)
            {
                qingheBook.SetCustomText(BookTitle, BookContent);
            }

            return book;
        }

        private static void SendSkillNodesLearnedLetter(Pawn reader, string bookTitle, IEnumerable<MX_QHSkillNodeDef> nodes)
        {
            if (Find.LetterStack == null)
            {
                return;
            }

            string nodeLabels = nodes == null
                ? "MX_QH_UnknownNode".Translate().ToString()
                : nodes.Select(node => node.LabelCap.ToString()).ToCommaList();
            Find.LetterStack.ReceiveLetter(
                "MX_QH_SkillNodesLearnedLetterLabel".Translate(),
                "MX_QH_SkillNodesLearnedLetterText".Translate(bookTitle, nodeLabels),
                LetterDefOf.PositiveEvent,
                reader == null ? null : new LookTargets(reader));
        }

        private static void SendMusicMasteryLearnedLetter(Pawn reader, string bookTitle, int masteryLevel)
        {
            if (Find.LetterStack == null)
            {
                return;
            }

            Find.LetterStack.ReceiveLetter(
                "MX_QH_MusicMasteryLearnedLetterLabel".Translate(),
                "MX_QH_MusicMasteryLearnedLetterText".Translate(bookTitle, masteryLevel),
                LetterDefOf.PositiveEvent,
                reader == null ? null : new LookTargets(reader));
        }
    }
}
