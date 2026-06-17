using Verse;
using RimWorld;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_StatusEffectBleeding : HediffCompProperties
    {
        public int woundCount = 2;
        public float woundSeverity = 3f;
        public ThingDef bloodFilthDef = ThingDefOf.Filth_Blood;
        public int bloodFilthCount = 3;

        public HediffCompProperties_StatusEffectBleeding()
        {
            compClass = typeof(HediffComp_StatusEffectBleeding);
        }
    }

    public class HediffComp_StatusEffectBleeding : HediffComp
    {
        private bool appliedWounds;

        private HediffCompProperties_StatusEffectBleeding PropsBleeding => (HediffCompProperties_StatusEffectBleeding)props;

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
                StatusEffectUtility.ApplyBleed(Pawn, PropsBleeding.woundSeverity);
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
