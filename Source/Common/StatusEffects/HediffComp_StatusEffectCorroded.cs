using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_StatusEffectCorroded : HediffCompProperties
    {
        public float armorMultiplier = 0.7f;
        public DamageDef damageDef;
        public int damageIntervalTicks = 240;
        public float damageAmount = 2f;
        public float armorPenetration = 0f;
        public FleckDef poisonFleckDef;
        public IntRange poisonFleckIntervalTicks = new IntRange(35, 80);
        public FloatRange poisonFleckScaleRange = new FloatRange(0.45f, 0.75f);
        public float poisonFleckPositionJitter = 0.35f;

        public HediffCompProperties_StatusEffectCorroded()
        {
            compClass = typeof(HediffComp_StatusEffectCorroded);
        }
    }

    public class HediffComp_StatusEffectCorroded : HediffComp
    {
        private int nextDamageTick;
        private int nextPoisonFleckTick;

        private HediffCompProperties_StatusEffectCorroded PropsCorroded => (HediffCompProperties_StatusEffectCorroded)props;

        public float ArmorMultiplier => PropsCorroded.armorMultiplier;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            int now = Find.TickManager.TicksGame;
            ScheduleNextDamage(now);
            ScheduleNextPoisonFleck(now);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref nextDamageTick, "nextDamageTick", 0);
            Scribe_Values.Look(ref nextPoisonFleckTick, "nextPoisonFleckTick", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (nextDamageTick <= 0)
            {
                ScheduleNextDamage(now);
            }
            else if (now >= nextDamageTick)
            {
                ApplyCorrosionDamage();
                ScheduleNextDamage(now);
            }

            if (Pawn.Spawned && Pawn.MapHeld != null)
            {
                if (nextPoisonFleckTick <= 0)
                {
                    ScheduleNextPoisonFleck(now);
                }
                else if (now >= nextPoisonFleckTick)
                {
                    ThrowPoisonFleck();
                    ScheduleNextPoisonFleck(now);
                }
            }
        }

        private void ApplyCorrosionDamage()
        {
            if (PropsCorroded.damageDef == null || PropsCorroded.damageAmount <= 0f)
            {
                return;
            }

            DamageInfo dinfo = new DamageInfo(PropsCorroded.damageDef, PropsCorroded.damageAmount, PropsCorroded.armorPenetration);
            dinfo.SetAllowDamagePropagation(false);
            Pawn.TakeDamage(dinfo);
        }

        private void ThrowPoisonFleck()
        {
            FleckDef fleckDef = PropsCorroded.poisonFleckDef ?? DefDatabase<FleckDef>.GetNamedSilentFail("Fleck_ToxGasSmall") ?? FleckDefOf.Smoke;
            Vector3 drawPos = Pawn.DrawPos;
            float jitter = Mathf.Max(0f, PropsCorroded.poisonFleckPositionJitter);
            drawPos.x += Rand.Range(-jitter, jitter);
            drawPos.z += Rand.Range(-jitter, jitter);
            if (!drawPos.ShouldSpawnMotesAt(Pawn.MapHeld))
            {
                return;
            }

            FleckCreationData data = FleckMaker.GetDataStatic(drawPos, Pawn.MapHeld, fleckDef, PropsCorroded.poisonFleckScaleRange.RandomInRange);
            data.rotationRate = Rand.Range(-25f, 25f);
            data.velocityAngle = Rand.Range(35f, 145f);
            data.velocitySpeed = Rand.Range(0.10f, 0.24f);
            Pawn.MapHeld.flecks.CreateFleck(data);
        }

        private void ScheduleNextDamage(int now)
        {
            nextDamageTick = now + Mathf.Max(1, PropsCorroded.damageIntervalTicks);
        }

        private void ScheduleNextPoisonFleck(int now)
        {
            nextPoisonFleckTick = now + Mathf.Max(1, PropsCorroded.poisonFleckIntervalTicks.RandomInRange);
        }
    }
}
