using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_YangChunField : CompProperties
    {
        public float radius = 7.9f;
        public int pulseIntervalTicks = 30;

        public DamageDef enemyDamageDef = null;
        public float enemyDamageAmount = 2f;
        public float enemyArmorPenetration = 0.15f;
        public float enemyDesyncedDamageAmount = 1f;

        public HediffDef allyBuffHediff;
        public float allyBuffSeverity = 1f;
        public int allyBuffDurationTicks = 300;
        public float allyInstantHeal = 0.8f;

        public HediffDef enemyDebuffHediff;
        public float enemyDebuffSeverity = 1f;
        public int enemyDebuffDurationTicks = 300;

        public float eleganceGainFlat = 6f;
        public float eleganceGainPerAlly = 0.8f;
        public float eleganceGainPerEnemy = 1.5f;

        public CompProperties_YangChunField()
        {
            compClass = typeof(CompYangChunField);
        }
    }

    public class CompYangChunField : ThingComp
    {
        private Pawn caster;
        private int ticksToNextPulse;

        private CompProperties_YangChunField Props
        {
            get { return (CompProperties_YangChunField)props; }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Destroyed || parent.Map == null)
            {
                return;
            }

            if (caster == null || caster.Dead || !caster.Spawned)
            {
                parent.Destroy();
                return;
            }

            if (ticksToNextPulse <= 0)
            {
                ticksToNextPulse = Props.pulseIntervalTicks;
                ApplyPulse();
            }

            ticksToNextPulse--;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster", false);
            Scribe_Values.Look(ref ticksToNextPulse, "ticksToNextPulse", 0, false);
        }

        public void Init(Pawn newCaster)
        {
            caster = newCaster;
            ticksToNextPulse = 1;
        }

        private void ApplyPulse()
        {
            int allyCount = 0;
            int enemyCount = 0;
            bool shouldRefreshDecayTimer = false;
            DamageDef resolvedEnemyDamageDef = Props.enemyDamageDef ?? MX_QHDefOf.MX_Dehydrate ?? DamageDefOf.Blunt;
            DamageDef desyncedDamageDef = MX_QHDefOf.MX_Desynced ?? DamageDefOf.Blunt;

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.radius, true))
            {
                Pawn pawn = thing as Pawn;
                if (pawn == null || pawn.Dead || pawn.Destroyed || pawn == caster)
                {
                    continue;
                }

                bool hostile = GenHostility.HostileTo(caster, pawn);
                if (hostile)
                {
                    if (Props.enemyDamageAmount > 0f)
                    {
                        DamageWorker.DamageResult damageResult = pawn.TakeDamage(new DamageInfo(resolvedEnemyDamageDef, Props.enemyDamageAmount, Props.enemyArmorPenetration, -1f, caster));
                        if (damageResult != null && damageResult.totalDamageDealt > 0f)
                        {
                            shouldRefreshDecayTimer = true;
                        }
                    }

                    if (Props.enemyDesyncedDamageAmount > 0f)
                    {
                        DamageWorker.DamageResult damageResult = pawn.TakeDamage(new DamageInfo(desyncedDamageDef, Props.enemyDesyncedDamageAmount, Props.enemyArmorPenetration, -1f, caster));
                        if ((damageResult != null && damageResult.totalDamageDealt > 0f) || desyncedDamageDef == MX_QHDefOf.MX_Desynced)
                        {
                            shouldRefreshDecayTimer = true;
                        }
                    }

                    MX_QHUtility.TryApplyOrRefreshHediff(pawn, Props.enemyDebuffHediff, Props.enemyDebuffSeverity, Props.enemyDebuffDurationTicks);
                    enemyCount++;
                }
                else
                {
                    bool allyEffectApplied = false;
                    if (Props.allyInstantHeal > 0f)
                    {
                        MX_QHUtility.HealInjuries(pawn, Props.allyInstantHeal);
                        allyEffectApplied = true;
                    }

                    if (Props.allyBuffHediff != null)
                    {
                        MX_QHUtility.TryApplyOrRefreshHediff(pawn, Props.allyBuffHediff, Props.allyBuffSeverity, Props.allyBuffDurationTicks);
                        allyEffectApplied = true;
                    }

                    if (allyEffectApplied)
                    {
                        shouldRefreshDecayTimer = true;
                    }

                    allyCount++;
                }
            }

            if (shouldRefreshDecayTimer)
            {
                EleganceUtility.NotifyDecayEvent(caster);
            }

            float gain = Props.eleganceGainFlat + allyCount * Props.eleganceGainPerAlly + enemyCount * Props.eleganceGainPerEnemy;
            EleganceUtility.AddElegance(caster, gain);
        }
    }
}
