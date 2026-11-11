using MiliraXian.Characters.Common;
using MiliraXian.Characters.Common.SweepDamage;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    /// <summary>
    /// A Flower Bell note. It flies the homing curve of <see cref="ProjectileHomingCurveBase"/>
    /// unchanged; it does not draw itself but reports its place (with the vanilla arc height) to
    /// <see cref="FlowerBellVfx"/> every tick. On impact it starts the expanding damage ring
    /// (<see cref="FlowerBellWave"/>).
    /// </summary>
    public class Projectile_FlowerBell : ProjectileHomingCurveBase
    {
        private const string SweepDefName = "MX_QH_FlowerBellWave";
        private static SweepDamageDef sweepDef;

        private int degree = -1;
        // The note's own damage from the charge; damage < 0 = the def's.
        private float noteDamage = -1f;
        private float notePenetrationFactor = 1f;
        private float noteRadius = FlowerBellWave.Radius;
        private float noteAbnormalFactor = 1f;
        private float liftStart;
        private int launchTick;
        private FlowerBellVfx.Note note;
        private const int LiftTicks = 9;

        /// <summary>The note's degree in the run's mode; rung on impact. -1 = none.</summary>
        public int Degree => degree;

        public override int DamageAmount => noteDamage >= 0f ? Mathf.Max(1, GenMath.RoundRandom(noteDamage)) : base.DamageAmount;

        public override float ArmorPenetration => base.ArmorPenetration * notePenetrationFactor;

        /// <summary>Full radius of this note's shock wave.</summary>
        public float WaveRadius => noteRadius;

        internal Vector3 LaunchDirection
        {
            get
            {
                Vector3 d = (destination - origin).Yto0();
                return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
            }
        }

        /// <summary>
        /// Where the note is seen: the ground position plus the vanilla arc height shifted up the
        /// screen (the formula of <c>Projectile.DrawAt</c>; its ArcHeightFactor is private).
        /// </summary>
        internal Vector3 VisualPosition
        {
            get
            {
                float arc = def.projectile.arcHeightFactor;
                float span = (destination - origin).MagnitudeHorizontalSquared();
                if (arc * arc > span * 0.2f * 0.2f)
                {
                    arc = Mathf.Sqrt(span) * 0.2f;
                }
                return ExactPosition + Vector3.forward * (arc * GenMath.InverseParabola(DistanceCoveredFractionArc) + LaunchLift);
            }
        }

        /// <summary>
        /// A note leaves from where it hovered, <see cref="liftStart"/> above the ground: the height
        /// eases down to the plain arc over <see cref="LiftTicks"/> so it does not jump on release.
        /// </summary>
        private float LaunchLift
        {
            get
            {
                if (liftStart <= 0f)
                {
                    return 0f;
                }
                float u = Mathf.Clamp01((Find.TickManager.TicksGame - launchTick) / (float)LiftTicks);
                float s = 1f - u;
                return liftStart * s * s;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref degree, "mx_noteDegree", -1);
            Scribe_Values.Look(ref liftStart, "mx_noteLift", 0f);
            Scribe_Values.Look(ref launchTick, "mx_noteLaunchTick", 0);
            Scribe_Values.Look(ref noteDamage, "mx_noteDamage", -1f);
            Scribe_Values.Look(ref notePenetrationFactor, "mx_notePen", 1f);
            Scribe_Values.Look(ref noteRadius, "mx_noteRadius", FlowerBellWave.Radius);
            Scribe_Values.Look(ref noteAbnormalFactor, "mx_noteAbnormal", 1f);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad)
            {
                // The picture is not saved: a loaded note starts its trail where it is.
                note = FlowerBellVfx.Track(this);
            }
        }

        public override void Launch(
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false,
            Thing equipment = null,
            ThingDef targetCoverDef = null)
        {
            degree = MiliraXian.Characters.QingHe.Verbs.Verb_ShootFlowerBell.CastingDegree;
            var charge = MiliraXian.Characters.QingHe.Verbs.Verb_ShootFlowerBell.CastingCharge;
            int tier = MiliraXian.Characters.QingHe.Verbs.Verb_ShootFlowerBell.CastingTier;
            if (charge != null && tier >= 1)
            {
                noteDamage = charge.NoteDamage(launcher as Pawn, tier);
                notePenetrationFactor = charge.NotePenetrationFactor(tier);
                noteRadius = charge.NoteRadius(tier);
                noteAbnormalFactor = charge.NoteAbnormalFactor(tier);
            }
            if (MiliraXian.Characters.QingHe.Verbs.Verb_ShootFlowerBell.CastingOrigin is Vector3 hover)
            {
                origin = new Vector3(hover.x, origin.y, hover.z);
                liftStart = MiliraXian.Characters.QingHe.Verbs.Verb_ShootFlowerBell.CastingLift;
            }
            launchTick = Find.TickManager.TicksGame;
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            note = FlowerBellVfx.Track(this);
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Destroyed && note != null)
            {
                note.AddPoint(VisualPosition, Find.TickManager.TicksGame);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // Drawn by FlowerBellVfx.
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;
            Vector3 seenAt = VisualPosition;
            Thing resolvedHitThing = ResolveImpactHitThing(hitThing, impactPos, map);
            int now = Find.TickManager.TicksGame;

            // The wave stands on the ground under the note: under a struck pawn's feet, else at the
            // impact point (also where a shield stopped it).
            // The ground ring is the damage reach, so the ring and the damage share this centre.
            Vector3 centre = impactPos.Yto0();
            if (resolvedHitThing is Pawn pawn && pawn.Spawned)
            {
                centre = pawn.DrawPos.Yto0() + Vector3.back * PawnFootOffset;
            }
            note?.Impact(seenAt.Yto0(), centre, now);
            if (map != null && map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(ImpactShake);
            }
            if (map != null && degree >= 0)
            {
                // Each note rings its own degree on impact.
                MiliraXian.Characters.QingHe.Things.Weapons.FlowerBellChimes.Ring(map, impactPos.ToIntVec3(), degree, 0.7f);
            }

            if (map != null && !blockedByShield)
            {
                sweepDef ??= DefDatabase<SweepDamageDef>.GetNamedSilentFail(SweepDefName);
                if (sweepDef != null)
                {
                    MapComponent_SweepDamage.AddRing(map, new ExpandingRingDamage
                    {
                        def = sweepDef,
                        caster = launcher as Pawn,
                        weaponDef = equipmentDef,
                        damageDef = DamageDef,
                        projectileDef = def,
                        center = centre,
                        damage = DamageAmount,
                        armorPenetration = ArmorPenetration,
                        startTick = now,
                        radii = FlowerBellWave.Radii(noteRadius),
                        abnormalFactor = noteAbnormalFactor,
                        guaranteedTarget = resolvedHitThing,
                        // The attacked thing is hit whatever its side, as any vanilla attack on a
                        // selected target.
                        primaryTarget = intendedTarget.Thing,
                    });
                }
            }

            base.Impact(null, blockedByShield);
        }

        /// <summary>A pawn's drawn centre to the ground under it.</summary>
        private const float PawnFootOffset = 0.3f;
        /// <summary>Camera shake of an impact.</summary>
        private const float ImpactShake = 0.02f;
    }
}
