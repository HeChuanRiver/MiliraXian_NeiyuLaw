using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerBellShortCircuit : HediffCompProperties
    {
        public DamageDef explosionDamageDef;
        public float explosionRadius = 1.5f;
        public SimpleCurve explosionRadiusMultiplierByBodySize;
        public int explosionDamageAmount = 8;
        public float explosionArmorPenetration = 0f;
        public int stunTicks = 180;
        public int interruptCooldownTicks = 30;
        public float energyLossPercent = 0.20f;
        public FleckDef steamFleckDef;
        public IntRange steamIntervalTicks = new IntRange(45, 90);
        public FloatRange steamScaleRange = new FloatRange(0.55f, 0.9f);
        public float steamPositionJitter = 0.28f;

        public HediffCompProperties_FlowerBellShortCircuit()
        {
            compClass = typeof(HediffComp_FlowerBellShortCircuit);
        }
    }

    public class HediffComp_FlowerBellShortCircuit : HediffComp
    {
        private bool triggered;
        private int nextSteamTick;

        private HediffCompProperties_FlowerBellShortCircuit PropsShortCircuit => (HediffCompProperties_FlowerBellShortCircuit)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            Trigger(dinfo?.Instigator);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref triggered, "triggered", false);
            Scribe_Values.Look(ref nextSteamTick, "nextSteamTick", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn?.Spawned != true || Pawn.MapHeld == null)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (nextSteamTick <= 0)
            {
                ScheduleNextSteam(now);
                return;
            }

            if (now >= nextSteamTick)
            {
                ThrowSteam();
                ScheduleNextSteam(now);
            }
        }

        private void Trigger(Thing instigator)
        {
            if (triggered || Pawn == null || Pawn.Dead)
            {
                return;
            }

            triggered = true;
            DoExplosion(instigator);
            InterruptPawnAction();
            if (PropsShortCircuit.stunTicks > 0 && Pawn.Spawned)
            {
                Pawn.stances?.stunner?.StunFor(PropsShortCircuit.stunTicks, instigator, addBattleLog: true, showMote: true, disableRotation: true);
            }

            MX_QHUtility.ReduceMechEnergyNeed(Pawn, PropsShortCircuit.energyLossPercent);
        }

        private void InterruptPawnAction()
        {
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            Pawn.jobs?.StopAll(false, true);
            Pawn.stances?.CancelBusyStanceHard();
            if (PropsShortCircuit.interruptCooldownTicks > 0)
            {
                Pawn.stances?.SetStance(new Stance_Cooldown(PropsShortCircuit.interruptCooldownTicks, Pawn, null)
                {
                    neverAimWeapon = true
                });
            }
        }

        private void DoExplosion(Thing instigator)
        {
            if (Pawn?.Spawned != true || Pawn.MapHeld == null || PropsShortCircuit.explosionDamageDef == null || PropsShortCircuit.explosionRadius <= 0f)
            {
                return;
            }

            GenExplosion.DoExplosion(
                Pawn.PositionHeld,
                Pawn.MapHeld,
                GetExplosionRadius(),
                PropsShortCircuit.explosionDamageDef,
                instigator,
                PropsShortCircuit.explosionDamageAmount,
                PropsShortCircuit.explosionArmorPenetration);
        }

        private float GetExplosionRadius()
        {
            float radius = PropsShortCircuit.explosionRadius;
            SimpleCurve curve = PropsShortCircuit.explosionRadiusMultiplierByBodySize;
            if (curve != null)
            {
                radius *= Mathf.Max(0f, curve.Evaluate(Mathf.Max(0f, Pawn.BodySize)));
            }

            return Mathf.Max(0f, radius);
        }

        private void ThrowSteam()
        {
            FleckDef fleckDef = PropsShortCircuit.steamFleckDef ?? DefDatabase<FleckDef>.GetNamedSilentFail("Steam") ?? FleckDefOf.Smoke;
            Vector3 drawPos = Pawn.DrawPos;
            float jitter = Mathf.Max(0f, PropsShortCircuit.steamPositionJitter);
            drawPos.x += Rand.Range(-jitter, jitter);
            drawPos.z += Rand.Range(-jitter, jitter);
            if (!drawPos.ShouldSpawnMotesAt(Pawn.MapHeld))
            {
                return;
            }

            FleckCreationData data = FleckMaker.GetDataStatic(drawPos, Pawn.MapHeld, fleckDef, PropsShortCircuit.steamScaleRange.RandomInRange);
            data.rotationRate = Rand.Range(-20f, 20f);
            data.velocityAngle = Rand.Range(25f, 55f);
            data.velocitySpeed = Rand.Range(0.25f, 0.45f);
            Pawn.MapHeld.flecks.CreateFleck(data);
        }

        private void ScheduleNextSteam(int now)
        {
            IntRange interval = PropsShortCircuit.steamIntervalTicks;
            nextSteamTick = now + Mathf.Max(1, interval.RandomInRange);
        }
    }
}
