using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class Hediff_AbnormalFrozen : HediffWithComps
    {
        private const string FreezeMoteDefNameA = "MX_Mote_AbnormalFreeze_A";
        private const string FreezeMoteDefNameB = "MX_Mote_AbnormalFreeze_B";

        private Mote freezeMote;
        private bool interruptedOnAdd;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            InterruptPawnAction();
            StunPawn();
            SpawnFreezeMote();
        }

        public override void Tick()
        {
            base.Tick();
            StunPawn();
            MaintainFreezeMote();
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (freezeMote != null && !freezeMote.Destroyed)
            {
                freezeMote.Destroy(DestroyMode.Vanish);
            }
        }

        private void StunPawn()
        {
            if (pawn == null || pawn.Dead || pawn.stances?.stunner == null)
            {
                return;
            }

            pawn.stances.stunner.StunFor(2, null, addBattleLog: false, showMote: false, disableRotation: true);
        }

        private void InterruptPawnAction()
        {
            if (interruptedOnAdd || pawn == null || pawn.Dead)
            {
                return;
            }

            interruptedOnAdd = true;
            pawn.jobs?.StopAll(false, true);
            pawn.stances?.CancelBusyStanceHard();
            pawn.stances?.SetStance(new Stance_Cooldown(30, pawn, null)
            {
                neverAimWeapon = true
            });
        }

        private void SpawnFreezeMote()
        {
            if (pawn == null || !pawn.Spawned || pawn.MapHeld == null)
            {
                return;
            }

            ThingDef moteDef = DefDatabase<ThingDef>.GetNamedSilentFail(Rand.Chance(0.5f) ? FreezeMoteDefNameA : FreezeMoteDefNameB);
            if (moteDef == null)
            {
                return;
            }

            freezeMote = MoteMaker.MakeAttachedOverlay(pawn, moteDef, Vector3.zero, 1f, 5f);
        }

        private void MaintainFreezeMote()
        {
            if (freezeMote == null || freezeMote.Destroyed)
            {
                return;
            }

            freezeMote.Maintain();
        }
    }
}
