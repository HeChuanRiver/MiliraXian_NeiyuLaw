using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_YangChunField : CompProperties
    {
        public float radius = 7.9f;
        public int pulseIntervalTicks = 180;

        public DamageDef enemyDamageDef = null;
        public float enemyDamageAmount = 2f;
        public float enemyArmorPenetration = 0.15f;
        public float enemyDesyncedDamageAmount = 1f;
        public float damageFactorMax = 0.5f;

        public HediffDef allyBuffHediff;
        public float allyBuffSeverity = 1f;
        public int allyBuffDurationTicks = 300;
        public float allyInstantHeal = 0.8f;
        public float healFactorMax = 1f;
        public float hediffSeverityFactorMax = 1f;

        public HediffDef enemyDebuffHediff;
        public float enemyDebuffSeverity = 1f;
        public int enemyDebuffDurationTicks = 300;

        public float eleganceGainFlat = 6f;
        public float eleganceGainPerAlly = 0.8f;
        public float eleganceGainPerEnemy = 1.5f;
        public float eleganceGainPerTick = 0.01f;

        public string startFx = "MX_QH_Effecter_YangChunStart";
        public float startFxScale = 1.2f;
        public string pulseFx = "MX_QH_Effecter_YangChunPulse";
        public float pulseFxScale = 1.9f;
        public string endFx = "MX_QH_Effecter_YangChunEnd";
        public float endFxScale = 1.05f;

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
                ticksToNextPulse = UnityEngine.Mathf.Max(1, Props.pulseIntervalTicks);
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
            parent.TryGetComp<CompResourceTick>()?.Init(newCaster);
            EleganceUtility.NotifyDecayEvent(newCaster);
        }

        public void SpawnFx()
        {
            GraphicsUtility.Fx(parent.Map, parent.Position, Props.startFx, Props.startFxScale * Props.radius / 7.9f);
        }

        public void EndFx()
        {
            GraphicsUtility.Fx(parent.Map, parent.Position, Props.endFx, Props.endFxScale * Props.radius / 7.9f);
        }

        private void ApplyPulse()
        {
            GraphicsUtility.Fx(parent.Map, parent.Position, Props.pulseFx, Props.pulseFxScale * Props.radius / 7.9f);
            EleganceUtility.NotifyDecayEvent(caster);

            var allyCount = 0;
            var enemyCount = 0;
            var refreshDecay = false;
            var enemyDamageDef = Props.enemyDamageDef ?? MX_QHDefOf.MX_Dehydrate ?? DamageDefOf.Blunt;
            var desyncedDamageDef = MX_QHDefOf.MX_Desynced ?? DamageDefOf.Blunt;
            var damageFactor = EleganceUtility.FactorLinear(Props.damageFactorMax, caster);
            var healFactor = EleganceUtility.FactorLinear(Props.healFactorMax, caster);
            var severityFactor = EleganceUtility.FactorLinear(Props.hediffSeverityFactorMax, caster);

            float radius = UnityEngine.Mathf.Max(0f, Props.radius);
            float radiusSquared = radius * radius;
            var pawns = parent.Map.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn pawn = pawns[index];
                if (pawn == null || pawn.Dead || pawn.Destroyed || pawn == caster)
                {
                    continue;
                }

                int dx = pawn.Position.x - parent.Position.x;
                int dz = pawn.Position.z - parent.Position.z;
                if (dx * dx + dz * dz > radiusSquared)
                {
                    continue;
                }

                if (GenHostility.HostileTo(caster, pawn))
                {
                    if (pawn.Downed)
                    {
                        continue;
                    }

                    var enemyAffected = false;
                    if (Props.enemyDamageAmount > 0f)
                    {
                        var result = pawn.TakeDamage(new DamageInfo(enemyDamageDef, Props.enemyDamageAmount * damageFactor, Props.enemyArmorPenetration, -1f, caster));
                        if (result != null && result.totalDamageDealt > 0f)
                        {
                            refreshDecay = true;
                            enemyAffected = true;
                        }
                    }

                    if (Props.enemyDesyncedDamageAmount > 0f)
                    {
                        var result = pawn.TakeDamage(new DamageInfo(desyncedDamageDef, Props.enemyDesyncedDamageAmount * damageFactor, Props.enemyArmorPenetration, -1f, caster));
                        if ((result != null && result.totalDamageDealt > 0f) || desyncedDamageDef == MX_QHDefOf.MX_Desynced)
                        {
                            refreshDecay = true;
                            enemyAffected = true;
                        }
                    }

                    if (Props.enemyDebuffHediff != null)
                    {
                        MX_QHUtility.TryApplyOrRefreshHediff(pawn, Props.enemyDebuffHediff, Props.enemyDebuffSeverity * severityFactor, Props.enemyDebuffDurationTicks);
                        enemyAffected = true;
                    }

                    if (enemyAffected)
                    {
                        enemyCount++;
                    }

                    continue;
                }

                var allyAffected = false;
                if (Props.allyInstantHeal > 0f)
                {
                    MX_QHUtility.HealInjuries(pawn, Props.allyInstantHeal * healFactor);
                    allyAffected = true;
                }

                if (Props.allyBuffHediff != null)
                {
                    MX_QHUtility.TryApplyOrRefreshHediff(pawn, Props.allyBuffHediff, Props.allyBuffSeverity * severityFactor, Props.allyBuffDurationTicks);
                    allyAffected = true;
                }

                if (!allyAffected)
                {
                    continue;
                }

                refreshDecay = true;
                allyCount++;
            }

            if (refreshDecay)
            {
                EleganceUtility.NotifyDecayEvent(caster);
            }

            if (allyCount > 0 || enemyCount > 0)
            {
                EleganceUtility.AddElegance(caster, Props.eleganceGainFlat + allyCount * Props.eleganceGainPerAlly + enemyCount * Props.eleganceGainPerEnemy);
            }
        }
    }
}
