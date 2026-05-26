using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Ability;
using MiliraXian.Characters.QingHe.UI;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public enum AttunedSeason
    {
        None,
        Spring,
        Summer,
        Autumn,
        Winter
    }

    public class Hediff_SeasonResonance : HediffWithComps
    {
    }

    public class HediffCompProperties_SeasonResonance : HediffCompProperties
    {
        public AttunedSeason initialAttunedSeason = AttunedSeason.None;
        public float initialAttunement;
        public float maxAttunement = 100f;
        public bool onlyWhenSelected = true;
        public string defaultFlowerMandateLabel = "飞花令";
        public string defaultFlowerMandateDesc = "清荷尚未调谐四时共鸣，暂时无法回应花神。";
        public string defaultFlowerMandateDisabledReason = "尚未调谐四时共鸣。";

        public HediffCompProperties_SeasonResonance()
        {
            compClass = typeof(HediffComp_SeasonResonance);
        }
    }

    public class HediffComp_SeasonResonance : HediffComp
    {
        private bool initialized;
        private AttunedSeason currentAttunedSeason;
        private float attunement;

        public HediffCompProperties_SeasonResonance Props => (HediffCompProperties_SeasonResonance)props;

        public AttunedSeason CurrentAttunedSeason
        {
            get
            {
                EnsureInitialized();
                return currentAttunedSeason;
            }
        }

        public float Attunement
        {
            get
            {
                EnsureInitialized();
                return attunement;
            }
        }

        public float MaxAttunement => Props?.maxAttunement > 0f ? Props.maxAttunement : 100f;

        public HediffComp_FlowerGodDescent FlowerGodDescent => parent?.GetComp<HediffComp_FlowerGodDescent>();

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void Notify_Spawned()
        {
            EnsureInitialized();
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref initialized, "initialized", false);
            Scribe_Values.Look(ref currentAttunedSeason, "currentAttunedSeason", AttunedSeason.None);
            Scribe_Values.Look(ref attunement, "attunement", 0f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureInitialized();
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn == null || Pawn.Dead)
            {
                yield break;
            }

            if (Props.onlyWhenSelected && Find.Selector.SingleSelectedThing != Pawn)
            {
                yield break;
            }

            if (CurrentAttunedSeason != AttunedSeason.None)
            {
                yield return FlowerResourceGizmoFactory.BuildResourceStatusGizmo(Pawn);
            }

            foreach (Gizmo gizmo in SeasonResonanceGizmoFactory.BuildDevCommands(this))
            {
                yield return gizmo;
            }
        }

        public void SetAttunedSeason(AttunedSeason season)
        {
            EnsureInitialized();
            currentAttunedSeason = season;
            SyncFlowerGodFramework();
        }

        public void SetAttunement(float value)
        {
            EnsureInitialized();
            attunement = ClampAttunement(value);
        }

        public void MeditateAtFlowerCourt(float gain)
        {
            EnsureInitialized();
            if (CurrentAttunedSeason == AttunedSeason.None)
            {
                return;
            }

            AddAttunement(gain);
        }

        public void AddAttunement(float value)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return;
            }

            SetAttunement(Attunement + value);
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            currentAttunedSeason = Props?.initialAttunedSeason ?? AttunedSeason.None;
            attunement = ClampAttunement(Props?.initialAttunement ?? 0f);
            initialized = true;
        }

        private float ClampAttunement(float value)
        {
            return value < 0f ? 0f : value > MaxAttunement ? MaxAttunement : value;
        }

        public void SyncFlowerGodFramework()
        {
            FlowerGodFrameworkUtility.SyncSeason(Pawn, CurrentAttunedSeason);
        }
    }
}
