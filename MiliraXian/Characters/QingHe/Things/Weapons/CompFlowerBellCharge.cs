using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.UI;
using MiliraXian.Characters.QingHe.Verbs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    public class CompProperties_FlowerBellCharge : CompProperties
    {
        /// <summary>One tier every this many seconds (RangedRework.md 2.9: 2/4/6/8/10 s).</summary>
        public float tierSeconds = 2f;
        public int tiers = 5;
        /// <summary>Loss after an interruption: L(t) = r0 t + a t² / 2 seconds of charge.</summary>
        public float decayR0 = 0.5f;
        public float decayA = 1f;

        // --- damage (RangedRework.md 2.9 "伤害设计", owner 11-11): the k-th note of a volley hits harder
        /// <summary>Damage of the k-th note (0-based) as a multiple of the base: later notes hurt more.</summary>
        public List<float> noteDamageFactors = new() { 1f, 1.5f, 2.2f, 3f, 4f };
        /// <summary>Base damage of a note by aura mastery level (replaces the projectile's damage and RangedWeapon_DamageMultiplier).</summary>
        public SimpleCurve baseDamageByMastery = new() { new CurvePoint(0f, 10f), new CurvePoint(12f, 18f), new CurvePoint(24f, 28f) };
        /// <summary>Armour penetration of the k-th note: projectile's × (penetrationBase + penetrationPerNote k), k from 1.</summary>
        public float penetrationBase = 0.8f;
        public float penetrationPerNote = 0.1f;
        /// <summary>Shock wave radius of the k-th note (k from 1): radiusBase + radiusPerNote k.</summary>
        public float radiusBase = 1.2f;
        public float radiusPerNote = 0.2f;

        public CompProperties_FlowerBellCharge()
        {
            compClass = typeof(CompFlowerBellCharge);
        }
    }

    /// <summary>
    /// The Flower Bell's charge (RangedRework.md 2.9, five tiers), kept on the weapon so it survives
    /// an interrupted cast. The charge stance adds to it each tick; once nothing adds to it (the
    /// stance was replaced: moving, a new job, a lost target) it decays, computed lazily from the
    /// last charge tick. Also holds the auto-release threshold and the run's random mode.
    /// Replaces the weapon's plain <see cref="CompEquippable"/> (its verbs are kept).
    /// </summary>
    public class CompFlowerBellCharge : CompEquippable
    {
        private float charge;
        private int lastChargeTick = -1;
        private int threshold = 5;
        private int modeStart = -1;
        private FlowerBellThreatMode threatMode = FlowerBellThreatMode.Hold;
        /// <summary>Set by the Gizmo; the charge stance releases on its next tick. Not saved.</summary>
        internal bool releaseRequested;
        /// <summary>Tier shown last tick, to hear the decay drop tier by tier. Not saved.</summary>
        internal int shownTier = -1;

        public CompProperties_FlowerBellCharge PropsCharge => (CompProperties_FlowerBellCharge)props;

        public Pawn Wielder => Holder;

        public float FullSeconds => PropsCharge.tierSeconds * PropsCharge.tiers;

        public int Tiers => PropsCharge.tiers;

        public float TierSeconds => PropsCharge.tierSeconds;

        public int Threshold
        {
            get => Mathf.Clamp(threshold, 1, Tiers);
            set => threshold = Mathf.Clamp(value, 1, Tiers);
        }

        /// <summary>Seconds of charge now, after the decay since the last charge tick.</summary>
        public float Charge
        {
            get
            {
                if (lastChargeTick < 0 || charge <= 0f)
                {
                    return 0f;
                }
                int gap = Find.TickManager.TicksGame - lastChargeTick;
                if (gap <= 1)
                {
                    return charge;
                }
                float t = (gap - 1) / 60f;
                return Mathf.Max(0f, charge - (PropsCharge.decayR0 * t + PropsCharge.decayA * t * t * 0.5f));
            }
        }

        public int Tier => Mathf.Min(Tiers, Mathf.FloorToInt(Charge / TierSeconds + 1e-4f));

        /// <summary>Share of the tier being charged (0 at a full charge).</summary>
        public float TierProgress
        {
            get
            {
                int k = Tier;
                return k >= Tiers ? 0f : Mathf.Clamp01(Charge / TierSeconds - k);
            }
        }

        /// <summary>Charge left and nothing adding to it.</summary>
        public bool Decaying => Charge > 0f && Find.TickManager.TicksGame - lastChargeTick > 1;

        /// <summary>The wielder is in this weapon's charge stance.</summary>
        public bool Charging => Holder?.stances?.curStance is Stance_FlowerBellCharge s && s.verb?.EquipmentSource == parent;

        public bool CanRelease => Charging && Tier >= 1;

        /// <summary>The answer to a melee threat below tier 1 (the player's toggle; the AI always steps away).</summary>
        public FlowerBellThreatMode ThreatMode
        {
            get => Holder != null && Holder.Faction != Faction.OfPlayer ? FlowerBellThreatMode.Retreat : threatMode;
            set => threatMode = value;
        }

        /// <summary>The threshold can change only with no charge at all (owner 11-09).</summary>
        public bool ThresholdLocked => Charge > 0f;

        /// <summary>Damage factor of note <paramref name="index"/> (0-based) of a volley.</summary>
        public float NoteFactor(int index)
        {
            List<float> f = PropsCharge.noteDamageFactors;
            if (f == null || f.Count == 0)
            {
                return 1f;
            }
            return f[Mathf.Clamp(index, 0, f.Count - 1)];
        }

        /// <summary>Damage of note <paramref name="index"/> fired by <paramref name="pawn"/>.</summary>
        public float NoteDamage(Pawn pawn, int index)
        {
            int level = MX_QH_HediffUtility.GetAuraMasteryLevel(pawn);
            return PropsCharge.baseDamageByMastery.Evaluate(level) * NoteFactor(index);
        }

        public float NotePenetrationFactor(int index) => PropsCharge.penetrationBase + PropsCharge.penetrationPerNote * (index + 1);

        public float NoteRadius(int index) => PropsCharge.radiusBase + PropsCharge.radiusPerNote * (index + 1);

        /// <summary>Abnormal build-up of note <paramref name="index"/>: the square root of its damage factor.</summary>
        public float NoteAbnormalFactor(int index) => Mathf.Sqrt(NoteFactor(index));

        /// <summary>Degree (0 = gong of the lowest octave) of the run's first note; picked when a run starts.</summary>
        public int ModeStart => modeStart < 0 ? 0 : modeStart;

        internal void BeginRun()
        {
            if (Charge <= 0f || modeStart < 0)
            {
                modeStart = Rand.Range(0, 5);
            }
        }

        internal void AddCharge(float seconds)
        {
            charge = Mathf.Min(FullSeconds, Charge + seconds);
            lastChargeTick = Find.TickManager.TicksGame;
            FlowerBellChargeTracker.Track(this);
        }

        /// <summary>Stunned in the stance: the charge waits, it does not decay.</summary>
        internal void HoldCharge()
        {
            charge = Charge;
            lastChargeTick = Find.TickManager.TicksGame;
        }

        /// <summary>Released: the charge is spent (no decay drops are heard).</summary>
        internal void Consume()
        {
            charge = 0f;
            lastChargeTick = -1;
            modeStart = -1;
            shownTier = 0;
        }

        /// <summary>Once a tick while charge is left (from <see cref="FlowerBellChargeTracker"/>): hears the decay drops.</summary>
        /// <returns>False once nothing is left to follow.</returns>
        internal bool TickTracked()
        {
            int tier = Tier;
            if (shownTier < 0)
            {
                shownTier = tier;
            }
            if (tier < shownTier && Decaying)
            {
                // The lowest of the dropped tiers: the note it had rung, lower and dull.
                FlowerBellChimes.Dull(parent.MapHeld, parent.PositionHeld, ModeStart + tier);
                for (int i = shownTier - 1; i >= tier; i--)
                {
                    FlowerBellChargeVfx.TierDown(this, Holder, i);
                }
            }
            shownTier = tier;
            if (Charge <= 0f)
            {
                charge = 0f;
                lastChargeTick = -1;
                modeStart = -1;
                shownTier = 0;
                return false;
            }
            return true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref charge, "mx_bellCharge", 0f);
            Scribe_Values.Look(ref lastChargeTick, "mx_bellLastChargeTick", -1);
            Scribe_Values.Look(ref threshold, "mx_bellThreshold", 5);
            Scribe_Values.Look(ref modeStart, "mx_bellModeStart", -1);
            Scribe_Values.Look(ref threatMode, "mx_bellThreatMode", FlowerBellThreatMode.Hold);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && charge > 0f)
            {
                FlowerBellChargeTracker.Track(this);
            }
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetEquippedGizmosExtra())
            {
                yield return g;
            }
            Pawn pawn = Holder;
            if (pawn != null && pawn.Faction == Faction.OfPlayer)
            {
                yield return new Gizmo_FlowerBellCharge(this);
                bool retreat = threatMode == FlowerBellThreatMode.Retreat;
                yield return new Command_Action
                {
                    defaultLabel = (retreat ? "MX_QH_BellThreatRetreat" : "MX_QH_BellThreatHold").Translate(),
                    defaultDesc = "MX_QH_BellThreatDesc".Translate(),
                    // Placeholder icons: vanilla's "pause caravan" for hold, "release animals" (running) for retreat.
                    icon = retreat ? TexCommand.ReleaseAnimals : TexCommand.PauseCaravan,
                    Order = -97f,
                    action = () => threatMode = retreat ? FlowerBellThreatMode.Hold : FlowerBellThreatMode.Retreat,
                };
            }
        }
    }
}
