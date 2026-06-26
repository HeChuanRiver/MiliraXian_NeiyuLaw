using Verse;
using MiliraXian.Characters.QingHe.Things;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class Hediff_DivineProtection : HediffWithComps
    {
    }

    public class HediffCompProperties_DivineProtection : HediffCompProperties
    {
        public CompProperties_LotusShield shieldCompProperties;

        public HediffCompProperties_DivineProtection()
        {
            compClass = typeof(HediffComp_DivineProtection);
        }
    }

    /// <summary>
    /// Bind LotusShield ThingComp lifecycle to a Hediff.
    /// </summary>
    public class HediffComp_DivineProtection : HediffComp
    {
        public HediffCompProperties_DivineProtection Props => (HediffCompProperties_DivineProtection)props;

        public override string CompTipStringExtra
        {
            get
            {
                CompLotusShield shield = GetLotusShield();
                if (shield == null)
                {
                    return null;
                }

                return "MX_QH_LotusShieldCapacity".Translate(shield.MaxEnergy.ToString("F0")).ToString()
                       + "\n" + "MX_QH_LotusShieldRegen".Translate(shield.CurrentRegenPerSecond.ToString("F2"));
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

        public void EnsureShieldBound()
        {
            if (Pawn == null || Pawn.Destroyed)
            {
                return;
            }

            CompLotusShield existed = GetLotusShield();
            if (existed != null)
            {
                return;
            }

            CompLotusShield comp = new CompLotusShield
            {
                parent = Pawn
            };
            comp.Initialize(Props?.shieldCompProperties ?? new CompProperties_LotusShield());
            comp.PostPostMake();
            Pawn.AllComps.Add(comp);
        }

        private void RemoveShieldComp()
        {
            if (Pawn == null || Pawn.AllComps == null)
            {
                return;
            }

            CompLotusShield existed = GetLotusShield();
            if (existed != null)
            {
                Pawn.AllComps.Remove(existed);
            }
        }

        private CompLotusShield GetLotusShield()
        {
            if (Pawn?.AllComps == null)
            {
                return null;
            }

            for (int i = 0; i < Pawn.AllComps.Count; i++)
            {
                if (Pawn.AllComps[i] is CompLotusShield shield)
                {
                    return shield;
                }
            }

            return null;
        }
    }
}
