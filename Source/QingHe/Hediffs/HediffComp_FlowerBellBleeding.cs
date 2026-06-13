using Verse;
using RimWorld;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerBellBleeding : HediffCompProperties
    {
        public int woundCount = 2;
        public float woundSeverity = 3f;
        public ThingDef bloodFilthDef = ThingDefOf.Filth_Blood;
        public int bloodFilthCount = 3;

        public HediffCompProperties_FlowerBellBleeding()
        {
            compClass = typeof(HediffComp_FlowerBellBleeding);
        }
    }

    public class HediffComp_FlowerBellBleeding : HediffComp
    {
        private bool appliedWounds;

        private HediffCompProperties_FlowerBellBleeding PropsBleeding => (HediffCompProperties_FlowerBellBleeding)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            ApplyWounds();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref appliedWounds, "appliedWounds", false);
        }

        private void ApplyWounds()
        {
            if (appliedWounds || Pawn == null || Pawn.Dead)
            {
                return;
            }

            appliedWounds = true;
            for (int i = 0; i < PropsBleeding.woundCount; i++)
            {
                MX_QHUtility.ApplyBleed(Pawn, PropsBleeding.woundSeverity);
            }

            MakeBloodFilth();
        }

        private void MakeBloodFilth()
        {
            if (Pawn?.Spawned != true || Pawn.MapHeld == null || PropsBleeding.bloodFilthDef == null || PropsBleeding.bloodFilthCount <= 0)
            {
                return;
            }

            FilthMaker.TryMakeFilth(Pawn.PositionHeld, Pawn.MapHeld, PropsBleeding.bloodFilthDef, PropsBleeding.bloodFilthCount, FilthSourceFlags.Pawn);
        }
    }
}
