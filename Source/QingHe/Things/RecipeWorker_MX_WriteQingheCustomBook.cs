using System.Collections.Generic;
using System;
using System.Linq;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Things
{
    public class MX_QingheRecipeRequirementExtension : DefModExtension
    {
        public List<PawnKindDef> allowedPawnKinds;
        public string failureReasonKey = "MX_QH_RecipeRequiresQinghe";
    }

    public class MX_QingheCustomBookText
    {
        public string title;
        public string content;
    }

    public class MX_WriteQingheCustomBookExtension : DefModExtension
    {
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
        }

        private Thing MakeBook(Pawn billDoer)
        {
            MX_WriteQingheCustomBookExtension extension = recipe.GetModExtension<MX_WriteQingheCustomBookExtension>();
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
                .RandomElementWithFallback();
            book.SetCustomText(
                text?.title ?? "荷雨亭曲艺",
                text?.content ?? "清荷将荷雨亭中的花影、水声与曲调细细写下，记成一卷可供闲读的曲艺手稿。");
            MX_QH_HediffUtility.AddDivineGraceProgressFromCraft(billDoer, recipe, book);

            return book;
        }
    }
}
