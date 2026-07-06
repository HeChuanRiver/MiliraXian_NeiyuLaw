using System.Collections.Generic;
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
                ? MakeSkillBook()
                : MakePlainBook(extension?.plainBookDef ?? ThingDefOf.TextBook);
            if (result == null)
            {
                return;
            }

            Thing billGiver = billDoer.CurJob?.GetTarget(TargetIndex.A).Thing;
            IntVec3 placeCell = billGiver?.Position ?? billDoer.Position;
            GenPlace.TryPlaceThing(result, placeCell, map, ThingPlaceMode.Near);

            if (result is Thing_QingheMusicScoreBook)
            {
                SendSkillBookAcquiredLetter(billDoer, result);
            }
        }

        private static Thing MakeSkillBook()
        {
            QingheMusicScoreDef score = DefDatabase<QingheMusicScoreDef>.AllDefsListForReading.RandomElementWithFallback();
            if (score == null)
            {
                return null;
            }

            Thing thing = ThingMaker.MakeThing(MX_QHDefOf.MX_QH_SkillBook);
            thing.TryGetComp<Comp_QingheMusicScore>()?.Initialize(score);
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

            string bookTitle = (book as Thing_QingheMusicScoreBook)?.ScoreComp?.BookTitle ?? book.LabelCap;
            Find.LetterStack.ReceiveLetter(
                "MX_QH_SkillBookAcquiredLetterLabel".Translate(),
                "MX_QH_SkillBookAcquiredLetterText".Translate(bookTitle),
                LetterDefOf.PositiveEvent,
                new LookTargets(book));
        }
    }
}
