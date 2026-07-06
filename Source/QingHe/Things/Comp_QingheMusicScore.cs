using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public GraphicData openGraphic;
        public GraphicData storageGraphic;

        public CompProperties_QingheMusicScore()
        {
            compClass = typeof(Comp_QingheMusicScore);
        }
    }

    public class Comp_QingheMusicScore : ThingComp
    {
        private QingheMusicScoreDef score;
        private string title;
        private string content;
        private float readingProgress;
        private bool consumed;

        public CompProperties_QingheMusicScore Props => (CompProperties_QingheMusicScore)props;

        public QingheMusicScoreDef ScoreDef => score ?? Props.score;

        public IReadOnlyList<MX_QHSkillNodeDef> UnlocksNodes => ScoreDef?.unlocksNodes;

        public string UnlocksNodeLabel => ScoreDef?.LabelCap ?? BookTitle;

        public string BookTitle => title.NullOrEmpty() ? (ScoreDef != null ? ScoreDef.LabelCap.ToString() : parent.def.LabelCap.ToString()) : title;

        public string BookContent => content.NullOrEmpty() ? ScoreDef?.description ?? parent.def.description : content;

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
            Scribe_Values.Look(ref title, "mx_qh_title");
            Scribe_Values.Look(ref content, "mx_qh_content");
            Scribe_Values.Look(ref readingProgress, "mx_qh_readingProgress", 0f);
            Scribe_Values.Look(ref consumed, "mx_qh_consumed", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureInitialized();
            }
        }

        public void Initialize(QingheMusicScoreDef newScore, string newTitle = null, string newContent = null)
        {
            score = newScore;
            title = newTitle.NullOrEmpty() ? newScore?.LabelCap.ToString() : newTitle;
            content = newContent.NullOrEmpty() ? newScore?.description : newContent;
            readingProgress = 0f;
            consumed = false;
        }

        public void EnsureInitialized()
        {
            if (ScoreDef != null)
            {
                if (ScoreDef.requiredReadingTicks <= 0)
                {
                    ScoreDef.requiredReadingTicks = RequiredReadingTicks;
                }
                if (title.NullOrEmpty())
                {
                    title = ScoreDef.LabelCap.ToString();
                }
                if (content.NullOrEmpty())
                {
                    content = ScoreDef.description;
                }
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

            if (!MX_QHUtility.IsQinghe(pawn))
            {
                disabledReason = "MX_QH_SkillBookRequiresQinghe".Translate();
                return false;
            }

            if (!BookUtility.CanReadEver(pawn))
            {
                disabledReason = "MX_QH_SkillBookCannotReadNow".Translate();
                return false;
            }

            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
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

            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
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
            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(reader);
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

                QingheSkillBookUtility.SendMusicMasteryLearnedLetter(reader, bookTitle, state.MusicMasteryLevel);
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

                QingheSkillBookUtility.SendSkillNodesLearnedLetter(reader, bookTitle, newlyLearnedNodes);
                state.ClearMusicScoreReadingProgress(ScoreDef);
            }

            consumed = true;
            reader.jobs?.EndCurrentJob(JobCondition.Succeeded);
            QingheSkillBookUtility.ReplaceWithPlainBook(parent, reader);
        }
    }

    public class Thing_QingheMusicScoreBook : ThingWithComps
    {
        private Comp_QingheMusicScore cachedScoreComp;
        private Graphic openGraphic;
        private Graphic storageGraphic;
        private bool isOpen;

        public Comp_QingheMusicScore ScoreComp => cachedScoreComp ?? (cachedScoreComp = GetComp<Comp_QingheMusicScore>());

        private Graphic OpenGraphic => openGraphic ?? (openGraphic = ScoreComp?.Props.openGraphic?.Graphic);

        public Graphic StorageGraphic => storageGraphic ?? (storageGraphic = ScoreComp?.Props.storageGraphic?.Graphic);

        public bool IsOpen
        {
            get => isOpen;
            set => isOpen = value;
        }

        public override string LabelNoCount => (ScoreComp?.BookTitle ?? def.LabelCap)
            + GenLabel.LabelExtras(this, includeHp: true, includeQuality: true);

        public override string LabelNoParenthesis => ScoreComp?.BookTitle ?? def.LabelCap;

        public override string DescriptionDetailed
        {
            get
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine((ScoreComp?.BookTitle ?? LabelCap).Colorize(ColoredText.TipSectionTitleColor)
                    + GenLabel.LabelExtras(this, includeHp: false, includeQuality: true)
                    + "\n");
                builder.AppendLine((ScoreComp?.BookContent ?? def.description) + "\n");

                string benefits = ScoreComp?.GetBenefitsString();
                if (!benefits.NullOrEmpty())
                {
                    builder.AppendLine(" - " + benefits);
                }

                return builder.ToString().TrimEndNewlines();
            }
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            ScoreComp?.EnsureInitialized();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isOpen, "mx_qh_isOpen", false);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn))
            {
                option.iconThing = this;
                yield return option;
            }
        }

        public void Notify_ReadTick(Pawn pawn, int delta)
        {
            ScoreComp?.AddReadingProgress(pawn, delta);
        }

        public override bool CanStackWith(Thing other)
        {
            return false;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (isOpen && OpenGraphic != null)
            {
                Pawn_CarryTracker carryTracker = ParentHolder as Pawn_CarryTracker;
                Rot4 rot = carryTracker != null ? carryTracker.pawn.Rotation : Rotation;
                OpenGraphic.Draw(drawLoc, flip ? rot.Opposite : rot, this);
                return;
            }

            base.DrawAt(drawLoc, flip);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats())
            {
                yield return entry;
            }

            string benefits = ScoreComp?.GetBenefitsString();
            if (!benefits.NullOrEmpty())
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    "MX_QH_MusicScoreStatCategory".Translate(),
                    benefits,
                    benefits,
                    1000);
            }

            if (ScoreComp != null)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    "MX_QH_ReadingProgressStat".Translate(),
                    ScoreComp.ReadingProgressPercent.ToStringPercent("F0"),
                    "MX_QH_ReadingProgressStatDesc".Translate(),
                    999);
            }
        }
    }

    public static class QingheSkillBookUtility
    {
        public static Thing MakeSkillBook(QingheMusicScoreDef scoreDef)
        {
            Thing thing = ThingMaker.MakeThing(MX_QHDefOf.MX_QH_SkillBook);
            thing.TryGetComp<Comp_QingheMusicScore>()?.Initialize(scoreDef);
            return thing;
        }

        public static void ReplaceWithPlainBook(Thing skillBook, Pawn reader)
        {
            if (skillBook == null || skillBook.Destroyed)
            {
                return;
            }

            Comp_QingheMusicScore scoreComp = skillBook.TryGetComp<Comp_QingheMusicScore>();
            ThingDef plainBookDef = scoreComp?.PlainBookDef ?? ThingDefOf.TextBook;
            Book plainBook = MakePlainBook(plainBookDef, scoreComp?.BookTitle, scoreComp?.BookContent);
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

        public static Book MakePlainBook(ThingDef plainBookDef, string title = null, string content = null)
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
                qingheBook.SetCustomText(title, content);
            }

            return book;
        }

        public static void SendSkillBookAcquiredLetter(Pawn pawn, Thing book)
        {
            if (Find.LetterStack == null || book == null)
            {
                return;
            }

            string bookTitle = (book as Thing_QingheMusicScoreBook)?.ScoreComp?.BookTitle ?? book.LabelCap;
            Find.LetterStack.ReceiveLetter(
                "MX_QH_SkillBookAcquiredLetterLabel".Translate(),
                "MX_QH_SkillBookAcquiredLetterText".Translate(bookTitle),
                LetterDefOf.PositiveEvent,
                new LookTargets(book));
        }

        public static void SendSkillNodesLearnedLetter(Pawn reader, string bookTitle, IEnumerable<MX_QHSkillNodeDef> nodes)
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

        public static void SendMusicMasteryLearnedLetter(Pawn reader, string bookTitle, int masteryLevel)
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
