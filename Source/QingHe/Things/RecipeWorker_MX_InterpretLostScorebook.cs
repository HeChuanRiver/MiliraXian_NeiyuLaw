using System.Collections.Generic;
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
                QingheSkillBookUtility.SendSkillBookAcquiredLetter(billDoer, result);
            }
        }

        private static Thing MakeSkillBook()
        {
            QingheMusicScoreDef score = DefDatabase<QingheMusicScoreDef>.AllDefsListForReading.RandomElementWithFallback();
            return score == null ? null : QingheSkillBookUtility.MakeSkillBook(score);
        }

        private static Thing MakePlainBook(ThingDef plainBookDef)
        {
            return QingheSkillBookUtility.MakePlainBook(plainBookDef ?? ThingDefOf.TextBook);
        }
    }
}
