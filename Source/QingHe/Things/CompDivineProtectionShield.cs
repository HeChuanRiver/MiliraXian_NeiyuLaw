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
        public float shieldDamageCap = 20f;

        public DivineProtectionShieldVisualProperties visual = new DivineProtectionShieldVisualProperties();

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
        private float energy = 100f;
        private int ticksToReset = -1;
        private int ticksToRegen = 0;

        private int fullEnergyAccumulatedTicks = 0;
        private DivineProtectionShieldRenderer renderer;

        public CompProperties_DivineProtectionShield Props => (CompProperties_DivineProtectionShield)props;

        private DivineProtectionShieldRenderer Renderer => renderer ?? (renderer = new DivineProtectionShieldRenderer(this));

        private Pawn PawnOwner => parent as Pawn;

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        public float MaxEnergy => Mathf.Max(
            1f,
            Props.maxEnergy * GetStatValue(MX_QHDefOf.MX_QH_LotusShieldMaxEnergyFactor, 1f));

        public float Energy => Mathf.Clamp(energy, 0f, MaxEnergy);

        public float ShieldDamageCap
        {
            get
            {
                float factor = GetStatValue(MX_QHDefOf.MX_QH_LotusShieldDamageCapFactor, 0f);
                if (Mathf.Approximately(factor, 0f))
                {
                    return 0f;
                }

                float offset = GetStatValue(MX_QHDefOf.MX_QH_LotusShieldDamageCapOffset, 0f);
                float afterOffset = Mathf.Max(1f, Props.shieldDamageCap + offset);
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

        public bool InBreak => ticksToReset > 0;

        public int BreakTicksLeft => Mathf.Max(0, ticksToReset);

        public bool InRegenDelay => ticksToRegen > 0;

        public int RegenDelayTicksLeft => Mathf.Max(0, ticksToRegen);

        public float CurrentRegenPerSecond
        {
            get
            {
                return Mathf.Max(
                    0f,
                    Props.baseRegenPerSecond * GetStatValue(MX_QHDefOf.MX_QH_LotusShieldRegenPerSecondFactor, 1f));
            }
        }

        public int FullEnergyAccumulatedTicks => fullEnergyAccumulatedTicks;

        /// <summary>
        /// Flash intensity for the shield bar, decaying from 1 to 0 over ~40 ticks after absorbing damage.
        /// </summary>
        public float AbsorbFlashPercent => Renderer.AbsorbFlashPercent(CurrentTick);

        public override void PostPostMake()
        {
            base.PostPostMake();
            energy = MaxEnergy;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref energy, "mx_qh_lotus_energy", 100f);
            Scribe_Values.Look(ref ticksToReset, "mx_qh_lotus_ticksToReset", -1);
            Scribe_Values.Look(ref ticksToRegen, "mx_qh_lotus_ticksToRegen", 0);
            Scribe_Values.Look(ref fullEnergyAccumulatedTicks, "mx_qh_lotus_fullEnergyAccumulatedTicks", 0);
        }

        public override void CompTick()
        {
            base.CompTick();

            if (PawnOwner == null)
            {
                energy = 0f;
                return;
            }

            energy = Mathf.Min(energy, MaxEnergy);

            if (ticksToReset > 0)
            {
                ticksToReset--;
                fullEnergyAccumulatedTicks = 0;
                return;
            }

            if (ticksToRegen > 0)
            {
                ticksToRegen--;
                fullEnergyAccumulatedTicks = 0;
                return;
            }

            float gain = CurrentRegenPerSecond / 60f;
            if (gain > 0f)
            {
                energy = Mathf.Min(MaxEnergy, energy + gain);
            }

            if (Energy < MaxEnergy - 0.0001f)
            {
                fullEnergyAccumulatedTicks = 0;
            }
            else
            {
                fullEnergyAccumulatedTicks = Mathf.Min(fullEnergyAccumulatedTicks + 1, 1000000);
            }
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;

            Pawn owner = PawnOwner;
            if (owner == null || owner.Dead || dinfo.Amount <= 0f || InBreak || dinfo.Def.ignoreShields ||energy <= 0f)
            {
                return;
            }
            float damageCap = ShieldDamageCap;
            float shieldDamage = damageCap > 0f ? Mathf.Min(dinfo.Amount, damageCap) : Mathf.Max(0f, dinfo.Amount);
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

            ticksToRegen = ResolveRegenDelayTicks();
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

        private void Break()
        {
            float energyRatio = Energy / MaxEnergy;
            energy = 0f;
            ticksToRegen = 0;
            ticksToReset = ResolveBreakDelayTicks();
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

            return owner.GetStatValue(statDef);
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
