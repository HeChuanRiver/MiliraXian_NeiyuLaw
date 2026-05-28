using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Verbs
{
    public class Verb_ShootFlowerBell : Verb_Shoot
    {
        public override ThingDef Projectile
        {
            get
            {
                HediffComp_SeasonResonance resonance = GetSeasonResonance(CasterPawn);
                if (resonance?.CurrentAttunedSeason == AttunedSeason.Winter && MX_QHDefOf.MX_Bullet_Qinghe_FlowerBell_Winter != null)
                {
                    return MX_QHDefOf.MX_Bullet_Qinghe_FlowerBell_Winter;
                }

                return base.Projectile;
            }
        }

        private static HediffComp_SeasonResonance GetSeasonResonance(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_SeasonResonance == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_SeasonResonance);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_SeasonResonance>();
        }
    }
}
