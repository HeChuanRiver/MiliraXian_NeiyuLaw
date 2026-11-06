using RimWorld;
using Verse;
using Verse.AI.Group;

namespace MiliraXian.Characters.Common.Combat;

/// <summary>
/// Damage that nothing before the wound can stop: it skips <see cref="Thing.PreApplyDamage"/> (shield
/// belts, apparel and comp shields, every mod's absorb patch on it, damage factors of hediffs and genes)
/// and armour, and goes straight to the damage worker. What follows the wound is kept: the damage
/// watcher, the attacker's hediffs, filth, records, faction and lord notices and
/// <see cref="Thing.PostApplyDamage"/> (bleeding, death checks). Used for executions.
/// </summary>
public static class DirectDamage
{
    public static DamageWorker.DamageResult Apply(Thing target, DamageInfo dinfo)
    {
        if (target == null || target.Destroyed || dinfo.Def == null || dinfo.Amount <= 0f)
        {
            return new DamageWorker.DamageResult();
        }
        dinfo.SetIgnoreArmor(true);
        dinfo.SetIgnoreInstantKillProtection(true);
        bool spawned = target.SpawnedOrAnyParentSpawned;
        Map map = target.MapHeld;

        if (target is Pawn pawn && spawned)
        {
            // The parts of the vanilla pre-damage step that are notices, not protection.
            pawn.mindState.Active = true;
            pawn.GetLord()?.Notify_PawnDamaged(pawn, dinfo);
            pawn.HomeFaction?.Notify_MemberTookDamage(pawn, dinfo);
        }

        DamageWorker.DamageResult result = dinfo.Def.Worker.Apply(dinfo, target);

        if (dinfo.Def.harmsHealth && spawned)
        {
            map?.damageWatcher.Notify_DamageTaken(target, result.totalDamageDealt);
        }
        if (dinfo.Instigator is Pawn attacker)
        {
            foreach (Hediff hediff in attacker.health.hediffSet.hediffs)
            {
                hediff.Notify_PawnDamagedThing(target, dinfo, result);
            }
            if (dinfo.Def.ExternalViolenceFor(target))
            {
                attacker.records.AddTo(RecordDefOf.DamageDealt, result.totalDamageDealt);
            }
        }
        if (dinfo.Def.ExternalViolenceFor(target) && dinfo.SpawnFilth && !target.Destroyed)
        {
            GenLeaving.DropFilthDueToDamage(target, result.totalDamageDealt);
        }
        if (!target.Destroyed)
        {
            target.PostApplyDamage(dinfo, result.totalDamageDealt);
        }
        return result;
    }
}
