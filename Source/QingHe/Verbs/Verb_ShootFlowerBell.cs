using MiliraXian.Characters.QingHe.Hediffs;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Verbs
{
    public class FlowerBellMandateVerbProperties
    {
        public AbilityDef flowerMandate;
        public ThingDef projectile;
        public int burstShotCount = -1;
        public float cooldownTime = -1f;
    }

    public class VerbProperties_FlowerBell : VerbProperties
    {
        public List<FlowerBellMandateVerbProperties> mandateSettings;
    }

    public class Verb_ShootFlowerBell : Verb_Shoot
    {
        private VerbProperties_FlowerBell FlowerBellProps => verbProps as VerbProperties_FlowerBell;

        public override ThingDef Projectile => CurrentSettings()?.projectile ?? base.Projectile;

        protected override int ShotsPerBurst
        {
            get
            {
                FlowerBellMandateVerbProperties settings = CurrentSettings();
                if (settings != null && settings.burstShotCount > 0)
                {
                    return Mathf.Max(1, settings.burstShotCount);
                }

                return base.ShotsPerBurst;
            }
        }

        public FlowerBellMandateVerbProperties CurrentSettings()
        {
            return ResolveSettings(CurrentFlowerMandateDefName());
        }

        public FlowerBellMandateVerbProperties ResolveSettings(string flowerMandateDefName)
        {
            var settings = FlowerBellProps?.mandateSettings;
            if (settings.NullOrEmpty())
            {
                return null;
            }

            FlowerBellMandateVerbProperties fallback = null;
            for (int i = 0; i < settings.Count; i++)
            {
                FlowerBellMandateVerbProperties entry = settings[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.flowerMandate == null)
                {
                    fallback = entry;
                    continue;
                }

                if (entry.flowerMandate.defName == flowerMandateDefName)
                {
                    return entry;
                }
            }

            return fallback;
        }

        private string CurrentFlowerMandateDefName()
        {
            return FlowerCourtUtility.EnsureSkillTreeState(CasterPawn)?.SelectedFlowerMandateDefName;
        }
    }
}
