using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters
{
    public class CompProperties_ResourceTick : CompProperties
    {
        public List<ResourceTransactionEntry> entries;

        public CompProperties_ResourceTick()
        {
            compClass = typeof(CompResourceTick);
        }
    }

    public class CompResourceTick : ThingComp
    {
        public CompProperties_ResourceTick Props => (CompProperties_ResourceTick)props;

        private Pawn caster;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster");
        }

        public void Init(Pawn newCaster)
        {
            caster = newCaster;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (caster == null || caster.Dead)
            {
                return;
            }

            if (Props.entries != null)
            {
                foreach (var entry in Props.entries)
                {
                    ResourceTransactionUtility.ApplyTransaction(caster, entry);
                }
            }
        }
    }
}
