using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Vfx;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_AuraShield : CompProperties
    {
        public float maxEnergy = 100f;

        // Shield regeneration per second.
        public float baseRegenPerSecond = 0.8f;

        public int hitRegenDelayTicks = 120;

        // After breaking, shield is disabled for these ticks.
        public int breakDisabledTicks = 600;
        public bool breakOnEmp = true;
        public float shieldDamageCap;
        public float staggerDurationFactor = 1f;

        public AuraShieldVisualProperties visual = new();

        public CompProperties_AuraShield()
        {
            compClass = typeof(CompAuraShield);
        }
    }

    /// <summary>
    /// Recoverable Lotus Shield for QingHe.
    /// </summary>
    public class CompAuraShield : ThingComp
    {
        private const int RegenFlushIntervalTicks = 10;

        private float energy = 100f;
        private int fullEnergyAccumulatedTicks = 0;
        private int lastRegenUpdateTick = -1;
        private int resetUntilTick = -1;
        private int regenUntilTick = -1;
        private float cachedMaxEnergy = -1f;
        private float cachedRegenPerSecond;
        private AuraShieldRenderer renderer;

        public CompProperties_AuraShield Props => (CompProperties_AuraShield)props;

        private AuraShieldRenderer Renderer => renderer ??= new AuraShieldRenderer(this);

        private Pawn PawnOwner => parent as Pawn;

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        public float MaxEnergy
        {
            get
            {
                FlushAccumulatedRegen(CurrentTick, force: false);
                return cachedMaxEnergy > 0f ? cachedMaxEnergy : ResolveMaxEnergy();
            }
        }

        public float Energy
        {
            get
            {
                FlushAccumulatedRegen(CurrentTick, force: false);
                float maxEnergy = cachedMaxEnergy > 0f ? cachedMaxEnergy : ResolveMaxEnergy();
                return Mathf.Clamp(energy, 0f, maxEnergy);
            }
        }

        public float ShieldDamageCap
        {
            get
            {
                float factor = GetStatValue(MX_QHDefOf.MX_QH_AuraShieldDamageCapFactor, 0f);
                float offset = GetStatValue(MX_QHDefOf.MX_QH_AuraShieldDamageCapOffset, 20f);
                float afterOffset = Props.shieldDamageCap + offset;
                if (factor <= 0f || afterOffset <= 0f)
                {
                    return float.PositiveInfinity;
                }
                return Mathf.Max(1f, afterOffset * factor);
            }
        }

        public int CurrentRegenDelayTicks
        {
            get
            {
                return ApplyDelayFactorOffset(
                    Props.hitRegenDelayTicks,
                    MX_QHDefOf.MX_QH_AuraShieldHitRegenDelayFactor);
            }
        }

        public int CurrentBreakDelayTicks
        {
            get
            {
                return Mathf.Max(
                    0,
                    Mathf.RoundToInt(Props.breakDisabledTicks + GetStatValue(MX_QHDefOf.MX_QH_AuraShieldBreakDelayOffset, 0f)));
            }
        }

        public bool InBreak => CurrentTick < resetUntilTick;

        public int BreakTicksLeft => Mathf.Max(0, resetUntilTick - CurrentTick);

        public bool InRegenDelay => CurrentTick < regenUntilTick;

        public int RegenDelayTicksLeft => Mathf.Max(0, regenUntilTick - CurrentTick);

        public float CurrentRegenPerSecond
        {
            get
            {
                FlushAccumulatedRegen(CurrentTick, force: false);
                return Mathf.Max(0f, cachedRegenPerSecond);
            }
        }

        public int FullEnergyAccumulatedTicks
        {
            get
            {
                int currentTick = CurrentTick;
                FlushAccumulatedRegen(currentTick, force: false);

                int visualTicks = fullEnergyAccumulatedTicks;
                if (lastRegenUpdateTick >= 0
                    && cachedMaxEnergy > 0f
                    && energy >= cachedMaxEnergy - 0.0001f)
                {
                    visualTicks += Mathf.Max(0, currentTick - lastRegenUpdateTick);
                }

                return Mathf.Min(visualTicks, 1000000);
            }
        }

        /// <summary>
        /// Flash intensity for the shield bar, decaying from 1 to 0 over ~40 ticks after absorbing damage.
        /// </summary>
        public float AbsorbFlashPercent => Renderer.AbsorbFlashPercent(CurrentTick);

        public override void PostPostMake()
        {
            base.PostPostMake();
            energy = cachedMaxEnergy;
        }

        public void BindToPawn(Pawn pawn)
        {
            parent = pawn;
            lastRegenUpdateTick = CurrentTick;
            RefreshCachedStats();
            renderer?.NotifyHidden();
        }

        // Only the owning HediffComp calls this, under the Hediff's save node.
        // Pawn's normal ThingComp serialization must not write another copy.
        public void ExposeShieldData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                FlushAccumulatedRegen(CurrentTick, force: true);
            }

            Scribe_Values.Look(ref energy, "mx_qh_auraShield_energy", 100f);
            Scribe_Values.Look(ref resetUntilTick, "mx_qh_auraShield_resetUntilTick", -1);
            Scribe_Values.Look(ref regenUntilTick, "mx_qh_auraShield_regenUntilTick", -1);
            Scribe_Values.Look(ref fullEnergyAccumulatedTicks, "mx_qh_auraShield_fullEnergyAccumulatedTicks", 0);
        }

        public override void CompTick()
        {
            base.CompTick();

            if (QinghePowerBalance.Sealed)
            {
                lastRegenUpdateTick = CurrentTick;
                return;
            }

            int currentTick = CurrentTick;

            if (currentTick < resetUntilTick)
            {
                fullEnergyAccumulatedTicks = 0;
                lastRegenUpdateTick = currentTick;
                return;
            }

            if (currentTick < regenUntilTick)
            {
                fullEnergyAccumulatedTicks = 0;
                lastRegenUpdateTick = currentTick;
                return;
            }

            FlushAccumulatedRegen(currentTick, force: false);
        }

        private void FlushAccumulatedRegen(int currentTick, bool force)
        {
            if (QinghePowerBalance.Sealed)
            {
                lastRegenUpdateTick = currentTick;
                return;
            }

            if (currentTick < resetUntilTick || currentTick < regenUntilTick)
            {
                lastRegenUpdateTick = currentTick;
                return;
            }

            int elapsedTicks = lastRegenUpdateTick < 0
                ? RegenFlushIntervalTicks
                : Mathf.Max(0, currentTick - lastRegenUpdateTick);
            if (!force && elapsedTicks < RegenFlushIntervalTicks)
            {
                return;
            }

            if (elapsedTicks <= 0)
            {
                lastRegenUpdateTick = currentTick;
                return;
            }

            RefreshCachedStats();
            float maxEnergy = cachedMaxEnergy;
            energy = Mathf.Min(energy, maxEnergy);
            float energyBeforeGain = energy;
            float regenPerTick = cachedRegenPerSecond / 60f;
            float gain = regenPerTick * elapsedTicks;
            if (gain > 0f)
            {
                energy = Mathf.Min(maxEnergy, energy + gain);
            }

            lastRegenUpdateTick = currentTick;
            if (energy < maxEnergy - 0.0001f)
            {
                fullEnergyAccumulatedTicks = 0;
            }
            else
            {
                int ticksAtFullEnergy = elapsedTicks;
                if (energyBeforeGain < maxEnergy - 0.0001f && regenPerTick > 0f)
                {
                    int ticksToReachFull = Mathf.CeilToInt((maxEnergy - energyBeforeGain) / regenPerTick);
                    ticksAtFullEnergy = Mathf.Clamp(elapsedTicks - ticksToReachFull, 0, elapsedTicks);
                }

                fullEnergyAccumulatedTicks = Mathf.Min(fullEnergyAccumulatedTicks + ticksAtFullEnergy, 1000000);
            }
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;

            Pawn owner = PawnOwner;
            if (QinghePowerBalance.Sealed || owner.Dead)
            {
                return;
            }

            FlushAccumulatedRegen(CurrentTick, force: true);
            if (dinfo.Amount <= 0f || InBreak || energy <= 0f)
            {
                return;
            }
            float incomingDamageFactor = Mathf.Max(0f, GetStatValue(StatDefOf.IncomingDamageFactor, 1f));
            // Scale only the shield cost; unabsorbed damage keeps its normal body damage processing.
            float shieldDamage = Mathf.Min(dinfo.Amount * incomingDamageFactor, ShieldDamageCap);
            float hardening = Mathf.Max(0f, GetStatValue(MX_QHDefOf.MX_QH_AuraShieldHardening, 0f));
            float hardeningFactor = Mathf.Max(0f, GetStatValue(MX_QHDefOf.MX_QH_AuraShieldHardeningFactor, 0f));
            shieldDamage -= hardening * hardeningFactor;
            if (shieldDamage <= 0f)
            {
                Renderer.NotifyAbsorbed(owner, CurrentTick);
                dinfo.SetAmount(0f);
                absorbed = true;
                return;
            }

            if (shieldDamage >= energy - 0.0001f)
            {
                energy = 0f;
            }
            else
            {
                energy -= shieldDamage;
            }

            int currentTick = CurrentTick;
            regenUntilTick = currentTick + CurrentRegenDelayTicks;
            lastRegenUpdateTick = CurrentTick;
            Renderer.NotifyAbsorbed(owner, CurrentTick);
            dinfo.SetAmount(0f);
            absorbed = true;

            if (energy <= 0.0001f)
            {
                Break();
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();
            Pawn owner = PawnOwner;
            if (QinghePowerBalance.Sealed
                || !owner.Spawned
                || owner.Dead
                || InBreak
                || (Energy >= MaxEnergy - 0.0001f && fullEnergyAccumulatedTicks >= Renderer.FullEnergyFadeOutTicks))
            {
                Renderer.NotifyHidden();
                return;
            }

            Renderer.Draw(owner);
        }

        public override bool CompAllowVerbCast(Verb verb)
        {
            return true;
        }

        public override float GetStatFactor(StatDef stat)
        {
            if (stat == StatDefOf.StaggerDurationFactor
                && !QinghePowerBalance.Sealed
                && !PawnOwner.Dead
                && !InBreak
                && Energy > 0f)
            {
                return Props.staggerDurationFactor;
            }

            return base.GetStatFactor(stat);
        }

        public void RestoreEnergy(float amount)
        {
            FlushAccumulatedRegen(CurrentTick, force: true);
            if (QinghePowerBalance.Sealed || amount <= 0f || InBreak)
            {
                return;
            }

            energy = Mathf.Min(MaxEnergy, energy + amount);
        }

        public void RestoreFraction(float fraction)
        {
            RestoreEnergy(MaxEnergy * Mathf.Max(0f, fraction));
        }

        private void Break()
        {
            float energyRatio = Energy / MaxEnergy;
            energy = 0f;
            regenUntilTick = -1;
            resetUntilTick = CurrentTick + CurrentBreakDelayTicks;
            lastRegenUpdateTick = CurrentTick;
            Renderer.NotifyBroken(PawnOwner, parent, energyRatio);
        }

        private int ApplyDelayFactorOffset(float baseValue, StatDef factorStat)
        {
            return Mathf.Max(0, Mathf.RoundToInt(baseValue * GetStatValue(factorStat, 1f)));
        }

        private float GetStatValue(StatDef statDef, float fallback)
        {
            Pawn owner = PawnOwner;
            if (owner == null || statDef == null)
            {
                return fallback;
            }

            return owner.GetStatValue(statDef, true, 1);
        }

        private void RefreshCachedStats()
        {
            cachedMaxEnergy = ResolveMaxEnergy();
            cachedRegenPerSecond = Mathf.Max(
                0f,
                Props.baseRegenPerSecond * GetStatValue(MX_QHDefOf.MX_QH_AuraShieldRegenPerSecondFactor, 1f));
        }

        private float ResolveMaxEnergy()
        {
            return Mathf.Max(
                1f,
                Props.maxEnergy * GetStatValue(MX_QHDefOf.MX_QH_AuraShieldMaxEnergyFactor, 1f));
        }

        public string BuildShieldTooltip()
        {
            string status = InBreak
                ? "MX_QH_AuraShieldStatusDown".Translate(Mathf.CeilToInt(BreakTicksLeft / 60f)).ToString()
                : "MX_QH_AuraShieldStatusActive".Translate().ToString();

            return "MX_QH_AuraShieldTooltipTitle".Translate().ToString() + "\n\n"
                   + status + "\n"
                   + "MX_QH_AuraShieldEnergyLine".Translate(Energy.ToString("F0"), MaxEnergy.ToString("F0")).ToString() + "\n"
                   + "MX_QH_AuraShieldRegenLine".Translate(CurrentRegenPerSecond.ToString("F2")).ToString()
                   + (InRegenDelay ? "\n" + "MX_QH_AuraShieldRegenDelayLine".Translate(Mathf.CeilToInt(RegenDelayTicksLeft / 60f)).ToString() : "");
        }

    }
}
