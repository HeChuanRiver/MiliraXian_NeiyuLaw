using Verse;
using MiliraXian.Characters.QingHe.Things;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_AuraShield : HediffCompProperties
    {
        public CompProperties_AuraShield shieldCompProperties;

        public HediffCompProperties_AuraShield()
        {
            compClass = typeof(HediffComp_AuraShield);
        }
    }

    /// <summary>
    /// Own the shield's binding and save lifecycle; runtime state stays in the ThingComp.
    /// </summary>
    public class HediffComp_AuraShield : HediffComp
    {
        private CompAuraShield shield;
        private bool shieldBound;

        public HediffCompProperties_AuraShield Props => (HediffCompProperties_AuraShield)props;

        public override string CompTipStringExtra
        {
            get
            {
                if (!shieldBound || QinghePowerBalance.Sealed)
                {
                    return null;
                }

                return "MX_QH_AuraShieldCapacity".Translate(shield.MaxEnergy.ToString("F0")).ToString()
                       + "\n" + "MX_QH_AuraShieldRegen".Translate(shield.CurrentRegenPerSecond.ToString("F2"));
            }
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            EnsureShieldBound();
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            RemoveShieldComp();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                shield = CreateShield();
            }

            shield.ExposeShieldData();

            // HediffSet assigns the pawn during ResolvingCrossRefs.
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureShieldBound();
            }
        }

        public void EnsureShieldBound()
        {
            if (Pawn.Destroyed || shieldBound)
            {
                return;
            }

            if (shield == null)
            {
                shield = CreateShield();
                shield.BindToPawn(Pawn);
                shield.PostPostMake();
            }
            else
            {
                shield.BindToPawn(Pawn);
            }

            if (!QinghePowerBalance.Sealed)
            {
                Pawn.AllComps.Add(shield);
                shieldBound = true;
            }
        }

        private CompAuraShield CreateShield()
        {
            CompAuraShield comp = new();
            comp.Initialize(Props.shieldCompProperties);
            return comp;
        }

        public void DisableShield()
        {
            RemoveShieldComp();
        }

        public void SyncForPowerLevel()
        {
            if (QinghePowerBalance.Sealed)
            {
                DisableShield();
            }
            else
            {
                EnsureShieldBound();
            }
        }

        private void RemoveShieldComp()
        {
            if (shieldBound)
            {
                Pawn.AllComps.Remove(shield);
                shieldBound = false;
            }
        }
    }
}
