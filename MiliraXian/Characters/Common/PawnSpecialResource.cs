using System.Collections.Generic;
using MiliraXian.Characters.Zhaoli;
using UnityEngine;
using Verse;

using RimWorld;

namespace MiliraXian.Characters.Common
{
    public class HediffCompProperties_PawnSpecialResource : HediffCompProperties
    {
        public string resourceLabel = "Resource";
        public string resourceDescription = string.Empty;
        public float maxValue = 100f;
        public float recoveryPerSecond;
        public bool clampToMax = true;
        public bool showGizmo = true;
        public bool hideOnHealthTab = true;
        public Color barColor = new(0.72f, 0.18f, 0.24f, 1f);
        public Color barHighlightColor = new(0.9f, 0.35f, 0.42f, 1f);

        public HediffCompProperties_PawnSpecialResource()
        {
            compClass = typeof(HediffComp_PawnSpecialResource);
        }
    }
    
    /// <summary>
    /// Carrier for a special resource. The value drives severity, so severity legitimately reaches
    /// zero; vanilla would then remove the hediff and discard the comp's saved value with it.
    /// A resource's existence is decided by whoever grants it, never by its current value.
    /// </summary>
    public class Hediff_PawnSpecialResource : HediffWithComps
    {
        public override bool ShouldRemove => false;
    }

    public class HediffComp_PawnSpecialResource : HediffComp
    {
        public const int SettleIntervalTicks = 10;

        private float currentValue;
        private int lastSettleTick = -1;

        public HediffCompProperties_PawnSpecialResource PropsResource => (HediffCompProperties_PawnSpecialResource)props;

        public virtual float CurrentValue => currentValue;

        public virtual float MaxValue
        {
            get
            {
                StatDef stat = PawnSpecialResourceStats.MaxValueStatFor(parent?.def);
                return stat != null && Pawn != null ? Pawn.GetStatValue(stat) : PropsResource.maxValue;
            }
        }

        /// <summary>Signed rate per second: positive recovers, negative decays, zero holds.</summary>
        public float RecoveryPerSecond
        {
            get
            {
                StatDef stat = PawnSpecialResourceStats.RecoveryStatFor(parent?.def);
                return stat != null && Pawn != null ? Pawn.GetStatValue(stat) : PropsResource.recoveryPerSecond;
            }
        }

        public bool IsOverflowing => MaxValue > 0f && CurrentValue > MaxValue;

        public float ValuePercent => MaxValue <= 0f ? 0f : Mathf.Clamp01(CurrentValue / MaxValue);

        public string ResourceLabel => PropsResource.resourceLabel;

        public string ResourceDescription => PropsResource.resourceDescription;

        public Color BarColor => PropsResource.barColor;

