using System.Collections.Generic;
using System;
using System.Linq;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Things
{
    public class MX_InterpretLostScorebookExtension : DefModExtension
    {
        public float skillBookChance = 0.35f;
        public ThingDef plainBookDef;
    }

    public class MX_QingheRecipeRequirementExtension : DefModExtension
    {
        public List<PawnKindDef> allowedPawnKinds;
        public string failureReasonKey = "MX_QH_RecipeRequiresQinghe";
    }

    public class MX_QingheCustomBookText
    {
        public string title;
        public string content;
        public List<MX_BookSpawnCondition> spawnConditions;

        public bool CanSpawnFor(Pawn pawn)
        {
            return MX_BookSpawnConditionUtility.Allows(spawnConditions, pawn);
        }
    }

    public class MX_WriteQingheCustomBookExtension : DefModExtension
    {
        public float skillBookChance = 0.25f;
        public ThingDef bookDef;
        public List<MX_QingheCustomBookText> variants;
    }

    public class BookOutcomeProperties_QingheCustomBookThought : BookOutcomeProperties
    {
        public ThoughtDef thoughtDef;
        public int gainIntervalTicks = 600;
        public string benefitKey = "MX_QH_CustomBookThoughtBenefit";

        public override Type DoerClass => typeof(BookOutcomeDoer_QingheCustomBookThought);
    }

    public class BookOutcomeDoer_QingheCustomBookThought : BookOutcomeDoer
    {
        private float progressTicks;
        private Pawn lastReader;

        public new BookOutcomeProperties_QingheCustomBookThought Props => (BookOutcomeProperties_QingheCustomBookThought)props;

        public override bool DoesProvidesOutcome(Pawn reader)
        {
            return Props.thoughtDef != null && reader?.needs?.mood != null;
        }

        public override void OnReadingTick(Pawn reader, float factor)
        {
            if (reader == null || Props.thoughtDef == null || factor <= 0f)
            {
                return;
            }

            if (lastReader != reader)
            {
                lastReader = reader;
                progressTicks = 0f;
            }

            progressTicks += factor;
            int interval = Math.Max(1, Props.gainIntervalTicks);
            if (progressTicks < interval)
            {
                return;
            }

            progressTicks = 0f;
            reader.needs?.mood?.thoughts?.memories?.TryGainMemory(Props.thoughtDef);
        }

        public override string GetBenefitsString(Pawn reader = null)
        {
            return Props.benefitKey.NullOrEmpty() ? null : Props.benefitKey.Translate();
        }
    }

    public class RecipeWorker_MX_InterpretLostScorebook : RecipeWorker
    {
        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            base.Notify_IterationCompleted(billDoer, ingredients);

            Map map = billDoer?.Map;
            if (map == null)
            {
                return;
            }

            MX_InterpretLostScorebookExtension extension = recipe.GetModExtension<MX_InterpretLostScorebookExtension>();
            Thing result = Rand.Chance(extension?.skillBookChance ?? 0.35f)
                ? MakeSkillBook(billDoer)
                : MakePlainBook(extension?.plainBookDef ?? ThingDefOf.TextBook);
            if (result == null)
            {
                result = MakePlainBook(extension?.plainBookDef ?? ThingDefOf.TextBook);
            }

            if (result == null)
            {
                return;
            }

            Thing billGiver = billDoer.CurJob?.GetTarget(TargetIndex.A).Thing;
            IntVec3 placeCell = billGiver?.Position ?? billDoer.Position;
            GenPlace.TryPlaceThing(result, placeCell, map, ThingPlaceMode.Near);

            if (result is Thing_MX_SkillBook)
            {
                SendSkillBookAcquiredLetter(billDoer, result);
            }
        }

        internal static Thing MakeSkillBook(
            Pawn billDoer,
            QualityCategory? quality = null,
            ArtGenerationContext qualityContext = ArtGenerationContext.Outsider)
        {
            Thing thing = ThingMaker.MakeThing(MX_QHDefOf.MX_QH_SkillBook);
            Thing_MX_SkillBook book = thing as Thing_MX_SkillBook;
            if (book == null)
            {
                return null;
            }

            if (!book.InitializeFor(billDoer))
            {
                return null;
            }

            CompQuality compQuality = book.TryGetComp<CompQuality>();
            if (compQuality != null)
            {
                compQuality.SetQuality(quality ?? QualityUtility.GenerateQualityRandomEqualChance(), qualityContext);
            }

            return thing;
        }

        private static Thing MakePlainBook(ThingDef plainBookDef)
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

            return book;
        }

        internal static void SendSkillBookAcquiredLetter(Pawn pawn, Thing book)
        {
            if (Find.LetterStack == null || book == null)
            {
                return;
            }

            string bookTitle = (book as Thing_MX_SkillBook)?.BookTitle ?? book.LabelCap;
            Find.LetterStack.ReceiveLetter(
                "MX_QH_SkillBookAcquiredLetterLabel".Translate(),
                "MX_QH_SkillBookAcquiredLetterText".Translate(bookTitle),
                LetterDefOf.PositiveEvent,
                new LookTargets(book));
        }
    }

    public class RecipeWorker_MX_WriteQingheCustomBook : RecipeWorker
    {
        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            base.Notify_IterationCompleted(billDoer, ingredients);

            Map map = billDoer?.Map;
            if (map == null)
            {
                return;
            }

            Thing book = MakeBook(billDoer);
            if (book == null)
            {
                return;
            }

            Thing billGiver = billDoer.CurJob?.GetTarget(TargetIndex.A).Thing;
            IntVec3 placeCell = billGiver?.Position ?? billDoer.Position;
            GenPlace.TryPlaceThing(book, placeCell, map, ThingPlaceMode.Near);

            if (book is Thing_MX_SkillBook)
            {
                RecipeWorker_MX_InterpretLostScorebook.SendSkillBookAcquiredLetter(billDoer, book);
            }
        }

        private Thing MakeBook(Pawn billDoer)
        {
            MX_WriteQingheCustomBookExtension extension = recipe.GetModExtension<MX_WriteQingheCustomBookExtension>();
            if (Rand.Chance(extension?.skillBookChance ?? 0.25f))
            {
                SkillDef workSkill = recipe.workSkill ?? SkillDefOf.Artistic;
                Thing skillBook = RecipeWorker_MX_InterpretLostScorebook.MakeSkillBook(
                    billDoer,
                    QualityUtility.GenerateQualityCreatedByPawn(billDoer, workSkill),
                    ArtGenerationContext.Colony);
                if (skillBook != null)
                {
                    QualityUtility.SendCraftNotification(skillBook, billDoer);
                    return skillBook;
                }
            }

            ThingDef bookDef = extension?.bookDef ?? MX_QHDefOf.MX_QH_Book;
            if (bookDef == null)
            {
                return null;
            }

            ThingDef stuff = bookDef.MadeFromStuff ? GenStuff.RandomStuffFor(bookDef) : null;
            Thing thing = ThingMaker.MakeThing(bookDef, stuff);
            Thing_MX_CustomBook book = thing as Thing_MX_CustomBook;
            if (book == null)
            {
                return null;
            }

            CompQuality compQuality = book.TryGetComp<CompQuality>();
            if (compQuality != null)
            {
                SkillDef workSkill = recipe.workSkill ?? SkillDefOf.Artistic;
                QualityCategory quality = QualityUtility.GenerateQualityCreatedByPawn(billDoer, workSkill);
                compQuality.SetQuality(quality, ArtGenerationContext.Colony);
                QualityUtility.SendCraftNotification(book, billDoer);
            }

            MX_QingheCustomBookText text = extension?.variants?
                .Where(variant => variant != null)
                .Where(variant => variant.CanSpawnFor(billDoer))
                .RandomElementWithFallback();
            book.SetCustomText(
                text?.title ?? "荷雨亭曲艺",
                text?.content ?? "清荷将荷雨亭中的花影、水声与曲调细细写下，记成一卷可供闲读的曲艺手稿。");

            return book;
        }
    }
}


