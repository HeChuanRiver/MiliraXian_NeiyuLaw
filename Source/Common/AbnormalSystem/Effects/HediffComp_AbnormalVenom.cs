using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_AbnormalVenom : HediffCompProperties
    {
        public DamageDef damageDef;
        public int damageIntervalTicks = 120;
        public float damageAmount = 4f;
        public float armorPenetration = 0f;
        public FleckDef poisonFleckDef;
        public IntRange poisonFleckIntervalTicks = new IntRange(30, 70);
        public FloatRange poisonFleckScaleRange = new FloatRange(0.55f, 0.95f);
        public float poisonFleckPositionJitter = 0.35f;

        public HediffCompProperties_AbnormalVenom()
        {
            compClass = typeof(HediffComp_AbnormalVenom);
        }
    }

    public class HediffComp_AbnormalVenom : HediffComp
    {
        private int nextDamageTick;
        private int nextPoisonFleckTick;

        private HediffCompProperties_AbnormalVenom PropsVenom => (HediffCompProperties_AbnormalVenom)props;

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
                ApplyPoisonDamage();
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

        private void ApplyPoisonDamage()
        {
            if (PropsVenom.damageDef == null || PropsVenom.damageAmount <= 0f || Pawn.RaceProps?.IsFlesh != true)
            {
                return;
            }

            DamageInfo dinfo = new DamageInfo(PropsVenom.damageDef, PropsVenom.damageAmount, PropsVenom.armorPenetration);
            dinfo.SetAllowDamagePropagation(false);
            Pawn.TakeDamage(dinfo);
        }

        private void ThrowPoisonFleck()
        {
            FleckDef fleckDef = PropsVenom.poisonFleckDef ?? DefDatabase<FleckDef>.GetNamedSilentFail("Fleck_ToxGasSmall") ?? FleckDefOf.Smoke;
            Vector3 drawPos = Pawn.DrawPos;
            float jitter = Mathf.Max(0f, PropsVenom.poisonFleckPositionJitter);
            drawPos.x += Rand.Range(-jitter, jitter);
            drawPos.z += Rand.Range(-jitter, jitter);
            if (!drawPos.ShouldSpawnMotesAt(Pawn.MapHeld))
            {
                return;
            }

            FleckCreationData data = FleckMaker.GetDataStatic(drawPos, Pawn.MapHeld, fleckDef, PropsVenom.poisonFleckScaleRange.RandomInRange);
            data.rotationRate = Rand.Range(-25f, 25f);
            data.velocityAngle = Rand.Range(35f, 145f);
            data.velocitySpeed = Rand.Range(0.12f, 0.28f);
            Pawn.MapHeld.flecks.CreateFleck(data);
        }

        private void ScheduleNextDamage(int now)
        {
            nextDamageTick = now + Mathf.Max(1, PropsVenom.damageIntervalTicks);
        }

        private void ScheduleNextPoisonFleck(int now)
        {
            nextPoisonFleckTick = now + Mathf.Max(1, PropsVenom.poisonFleckIntervalTicks.RandomInRange);
        }
    }
}
