using MiliraXian.Characters.QingHe.Things.Weapons;
using Verse;

namespace MiliraXian.Characters.QingHe.Verbs
{
    public class Verb_ShootFlowerBell : Verb_Shoot
    {
        public override ThingDef Projectile
        {
            get
            {
                ThingDef projectile = EquipmentSource?.TryGetComp<CompFlowerBellResonance>()?.CurrentProjectile;
                return projectile ?? base.Projectile;
            }
        }
    }
}
