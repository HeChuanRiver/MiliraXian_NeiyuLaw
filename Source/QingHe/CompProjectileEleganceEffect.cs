using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_ProjectileEleganceEffect : CompProperties
    {
        public float eleganceGainOnHit = 0f;
        public bool requireHostileTarget = true;

        public CompProperties_ProjectileEleganceEffect()
        {
            compClass = typeof(CompProjectileEleganceEffect);
        }
    }

    public class CompProjectileEleganceEffect : ThingComp
    {
        private CompProperties_ProjectileEleganceEffect Props => (CompProperties_ProjectileEleganceEffect)props;
        private Projectile ProjectileParent => parent as Projectile;

        public void NotifyImpact(Thing hitThing, bool blockedByShield)
        {
            if (Props == null)
            {
                return;
            }

            Pawn caster = ProjectileParent != null ? ProjectileParent.Launcher as Pawn : null;
            if (blockedByShield || hitThing == null || caster == null)
            {
                return;
            }

            if (Props.requireHostileTarget && !caster.HostileTo(hitThing))
            {
                return;
            }

            if (Props.eleganceGainOnHit > 0f)
            {
                EleganceUtility.AddElegance(caster, Props.eleganceGainOnHit);
            }
        }
    }
}
