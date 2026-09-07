using System.Collections.Generic;
using MiliraXian.Characters.Zhaoli;
using UnityEngine;
using Verse;

using RimWorld;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_PawnSpecialResource : HediffCompProperties
    {
        public string resourceLabel = "Resource";
        public string resourceDescription = string.Empty;
        public float initialValue;
        public float maxValue = 100f;
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
    
    public class HediffComp_PawnSpecialResource : HediffComp
    {
        private float currentValue;
        private bool initialized;

        public HediffCompProperties_PawnSpecialResource PropsResource => (HediffCompProperties_PawnSpecialResource)props;

        public virtual float CurrentValue
        {
            get
            {
                EnsureInitialized();
                return currentValue;
            }
        }

        public virtual float MaxValue => PropsResource.maxValue;

        public bool IsOverflowing => MaxValue > 0f && CurrentValue > MaxValue;

        public float ValuePercent => MaxValue <= 0f ? 0f : Mathf.Clamp01(CurrentValue / MaxValue);

        public string ResourceLabel => PropsResource.resourceLabel;

        public string ResourceDescription => PropsResource.resourceDescription;

        public Color BarColor => PropsResource.barColor;

        public Color BarHighlightColor => PropsResource.barHighlightColor;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref currentValue, "currentValue", 0f);
            Scribe_Values.Look(ref initialized, "initialized", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureInitialized();
            }
        }

        public override void Notify_Spawned()
        {
            EnsureInitialized();
        }

        public override bool CompDisallowVisible()
        {
            return PropsResource.hideOnHealthTab;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            EnsureInitialized();
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
            EnsureInitialized();
            currentValue = NormalizeValue(value);
        }

        protected void ClampCurrentValueTo(float maxValue)
        {
            EnsureInitialized();
            if (PropsResource.clampToMax && maxValue > 0f && currentValue > maxValue)
            {
                currentValue = maxValue;
            }
        }

        public void AddValue(float value)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return;
            }

            EnsureInitialized();
            currentValue = NormalizeValue(currentValue + value);
        }

        public bool TryConsume(float value)
        {
            if (value < 0f)
            {
                return false;
            }

            EnsureInitialized();
            if (currentValue + 1E-05f < value)
            {
                return false;
            }

            currentValue = NormalizeValue(currentValue - value);
            return true;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            currentValue = NormalizeValue(PropsResource.initialValue);
            initialized = true;
        }

        private float NormalizeValue(float value)
        {
            value = Mathf.Max(0f, value);
            if (PropsResource.clampToMax && MaxValue > 0f)
            {
                value = Mathf.Min(value, MaxValue);
            }

            return value;
        }
    }

    public interface ISpecialResourceAddHandler
    {
        void AddResourceValue(float value);

        bool TryConsumeResourceValue(float value);
    }

    public interface ISpecialResourceValueAdapter
    {
        float CurrentResourceValue { get; }

        float MaxResourceValue { get; }
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
