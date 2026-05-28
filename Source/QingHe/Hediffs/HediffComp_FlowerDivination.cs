using RimWorld;
using MiliraXian.Characters.QingHe.Abilities;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public enum FlowerDivinationState
    {
        Ready,
        Active,
        Cooldown
    }

    public class HediffCompProperties_FlowerDivination : HediffCompProperties
    {
        public int activeDurationTicks = 600;
        public int cooldownTicks = 3600;
        public int warmupTicks = 120;
        public int warmupLightningIntervalTicks = 18;
        public float warmupLightningRadius = 1.4f;
        public int afterimageIntervalTicks = 6;
        public int slashAfterimageIntervalTicks = 2;
        public int afterimageFadeTicks = 60;
        public float afterimageStartAlpha = 0.44f;
        public float afterimageMinDistance = 0.55f;
        public EffecterDef activeEffecter;
        public bool startReady = true;
        public string activeLabel = "花神降临";
        public string noResonanceReason = "尚未调谐四时共鸣。";
        public string alreadyActiveReason = "花神已经降临。";
        public string cooldownReason = "花神降临仍在冷却中。";
        public string activatedMessage = "花神降临。";

        public HediffCompProperties_FlowerDivination()
        {
            compClass = typeof(HediffComp_FlowerDivination);
        }
    }

    public class HediffComp_FlowerDivination : HediffComp
    {
        private bool initialized;
        private FlowerDivinationState state = FlowerDivinationState.Ready;
        private int activeTicksLeft;
        private int cooldownTicksLeft;
        private int ticksUntilAfterimage;
        private Vector3 lastAfterimageDrawPos = Vector3.zero;
        private Rot4 lastAfterimageFacing = Rot4.Invalid;
        private bool hasLastAfterimageDrawPos;
        private Effecter activeEffecter;

        public HediffCompProperties_FlowerDivination Props => (HediffCompProperties_FlowerDivination)props;

        public FlowerDivinationState State
        {
            get
            {
                EnsureInitialized();
                return state;
            }
        }

        public bool Ready => State == FlowerDivinationState.Ready;

        public bool Active => State == FlowerDivinationState.Active;

        public bool OnCooldown => State == FlowerDivinationState.Cooldown;

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

            if (state == FlowerDivinationState.Active)
            {
                EnsureActiveEffecter();
                TickAfterimages();

                if (activeTicksLeft > 0)
                {
                    activeTicksLeft--;
                }

                if (activeTicksLeft <= 0)
                {
                    FinishActive();
                }
            }
            else if (state == FlowerDivinationState.Cooldown)
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
            Scribe_Values.Look(ref initialized, "flowerDivination_initialized", false);
            Scribe_Values.Look(ref state, "flowerDivination_state", FlowerDivinationState.Ready);
            Scribe_Values.Look(ref activeTicksLeft, "flowerDivination_activeTicksLeft", 0);
            Scribe_Values.Look(ref cooldownTicksLeft, "flowerDivination_cooldownTicksLeft", 0);
            Scribe_Values.Look(ref ticksUntilAfterimage, "flowerDivination_ticksUntilAfterimage", 0);
            Scribe_Values.Look(ref lastAfterimageDrawPos, "flowerDivination_lastAfterimageDrawPos", Vector3.zero);
            Scribe_Values.Look(ref lastAfterimageFacing, "flowerDivination_lastAfterimageFacing", Rot4.Invalid);
            Scribe_Values.Look(ref hasLastAfterimageDrawPos, "flowerDivination_hasLastAfterimageDrawPos", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureInitialized();
                NormalizeState();
            }
        }

        public bool TryStartDivination()
        {
            EnsureInitialized();
            if (!CanStartDivination(out _))
            {
                return false;
            }

            int activeTicks = ActiveTicksTotal;
            if (activeTicks > 0)
            {
                state = FlowerDivinationState.Active;
                activeTicksLeft = activeTicks;
                cooldownTicksLeft = 0;
                ticksUntilAfterimage = 0;
                EnsureActiveEffecter();
                ResetAfterimageTracking();
            }
            else
            {
                StartCooldown();
            }

            return true;
        }

        public bool CanStartDivination(out string reason)
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

            if (state == FlowerDivinationState.Active)
            {
                reason = Props?.alreadyActiveReason ?? "花神已经降临。";
                return false;
            }

            if (state == FlowerDivinationState.Cooldown)
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
            if (state != FlowerDivinationState.Active)
            {
                return;
            }

            activeTicksLeft = 0;
            ticksUntilAfterimage = 0;
            CleanupActiveEffecter();
            ResetAfterimageTracking();
            StartCooldown();
        }

        public void ConsumeActiveTicks(int ticks)
        {
            EnsureInitialized();
            if (state != FlowerDivinationState.Active || ticks <= 0)
            {
                return;
            }

            activeTicksLeft = Mathf.Max(0, activeTicksLeft - ticks);
            if (activeTicksLeft <= 0)
            {
                FinishActive();
            }
        }

        public void StartCooldown()
        {
            StartCooldown(CooldownTicksTotal);
        }

        public void StartCooldown(int ticks)
        {
            EnsureInitialized();
            activeTicksLeft = 0;
            ticksUntilAfterimage = 0;
            CleanupActiveEffecter();
            ResetAfterimageTracking();
            cooldownTicksLeft = Mathf.Max(0, ticks);
            if (cooldownTicksLeft > 0)
            {
                state = FlowerDivinationState.Cooldown;
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
            state = FlowerDivinationState.Ready;
            activeTicksLeft = 0;
            cooldownTicksLeft = 0;
            ticksUntilAfterimage = 0;
            CleanupActiveEffecter();
            ResetAfterimageTracking();
        }

        private void NormalizeState()
        {
            activeTicksLeft = Mathf.Clamp(activeTicksLeft, 0, ActiveTicksTotal);
            cooldownTicksLeft = Mathf.Clamp(cooldownTicksLeft, 0, CooldownTicksTotal);

            if (state == FlowerDivinationState.Active && activeTicksLeft <= 0)
            {
                StartCooldown();
            }
            else if (state == FlowerDivinationState.Cooldown && cooldownTicksLeft <= 0)
            {
                SetReady();
            }
            else if (state == FlowerDivinationState.Ready)
            {
                activeTicksLeft = 0;
                cooldownTicksLeft = 0;
                ticksUntilAfterimage = 0;
                CleanupActiveEffecter();
                ResetAfterimageTracking();
            }
            if (state == FlowerDivinationState.Active)
            {
                EnsureActiveEffecter();
            }
        }

        private void EnsureActiveEffecter()
        {
            Pawn pawn = Pawn;
            if (activeEffecter != null || Props?.activeEffecter == null || pawn == null || !pawn.Spawned || pawn.MapHeld == null)
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

        public void TickFlowerDivinationSlashAfterimage(Map map, Vector3 drawPos, Rot4 facing)
        {
            EnsureInitialized();
            if (state != FlowerDivinationState.Active)
            {
                return;
            }

            TickAfterimagesAt(map, drawPos, facing, Props?.slashAfterimageIntervalTicks ?? 2);
        }

        private void TickAfterimages()
        {
            Pawn pawn = Pawn;
            if (pawn?.Map == null || !pawn.Spawned || pawn.Dead || pawn.Destroyed)
            {
                return;
            }

            TickAfterimagesAt(pawn.Map, pawn.DrawPos, pawn.Rotation, Props?.afterimageIntervalTicks ?? 6);
        }

        private void TickAfterimagesAt(Map map, Vector3 currentDrawPos, Rot4 currentFacing, int intervalTicks)
        {
            Pawn pawn = Pawn;
            if (map == null || pawn == null || pawn.Dead || pawn.Destroyed)
            {
                return;
            }

            int interval = Mathf.Max(1, intervalTicks);
            if (!hasLastAfterimageDrawPos)
            {
                lastAfterimageDrawPos = currentDrawPos;
                lastAfterimageFacing = currentFacing;
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
            float distanceSquared = (currentDrawPos - lastAfterimageDrawPos).sqrMagnitude;
            float minDistance = Mathf.Max(0.01f, Props?.afterimageMinDistance ?? 0.55f);
            if (distanceSquared < minDistance * minDistance)
            {
                lastAfterimageFacing = currentFacing;
                return;
            }

            map.GetComponent<MapComponent_FlowerDivinationVisuals>()?.AddAfterimage(
                pawn,
                currentDrawPos,
                currentFacing.IsValid ? currentFacing : lastAfterimageFacing,
                Mathf.Max(1, Props?.afterimageFadeTicks ?? 60),
                Mathf.Clamp01(Props?.afterimageStartAlpha ?? 0.44f));

            lastAfterimageDrawPos = currentDrawPos;
            lastAfterimageFacing = currentFacing;
        }

        private void ResetAfterimageTracking()
        {
            lastAfterimageDrawPos = Vector3.zero;
            lastAfterimageFacing = Rot4.Invalid;
            hasLastAfterimageDrawPos = false;
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
