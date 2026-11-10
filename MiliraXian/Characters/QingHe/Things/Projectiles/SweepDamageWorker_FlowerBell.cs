using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.QingHe.Things.Weapons;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    /// <summary>
    /// The shock wave of a note: the plain hit of the ring, then the note's season abnormals on a
    /// struck pawn (read from the projectile def's <see cref="CompProperties_FlowerBellStatusOnHit"/>).
    /// </summary>
    public class SweepDamageWorker_FlowerBell : SweepDamageWorker
    {
        public override void ApplyHit(SweepDamageSource source, Thing target)
        {
            base.ApplyHit(source, target);
            if (target is Pawn pawn && !pawn.Dead && source.projectileDef != null)
            {
                CompFlowerBellStatusOnHit.ApplyProjectileAbnormals(source.caster, pawn, source.projectileDef, source.resourceFactor);
            }
        }
    }
}
