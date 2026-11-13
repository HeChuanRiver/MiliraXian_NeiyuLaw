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
        /// <summary>One tier every this many seconds.</summary>
        public float tierSeconds = 2f;
        public int tiers = 5;
        /// <summary>Loss after an interruption: L(t) = r0 t + a t² / 2 seconds of charge.</summary>
        public float decayR0 = 0.5f;
        public float decayA = 1f;

        /// <summary>Damage of every note of a volley released at tier k (1-based, index k - 1), as a multiple of the base.</summary>
        public List<float> tierDamageFactors = new() { 1f, 1.5f, 2f, 2.5f, 3f };
        /// <summary>Base damage of a note by aura mastery level (replaces the projectile's damage and RangedWeapon_DamageMultiplier).</summary>
        public SimpleCurve baseDamageByMastery = new() { new CurvePoint(0f, 10f), new CurvePoint(12f, 18f), new CurvePoint(24f, 28f) };
        /// <summary>Armour penetration at tier k: projectile's × (penetrationBase + penetrationPerTier k).</summary>
        public float penetrationBase = 0.8f;
        public float penetrationPerTier = 0.1f;
        /// <summary>Shock wave radius at tier k: radiusBase + radiusPerTier k.</summary>
        public float radiusBase = 1.2f;
        public float radiusPerTier = 0.2f;
        /// <summary>Scatter aim: the other notes pick targets within this radius of the selected one.</summary>
        public float scatterRadius = 4f;

        public CompProperties_FlowerBellCharge()
        {
            compClass = typeof(CompFlowerBellCharge);
        }
    }

    /// <summary>
    /// The Flower Bell's five-tier charge, kept on the weapon so it survives
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
        private FlowerBellAimMode aimMode = FlowerBellAimMode.Focus;
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

        /// <summary>The threshold can change only with no charge at all.</summary>
        public bool ThresholdLocked => Charge > 0f;

        /// <summary>Damage factor of every note of a volley released at <paramref name="tier"/> (1-based).</summary>
        public float TierFactor(int tier)
        {
            List<float> f = PropsCharge.tierDamageFactors;
            if (f == null || f.Count == 0)
            {
                return 1f;
            }
            return f[Mathf.Clamp(tier - 1, 0, f.Count - 1)];
        }

        /// <summary>Damage of each note fired by <paramref name="pawn"/> at <paramref name="tier"/>.</summary>
        public float NoteDamage(Pawn pawn, int tier)
        {
            int level = MX_QH_HediffUtility.GetAuraMasteryLevel(pawn);
            return PropsCharge.baseDamageByMastery.Evaluate(level) * TierFactor(tier);
        }

        public float NotePenetrationFactor(int tier) => PropsCharge.penetrationBase + PropsCharge.penetrationPerTier * tier;

        public float NoteRadius(int tier) => PropsCharge.radiusBase + PropsCharge.radiusPerTier * tier;

        /// <summary>Abnormal build-up of each note at <paramref name="tier"/>: the square root of its damage factor.</summary>
        public float NoteAbnormalFactor(int tier) => Mathf.Sqrt(TierFactor(tier));

        public float ScatterRadius => PropsCharge.scatterRadius;

        /// <summary>How the notes of a volley pick their targets (the player's toggle; the AI always focuses).</summary>
        public FlowerBellAimMode AimMode
        {
            get => Holder != null && Holder.Faction != Faction.OfPlayer ? FlowerBellAimMode.Focus : aimMode;
            set => aimMode = value;
        }

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
            Scribe_Values.Look(ref aimMode, "mx_bellAimMode", FlowerBellAimMode.Focus);
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
                bool scatter = aimMode == FlowerBellAimMode.Scatter;
                yield return new Command_Action
                {
                    defaultLabel = (scatter ? "MX_QH_BellAimScatter" : "MX_QH_BellAimFocus").Translate(),
                    defaultDesc = "MX_QH_BellAimDesc".Translate(ScatterRadius.ToString("0.#")),
                    // Placeholder icons: vanilla's attack for focus, squad attack for scatter.
                    icon = scatter ? TexCommand.SquadAttack : TexCommand.Attack,
                    Order = -96f,
                    action = () => aimMode = scatter ? FlowerBellAimMode.Focus : FlowerBellAimMode.Scatter,
                };
            }
        }
    }
}
