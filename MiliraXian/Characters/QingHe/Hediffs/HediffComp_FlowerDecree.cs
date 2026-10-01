using UnityEngine;
using Verse;
using MiliraXian.Characters.Common;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerDecree : HediffCompProperties_PawnSpecialResource
    {
        public int highlightTicks = 90;

        public HediffCompProperties_FlowerDecree()
        {
            compClass = typeof(HediffComp_FlowerDecree);
        }
    }

    public class HediffComp_FlowerDecree : HediffComp_PawnSpecialResource
    {
        // Display only: the bar flashes the segment that just filled. Not saved, and tracked here
        // rather than in the widget because gizmos are rebuilt every frame.
        private int lastSegmentCount = -1;
        private int highlightTick = -1;

        public HediffCompProperties_FlowerDecree PropsDecree => (HediffCompProperties_FlowerDecree)props;

        public float SegmentHighlightPercent
        {
            get
            {
                if (highlightTick < 0)
                {
                    return 0f;
                }

                int elapsed = (Find.TickManager?.TicksGame ?? 0) - highlightTick;
                int duration = Mathf.Max(1, PropsDecree.highlightTicks);
                return elapsed < 0 || elapsed >= duration ? 0f : 1f - elapsed / (float)duration;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            int segments = Mathf.FloorToInt(CurrentValue);
            if (segments > lastSegmentCount && lastSegmentCount >= 0)
            {
                highlightTick = Find.TickManager?.TicksGame ?? 0;
            }

            lastSegmentCount = segments;
        }
    }
}
