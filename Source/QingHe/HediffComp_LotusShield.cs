using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class Hediff_LotusShield : HediffWithComps
    {
    }

    public class HediffCompProperties_LotusShield : HediffCompProperties
    {
        public CompProperties_LotusShield shieldCompProperties;

        public HediffCompProperties_LotusShield()
        {
            compClass = typeof(HediffComp_LotusShield);
        }
    }

    /// <summary>
    /// Bind LotusShield ThingComp lifecycle to a Hediff.
    /// </summary>
    public class HediffComp_LotusShield : HediffComp
    {
        public HediffCompProperties_LotusShield Props => (HediffCompProperties_LotusShield)props;

        public override string CompTipStringExtra
        {
            get
            {
                CompLotusShield shield = Pawn?.GetComp<CompLotusShield>();
                if (shield == null)
                {
                    return null;
                }

                return "当前每点护盾承伤: " + shield.CurrentDamagePerShieldPoint.ToString("F2")
                       + "\n当前护盾回复速率: " + shield.CurrentRegenPerSecond.ToString("F2") + " /s";
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

            CompLotusShield existed = Pawn.GetComp<CompLotusShield>();
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

            CompLotusShield existed = Pawn.GetComp<CompLotusShield>();
            if (existed != null)
            {
                Pawn.AllComps.Remove(existed);
            }
        }
    }
}
