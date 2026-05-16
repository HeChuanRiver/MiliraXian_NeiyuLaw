using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_FlowerMandate_PomegranateLifetime : CompProperties
    {
        public int durationTicks = 900;
        public int fadeOutTicks = 45;

        public CompProperties_FlowerMandate_PomegranateLifetime()
        {
            compClass = typeof(CompFlowerMandate_PomegranateLifetime);
        }
    }

    public class CompFlowerMandate_PomegranateLifetime : ThingComp
    {
        private Pawn caster;
        private int ticksLeft;

        public CompProperties_FlowerMandate_PomegranateLifetime Props => (CompProperties_FlowerMandate_PomegranateLifetime)props;
        public float VisualAlpha => Props.fadeOutTicks > 0 ? Mathf.Clamp01(ticksLeft / (float)Props.fadeOutTicks) : 1f;

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Destroyed)
            {
                return;
            }

            ticksLeft--;
            if (ticksLeft <= 0)
            {
                parent.Destroy(DestroyMode.Vanish);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster", false);
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", Props.durationTicks);
        }

        public override Color? ForceColor()
        {
            Color color = parent?.def?.graphicData?.color ?? Color.white;
            color.a *= VisualAlpha;
            return color;
        }

        public void Init(Pawn newCaster, int durationTicks)
        {
            caster = newCaster;
            ticksLeft = durationTicks > 0 ? durationTicks : Props.durationTicks;
        }

        public bool WasSummonedBy(Pawn pawn)
        {
            return caster == pawn;
        }
    }
}
