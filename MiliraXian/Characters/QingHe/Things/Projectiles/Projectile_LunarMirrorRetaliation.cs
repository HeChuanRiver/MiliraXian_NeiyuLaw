using MiliraXian.Characters.Common.SweepDamage;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    /// <summary>
    /// The frost retaliation of the Lunar Mirror: a small snowflake answered at a projectile. On
    /// impact it opens the same shock wave a bell note does (see <see cref="FlowerBellWave"/>), only
    /// smaller and quicker, and the ring that deals the damage is the ring that is drawn.
    /// </summary>
    public class Projectile_LunarMirrorRetaliation : Bullet
    {
        private const string SweepDefName = "MX_QH_FrostRetaliationWave";

        private static SweepDamageDef sweepDef;

        /// <summary>Full radius of this wave (the def's <c>MX_QH_FrostRetaliationWave</c> owns the damage).</summary>
        public const float WaveRadius = FlowerBellWave.FrostRadius;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;

            if (map != null && !blockedByShield)
            {
                // The wave stands on the ground under the snowflake; a struck pawn's feet are where
                // the picture and the damage agree.
                Vector3 waveCentre = impactPos.Yto0();
                if (hitThing is Pawn pawn && pawn.Spawned)
                {
                    waveCentre = pawn.DrawPos.Yto0() + Vector3.back * PawnFootOffset;
                }

                sweepDef ??= DefDatabase<SweepDamageDef>.GetNamedSilentFail(SweepDefName);
                if (sweepDef != null)
                {
                    int now = Find.TickManager.TicksGame;
                    MapComponent_SweepDamage.AddRing(map, new ExpandingRingDamage
                    {
                        def = sweepDef,
                        caster = launcher as Pawn,
                        weaponDef = equipmentDef,
                        damageDef = DamageDef,
                        projectileDef = def,
                        center = waveCentre,
                        damage = DamageAmount * MX_QH_PerkUtility.GetSpellEffectFactor(launcher as Pawn),
                        armorPenetration = ArmorPenetration,
                        startTick = now,
                        radii = FlowerBellWave.Radii(WaveRadius, FlowerBellWave.FrostRingSeconds),
                        guaranteedTarget = hitThing,
                        // The attacked thing is hit whatever its side, as with the notes.
                        primaryTarget = intendedTarget.Thing,
                    });
                    FrostRetaliationVfx.Notify(map, waveCentre, WaveRadius, now);
                }
            }

            base.Impact(null, blockedByShield);
        }

        /// <summary>A pawn's drawn centre to the ground under it.</summary>
        private const float PawnFootOffset = 0.3f;
    }
}
