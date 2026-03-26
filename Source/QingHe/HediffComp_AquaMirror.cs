using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_AquaMirror : HediffCompProperties
    {
        /// <summary>
        /// Base injury-heal amount applied once when AquaMirror ends.
        /// </summary>
        public float healAmount = 10f;

        /// <summary>
        /// Elegance scaling max for heal amount. Final factor uses EleganceUtility.FactorLinear(max, caster).
        /// Example: 1.0 means heal multiplier ranges from 1.0x to 2.0x.
        /// </summary>
        public float healAmountByEleganceMax = 1.0f;

        public float explosionRadius = 2.0f;
        public float explosionDamage = 10.0f;
        public float tempestPerMirror = 5.0f;
        public CompProperties_AquaMirrorShield shieldCompProperties;

        public HediffCompProperties_AquaMirror()
        {
            compClass = typeof(HediffComp_AquaMirror);
        }
    }

    public class HediffComp_AquaMirror : HediffComp
    {
        public HediffCompProperties_AquaMirror Props => (HediffCompProperties_AquaMirror)props;

        public Pawn caster;

        public CompAquaMirrorShield shieldInspected;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (parent.pawn == null)
            {
                return;
            }

            CompAquaMirrorShield existed = parent.pawn.GetComp<CompAquaMirrorShield>();
            if (existed != null)
            {
                parent.pawn.AllComps.Remove(existed);
            }

            CompAquaMirrorShield newShield = new CompAquaMirrorShield
            {
                parent = parent.pawn
            };
            newShield.Initialize(Props.shieldCompProperties);
            newShield.Init(EleganceUtility.FactorLinear(1.0f, caster));
            newShield.PostPostMake();
            Log.Message("Debug shield amount: " + newShield.Energy);
            parent.pawn.AllComps.Add(newShield);
            shieldInspected = newShield;
        }

        public override bool CompShouldRemove => shieldInspected?.Broken ?? false;

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            Pawn pawn = parent?.pawn;
            if (pawn == null)
            {
                return;
            }

            if (pawn.Spawned && pawn.Map != null)
            {
                MoteMaker.MakeStaticMote(pawn.TrueCenter(), pawn.Map, MX_QHDefOf.Mote_AquaMirrorExplode, 2.0f);

                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, Props.explosionRadius, true))
                {
                    if (thing is Pawn target && !target.Dead && target.HostileTo(caster))
                    {
                        DamageInfo dinfo = new DamageInfo(
                            MX_QHDefOf.MX_Dehydrate,
                            Props.explosionDamage * EleganceUtility.FactorLinear(1.0f, caster),
                            armorPenetration: 1000.0f,
                            instigator: caster);
                        dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Inside);
                        dinfo.SetIgnoreArmor(true);
                        dinfo.SetApplyAllDamage(true);
                        target.TakeDamage(dinfo);
                    }
                }
            }

            // Heal a fixed amount of injury severity, scaled by caster Elegance.
            if (Props.healAmount > 0f)
            {
                Pawn scalerPawn = caster ?? pawn;
                float healAmount = Props.healAmount * EleganceUtility.FactorLinear(Props.healAmountByEleganceMax, scalerPawn);
                MX_QHUtility.HealInjuries(pawn, healAmount);
            }

            PawnSpecialResourceUtility.AddResource(caster, MX_QHDefOf.MX_QH_Tempest, Props.tempestPerMirror);

            if (shieldInspected != null)
            {
                pawn.AllComps.Remove(shieldInspected);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref shieldInspected, "shieldInspected");
        }
    }
}
