using MiliraXian.Characters.QingHe.Abilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerDance : HediffCompProperties
    {
        public EffecterDef activeEffecter;
        public int afterimageIntervalTicks = 6;
        public int afterimageFadeTicks = 60;
        public float afterimageStartAlpha = 0.44f;
        public float afterimageMinDistance = 0.55f;

        public HediffCompProperties_FlowerDance()
        {
            compClass = typeof(HediffComp_FlowerDance);
        }
    }

    public class HediffComp_FlowerDance : HediffComp
    {
        private int ticksUntilAfterimage;
        private Vector3 lastAfterimageDrawPos = Vector3.zero;
        private Rot4 lastAfterimageFacing = Rot4.Invalid;
        private bool hasLastAfterimageDrawPos;
        private Effecter activeEffecter;

        public HediffCompProperties_FlowerDance Props => (HediffCompProperties_FlowerDance)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            EnsureActiveEffecter();
            TickAfterimages();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            FlowerCourtUtility.GetDivineFortune(Pawn)?.Recalculate();
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            CleanupActiveEffecter();
            ResetAfterimageTracking();
            FlowerCourtUtility.GetDivineFortune(Pawn)?.Recalculate();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksUntilAfterimage, "mx_qh_flowerDance_ticksUntilAfterimage", 0);
            Scribe_Values.Look(ref lastAfterimageDrawPos, "mx_qh_flowerDance_lastAfterimageDrawPos", Vector3.zero);
            Scribe_Values.Look(ref lastAfterimageFacing, "mx_qh_flowerDance_lastAfterimageFacing", Rot4.Invalid);
            Scribe_Values.Look(ref hasLastAfterimageDrawPos, "mx_qh_flowerDance_hasLastAfterimageDrawPos", false);
        }

        public void NotifyRefreshed()
        {
            EnsureActiveEffecter();
            FlowerCourtUtility.GetDivineFortune(Pawn)?.Recalculate();
        }

        private void EnsureActiveEffecter()
        {
            Pawn pawn = Pawn;
            if (activeEffecter != null || Props.activeEffecter == null || pawn == null || !pawn.Spawned || pawn.MapHeld == null)
            {
                return;
            }

            activeEffecter = Props.activeEffecter.SpawnAttached(pawn, pawn.MapHeld, 1f);
        }

        private void CleanupActiveEffecter()
        {
            if (activeEffecter != null)
            {
                activeEffecter.Cleanup();
                activeEffecter = null;
            }
        }

        private void TickAfterimages()
        {
            Pawn pawn = Pawn;
            if (pawn?.Map == null || !pawn.Spawned || pawn.Dead || pawn.Destroyed)
            {
                return;
            }

            int interval = Mathf.Max(1, Props.afterimageIntervalTicks);
            Vector3 drawPos = pawn.DrawPos;
            Rot4 facing = pawn.Rotation;
            if (!hasLastAfterimageDrawPos)
            {
                lastAfterimageDrawPos = drawPos;
                lastAfterimageFacing = facing;
                hasLastAfterimageDrawPos = true;
                ticksUntilAfterimage = interval;
                return;
            }

            ticksUntilAfterimage--;
            if (ticksUntilAfterimage > 0)
            {
                return;
            }

            ticksUntilAfterimage = interval;
            float minDistance = Mathf.Max(0.01f, Props.afterimageMinDistance);
            if ((drawPos - lastAfterimageDrawPos).sqrMagnitude < minDistance * minDistance)
            {
                lastAfterimageFacing = facing;
                return;
            }

            pawn.Map.GetComponent<MapComponent_QingheFlowerDanceVisuals>()?.AddAfterimage(
                pawn,
                drawPos,
                facing.IsValid ? facing : lastAfterimageFacing,
                Mathf.Max(1, Props.afterimageFadeTicks),
                Mathf.Clamp01(Props.afterimageStartAlpha));

            lastAfterimageDrawPos = drawPos;
            lastAfterimageFacing = facing;
        }

        private void ResetAfterimageTracking()
        {
            lastAfterimageDrawPos = Vector3.zero;
            lastAfterimageFacing = Rot4.Invalid;
            hasLastAfterimageDrawPos = false;
            ticksUntilAfterimage = 0;
        }
    }
}
