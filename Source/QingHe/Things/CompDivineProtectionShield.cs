using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Vfx;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_DivineProtectionShield : CompProperties
    {
        public float maxEnergy = 100f;

        // Shield regeneration per second.
        public float baseRegenPerSecond = 0.8f;

        public int hitRegenDelayTicks = 120;

        // After breaking, shield is disabled for these ticks.
        public int breakDisabledTicks = 600;
        public bool breakOnEmp = true;
        public float shieldDamageCap;

        public DivineProtectionShieldVisualProperties visual = new();

        public CompProperties_DivineProtectionShield()
        {
            compClass = typeof(CompDivineProtectionShield);
        }
    }

    /// <summary>
    /// Recoverable Lotus Shield for QingHe.
    /// </summary>
    public class CompDivineProtectionShield : ThingComp
    {
        private const int RegenFlushIntervalTicks = 10;

        private float energy = 100f;
        private int fullEnergyAccumulatedTicks = 0;
        private int lastRegenUpdateTick = -1;
        private int resetUntilTick = -1;
        private int regenUntilTick = -1;
        private float cachedMaxEnergy = -1f;
        private float cachedRegenPerSecond;
        private bool runtimeStateInitialized;
        private DivineProtectionShieldRenderer renderer;

        public CompProperties_DivineProtectionShield Props => (CompProperties_DivineProtectionShield)props;

        private DivineProtectionShieldRenderer Renderer => renderer ??= new DivineProtectionShieldRenderer(this);

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
                float factor = GetStatValue(MX_QHDefOf.MX_QH_LotusShieldDamageCapFactor, 1f);
                float offset = GetStatValue(MX_QHDefOf.MX_QH_LotusShieldDamageCapOffset, 0f);
                float afterOffset = Props.shieldDamageCap + offset;
                if (afterOffset <= 0f)
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
                    MX_QHDefOf.MX_QH_LotusShieldHitRegenDelayFactor);
            }
        }

        public int CurrentBreakDelayTicks
        {
            get
            {
                return Mathf.Max(
                    0,
                    Mathf.RoundToInt(Props.breakDisabledTicks + GetStatValue(MX_QHDefOf.MX_QH_LotusShieldBreakDelayOffset, 0f)));
            }
        }

        public bool InBreak => RuntimeInitialized() && CurrentTick < resetUntilTick;

        public int BreakTicksLeft => RuntimeInitialized() ? Mathf.Max(0, resetUntilTick - CurrentTick) : 0;

        public bool InRegenDelay => RuntimeInitialized() && CurrentTick < regenUntilTick;

        public int RegenDelayTicksLeft => RuntimeInitialized() ? Mathf.Max(0, regenUntilTick - CurrentTick) : 0;

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
            InitializeRuntimeState();
            energy = cachedMaxEnergy;
        }

        public override void PostExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                FlushAccumulatedRegen(CurrentTick, force: true);
            }

            base.PostExposeData();
            Scribe_Values.Look(ref energy, "mx_qh_lotus_energy", 100f);
            Scribe_Values.Look(ref resetUntilTick, "mx_qh_lotus_resetUntilTick", -1);
            Scribe_Values.Look(ref regenUntilTick, "mx_qh_lotus_regenUntilTick", -1);
            Scribe_Values.Look(ref fullEnergyAccumulatedTicks, "mx_qh_lotus_fullEnergyAccumulatedTicks", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitializeRuntimeState();
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (QinghePowerBalance.Sealed)
            {
                PawnOwner?.AllComps?.Remove(this);
                return;
            }

            if (PawnOwner == null)
            {
                energy = 0f;
                return;
            }

            int currentTick = CurrentTick;
            RuntimeInitialized();

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
            if (!RuntimeInitialized() || PawnOwner == null)
            {
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
            if (owner == null || owner.Dead)
            {
                return;
            }

            FlushAccumulatedRegen(CurrentTick, force: true);
            if (dinfo.Amount <= 0f || InBreak || dinfo.Def.ignoreShields || energy <= 0f)
            {
                return;
            }
            float shieldDamage = Mathf.Min(dinfo.Amount, ShieldDamageCap);
            if (QingHe.Things.Weapons.QingheSwordCombatUtility.IsSwordMode(owner))
            {
                shieldDamage *= 0.5f;
            }
            if (shieldDamage <= 0f)
            {
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
            regenUntilTick = currentTick + ResolveRegenDelayTicks();
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
            if (owner == null
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

        public void RestoreEnergy(float amount)
        {
            FlushAccumulatedRegen(CurrentTick, force: true);
            if (amount <= 0f || InBreak)
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
            resetUntilTick = CurrentTick + ResolveBreakDelayTicks();
            lastRegenUpdateTick = CurrentTick;
            Renderer.NotifyBroken(PawnOwner, parent, energyRatio);
        }

        private int ResolveRegenDelayTicks()
        {
            return CurrentRegenDelayTicks;
        }

        private int ResolveBreakDelayTicks()
        {
            return CurrentBreakDelayTicks;
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

        private bool RuntimeInitialized()
        {
            if (!runtimeStateInitialized)
            {
                InitializeRuntimeState();
            }

            return runtimeStateInitialized;
        }

        private void InitializeRuntimeState()
        {
            lastRegenUpdateTick = CurrentTick;
            RefreshCachedStats();
            runtimeStateInitialized = true;
        }

        private void RefreshCachedStats()
        {
            cachedMaxEnergy = ResolveMaxEnergy();
            cachedRegenPerSecond = Mathf.Max(
                0f,
                Props.baseRegenPerSecond * GetStatValue(MX_QHDefOf.MX_QH_LotusShieldRegenPerSecondFactor, 1f));
        }

        private float ResolveMaxEnergy()
        {
            return Mathf.Max(
                1f,
                Props.maxEnergy * GetStatValue(MX_QHDefOf.MX_QH_LotusShieldMaxEnergyFactor, 1f));
        }

        public string BuildShieldTooltip()
        {
            string status = InBreak
                ? "MX_QH_LotusShieldStatusDown".Translate(Mathf.CeilToInt(BreakTicksLeft / 60f)).ToString()
                : "MX_QH_LotusShieldStatusActive".Translate().ToString();

            return "MX_QH_LotusShieldTooltipTitle".Translate().ToString() + "\n\n"
                   + status + "\n"
                   + "MX_QH_LotusShieldEnergyLine".Translate(Energy.ToString("F0"), MaxEnergy.ToString("F0")).ToString() + "\n"
                   + "MX_QH_LotusShieldRegenLine".Translate(CurrentRegenPerSecond.ToString("F2")).ToString()
                   + (InRegenDelay ? "\n" + "MX_QH_LotusShieldRegenDelayLine".Translate(Mathf.CeilToInt(RegenDelayTicksLeft / 60f)).ToString() : "");
        }

    }
}
