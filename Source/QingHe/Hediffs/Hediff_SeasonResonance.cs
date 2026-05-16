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
        public float initialSpringAttunement;
        public float initialSummerAttunement;
        public float initialAutumnAttunement;
        public float initialWinterAttunement;
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
        private float springAttunement;
        private float summerAttunement;
        private float autumnAttunement;
        private float winterAttunement;

        public HediffCompProperties_SeasonResonance Props => (HediffCompProperties_SeasonResonance)props;

        public AttunedSeason CurrentAttunedSeason
        {
            get
            {
                EnsureInitialized();
                return currentAttunedSeason;
            }
        }

        public float SpringAttunement
        {
            get
            {
                EnsureInitialized();
                return springAttunement;
            }
        }

        public float SummerAttunement
        {
            get
            {
                EnsureInitialized();
                return summerAttunement;
            }
        }

        public float AutumnAttunement
        {
            get
            {
                EnsureInitialized();
                return autumnAttunement;
            }
        }

        public float WinterAttunement
        {
            get
            {
                EnsureInitialized();
                return winterAttunement;
            }
        }

        public float MaxAttunement => Props?.maxAttunement > 0f ? Props.maxAttunement : 100f;

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
            Scribe_Values.Look(ref springAttunement, "springAttunement", 0f);
            Scribe_Values.Look(ref summerAttunement, "summerAttunement", 0f);
            Scribe_Values.Look(ref autumnAttunement, "autumnAttunement", 0f);
            Scribe_Values.Look(ref winterAttunement, "winterAttunement", 0f);

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

        public void SetAttunement(AttunedSeason season, float value)
        {
            EnsureInitialized();
            value = ClampAttunement(value);
            switch (season)
            {
                case AttunedSeason.Spring:
                    springAttunement = value;
                    break;
                case AttunedSeason.Summer:
                    summerAttunement = value;
                    break;
                case AttunedSeason.Autumn:
                    autumnAttunement = value;
                    break;
                case AttunedSeason.Winter:
                    winterAttunement = value;
                    break;
            }
        }

        public void MeditateAtFlowerCourt(float focusedGain, float secondaryGain)
        {
            EnsureInitialized();
            if (CurrentAttunedSeason == AttunedSeason.None)
            {
                return;
            }

            AddAttunement(CurrentAttunedSeason, focusedGain);
            AddSecondaryAttunement(AttunedSeason.Spring, secondaryGain);
            AddSecondaryAttunement(AttunedSeason.Summer, secondaryGain);
            AddSecondaryAttunement(AttunedSeason.Autumn, secondaryGain);
            AddSecondaryAttunement(AttunedSeason.Winter, secondaryGain);
        }

        public float GetAttunement(AttunedSeason season)
        {
            EnsureInitialized();
            switch (season)
            {
                case AttunedSeason.Spring:
                    return springAttunement;
                case AttunedSeason.Summer:
                    return summerAttunement;
                case AttunedSeason.Autumn:
                    return autumnAttunement;
                case AttunedSeason.Winter:
                    return winterAttunement;
                default:
                    return 0f;
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            currentAttunedSeason = Props?.initialAttunedSeason ?? AttunedSeason.None;
            springAttunement = ClampAttunement(Props?.initialSpringAttunement ?? 0f);
            summerAttunement = ClampAttunement(Props?.initialSummerAttunement ?? 0f);
            autumnAttunement = ClampAttunement(Props?.initialAutumnAttunement ?? 0f);
            winterAttunement = ClampAttunement(Props?.initialWinterAttunement ?? 0f);
            initialized = true;
        }

        private void AddSecondaryAttunement(AttunedSeason season, float value)
        {
            if (season != CurrentAttunedSeason)
            {
                AddAttunement(season, value);
            }
        }

        public void AddAttunement(AttunedSeason season, float value)
        {
            if (Mathf.Approximately(value, 0f) || season == AttunedSeason.None)
            {
                return;
            }

            SetAttunement(season, GetAttunement(season) + value);
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
