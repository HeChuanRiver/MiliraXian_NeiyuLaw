using System.Collections.Generic;
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

        private static Thing MakeSkillBook(Pawn billDoer)
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
                compQuality.SetQuality(QualityUtility.GenerateQualityRandomEqualChance(), ArtGenerationContext.Outsider);
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

        private static void SendSkillBookAcquiredLetter(Pawn pawn, Thing book)
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
}


