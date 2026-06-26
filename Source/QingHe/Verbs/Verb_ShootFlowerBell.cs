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
        private const float FlowerDecreeCostPerBurst = 1f;
        private const float MinimumFlowerDecreeToAttackEnhanced = 1f;

        private bool enhancedBurstInitialized;
        private bool enhancedForCurrentBurst;
        private bool enhancedForCurrentShot;
        private float flowerDecreeCostPerShot;

        private VerbProperties_FlowerBell FlowerBellProps => verbProps as VerbProperties_FlowerBell;

        public bool EnhancedForCurrentShot => enhancedForCurrentShot;

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
            return ResolveSettings(CurrentFlowerMandateDef());
        }

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            CheckEnhancedAttackAvailable();
            return base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        }

        public override void WarmupComplete()
        {
            enhancedBurstInitialized = false;
            enhancedForCurrentBurst = false;
            enhancedForCurrentShot = false;
            flowerDecreeCostPerShot = 0f;
            base.WarmupComplete();
        }

        public override void Reset()
        {
            enhancedBurstInitialized = false;
            enhancedForCurrentBurst = false;
            enhancedForCurrentShot = false;
            flowerDecreeCostPerShot = 0f;
            base.Reset();
        }

        protected override bool TryCastShot()
        {
            InitializeEnhancedBurstIfNeeded();
            enhancedForCurrentShot = false;

            if (enhancedForCurrentBurst)
            {
                HediffComp_FlowerDecree decree = FlowerCourtUtility.GetFlowerDecree(CasterPawn);
                if (decree == null || decree.CurrentResourceValue + 1E-05f < flowerDecreeCostPerShot)
                {
                    DisableFlowerBellEnhanced();
                    enhancedForCurrentBurst = false;
                }
                else
                {
                    enhancedForCurrentShot = true;
                }
            }

            bool shotFired = base.TryCastShot();
            if (shotFired && enhancedForCurrentShot)
            {
                FlowerCourtUtility.GetFlowerDecree(CasterPawn)?.TryConsumeDecree(flowerDecreeCostPerShot);
            }

            return shotFired;
        }

        public FlowerBellMandateVerbProperties ResolveSettings(AbilityDef flowerMandate)
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

                if (entry.flowerMandate == flowerMandate)
                {
                    return entry;
                }
            }

            return fallback;
        }

        private AbilityDef CurrentFlowerMandateDef()
        {
            return FlowerCourtUtility.EnsureFlowerChoices(CasterPawn)?.SelectedFlowerMandate;
        }

        private void InitializeEnhancedBurstIfNeeded()
        {
            if (enhancedBurstInitialized)
            {
                return;
            }

            enhancedBurstInitialized = true;
            flowerDecreeCostPerShot = FlowerDecreeCostPerBurst / Mathf.Max(1, ShotsPerBurst);

            if (!CheckEnhancedAttackAvailable())
            {
                enhancedForCurrentBurst = false;
                return;
            }

            enhancedForCurrentBurst = true;
        }

        private bool CheckEnhancedAttackAvailable()
        {
            if (!FlowerBellEnhanced())
            {
                return false;
            }

            HediffComp_FlowerDecree decree = FlowerCourtUtility.GetFlowerDecree(CasterPawn);
            if (decree != null && decree.CurrentResourceValue + 1E-05f >= MinimumFlowerDecreeToAttackEnhanced)
            {
                return true;
            }

            DisableFlowerBellEnhanced();
            return false;
        }

        private bool FlowerBellEnhanced()
        {
            return FlowerCourtUtility.GetFlowerChoices(CasterPawn)?.FlowerBellEnhanced == true;
        }

        private void DisableFlowerBellEnhanced()
        {
            FlowerCourtUtility.GetFlowerChoices(CasterPawn)?.SetFlowerBellEnhanced(false);
        }
    }
}
