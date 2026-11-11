using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Mingyuan;

public sealed class DamageWorker_MingyuanCombustionVisual : DamageWorker
{
    // The volley applies its own damage and Life Burn with hostile-only targeting.
    protected override void ExplosionDamageThing(Explosion explosion, Thing thing, List<Thing> damagedThings,
        List<Thing> ignoredThings, IntVec3 cell)
    {
    }

    internal static void Play(IntVec3 cell, Map map, float radius)
    {
        DamageDef visualDef = MX_MingyuanDefOf.MX_Mingyuan_CombustionVisualExplosion;
        // The native worker reads the map from a spawned Explosion. Run its presentation
        // hooks directly to avoid the Explosion tick's terrain changes and nearby notifications.
        var explosion = (Explosion)GenSpawn.Spawn(ThingDefOf.Explosion, cell, map);
        explosion.radius = radius;
        explosion.damType = visualDef;

        Rand.PushState();
        try
        {
            DamageWorker worker = visualDef.Worker;
            List<IntVec3> cells = worker.ExplosionCellsToHit(explosion).ToList();
            worker.ExplosionStart(explosion, cells);
            visualDef.soundExplosion.PlayOneShot(new TargetInfo(cell, map));
            FleckMaker.WaterSplash(cell.ToVector3Shifted(), map, radius * 6f, 20f);
            foreach (IntVec3 blastCell in cells)
                worker.ExplosionAffectCell(explosion, blastCell, damagedThings: null, ignoredThings: null, canThrowMotes: true);
        }
        finally
        {
            Rand.PopState();
            explosion.Destroy();
        }
    }
}
