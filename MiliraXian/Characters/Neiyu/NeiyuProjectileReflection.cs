using HarmonyLib;
using MiliraXian.Characters.Common;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu;

// Called by NeiyuSwordBlocker from the shared projectile interception (Common/Combat).
// Projectile has no public in-flight redirect API. Resolve only the native
// trajectory fields once; do not clone, relaunch or replace the projectile.
internal static class NeiyuProjectileReflection
{
    private static readonly AccessTools.FieldRef<Projectile, Vector3> Origin = AccessTools.FieldRefAccess<Projectile, Vector3>("origin");
    private static readonly AccessTools.FieldRef<Projectile, Vector3> Destination = AccessTools.FieldRefAccess<Projectile, Vector3>("destination");
    private static readonly AccessTools.FieldRef<Projectile, Thing> Launcher = AccessTools.FieldRefAccess<Projectile, Thing>("launcher");
    private static readonly AccessTools.FieldRef<Projectile, ThingDef> Cover = AccessTools.FieldRefAccess<Projectile, ThingDef>("targetCoverDef");
    private static readonly AccessTools.FieldRef<Projectile, int> Ticks = AccessTools.FieldRefAccess<Projectile, int>("ticksToImpact");
    private static readonly AccessTools.FieldRef<Projectile, int> Lifetime = AccessTools.FieldRefAccess<Projectile, int>("lifetime");
    private static readonly AccessTools.FieldRef<Projectile, bool> Landed = AccessTools.FieldRefAccess<Projectile, bool>("landed");
    private static readonly AccessTools.FieldRef<Projectile, bool> FriendlyFire = AccessTools.FieldRefAccess<Projectile, bool>("preventFriendlyFire");

    internal static bool TryReflect(Projectile projectile, Pawn defender)
    {
        NeiyuSwordCombatDef c = NeiyuSwordCombat.Config;
        if (!c.reflectProjectiles || projectile is not { Spawned: true, Destroyed: false }
            || !NeiyuSwordCombat.CanAct(defender) || projectile.Map != defender.Map
            || !DebugSettings.enableDamage) return false;
        Thing shooter = projectile.Launcher;
        if (shooter == null || shooter == defender || !GenHostility.HostileTo(shooter, defender)) return false;
        float speed = projectile.def.projectile?.SpeedTilesPerTick ?? 0f;
        if (!float.IsFinite(speed) || speed <= 0f) return false;
        Vector3 contact = defender.DrawPos.Yto0();
        Vector3 returnPoint = shooter.Spawned && shooter.Map == projectile.Map ? shooter.DrawPos.Yto0() : Origin(projectile).Yto0();
        Vector3 back = returnPoint - contact;
        if (back.sqrMagnitude < .01f) back = (Origin(projectile) - Destination(projectile)).Yto0();
        if (back.sqrMagnitude < .01f || !float.IsFinite(back.sqrMagnitude)) return false;
        int outcome = NeiyuSwordCombatRules.Deflection(Rand.Value, c.successChance, c.perfectChance);
        if (outcome == 0) return false;

        back = Quaternion.AngleAxis(Rand.Range(-c.projectileReturnSpreadDegrees, c.projectileReturnSpreadDegrees), Vector3.up) * back;
        Vector3 destination = contact + back;
        destination.x = Mathf.Clamp(destination.x, .05f, projectile.Map.Size.x - .05f);
        destination.z = Mathf.Clamp(destination.z, .05f, projectile.Map.Size.z - .05f);
        Vector3 start = contact + back.normalized * .18f;
        start.x = Mathf.Clamp(start.x, .05f, projectile.Map.Size.x - .05f);
        start.z = Mathf.Clamp(start.z, .05f, projectile.Map.Size.z - .05f);
        int ticks = Mathf.Max(1, Mathf.CeilToInt((destination - start).magnitude / speed));

        Redirect(projectile, defender, shooter, start, destination, ticks);
        NeiyuSwordCombatVfx.Reflect(defender, contact, back, outcome == 2);
        // No posture, stun or opening is applied to the shooter. The existing
        // projectile keeps its Def, equipment, quality, payload, damage override,
        // armor penetration, extra damages, comps and split/bounce state.
        return true;
    }

    internal static void Redirect(Projectile projectile, Pawn defender, Thing shooter, Vector3 start, Vector3 destination, int ticks)
    {
        Origin(projectile) = start;
        Destination(projectile) = destination;
        Ticks(projectile) = ticks;
        Lifetime(projectile) = ticks;
        Landed(projectile) = false;
        Launcher(projectile) = defender; // Native CanHit excludes the new launcher, not the old shooter.
        Cover(projectile) = null;
        FriendlyFire(projectile) = false;
        projectile.HitFlags = ProjectileHitFlags.All;
        // Aim toward the firing direction, without teleporting damage onto a
        // shooter who has since moved away. Homing projectiles retain homing.
        projectile.usedTarget = new LocalTargetInfo(destination.ToIntVec3());
        projectile.intendedTarget = shooter.Spawned && shooter.Map == projectile.Map
            ? new LocalTargetInfo(shooter) : projectile.usedTarget;
        projectile.Position = start.ToIntVec3();
        if (projectile is ProjectileHomingCurveBase homing) homing.NotifyDeflected(start, destination, ticks);
    }
}
