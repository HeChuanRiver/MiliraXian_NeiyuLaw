using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public enum FlowerGodDescentState
    {
        Ready,
        Active,
        Cooldown
    }

    public class HediffCompProperties_FlowerGodDescent : HediffCompProperties
    {
        public int activeDurationTicks = 600;
        public int cooldownTicks = 3600;
        public bool startReady = true;
        public string activeLabel = "花神降临";
        public string noResonanceReason = "尚未调谐四时共鸣。";
        public string alreadyActiveReason = "花神已经降临。";
        public string cooldownReason = "花神降临仍在冷却中。";
        public string activatedMessage = "花神降临。";

        public HediffCompProperties_FlowerGodDescent()
        {
            compClass = typeof(HediffComp_FlowerGodDescent);
        }
    }

    public class HediffComp_FlowerGodDescent : HediffComp
    {
        private bool initialized;
        private FlowerGodDescentState state = FlowerGodDescentState.Ready;
        private int activeTicksLeft;
        private int cooldownTicksLeft;

        public HediffCompProperties_FlowerGodDescent Props => (HediffCompProperties_FlowerGodDescent)props;

        public FlowerGodDescentState State
        {
            get
            {
                EnsureInitialized();
                return state;
            }
        }

        public bool Ready => State == FlowerGodDescentState.Ready;

        public bool Active => State == FlowerGodDescentState.Active;

        public bool OnCooldown => State == FlowerGodDescentState.Cooldown;

        public int ActiveTicksLeft
        {
            get
            {
                EnsureInitialized();
                return activeTicksLeft;
            }
        }

        public int CooldownTicksLeft
        {
            get
            {
                EnsureInitialized();
                return cooldownTicksLeft;
            }
        }

        public int ActiveTicksTotal => Mathf.Max(0, Props?.activeDurationTicks ?? 0);

        public int CooldownTicksTotal => Mathf.Max(0, Props?.cooldownTicks ?? 0);

        public float ActiveRemainingPercent => ActiveTicksTotal <= 0 ? 0f : Mathf.Clamp01(ActiveTicksLeft / (float)ActiveTicksTotal);

        public float ActiveElapsedPercent => 1f - ActiveRemainingPercent;

        public float CooldownRemainingPercent => CooldownTicksTotal <= 0 ? 0f : Mathf.Clamp01(CooldownTicksLeft / (float)CooldownTicksTotal);

        public float CooldownReadyPercent => 1f - CooldownRemainingPercent;

        public string Label => Props?.activeLabel ?? "花神降临";

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            EnsureInitialized();

            if (state == FlowerGodDescentState.Active)
            {
                if (activeTicksLeft > 0)
                {
                    activeTicksLeft--;
                }

                if (activeTicksLeft <= 0)
                {
                    FinishActive();
                }
            }
            else if (state == FlowerGodDescentState.Cooldown)
            {
                if (cooldownTicksLeft > 0)
                {
                    cooldownTicksLeft--;
                }

                if (cooldownTicksLeft <= 0)
                {
                    SetReady();
                }
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref initialized, "flowerGodDescent_initialized", false);
            Scribe_Values.Look(ref state, "flowerGodDescent_state", FlowerGodDescentState.Ready);
            Scribe_Values.Look(ref activeTicksLeft, "flowerGodDescent_activeTicksLeft", 0);
            Scribe_Values.Look(ref cooldownTicksLeft, "flowerGodDescent_cooldownTicksLeft", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureInitialized();
                NormalizeState();
            }
        }

        public bool TryStartDescent()
        {
            EnsureInitialized();
            if (!CanStartDescent(out _))
            {
                return false;
            }

            int activeTicks = ActiveTicksTotal;
            if (activeTicks > 0)
            {
                state = FlowerGodDescentState.Active;
                activeTicksLeft = activeTicks;
                cooldownTicksLeft = 0;
            }
            else
            {
                StartCooldown();
            }

            return true;
        }

        public bool CanStartDescent(out string reason)
        {
            EnsureInitialized();
            if (Pawn == null || Pawn.Dead)
            {
                reason = "清荷无法回应花神。";
                return false;
            }

            HediffComp_SeasonResonance resonance = parent?.GetComp<HediffComp_SeasonResonance>();
            if (resonance == null || resonance.CurrentAttunedSeason == AttunedSeason.None)
            {
                reason = Props?.noResonanceReason ?? "尚未调谐四时共鸣。";
                return false;
            }

            if (state == FlowerGodDescentState.Active)
            {
                reason = Props?.alreadyActiveReason ?? "花神已经降临。";
                return false;
            }

            if (state == FlowerGodDescentState.Cooldown)
            {
                reason = BuildCooldownReason();
                return false;
            }

            reason = null;
            return true;
        }

        public void FinishActive()
        {
            EnsureInitialized();
            if (state != FlowerGodDescentState.Active)
            {
                return;
            }

            activeTicksLeft = 0;
            StartCooldown();
        }

        public void StartCooldown()
        {
            StartCooldown(CooldownTicksTotal);
        }

        public void StartCooldown(int ticks)
        {
            EnsureInitialized();
            activeTicksLeft = 0;
            cooldownTicksLeft = Mathf.Max(0, ticks);
            if (cooldownTicksLeft > 0)
            {
                state = FlowerGodDescentState.Cooldown;
            }
            else
            {
                SetReady();
            }
        }

        public void ResetToReady()
        {
            EnsureInitialized();
            SetReady();
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            if (Props?.startReady ?? true)
            {
                SetReady();
            }
            else
            {
                StartCooldown(CooldownTicksTotal);
            }
        }

        private void SetReady()
        {
            state = FlowerGodDescentState.Ready;
            activeTicksLeft = 0;
            cooldownTicksLeft = 0;
        }

        private void NormalizeState()
        {
            activeTicksLeft = Mathf.Clamp(activeTicksLeft, 0, ActiveTicksTotal);
            cooldownTicksLeft = Mathf.Clamp(cooldownTicksLeft, 0, CooldownTicksTotal);

            if (state == FlowerGodDescentState.Active && activeTicksLeft <= 0)
            {
                StartCooldown();
            }
            else if (state == FlowerGodDescentState.Cooldown && cooldownTicksLeft <= 0)
            {
                SetReady();
            }
            else if (state == FlowerGodDescentState.Ready)
            {
                activeTicksLeft = 0;
                cooldownTicksLeft = 0;
            }
        }

        private string BuildCooldownReason()
        {
            string reason = Props?.cooldownReason ?? "花神降临仍在冷却中。";
            if (CooldownTicksLeft > 0)
            {
                reason += "\n剩余时间: " + CooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            return reason;
        }
    }
}
