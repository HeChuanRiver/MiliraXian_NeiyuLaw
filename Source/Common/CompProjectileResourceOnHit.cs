using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class ResourceHitEntry : IExposable
    {
        public HediffDef resourceDef;
        public float amount;
        public bool requireHostileTarget = true;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref resourceDef, "resourceDef");
            Scribe_Values.Look(ref amount, "amount", 0f);
            Scribe_Values.Look(ref requireHostileTarget, "requireHostileTarget", true);
        }
    }

    public class CompProperties_ProjectileResourceOnHit : CompProperties
    {
        public List<ResourceHitEntry> entries;

        public CompProperties_ProjectileResourceOnHit()
        {
            compClass = typeof(CompProjectileResourceOnHit);
        }
    }

    public class CompProjectileResourceOnHit : ThingComp
    {
        public CompProperties_ProjectileResourceOnHit Props => (CompProperties_ProjectileResourceOnHit)props;
        private Projectile ProjectileParent => parent as Projectile;

        public void NotifyImpact(Thing hitThing, bool blockedByShield)
        {
            if (Props?.entries == null)
            {
                return;
            }

            Pawn caster = ProjectileParent?.Launcher as Pawn;
            if (blockedByShield || hitThing == null || caster == null)
            {
                return;
            }

            foreach (var entry in Props.entries)
            {
                if (entry.requireHostileTarget && !GenHostility.HostileTo(caster, hitThing))
                {
                    continue;
                }

                ResourceTransactionUtility.ApplyTransaction(caster, new ResourceTransactionEntry
                {
                    resourceDef = entry.resourceDef,
                    amount = entry.amount
                });
            }
        }
    }
}
