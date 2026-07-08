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

        public HediffCompProperties_DivineBlessing Props => (HediffCompProperties_DivineBlessing)props;

        public int MaxCharges => ResolveMaxCharges();

        public int CurrentCharges => Mathf.Clamp(currentCharges, 0, MaxCharges);

        public bool IsRecharging => CurrentCharges < MaxCharges && RechargeTicksTotal > 0;

        public int RechargeTicksLeft => IsRecharging ? Mathf.Clamp(cooldownTicksLeft, 0, RechargeTicksTotal) : 0;

        public int RechargeTicksTotal
        {
            get
            {
                float factor = MX_QH_HediffUtility.GetDivineFortune(Pawn)?.DivineBlessingRechargeFactor ?? 1f;
                return Mathf.Max(0, Mathf.RoundToInt(Props.retriggerCooldownTicks * Mathf.Max(0f, factor)));
            }
        }

        public float RechargeProgressPercent => IsRecharging && RechargeTicksTotal > 0 ? 1f - Mathf.Clamp01(RechargeTicksLeft / (float)RechargeTicksTotal) : 0f;

        public override void CompPostMake()
        {
            base.CompPostMake();
            SyncChargeBounds();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            SyncChargeBounds();

            if (cooldownTicksLeft > 0)
            {
                cooldownTicksLeft--;
                if (cooldownTicksLeft <= 0)
                {
                    RestoreCharge();
                }
            }

            if (cooldownWarningCooldownTicksLeft > 0)
            {
                cooldownWarningCooldownTicksLeft--;
            }

            TickInvisibilityEndingEffect();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref cooldownTicksLeft, "mx_qh_longBreath_cooldownTicksLeft", 0);
            Scribe_Values.Look(ref cooldownWarningCooldownTicksLeft, "mx_qh_longBreath_cooldownWarningCooldownTicksLeft", 0);
            Scribe_Values.Look(ref invisibilityEndingEffectPlayed, "mx_qh_longBreath_invisibilityEndingEffectPlayed", false);
            Scribe_Values.Look(ref currentCharges, "mx_qh_longBreath_currentCharges", 1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                SyncChargeBounds();
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

            if (cooldownWarningCooldownTicksLeft > 0)
            {
                return;
            }

            if (Pawn.Faction == Faction.OfPlayer)
            {
                Messages.Message("MX_QH_LongBreathLowResourceWarning".Translate(), Pawn, MessageTypeDefOf.CautionInput);
            }

            int maxTicks = Props.cooldownWarningCooldownTicks > 0 ? Props.cooldownWarningCooldownTicks : 600;
            cooldownWarningCooldownTicksLeft = maxTicks;
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
            currentCharges = Mathf.Max(0, CurrentCharges - 1);
            if (CurrentCharges < MaxCharges && cooldownTicksLeft <= 0)
            {
                cooldownTicksLeft = RechargeTicksTotal;
            }
        }

        private void RestoreCharge()
        {
            currentCharges = Mathf.Min(MaxCharges, CurrentCharges + 1);
            cooldownTicksLeft = CurrentCharges < MaxCharges ? RechargeTicksTotal : 0;
        }

        private void SyncChargeBounds()
        {
            int maxCharges = MaxCharges;
            if (currentCharges < 0)
            {
                currentCharges = cooldownTicksLeft > 0 ? Mathf.Max(0, maxCharges - 1) : maxCharges;
            }
            else if (currentCharges > maxCharges)
            {
                currentCharges = maxCharges;
            }

            if (currentCharges >= maxCharges)
            {
                cooldownTicksLeft = 0;
            }
            else if (cooldownTicksLeft <= 0 && RechargeTicksTotal > 0)
            {
                cooldownTicksLeft = RechargeTicksTotal;
            }
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
                return;
            }

            Hediff hediff = GetInvisibilityHediff();
            if (hediff == null)
            {
                invisibilityEndingEffectPlayed = false;
                return;
            }

            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears == null || disappears.ticksToDisappear > 1)
            {
                return;
            }

            HediffComp_Invisibility invisibility = hediff.TryGetComp<HediffComp_Invisibility>();
            if (invisibility != null)
            {
                invisibility.BecomeVisible();
            }

            PlayInvisibilityEffect();
            invisibilityEndingEffectPlayed = true;
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



