using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_DivineBlessing : HediffCompProperties
    {
        public HediffDef invisibilityHediffDef;
        public HediffDef damageImmunityHediffDef;

        public float lethalTriggerHealthPercent = 0.12f;
        public float minimumDamageForLethalCheck = 8f;

        public int invisibilityDurationTicks = 600;
        public int damageImmunityDurationTicks = 180;
        public int retriggerCooldownTicks = 3600;
        public int cooldownWarningCooldownTicks = 600;
        public int maxCharges = 1;

        public HediffCompProperties_DivineBlessing()
        {
            compClass = typeof(HediffComp_DivineBlessing);
        }
    }

    /// <summary>
    /// Long Breath core:
    /// - Trigger on lethal or part-destroying incoming damage.
    /// - Uses an internal cooldown.
    /// - Negates current hit.
    /// - Restores missing parts and all non-permanent injuries.
    /// - Grants temporary Psychic Invisibility when available.
    /// </summary>
    public class HediffComp_DivineBlessing : HediffComp
    {
        private int cooldownTicksLeft;
        private int cooldownWarningCooldownTicksLeft;
        private bool invisibilityEndingEffectPlayed;
        private int currentCharges = -1;
        private int cooldownEndTick = -1;
        private int cooldownWarningEndTick = -1;
        private int nextChargeConfigurationRefreshTick;
        private int nextInvisibilityEndingCheckTick;
        private int cachedMaxCharges = 1;
        private int cachedRechargeTicksTotal;
        private bool runtimeStateInitialized;

        public HediffCompProperties_DivineBlessing Props => (HediffCompProperties_DivineBlessing)props;

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        public int MaxCharges
        {
            get
            {
                UpdateRuntimeState();
                return cachedMaxCharges;
            }
        }

        public int CurrentCharges
        {
            get
            {
                UpdateRuntimeState();
                return Mathf.Clamp(currentCharges, 0, cachedMaxCharges);
            }
        }

        public bool IsRecharging => CurrentCharges < MaxCharges && RechargeTicksTotal > 0;

        public int RechargeTicksLeft => IsRecharging ? Mathf.Clamp(cooldownEndTick - CurrentTick, 0, RechargeTicksTotal) : 0;

        public int RechargeTicksTotal
        {
            get
            {
                UpdateRuntimeState();
                return cachedRechargeTicksTotal;
            }
        }

        public float RechargeProgressPercent => IsRecharging && RechargeTicksTotal > 0 ? 1f - Mathf.Clamp01(RechargeTicksLeft / (float)RechargeTicksTotal) : 0f;

        public override void CompPostMake()
        {
            base.CompPostMake();
            InitializeRuntimeState(useSerializedRemainingTicks: false);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            UpdateRuntimeState();
            if (CurrentTick >= nextInvisibilityEndingCheckTick)
            {
                TickInvisibilityEndingEffect();
            }
        }

        public override void CompExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                UpdateRuntimeState();
                cooldownTicksLeft = Mathf.Max(0, cooldownEndTick - CurrentTick);
                cooldownWarningCooldownTicksLeft = Mathf.Max(0, cooldownWarningEndTick - CurrentTick);
            }

            base.CompExposeData();
            Scribe_Values.Look(ref cooldownTicksLeft, "mx_qh_longBreath_cooldownTicksLeft", 0);
            Scribe_Values.Look(ref cooldownWarningCooldownTicksLeft, "mx_qh_longBreath_cooldownWarningCooldownTicksLeft", 0);
            Scribe_Values.Look(ref invisibilityEndingEffectPlayed, "mx_qh_longBreath_invisibilityEndingEffectPlayed", false);
            Scribe_Values.Look(ref currentCharges, "mx_qh_longBreath_currentCharges", 1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitializeRuntimeState(useSerializedRemainingTicks: true);
            }
        }

        public bool CanTrigger(ref DamageInfo dinfo)
        {
            if (Pawn == null || Pawn.Dead || Pawn.health == null)
            {
                return false;
            }

            if (CurrentCharges <= 0)
            {
                return false;
            }

            return IsLikelyLethal(dinfo) || WillDestroyHitPart(dinfo);
        }

        public void NotifyDamageNotAbsorbed(ref DamageInfo dinfo)
        {
            if (Pawn == null || Pawn.Dead || Pawn.health == null || dinfo.Amount <= 0f)
            {
                return;
            }

            if (CurrentCharges > 0)
            {
                return;
            }

            if (!IsLikelyLethal(dinfo) && !WillDestroyHitPart(dinfo))
            {
                return;
            }

            UpdateRuntimeState();
            if (CurrentTick < cooldownWarningEndTick)
            {
                return;
            }

            if (Pawn.Faction == Faction.OfPlayer)
            {
                Messages.Message("MX_QH_LongBreathLowResourceWarning".Translate(), Pawn, MessageTypeDefOf.CautionInput);
            }

            int maxTicks = Props.cooldownWarningCooldownTicks > 0 ? Props.cooldownWarningCooldownTicks : 600;
            cooldownWarningCooldownTicksLeft = maxTicks;
            cooldownWarningEndTick = CurrentTick + maxTicks;
        }

        public void Trigger(ref DamageInfo dinfo, ref bool absorbed)
        {
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            absorbed = true;
            dinfo.SetAmount(0f);

            RestoreAllDamage();
            PlayInvisibilityEffect();
            invisibilityEndingEffectPlayed = false;
            ApplyInvisibility();
            ApplyDamageImmunity();
            StartCooldown();

            if (Pawn.Faction == Faction.OfPlayer)
            {
                Messages.Message("MX_QH_LongBreathTriggered".Translate(), Pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        private bool IsLikelyLethal(DamageInfo dinfo)
        {
            if (Pawn?.health == null || dinfo.Def == null)
            {
                return false;
            }

            var part = dinfo.HitPart ?? Pawn.RaceProps?.body.corePart;
            if (part == null)
            {
                return false;
            }

            var incomingHediff = HealthUtility.GetHediffDefFromDamage(dinfo.Def, Pawn, part);
            if (incomingHediff != null)
            {
                float projectedSeverity = GetProjectedSeverity(dinfo);
                var wouldDie = Pawn.health.WouldDieAfterAddingHediff(incomingHediff, part, projectedSeverity);
                if (wouldDie)
                {
                    return true;
                }

                var wouldDown = Pawn.health.WouldBeDownedAfterAddingHediff(incomingHediff, part, projectedSeverity);
                if (wouldDown)
                {
                    return true;
                }
            }

            // Fallback: when summary health is already critically low, treat a sufficient hit as lethal-like.
            if (dinfo.Amount >= Props.minimumDamageForLethalCheck
                && Pawn.health.summaryHealth.SummaryHealthPercent <= Props.lethalTriggerHealthPercent + 0.0001f)
            {
                return true;
            }

            return false;
        }

        private bool WillDestroyHitPart(DamageInfo dinfo)
        {
            if (Pawn?.health == null || dinfo.Def == null)
            {
                return false;
            }

            float projectedSeverity = GetProjectedSeverity(dinfo);
            if (projectedSeverity <= 0f)
            {
                return false;
            }

            BodyPartRecord hitPart = dinfo.HitPart;
            if (hitPart != null)
            {
                return WouldDestroySpecificPart(dinfo, hitPart, projectedSeverity);
            }

            // Fallback for cases where HitPart is not resolved yet at PreApplyDamage stage:
            // conservatively predict against all hittable violence parts.
            foreach (BodyPartRecord candidate in Pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null))
            {
                if (WouldDestroySpecificPart(dinfo, candidate, projectedSeverity))
                {
                    return true;
                }
            }

            return false;
        }

        private float GetProjectedSeverity(DamageInfo dinfo)
        {
            float projectedSeverity = dinfo.Amount;
            if (projectedSeverity <= 0f)
            {
                return 0f;
            }

            if (dinfo.Def != null && dinfo.Def.ExternalViolenceFor(Pawn))
            {
                projectedSeverity *= Pawn.GetStatValue(StatDefOf.IncomingDamageFactor, true);
            }

            return projectedSeverity;
        }

        private bool WouldDestroySpecificPart(DamageInfo dinfo, BodyPartRecord part, float projectedSeverity)
        {
            if (part == null || !part.def.destroyableByDamage || Pawn.health.hediffSet.PartIsMissing(part))
            {
                return false;
            }

            if (!IsViolenceHittablePart(part))
            {
                return false;
            }

            HediffDef incomingHediff = HealthUtility.GetHediffDefFromDamage(dinfo.Def, Pawn, part);
            if (incomingHediff == null)
            {
                return false;
            }

            if (Pawn.health.WouldLosePartAfterAddingHediff(incomingHediff, part, projectedSeverity))
            {
                return true;
            }

            // Fallback to direct part-health comparison for compatibility with unusual damage defs.
            float partHealth = Pawn.health.hediffSet.GetPartHealth(part);
            return partHealth > 0f && projectedSeverity >= partHealth;
        }

        private bool IsViolenceHittablePart(BodyPartRecord part)
        {
            return part.depth == BodyPartDepth.Outside
                   || (part.depth == BodyPartDepth.Inside && part.def.IsSolid(part, Pawn.health.hediffSet.hediffs));
        }

        private void StartCooldown()
        {
            UpdateRuntimeState();
            currentCharges = Mathf.Max(0, currentCharges - 1);
            if (currentCharges < cachedMaxCharges && CurrentTick >= cooldownEndTick)
            {
                cooldownTicksLeft = cachedRechargeTicksTotal;
                cooldownEndTick = cachedRechargeTicksTotal > 0 ? CurrentTick + cachedRechargeTicksTotal : -1;
            }
        }

        private void RestoreCharge()
        {
            currentCharges = Mathf.Min(cachedMaxCharges, Mathf.Max(0, currentCharges) + 1);
            cooldownTicksLeft = currentCharges < cachedMaxCharges ? cachedRechargeTicksTotal : 0;
            cooldownEndTick = cooldownTicksLeft > 0 ? CurrentTick + cooldownTicksLeft : -1;
        }

        private void SyncChargeBounds(int currentTick)
        {
            if (currentCharges < 0)
            {
                currentCharges = currentTick < cooldownEndTick ? Mathf.Max(0, cachedMaxCharges - 1) : cachedMaxCharges;
            }
            else if (currentCharges > cachedMaxCharges)
            {
                currentCharges = cachedMaxCharges;
            }

            if (currentCharges >= cachedMaxCharges)
            {
                cooldownTicksLeft = 0;
                cooldownEndTick = -1;
            }
            else if (currentTick >= cooldownEndTick && cachedRechargeTicksTotal > 0)
            {
                cooldownTicksLeft = cachedRechargeTicksTotal;
                cooldownEndTick = currentTick + cachedRechargeTicksTotal;
            }
        }

        private void InitializeRuntimeState(bool useSerializedRemainingTicks)
        {
            int currentTick = CurrentTick;
            cooldownEndTick = useSerializedRemainingTicks && cooldownTicksLeft > 0
                ? currentTick + cooldownTicksLeft
                : -1;
            cooldownWarningEndTick = useSerializedRemainingTicks && cooldownWarningCooldownTicksLeft > 0
                ? currentTick + cooldownWarningCooldownTicksLeft
                : -1;
            nextChargeConfigurationRefreshTick = currentTick;
            nextInvisibilityEndingCheckTick = currentTick;
            runtimeStateInitialized = true;
            RefreshChargeConfiguration(force: true);
        }

        private void UpdateRuntimeState()
        {
            if (!runtimeStateInitialized)
            {
                InitializeRuntimeState(useSerializedRemainingTicks: true);
            }

            int currentTick = CurrentTick;
            if (cooldownEndTick > 0 && currentTick >= cooldownEndTick && currentCharges < cachedMaxCharges)
            {
                RestoreCharge();
            }

            RefreshChargeConfiguration(force: false);

            if (cooldownWarningEndTick > 0 && currentTick >= cooldownWarningEndTick)
            {
                cooldownWarningEndTick = -1;
                cooldownWarningCooldownTicksLeft = 0;
            }
        }

        private void RefreshChargeConfiguration(bool force)
        {
            int currentTick = CurrentTick;
            if (!force && currentTick < nextChargeConfigurationRefreshTick)
            {
                return;
            }

            cachedMaxCharges = ResolveMaxCharges();
            float speed = Pawn == null || MX_QHDefOf.MX_QH_DivineBlessingRechargeSpeedFactor == null
                ? 1f
                : Pawn.GetStatValue(MX_QHDefOf.MX_QH_DivineBlessingRechargeSpeedFactor);
            cachedRechargeTicksTotal = speed <= 0f
                ? 0
                : Mathf.Max(0, Mathf.RoundToInt(Props.retriggerCooldownTicks / speed));
            nextChargeConfigurationRefreshTick = currentTick + 60;
            SyncChargeBounds(currentTick);
        }

        private int ResolveMaxCharges()
        {
            int maxCharges = Mathf.Max(1, Props.maxCharges);
            HediffComp_SkillTreeState state = MX_QH_HediffUtility.EnsureFlowerResonance(Pawn);
            if (state != null && state.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Yingyue))
            {
                maxCharges++;
            }

            return maxCharges;
        }

        private void RestoreAllDamage()
        {
            List<Hediff> hediffs = new List<Hediff>(Pawn.health.hediffSet.hediffs);

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_MissingPart missing = hediffs[i] as Hediff_MissingPart;
                if (missing != null && missing.Part != null)
                {
                    Pawn.health.RestorePart(missing.Part);
                }
            }

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_Injury injury = hediffs[i] as Hediff_Injury;
                if (injury != null && !injury.IsPermanent())
                {
                    Pawn.health.RemoveHediff(injury);
                }
            }
        }

        private void PlayInvisibilityEffect()
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.MapHeld == null)
            {
                return;
            }

            FleckDef fleck = MX_QHDefOf.PsycastPsychicEffect;
            if (fleck == null)
            {
                fleck = MX_QHDefOf.PsycastSkipFlashExit;
            }

            if (fleck == null)
            {
                fleck = FleckDefOf.ExplosionFlash;
            }

            FleckMaker.Static(Pawn.Position, Pawn.MapHeld, fleck, 1f);
        }

        private void TickInvisibilityEndingEffect()
        {
            if (invisibilityEndingEffectPlayed || Pawn?.health?.hediffSet == null)
            {
                nextInvisibilityEndingCheckTick = CurrentTick + 60;
                return;
            }

            Hediff hediff = GetInvisibilityHediff();
            if (hediff == null)
            {
                invisibilityEndingEffectPlayed = false;
                nextInvisibilityEndingCheckTick = CurrentTick + 60;
                return;
            }

            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears == null)
            {
                nextInvisibilityEndingCheckTick = CurrentTick + 60;
                return;
            }

            if (disappears.ticksToDisappear > 1)
            {
                nextInvisibilityEndingCheckTick = CurrentTick + Mathf.Max(1, disappears.ticksToDisappear - 1);
                return;
            }

            HediffComp_Invisibility invisibility = hediff.TryGetComp<HediffComp_Invisibility>();
            if (invisibility != null)
            {
                invisibility.BecomeVisible();
            }

            PlayInvisibilityEffect();
            invisibilityEndingEffectPlayed = true;
            nextInvisibilityEndingCheckTick = CurrentTick + 60;
        }

        private void ApplyInvisibility()
        {
            HediffDef invisDef = GetInvisibilityDef();
            if (invisDef == null)
            {
                return;
            }

            Hediff hediff = Pawn.health.hediffSet.GetFirstHediffOfDef(invisDef);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(invisDef, Pawn);
                Pawn.health.AddHediff(hediff);
            }

            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears != null && Props.invisibilityDurationTicks > 0)
            {
                disappears.SetDuration(Props.invisibilityDurationTicks);
                nextInvisibilityEndingCheckTick = CurrentTick + Mathf.Max(1, Props.invisibilityDurationTicks - 1);
            }
            else
            {
                nextInvisibilityEndingCheckTick = CurrentTick + 60;
            }
        }

        private void ApplyDamageImmunity()
        {
            HediffDef immunityDef = Props.damageImmunityHediffDef ?? MX_QHDefOf.MX_QH_DivineBlessingImmunity;
            if (immunityDef == null)
            {
                return;
            }

            Hediff hediff = Pawn.health.hediffSet.GetFirstHediffOfDef(immunityDef);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(immunityDef, Pawn);
                Pawn.health.AddHediff(hediff);
            }

            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears != null && Props.damageImmunityDurationTicks > 0)
            {
                disappears.SetDuration(Props.damageImmunityDurationTicks);
            }
        }

        private Hediff GetInvisibilityHediff()
        {
            HediffDef invisDef = GetInvisibilityDef();
            return invisDef == null ? null : Pawn.health.hediffSet.GetFirstHediffOfDef(invisDef);
        }

        private HediffDef GetInvisibilityDef()
        {
            return Props.invisibilityHediffDef ?? MX_QHDefOf.PsychicInvisibility;
        }
    }
}



