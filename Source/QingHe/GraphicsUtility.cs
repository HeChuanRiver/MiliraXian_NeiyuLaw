using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class GraphicsUtility
    {
        public static void Fx(Map map, IntVec3 cell, string defName, float scale = 1f)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map) || defName.NullOrEmpty())
            {
                return;
            }

            var def = DefDatabase<EffecterDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return;
            }

            var fx = def.Spawn(cell, map, Mathf.Max(0.01f, scale));
            fx?.Trigger(new TargetInfo(cell, map), new TargetInfo(cell, map));
            fx?.Cleanup();
        }

        public static void Fleck(Map map, IntVec3 cell, string defName, float scale = 1f)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map) || defName.NullOrEmpty())
            {
                return;
            }

            var def = DefDatabase<FleckDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return;
            }

            FleckMaker.Static(cell, map, def, Mathf.Max(0.01f, scale));
        }

        public static void Overlay(Map map, TargetInfo source, TargetInfo target, string defName)
        {
            if (map == null || defName.NullOrEmpty() || !source.IsValid || !target.IsValid)
            {
                return;
            }

            if (source.Map != map || target.Map != map)
            {
                return;
            }

            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return;
            }

            MoteMaker.MakeInteractionOverlay(def, source, target);
        }

        public static bool Mote(Map map, IntVec3 cell, Thing mote, WipeMode wipeMode = WipeMode.Vanish)
        {
            if (map == null || mote == null || !cell.IsValid || !cell.InBounds(map))
            {
                return false;
            }

            GenSpawn.Spawn(mote, cell, map, wipeMode);
            return true;
        }
    }
}