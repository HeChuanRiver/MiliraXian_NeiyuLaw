using MiliraXian.Characters.QingHe.Hediffs;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Verbs
{
    public class FlowerBellSeasonVerbProperties
    {
        public AttunedSeason season = AttunedSeason.None;
        public ThingDef projectile;
        public int burstShotCount = -1;
        public float cooldownTime = -1f;
    }

    public class VerbProperties_FlowerBell : VerbProperties
    {
        public List<FlowerBellSeasonVerbProperties> seasonSettings;
    }

    public class Verb_ShootFlowerBell : Verb_Shoot
    {
        private VerbProperties_FlowerBell FlowerBellProps => verbProps as VerbProperties_FlowerBell;

        public override ThingDef Projectile
        {
            get
            {
                return CurrentSettings()?.projectile ?? base.Projectile;
            }
        }

        protected override int ShotsPerBurst
        {
            get
            {
                FlowerBellSeasonVerbProperties settings = CurrentSettings();
                if (settings != null && settings.burstShotCount > 0)
                {
                    return Mathf.Max(1, settings.burstShotCount);
                }

                return base.ShotsPerBurst;
            }
        }

        public FlowerBellSeasonVerbProperties CurrentSettings()
        {
            return ResolveSettings(CurrentSeason());
        }

        public FlowerBellSeasonVerbProperties ResolveSettings(AttunedSeason season)
        {
            var settings = FlowerBellProps?.seasonSettings;
            if (settings.NullOrEmpty())
            {
                return null;
            }

            FlowerBellSeasonVerbProperties fallback = null;
            for (int i = 0; i < settings.Count; i++)
            {
                FlowerBellSeasonVerbProperties entry = settings[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.season == season)
                {
                    return entry;
                }

                if (entry.season == AttunedSeason.None)
                {
                    fallback = entry;
                }
            }

            return season == AttunedSeason.None ? fallback : null;
        }

        public AttunedSeason CurrentSeason()
        {
            HediffComp_SeasonResonance resonance = GetSeasonResonance(CasterPawn);
            return resonance?.CurrentAttunedSeason ?? AttunedSeason.None;
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