        public Color BarHighlightColor => PropsResource.barHighlightColor;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref currentValue, "currentValue", 0f);
            Scribe_Values.Look(ref lastSettleTick, "lastSettleTick", -1);
        }

        /// <summary>
        /// Pushes the value into severity so stage stat modifiers follow it, which also yields
        /// vanilla's Notify_HediffChanged invalidation. Resources whose def keeps severity pinned
        /// to a single stage are unaffected.
        /// </summary>
        private void SyncSeverity()
        {
            if (parent == null || parent.def.maxSeverity <= parent.def.minSeverity)
            {
                return;
            }

            float severity = Mathf.Clamp(currentValue, parent.def.minSeverity, parent.def.maxSeverity);
            if (Mathf.Abs(parent.Severity - severity) > 0.0001f)
            {
                parent.Severity = severity;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Settle(force: false);
        }

        /// <summary>
        /// Applies elapsed continuous recovery. Real elapsed ticks are used rather than assuming
        /// one interval, because map departure, re-adding the hediff and pausing all break that.
        /// Consumption checks must force a settle so they observe the current value.
        /// </summary>
        protected void Settle(bool force)
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (lastSettleTick < 0)
            {
                lastSettleTick = currentTick;
                return;
            }

            int elapsedTicks = Mathf.Max(0, currentTick - lastSettleTick);
            if (elapsedTicks == 0 || (!force && elapsedTicks < SettleIntervalTicks))
            {
                return;
            }

            lastSettleTick = currentTick;
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            float rate = RecoveryPerSecond;
            if (rate == 0f)
            {
                return;
            }

            float maxValue = MaxValue;
            currentValue = Mathf.Max(0f, currentValue + rate * elapsedTicks / 60f);
            ClampCurrentValueTo(maxValue);
            SyncSeverity();
        }

        public override bool CompDisallowVisible()
        {
            return PropsResource.hideOnHealthTab;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (!PropsResource.showGizmo || Pawn == null || Pawn.Dead)
            {
                yield break;
            }

            yield return new PawnSpecialResourceGizmo(this);
            if (Prefs.DevMode && ZhaoliKarmaUtility.IsZhaoli(Pawn))
            {
                yield return new Command_Action
                {
                    defaultLabel = "MX_ZL_DebugAddKarmaLabel".Translate().ToString(),
                    defaultDesc = "MX_ZL_DebugAddKarmaDesc".Translate().ToString(),
                    action = delegate
                    {
                        ZhaoliKarmaUtility.AddKarma(Pawn, 10f);
                    }
                };
            }
        }

        public void SetValue(float value)
        {
            currentValue = Mathf.Max(0f, value);
            ClampCurrentValue();
        }

        /// <summary>
        /// Write-then-check. Reads MaxValue only when a clamp could actually apply, so
        /// clampToMax=false resources never pay for the stat lookup.
        /// </summary>
        protected void ClampCurrentValue()
        {
            if (PropsResource.clampToMax && currentValue > 0f)
            {
                ClampCurrentValueTo(MaxValue);
            }

            SyncSeverity();
        }

        protected void ClampCurrentValueTo(float maxValue)
        {
            if (PropsResource.clampToMax && maxValue > 0f && currentValue > maxValue)
            {
                currentValue = maxValue;
            }
        }

        public virtual void AddValue(float value)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return;
            }

            if (value > 0f)
            {
                StatDef gainStat = PawnSpecialResourceStats.GainFactorStatFor(parent?.def);
                if (gainStat != null && Pawn != null)
                {
                    value *= Pawn.GetStatValue(gainStat);
                }
            }

            currentValue = Mathf.Max(0f, currentValue + value);
            ClampCurrentValue();
        }

        public virtual bool TryConsume(float value)
        {
            if (value < 0f)
            {
                return false;
            }

            // Consumption decides on the current value, so it cannot wait for the next settle point.
            Settle(force: true);

            if (currentValue + 1E-05f < value)
            {
                return false;
            }

            currentValue = Mathf.Max(0f, currentValue - value);
            SyncSeverity();
            return true;
        }
    }

    [StaticConstructorOnStartup]
    public class PawnSpecialResourceGizmo : Gizmo_Slider
    {
        private readonly HediffComp_PawnSpecialResource resource;
        private bool draggingBar;

        protected override float Target
        {
            get => resource.ValuePercent;
            set
            {
            }
        }

        protected override float ValuePercent => resource.ValuePercent;

        protected override Color BarColor => resource.BarColor;

        protected override Color BarHighlightColor => resource.BarHighlightColor;

        protected override bool IsDraggable => false;

        protected override string BarLabel => resource.CurrentValue.ToString("0") + " / " + resource.MaxValue.ToString("0");

        protected override string Title => resource.ResourceLabel;

        protected override bool DraggingBar
        {
            get => draggingBar;
            set => draggingBar = value;
        }

        public PawnSpecialResourceGizmo(HediffComp_PawnSpecialResource resource)
        {
            this.resource = resource;
        }

        protected override string GetTooltip()
        {
            string text = resource.ResourceLabel + ": " + resource.CurrentValue.ToString("0") + " / " + resource.MaxValue.ToString("0");
            if (!resource.ResourceDescription.NullOrEmpty())
            {
                text += "\n\n" + resource.ResourceDescription;
            }

            if (resource.IsOverflowing)
            {
                text += "\n\n" + "MX_Common_ResourceOverflowing".Translate();
            }

            return text;
        }
    }
}
