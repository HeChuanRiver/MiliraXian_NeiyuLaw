using MiliraXian.Characters.QingHe.Things.Weapons;
using Verse;

namespace MiliraXian.Characters.QingHe.Verbs
{
    /// <summary>
    /// The Flower Bell's charge (RangedRework.md 2.9). A vanilla warmup underneath: its checks
    /// (target lost, out of reach, moved, a new job) end it as they end any warmup, and the charge
    /// left on the weapon then decays. Each tick it adds to the charge; a tier reached rings the
    /// run's next note. It ends by releasing (the warmup completes) when the threshold tier is
    /// reached, the Gizmo asks for it, or the charge is full (ticksLeft counts to the full charge).
    /// </summary>
    public class Stance_FlowerBellCharge : Stance_Warmup
    {
        public Stance_FlowerBellCharge()
        {
        }

        public Stance_FlowerBellCharge(int ticks, LocalTargetInfo focusTarg, Verb verb)
            : base(ticks, focusTarg, verb)
        {
            // The aim pie is off in the verb's XML (drawAimPie): the charge shows on the Gizmo.
        }

        private CompFlowerBellCharge Comp => verb?.EquipmentSource?.GetComp<CompFlowerBellCharge>();

        public override void StanceTick()
        {
            CompFlowerBellCharge comp = Comp;
            if (comp == null)
            {
                base.StanceTick();
                return;
            }
            if (stanceTracker.stunner.Stunned)
            {
                comp.HoldCharge();
                base.StanceTick();
                return;
            }
            int before = comp.Tier;
            comp.AddCharge(1f / 60f);
            int after = comp.Tier;
            Pawn pawn = stanceTracker.pawn;
            FlowerBellChargeVfx.Notify(comp, pawn);
            for (int k = before; k < after; k++)
            {
                // Tier k + 1 reached: the run's (k + 1)-th note, its petal and hovering note.
                FlowerBellChimes.Ring(pawn.Map, pawn.Position, comp.ModeStart + k);
                FlowerBellChargeVfx.TierUp(comp, pawn, k);
            }
            comp.shownTier = after;
            bool release = after >= 1 && (after >= comp.Threshold || after >= comp.Tiers || comp.releaseRequested);
            comp.releaseRequested = false;
            // The base tick still runs the warmup's own checks (and may end this stance first); its
            // countdown is ours: it reaches 0 (and completes the warmup) only on a release.
            ticksLeft = release ? 1 : 2;
            base.StanceTick();
        }

        internal static int ChargeTicksLeft(CompFlowerBellCharge comp)
        {
            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.CeilToInt((comp.FullSeconds - comp.Charge) * 60f));
        }
    }
}
