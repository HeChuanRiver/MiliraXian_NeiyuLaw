using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_AttachedMote : HediffCompProperties
    {
        public ThingDef moteDef;
        public Vector3 offset = Vector3.zero;
        public float scale = 1f;

        public HediffCompProperties_AttachedMote()
        {
            compClass = typeof(HediffComp_AttachedMote);
        }
    }

    public class HediffComp_AttachedMote : HediffComp
    {
        private Mote mote;

        private HediffCompProperties_AttachedMote PropsAttachedMote => (HediffCompProperties_AttachedMote)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            SpawnMote();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            MaintainMote();
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            DestroyMote();
        }

        private void SpawnMote()
        {
            if (mote != null && !mote.Destroyed)
            {
                return;
            }

            if (Pawn?.Spawned != true || Pawn.MapHeld == null || PropsAttachedMote.moteDef == null)
            {
                return;
            }

            mote = MoteMaker.MakeAttachedOverlay(Pawn, PropsAttachedMote.moteDef, PropsAttachedMote.offset, PropsAttachedMote.scale, -1f);
        }

        private void MaintainMote()
        {
            if (mote == null || mote.Destroyed)
            {
                SpawnMote();
                return;
            }

            mote.Maintain();
        }

        private void DestroyMote()
        {
            if (mote != null && !mote.Destroyed)
            {
                mote.Destroy(DestroyMode.Vanish);
            }

            mote = null;
        }
    }
}
