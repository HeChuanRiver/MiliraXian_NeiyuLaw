using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx;

internal static class VanillaExplosionVfx
{
    internal static void Play(IntVec3 cell, Map map, float radius, DamageDef damageDef, int seed)
    {
        if (map == null || !cell.InBounds(map) || radius <= 0f) return;

        // Use the vanilla blast footprint and flecks without notifying things of an explosion.
        Vector3 center = cell.ToVector3Shifted();
        Rand.PushState(seed);
        try
        {
            FleckMaker.Static(center, map, FleckDefOf.ExplosionFlash, radius * 6f);
            for (int i = 0; i < 4; i++)
                FleckMaker.ThrowSmoke(center + Gen.RandomHorizontalVector(radius * 0.7f), map, radius * 0.6f);

            if (damageDef.explosionCellFleck is { } blast)
            {
                foreach (IntVec3 blastCell in damageDef.Worker.ExplosionCellsToHit(cell, map, radius))
                {
                    float distance = (blastCell - cell).LengthHorizontal;
                    Color color = Color.Lerp(damageDef.explosionColorCenter, damageDef.explosionColorEdge, distance / radius);
                    FleckMaker.ThrowExplosionCell(blastCell, map, blast, color);
                }
            }
        }
        finally
        {
            Rand.PopState();
        }

        if (map != Find.CurrentMap) return;
        Camera camera = Find.Camera;
        if (camera == null) return;
        float cameraDistance = Vector3.Distance(center, camera.transform.position);
        Find.CameraDriver?.shaker.DoShake(4f * radius / Mathf.Max(1f, cameraDistance));
    }
}
