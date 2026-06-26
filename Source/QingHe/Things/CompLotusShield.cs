using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_LotusShield : CompProperties
    {
        public float maxEnergy = 100f;

        // Shield regeneration per second.
        public float baseRegenPerSecond = 0.8f;

        public float shangMaxEnergyMultiplier = 2f;
        public float zhiRegenMultiplier = 3f;
        public int hitRegenDelayTicks = 120;
        public float gaoshanDamageCap = 40f;
        public float yuDamageCap = 10f;
        public int yuBreakDisabledTicks = 0;

        // After breaking, shield is disabled for these ticks.
        public int breakDisabledTicks = 600;
        public bool breakOnEmp = true;

        public LotusShieldVisualProperties visual = new LotusShieldVisualProperties();

        public CompProperties_LotusShield()
        {
            compClass = typeof(CompLotusShield);
        }
    }

    /// <summary>
    /// Recoverable Lotus Shield for QingHe.
    /// </summary>
    public class CompLotusShield : ThingComp
    {
        private float energy = 100f;
        private int ticksToReset = -1;
        private int ticksToRegen = 0;

        private int fullEnergyAccumulatedTicks = 0;
        private LotusShieldRenderer renderer;

        public CompProperties_LotusShield Props => (CompProperties_LotusShield)props;

        private LotusShieldRenderer Renderer => renderer ?? (renderer = new LotusShieldRenderer(this));

        private Pawn PawnOwner => parent as Pawn;

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        public float MaxEnergy => Mathf.Max(
            1f,
            Props.maxEnergy
            * (HasSkillNode(MX_QHSkillNodeDefOf.MX_QH_Node_Shang) ? Mathf.Max(0.01f, Props.shangMaxEnergyMultiplier) : 1f)
            * (FlowerCourtUtility.GetSkillTreeState(PawnOwner)?.LotusShieldCapacityMultiplierFromMastery ?? 1f));

        public float Energy => Mathf.Clamp(energy, 0f, MaxEnergy);

        public bool InBreak => ticksToReset > 0;

        public int BreakTicksLeft => Mathf.Max(0, ticksToReset);

        public bool InRegenDelay => ticksToRegen > 0;

        public int RegenDelayTicksLeft => Mathf.Max(0, ticksToRegen);

        public float CurrentRegenPerSecond
        {
            get
            {
                float multiplier = HasSkillNode(MX_QHSkillNodeDefOf.MX_QH_Node_Zhi) ? Mathf.Max(0f, Props.zhiRegenMultiplier) : 1f;
                return Mathf.Max(0f, Props.baseRegenPerSecond * multiplier);
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
            float damageCap = 0f;
            if (FlowerCourtUtility.GetFlowerDivination(PawnOwner)?.Active == true && HasSkillNode(MX_QHSkillNodeDefOf.MX_QH_Node_Yu))
            {
                damageCap = Mathf.Max(0f, Props.yuDamageCap);
            }
            else if (HasSkillNode(MX_QHSkillNodeDefOf.MX_QH_Node_Gaoshan))
            {
                damageCap = Mathf.Max(0f, Props.gaoshanDamageCap);
            }

            float shieldDamage = damageCap > 0f ? Mathf.Min(dinfo.Amount, damageCap) : Mathf.Max(0f, dinfo.Amount);
            if (FlowerDivinationBuffUtility.Active(PawnOwner))
            {
                shieldDamage *= FlowerDivinationBuffUtility.ShieldDamageFactor;
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

            if (FlowerCourtUtility.GetFlowerDivination(PawnOwner)?.Active == true)
            {
                ticksToRegen = 0;
            }
            else
            {
                ticksToRegen = Mathf.Max(0, Props.hitRegenDelayTicks);
            }
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
            ticksToReset = Mathf.Max(
                0,
                FlowerCourtUtility.GetFlowerDivination(PawnOwner)?.Active == true && HasSkillNode(MX_QHSkillNodeDefOf.MX_QH_Node_Yu)
                    ? Props.yuBreakDisabledTicks
                    : Props.breakDisabledTicks);
            Renderer.NotifyBroken(PawnOwner, parent, energyRatio);
        }

        private bool HasSkillNode(QingheSkillNodeDef node)
        {
            return FlowerCourtUtility.EnsureSkillTreeState(PawnOwner)?.HasNode(node) == true;
        }

        public string BuildShieldTooltip()
        {
            string status = InBreak
                ? "MX_QH_LotusShieldStatusDown".Translate(Mathf.CeilToInt(BreakTicksLeft / 60f)).ToString()
                : "MX_QH_LotusShieldStatusActive".Translate().ToString();

            return "花神护体\n\n"
                   + status + "\n"
                   + "护盾值：" + Energy.ToString("F0") + " / " + MaxEnergy.ToString("F0") + "\n"
                   + "护盾回复：" + CurrentRegenPerSecond.ToString("F2") + " /秒"
                   + (InRegenDelay ? "\n回复延迟：" + Mathf.CeilToInt(RegenDelayTicksLeft / 60f) + "秒" : "");
        }

    }
}
