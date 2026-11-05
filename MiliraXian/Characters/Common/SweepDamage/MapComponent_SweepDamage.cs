using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.Common.SweepDamage
{
    /// <summary>Advances every active <see cref="SweepDamageSource"/> on the map once per tick. Saved with the map.</summary>
    public sealed class MapComponent_SweepDamage : MapComponent
    {
        private List<SweepDamageSource> sources = new();

        public MapComponent_SweepDamage(Map map) : base(map)
        {
        }

        public static void Add(Map map, SweepDamageSource source)
        {
            if (map == null || source?.def == null || source.headings == null || source.headings.Count < 2)
            {
                return;
            }
            map.GetComponent<MapComponent_SweepDamage>()?.sources.Add(source);
        }

        public override void MapComponentTick()
        {
            if (sources.Count == 0)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                SweepDamageSource s = sources[i];
                s.Tick(map, now);
                if (s.Finished)
                {
                    sources.RemoveAt(i);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref sources, "mx_sweepDamageSources", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                sources ??= new List<SweepDamageSource>();
                sources.RemoveAll(s => s?.def == null);
            }
        }
    }
}
