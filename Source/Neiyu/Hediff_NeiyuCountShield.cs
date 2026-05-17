
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Neiyu
{
    public struct MXNeiyuStage3Profile
    {
        public float outgoingDamageFactor;
        public float aimingDelayFactor;
        public float incomingDamageFactor;
        public float moveSpeedFactor;
        public float injuryHealingFactor;
        public float meleeDodgeChanceFactor;
        public float rangedDodgeBonusPct;
        public float meleeArmorPenetrationFactor;
    }

    public class HediffCompProperties_MXNeiyuCountShield : HediffCompProperties
    {

        public float phase2Threshold = 18f;

        public int phase2MaxChargesNormal = 1000;
        public int phase2MaxChargesWeak = 250;
        
        public int phase2RecoverTicksNoChange = 30000;
        
        public int stage3AbsorbTicks = 5000;
        public int stage3BuffTicks = 55000;
        public int stage3DurationTicks = 60000;
        public int weakDurationTicks = 300000;


        public float stage3TierA_MaxDamage = 100f;
        public float stage3TierB_MaxDamage = 500f;
        public float stage3TierC_MaxDamage = 1000f;
        public float stage3TierD_ExtraStepDamage = 500f;


        public float bloodLossTierB = 0.10f;
        public float bloodLossTierC = 0.30f;
        public float bloodLossTierD = 0.50f;


        public string absorbFleckDefName = "ExplosionFlash";
        public List<string> hurtFleckDefNames = new List<string>();
        public float absorbFleckScale = 1.2f;
        public string absorbEffecterDefName = null;


        public bool drawActiveShield = true;
        public string activeShieldTexPath = "MiliraXianNeiyu/Effect/Neiyu_Shield/Shield";
        public Vector2 activeShieldDrawSize = new Vector2(3.6f, 3.6f);
        public float activeShieldAlpha = 0.55f;
        public float activeShieldAltitudeOffset = 0f;
        public float activeShieldPulseMin = 0.96f;
        public float activeShieldPulseMax = 1.06f;
        public int activeShieldPulseTicks = 75;

        public bool showDebugLabel = true;

        public HediffCompProperties_MXNeiyuCountShield()
        {
            compClass = typeof(HediffComp_MXNeiyuCountShield);
        }
    }

    public class HediffComp_MXNeiyuCountShield : HediffComp
    {

        private int stage = 1;

        private int phase2Charges;
        private int phase2LastChargeChangeTick = -1;

        private float phase3StoredDamage;
        private int phase3AbsorbUntilTick = -1;
        private int phase3EndTick = -1;
        private bool stage3BuffPhaseAnnounced;
        private bool weakShieldExhaustedAnnounced;
        private int lastAbsorbTick = -1;
        private int lastPenetrateTick = -1;

        private int weakUntilTick = -1;
        private bool weakWasActive;


        private List<string> phase2RecentHitLogs = new List<string>();

        public HediffCompProperties_MXNeiyuCountShield Props => (HediffCompProperties_MXNeiyuCountShield)props;

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        public bool InWeak => weakUntilTick > CurrentTick;

        // 基础属性（供 Gizmo 使用）
        public int Stage => stage;
        public int Phase2Charges => phase2Charges;
        public int Phase2MaxCharges => InWeak ? Props.phase2MaxChargesWeak : Props.phase2MaxChargesNormal;
        public float Phase3StoredDamage => phase3StoredDamage;
        public int Phase3EndTick => phase3EndTick;
        public int Phase3AbsorbUntilTick => phase3AbsorbUntilTick;
        public int WeakUntilTick => weakUntilTick;
        public int CurrentTickForDisplay => CurrentTick;
        public int LastAbsorbTick => lastAbsorbTick;
        public int LastPenetrateTick => lastPenetrateTick;

        // 三阶蓄伤阶段已过比例 0~1
        public float Stage3AbsorbProgress
        {
            get
            {
                if (stage != 3 || phase3AbsorbUntilTick <= 0) return 0f;
                int now = CurrentTick;
                if (now >= phase3AbsorbUntilTick) return 1f;
                int startTick = phase3AbsorbUntilTick - ResolveStage3AbsorbTicks();
                if (startTick <= 0) return 0f;
                return Mathf.Clamp01((float)(now - startTick) / (phase3AbsorbUntilTick - startTick));
            }
        }

        // 三阶增益阶段已过比例 0~1
        public float Stage3BuffProgress
        {
            get
            {
                if (stage != 3 || phase3EndTick <= 0 || phase3AbsorbUntilTick <= 0) return 0f;
                int now = CurrentTick;
                if (now < phase3AbsorbUntilTick) return 0f;
                if (now >= phase3EndTick) return 1f;
                return Mathf.Clamp01((float)(now - phase3AbsorbUntilTick) / (phase3EndTick - phase3AbsorbUntilTick));
            }
        }

        // 二阶命中日志（用于 tooltip）
        public List<string> Phase2RecentHitLogs => phase2RecentHitLogs;

        public override void CompPostMake()
        {
            base.CompPostMake();
            stage = 1;
            phase2Charges = 0;
            phase2LastChargeChangeTick = CurrentTick;
            phase3StoredDamage = 0f;
            phase3AbsorbUntilTick = -1;
            phase3EndTick = -1;
            stage3BuffPhaseAnnounced = false;
            weakShieldExhaustedAnnounced = false;
            weakUntilTick = -1;
            weakWasActive = false;
            EnsureRecentLogs();
            phase2RecentHitLogs.Clear();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref stage, "mxnl_shield_stage", 1);
            Scribe_Values.Look(ref phase2Charges, "mxnl_shield_phase2Charges", 0);
            Scribe_Values.Look(ref phase2LastChargeChangeTick, "mxnl_shield_phase2LastChangeTick", -1);
            Scribe_Values.Look(ref phase3StoredDamage, "mxnl_shield_phase3StoredDamage", 0f);
            Scribe_Values.Look(ref phase3AbsorbUntilTick, "mxnl_shield_phase3AbsorbUntilTick", -1);
            Scribe_Values.Look(ref phase3EndTick, "mxnl_shield_phase3EndTick", -1);
            Scribe_Values.Look(ref stage3BuffPhaseAnnounced, "mxnl_shield_stage3BuffPhaseAnnounced", false);
            Scribe_Values.Look(ref weakUntilTick, "mxnl_shield_weakUntilTick", -1);
            Scribe_Values.Look(ref weakWasActive, "mxnl_shield_weakWasActive", false);
            Scribe_Collections.Look(ref phase2RecentHitLogs, "mxnl_shield_phase2RecentHitLogs", LookMode.Value);
            EnsureRecentLogs();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            int now = CurrentTick;
            UpdateWeakTransition(now);
            NormalizeStage3Ticks(now);

            if (stage == 3)
            {
                if (phase3EndTick > 0 && now >= phase3EndTick)
                {
                    FinishStage3AndEnterWeak(now);
                    return;
                }

                if (!stage3BuffPhaseAnnounced && IsInStage3BuffWindow(now))
                {
                    stage3BuffPhaseAnnounced = true;
                    PlayShieldBreakFx();
                    if (Pawn != null)
                    {
                        Messages.Message("MX_NL_ShieldStage3BuffStarted".Translate(Pawn.LabelShort), Pawn, MessageTypeDefOf.NeutralEvent);
                    }
                }
            }

            if (stage == 2 && phase2LastChargeChangeTick > 0)
            {
                if (now - phase2LastChargeChangeTick >= Props.phase2RecoverTicksNoChange)
                {
                    EnterStage1(now);
                }
            }
        }

        public bool TryAbsorb(ref DamageInfo dinfo, ref bool absorbed)
        {
            if (absorbed || Pawn == null || Pawn.Dead || dinfo.Amount <= 0f)
            {
                return false;
            }

            int now = CurrentTick;
            UpdateWeakTransition(now);
            NormalizeStage3Ticks(now);

            if (stage == 3)
            {
                if (IsInStage3AbsorbWindow(now))
                {

                    phase3StoredDamage += dinfo.Amount;
                    absorbed = true;
                    PlayAbsorbFx(dinfo);
                    return true;
                }


                return false;
            }

            if (stage == 1)
            {
                bool lethalOrDowning = IsLethalOrDowning(dinfo);

                absorbed = true;
                PlayAbsorbFx(dinfo);

                if (lethalOrDowning && !InWeak)
                {
                    EnterStage3(now);
                    NotifyStage1Transition(lethalOrDowning: true, targetStage: 3);
                }
                else
                {
                    int maxCharges = InWeak ? Props.phase2MaxChargesWeak : Props.phase2MaxChargesNormal;
                    EnterStage2(now, maxCharges);
                    NotifyStage1Transition(lethalOrDowning, targetStage: 2);
                }
                return true;
            }

            if (stage == 2)
            {
                int before = phase2Charges;
                int cost = CalculatePhase2Cost(dinfo.Amount);

                if (cost <= 0)
                {
                    absorbed = true;
                    PlayAbsorbFx(dinfo);
                    RecordPhase2Hit(dinfo.Amount, 0, before, before);
                    return true;
                }

                if (before <= 0)
                {
                    if (InWeak)
                    {
                        if (!weakShieldExhaustedAnnounced)
                        {
                            weakShieldExhaustedAnnounced = true;
                            Messages.Message("MX_NL_ShieldWeakExhausted".Translate(Pawn.LabelShort), Pawn, MessageTypeDefOf.ThreatBig);
                        }
                        PlayShieldBreakFx();
                        return false;
                    }

                    absorbed = true;
                    PlayAbsorbFx(dinfo);
                    EnterStage3(now);
                    return true;
                }

                if (cost > before)
                {
                    if (InWeak)
                    {
                        if (!weakShieldExhaustedAnnounced)
                        {
                            weakShieldExhaustedAnnounced = true;
                            Messages.Message("MX_NL_ShieldWeakExhausted".Translate(Pawn.LabelShort), Pawn, MessageTypeDefOf.ThreatBig);
                        }

                        phase2Charges = 0;
                        phase2LastChargeChangeTick = now;
                        RecordPhase2Hit(dinfo.Amount, before, before, 0);
                        PlayShieldBreakFx();
                        return false;
                    }

                    absorbed = true;
                    PlayAbsorbFx(dinfo);
                    phase2Charges = 0;
                    phase2LastChargeChangeTick = now;
                    lastPenetrateTick = now;
                    RecordPhase2Hit(dinfo.Amount, before, before, 0);
                    EnterStage3(now);
                    return true;
                }

                phase2Charges = before - cost;
                phase2LastChargeChangeTick = now;
                lastPenetrateTick = now;
                absorbed = true;
                PlayAbsorbFx(dinfo);
                RecordPhase2Hit(dinfo.Amount, cost, before, phase2Charges);

                if (phase2Charges <= 0 && !InWeak)
                {
                    EnterStage3(now);
                }

                return true;
            }

            return false;
        }


        public bool TryGetStage3Profile(out MXNeiyuStage3Profile profile)
        {
            profile = default(MXNeiyuStage3Profile);
            NormalizeStage3Ticks(CurrentTick);

            if (!IsInStage3BuffWindow(CurrentTick))
            {
                return false;
            }

            profile.outgoingDamageFactor = 1f;
            profile.aimingDelayFactor = 1f;
            profile.incomingDamageFactor = 1f;
            profile.moveSpeedFactor = 1f;
            profile.injuryHealingFactor = 1f;
            profile.meleeDodgeChanceFactor = 1f;
            profile.rangedDodgeBonusPct = 0f;
            profile.meleeArmorPenetrationFactor = 1f;

            float d = phase3StoredDamage;


            if (d <= Props.stage3TierA_MaxDamage)
            {
                return true;
            }


            if (d <= Props.stage3TierB_MaxDamage)
            {
                profile.outgoingDamageFactor = 1.20f;
                profile.aimingDelayFactor = 0.90f;
                profile.incomingDamageFactor = 0.95f;
                profile.meleeArmorPenetrationFactor = 1.10f;
                profile.meleeDodgeChanceFactor = 1.10f;
                profile.rangedDodgeBonusPct = 0.10f;
                return true;
            }


            if (d <= Props.stage3TierC_MaxDamage)
            {
                profile.outgoingDamageFactor = 1.35f;
                profile.moveSpeedFactor = 1.20f;
                profile.injuryHealingFactor = 1.70f;
                profile.aimingDelayFactor = 0.80f;
                profile.incomingDamageFactor = 0.90f;
                profile.meleeArmorPenetrationFactor = 1.10f;
                profile.meleeDodgeChanceFactor = 1.10f;
                profile.rangedDodgeBonusPct = 0.10f;
                return true;
            }


            int stacks = 1 + Mathf.FloorToInt((d - Props.stage3TierC_MaxDamage) / Mathf.Max(1f, Props.stage3TierD_ExtraStepDamage));
            profile.outgoingDamageFactor = 1f + 0.35f * stacks;
            profile.moveSpeedFactor = 1f + 0.10f * stacks;
            profile.injuryHealingFactor = 1f + 0.35f * stacks;
            profile.incomingDamageFactor = Mathf.Max(0.10f, 1f - 0.10f * stacks);
            profile.meleeArmorPenetrationFactor = 1f + 0.10f * stacks;
            profile.meleeDodgeChanceFactor = 1f + 0.10f * stacks;
            profile.rangedDodgeBonusPct = 0.10f * stacks;


            return true;
        }

        public bool TryGetWeakPenaltyFactors(out float moveSpeedFactor, out float restFallRateFactor, out float workSpeedGlobalFactor)
        {
            moveSpeedFactor = 1f;
            restFallRateFactor = 1f;
            workSpeedGlobalFactor = 1f;

            if (!InWeak)
            {
                return false;
            }





            moveSpeedFactor = 0.5f;
            restFallRateFactor = 1.5f;
            workSpeedGlobalFactor = 0.2f;
            return true;
        }

        public bool ShouldDrawActiveShield
        {
            get
            {
                if (!Props.drawActiveShield || Pawn == null || !Pawn.Spawned || Pawn.Dead)
                {
                    return false;
                }

                int now = CurrentTick;
                NormalizeStage3Ticks(now);
                if (stage == 2 && InWeak && phase2Charges <= 0)
                    return false;
                return stage == 2 || IsInStage3AbsorbWindow(now);
            }
        }

        public float ActiveShieldPulseScale
        {
            get
            {
                int period = Mathf.Max(1, Props.activeShieldPulseTicks);
                float min = Mathf.Min(Props.activeShieldPulseMin, Props.activeShieldPulseMax);
                float max = Mathf.Max(Props.activeShieldPulseMin, Props.activeShieldPulseMax);
                float t = (CurrentTick % period) / (float)period;
                float wave = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
                return Mathf.Lerp(min, max, wave);
            }
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (!Props.showDebugLabel)
                {
                    return null;
                }

                string txt = "MX_NL_ShieldDebugName".Translate().ToString();
                if (stage == 1)
                {
                    txt += " I";
                }
                else if (stage == 2)
                {
                    txt += " II " + phase2Charges + "/" + (InWeak ? Props.phase2MaxChargesWeak : Props.phase2MaxChargesNormal);
                }
                else if (stage == 3)
                {
                    int now = CurrentTick;
                    if (IsInStage3AbsorbWindow(now))
                    {
                        int remainAbsorb = Math.Max(0, phase3AbsorbUntilTick - now);
                        txt += " " + "MX_NL_ShieldDebugStage3AbsorbLabel".Translate(
                            phase3StoredDamage.ToString("F0"),
                            (remainAbsorb / 2500f).ToString("F1")).ToString();
                    }
                    else
                    {
                        int remainBuff = Math.Max(0, phase3EndTick - now);
                        txt += " " + "MX_NL_ShieldDebugStage3BuffLabel".Translate(
                            GetStage3TierLabel(),
                            (remainBuff / 2500f).ToString("F1")).ToString();
                    }
                }

                if (InWeak)
                {
                    int weakRemain = Math.Max(0, weakUntilTick - CurrentTick);
                    txt += " " + "MX_NL_ShieldDebugWeakLabel".Translate((weakRemain / 2500f).ToString("F1")).ToString();
                }

                return txt;
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("MX_NL_ShieldTipStage".Translate(stage, InWeak ? "MX_NL_ShieldTipWeakSuffix".Translate().ToString() : "").ToString());

                if (stage == 2)
                {
                    sb.AppendLine("MX_NL_ShieldTipStage2Charges".Translate(phase2Charges, InWeak ? Props.phase2MaxChargesWeak : Props.phase2MaxChargesNormal).ToString());
                    sb.AppendLine("MX_NL_ShieldTipThreshold".Translate(Props.phase2Threshold.ToString("F1")).ToString());
                    sb.AppendLine("MX_NL_ShieldTipRecentHits".Translate().ToString());
                    EnsureRecentLogs();
                    if (phase2RecentHitLogs.Count == 0)
                    {
                        sb.AppendLine("MX_Common_None".Translate().ToString());
                    }
                    else
                    {
                        for (int i = 0; i < phase2RecentHitLogs.Count; i++)
                        {
                            sb.AppendLine((i + 1) + ". " + phase2RecentHitLogs[i]);
                        }
                    }
                }
                else if (stage == 3)
                {
                    int now = CurrentTick;
                    if (IsInStage3AbsorbWindow(now))
                    {
                        int remainAbsorb = Math.Max(0, phase3AbsorbUntilTick - now);
                        sb.AppendLine("MX_NL_ShieldTipStage3Absorb".Translate().ToString());
                        sb.AppendLine("MX_NL_ShieldTipAbsorbRemaining".Translate((remainAbsorb / 2500f).ToString("F1")).ToString());
                        sb.AppendLine("MX_NL_ShieldTipStoredDamage".Translate(phase3StoredDamage.ToString("F1")).ToString());
                        sb.AppendLine("MX_NL_ShieldTipBuffAfterAbsorb".Translate().ToString());
                    }
                    else
                    {
                        int remainBuff = Math.Max(0, phase3EndTick - now);
                        sb.AppendLine("MX_NL_ShieldTipStage3Buff".Translate().ToString());
                        sb.AppendLine("MX_NL_ShieldTipBuffRemaining".Translate((remainBuff / 2500f).ToString("F1")).ToString());
                        sb.AppendLine("MX_NL_ShieldTipLockedDamage".Translate(phase3StoredDamage.ToString("F1")).ToString());
                        sb.AppendLine("MX_NL_ShieldTipCurrentTier".Translate(GetStage3TierLabel()).ToString());

                        MXNeiyuStage3Profile profile;
                        if (TryGetStage3Profile(out profile))
                        {
                            string buffs = BuildStage3BuffLines(profile);
                            sb.AppendLine("MX_NL_ShieldTipCurrentBuffs".Translate().ToString());
                            if (buffs.NullOrEmpty())
                            {
                                sb.AppendLine("MX_Common_None".Translate().ToString());
                            }
                            else
                            {
                                sb.Append(buffs);
                            }
                        }
                    }
                }

                if (InWeak)
                {
                    int weakRemain = Math.Max(0, weakUntilTick - CurrentTick);
                    sb.AppendLine("MX_NL_ShieldTipWeakRemaining".Translate((weakRemain / 2500f).ToString("F1")).ToString());
                    sb.AppendLine("MX_NL_ShieldTipWeakPenalty".Translate().ToString());
                }

                return sb.ToString().TrimEnd();
            }
        }

        private void EnterStage1(int now)
        {
            stage = 1;
            phase2Charges = 0;
            phase2LastChargeChangeTick = now;
            phase3StoredDamage = 0f;
            phase3AbsorbUntilTick = -1;
            phase3EndTick = -1;
            stage3BuffPhaseAnnounced = false;
        }

        private void EnterStage2(int now, int maxCharges)
        {
            stage = 2;
            phase2Charges = Mathf.Max(0, maxCharges);

            if (InWeak)
            {
                phase2Charges = Math.Min(phase2Charges, Props.phase2MaxChargesWeak);
            }

            phase2LastChargeChangeTick = now;
            phase3StoredDamage = 0f;
            phase3AbsorbUntilTick = -1;
            phase3EndTick = -1;
            stage3BuffPhaseAnnounced = false;

            EnsureRecentLogs();
            phase2RecentHitLogs.Clear();
        }

        private void EnterStage3(int now)
        {
            if (InWeak)
            {

                return;
            }

            stage = 3;
            phase2Charges = 0;
            phase2LastChargeChangeTick = now;
            phase3StoredDamage = 0f;
            stage3BuffPhaseAnnounced = false;

            int absorbTicks = ResolveStage3AbsorbTicks();
            int buffTicks = ResolveStage3BuffTicks(absorbTicks);
            phase3AbsorbUntilTick = now + absorbTicks;
            phase3EndTick = phase3AbsorbUntilTick + buffTicks;
        }

        private void FinishStage3AndEnterWeak(int now)
        {
            ApplyStage3BloodLoss();
            EnterWeak(now);
            if (Pawn != null)
            {
                Messages.Message("MX_NL_ShieldStage3Ended".Translate(Pawn.LabelShort), Pawn, MessageTypeDefOf.CautionInput);
            }
        }

        private void EnterWeak(int now)
        {
            weakUntilTick = now + Mathf.Max(1, Props.weakDurationTicks);
            weakWasActive = true;


            stage = 1;
            phase2Charges = 0;
            phase2LastChargeChangeTick = now;
            phase3StoredDamage = 0f;
            phase3AbsorbUntilTick = -1;
            phase3EndTick = -1;
            stage3BuffPhaseAnnounced = false;
        }

        private void UpdateWeakTransition(int now)
        {
            bool weakNow = weakUntilTick > now;
            if (weakWasActive && !weakNow)
            {
                weakShieldExhaustedAnnounced = false;

                EnterStage1(now);
            }
            weakWasActive = weakNow;
        }

        private void ApplyStage3BloodLoss()
        {
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            float d = phase3StoredDamage;
            float bloodLoss = 0f;

            if (d > Props.stage3TierC_MaxDamage)
            {
                bloodLoss = Props.bloodLossTierD;
            }
            else if (d > Props.stage3TierB_MaxDamage)
            {
                bloodLoss = Props.bloodLossTierC;
            }
            else if (d > Props.stage3TierA_MaxDamage)
            {
                bloodLoss = Props.bloodLossTierB;
            }

            if (bloodLoss > 0f)
            {
                HealthUtility.AdjustSeverity(Pawn, HediffDefOf.BloodLoss, bloodLoss);
            }
        }

        private int CalculatePhase2Cost(float damageAmount)
        {
            float t = Mathf.Max(0.1f, Props.phase2Threshold);

            if (damageAmount < t)
            {
                return 0;
            }
            if (damageAmount < t * 3f)
            {
                return 1;
            }
            if (damageAmount < t * 6f)
            {
                return 10;
            }
            if (damageAmount < t * 15f)
            {
                return 100;
            }


            return Mathf.Max(1, phase2Charges);
        }

        private bool IsLethalOrDowning(DamageInfo dinfo)
        {
            if (Pawn == null || Pawn.health == null || dinfo.Def == null)
            {
                return false;
            }

            BodyPartRecord part = dinfo.HitPart;
            if (part == null)
            {
                part = Pawn.RaceProps != null ? Pawn.RaceProps.body.corePart : null;
            }
            if (part == null)
            {
                return false;
            }

            HediffDef incomingHediff = HealthUtility.GetHediffDefFromDamage(dinfo.Def, Pawn, part);
            if (incomingHediff == null)
            {
                return false;
            }


            float projectedSeverity = dinfo.Amount;

            bool wouldDie = Pawn.health.WouldDieAfterAddingHediff(incomingHediff, part, projectedSeverity);
            if (wouldDie)
            {
                return true;
            }

            bool wouldDown = Pawn.health.WouldBeDownedAfterAddingHediff(incomingHediff, part, projectedSeverity);
            return wouldDown;
        }


        private void PlayShieldBreakFx()
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.Map == null)
            {
                return;
            }

            float scale = (Props.activeShieldDrawSize.x + Props.activeShieldDrawSize.y) * 0.25f;
            EffecterDefOf.Shield_Break.SpawnAttached(Pawn, Pawn.Map, scale);
            FleckMaker.Static(Pawn.TrueCenter(), Pawn.Map, FleckDefOf.ExplosionFlash, 6f);
        }

        private void PlayAbsorbFx(DamageInfo dinfo)
        {
            lastAbsorbTick = CurrentTick;
            if (Pawn == null || !Pawn.Spawned || Pawn.Map == null)
            {
                return;
            }

            SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));

            FleckDef fleck = ResolveAbsorbFleck();

            if (fleck != null)
            {
                FleckMaker.Static(Pawn.TrueCenter(), Pawn.Map, fleck, Mathf.Max(0.1f, Props.absorbFleckScale));
            }

            if (!Props.absorbEffecterDefName.NullOrEmpty())
            {
                EffecterDef effecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail(Props.absorbEffecterDefName);
                if (effecterDef != null)
                {
                    Effecter effecter = effecterDef.Spawn(Pawn.Position, Pawn.Map);
                    TargetInfo t = new TargetInfo(Pawn.Position, Pawn.Map);
                    effecter.EffectTick(t, t);
                    effecter.Cleanup();
                }
            }
        }

        private FleckDef ResolveAbsorbFleck()
        {
            if (Props.hurtFleckDefNames != null && Props.hurtFleckDefNames.Count > 0)
            {
                int index = Rand.RangeInclusive(0, Props.hurtFleckDefNames.Count - 1);
                string fleckName = Props.hurtFleckDefNames[index];
                if (!fleckName.NullOrEmpty())
                {
                    FleckDef hurtFleck = DefDatabase<FleckDef>.GetNamedSilentFail(fleckName);
                    if (hurtFleck != null)
                    {
                        return hurtFleck;
                    }
                }
            }

            if (!Props.absorbFleckDefName.NullOrEmpty())
            {
                FleckDef customFleck = DefDatabase<FleckDef>.GetNamedSilentFail(Props.absorbFleckDefName);
                if (customFleck != null)
                {
                    return customFleck;
                }
            }

            return FleckDefOf.ExplosionFlash;
        }

        private void EnsureRecentLogs()
        {
            if (phase2RecentHitLogs == null)
            {
                phase2RecentHitLogs = new List<string>();
            }
        }

        private void RecordPhase2Hit(float damage, int consumed, int before, int after)
        {
            EnsureRecentLogs();
            string line = damage.ToString("F1") + " -> -" + consumed + " (" + before + "->" + after + ")";
            phase2RecentHitLogs.Insert(0, line);
            if (phase2RecentHitLogs.Count > 5)
            {
                phase2RecentHitLogs.RemoveAt(phase2RecentHitLogs.Count - 1);
            }
        }

        private void NotifyStage1Transition(bool lethalOrDowning, int targetStage)
        {
            if (Pawn == null)
            {
                return;
            }

            string damageType = lethalOrDowning ? "MX_NL_ShieldDamageTypeLethal".Translate().ToString() : "MX_NL_ShieldDamageTypeNonLethal".Translate().ToString();
            string text = "MX_NL_ShieldTriggered".Translate(Pawn.LabelShort, damageType, targetStage).ToString();
            Messages.Message(text, Pawn, MessageTypeDefOf.NeutralEvent);
        }

        public string GetStage3TierLabel()
        {
            float d = phase3StoredDamage;
            if (d <= Props.stage3TierA_MaxDamage) return "A";
            if (d <= Props.stage3TierB_MaxDamage) return "B";
            if (d <= Props.stage3TierC_MaxDamage) return "C";

            int stacks = 1 + Mathf.FloorToInt((d - Props.stage3TierC_MaxDamage) / Mathf.Max(1f, Props.stage3TierD_ExtraStepDamage));
            return "D x" + stacks;
        }

        private void NormalizeStage3Ticks(int now)
        {
            if (stage != 3)
            {
                return;
            }


            if (phase3EndTick <= 0)
            {
                int absorbTicks = ResolveStage3AbsorbTicks();
                int buffTicks = ResolveStage3BuffTicks(absorbTicks);
                phase3AbsorbUntilTick = now + absorbTicks;
                phase3EndTick = phase3AbsorbUntilTick + buffTicks;
                return;
            }

            if (phase3AbsorbUntilTick <= 0)
            {
                phase3AbsorbUntilTick = Math.Min(now, phase3EndTick - 1);
            }

            if (phase3AbsorbUntilTick >= phase3EndTick)
            {
                phase3AbsorbUntilTick = phase3EndTick - 1;
            }
        }

        private bool IsInStage3AbsorbWindow(int now)
        {
            if (stage != 3 || phase3AbsorbUntilTick <= 0)
            {
                return false;
            }
            return now < phase3AbsorbUntilTick;
        }

        private bool IsInStage3BuffWindow(int now)
        {
            if (stage != 3 || phase3AbsorbUntilTick <= 0 || phase3EndTick <= 0)
            {
                return false;
            }
            return now >= phase3AbsorbUntilTick && now < phase3EndTick;
        }

        private int ResolveStage3AbsorbTicks()
        {
            if (Props.stage3AbsorbTicks > 0)
            {
                return Props.stage3AbsorbTicks;
            }


            int total = Mathf.Max(1, Props.stage3DurationTicks);
            return Mathf.Clamp(total / 6, 1, total);
        }

        private int ResolveStage3BuffTicks(int absorbTicks)
        {
            if (Props.stage3BuffTicks > 0)
            {
                return Props.stage3BuffTicks;
            }


            int total = Mathf.Max(absorbTicks + 1, Props.stage3DurationTicks);
            return Mathf.Max(1, total - absorbTicks);
        }

        private string BuildStage3BuffLines(MXNeiyuStage3Profile profile)
        {
            StringBuilder sb = new StringBuilder();

            float outgoing = (profile.outgoingDamageFactor - 1f) * 100f;
            if (Mathf.Abs(outgoing) > 0.01f)
            {
                sb.AppendLine("MX_NL_ShieldBuffOutgoingDamage".Translate(outgoing.ToString("F0")).ToString());
            }

            float aimReduce = (1f - profile.aimingDelayFactor) * 100f;
            if (Mathf.Abs(aimReduce) > 0.01f)
            {
                sb.AppendLine("MX_NL_ShieldBuffAimingTime".Translate(aimReduce >= 0f ? "-" : "+", Mathf.Abs(aimReduce).ToString("F0")).ToString());
            }

            float incomingReduce = (1f - profile.incomingDamageFactor) * 100f;
            if (Mathf.Abs(incomingReduce) > 0.01f)
            {
                sb.AppendLine("MX_NL_ShieldBuffIncomingDamage".Translate(incomingReduce >= 0f ? "-" : "+", Mathf.Abs(incomingReduce).ToString("F0")).ToString());
            }

            float move = (profile.moveSpeedFactor - 1f) * 100f;
            if (Mathf.Abs(move) > 0.01f)
            {
                sb.AppendLine("MX_NL_ShieldBuffMoveSpeed".Translate(move.ToString("F0")).ToString());
            }

            float heal = (profile.injuryHealingFactor - 1f) * 100f;
            if (Mathf.Abs(heal) > 0.01f)
            {
                sb.AppendLine("MX_NL_ShieldBuffHealing".Translate(heal.ToString("F0")).ToString());
            }

            float armorPen = (profile.meleeArmorPenetrationFactor - 1f) * 100f;
            if (Mathf.Abs(armorPen) > 0.01f)
            {
                sb.AppendLine("MX_NL_ShieldBuffMeleeArmorPen".Translate(armorPen.ToString("F0")).ToString());
            }

            float meleeDodge = (profile.meleeDodgeChanceFactor - 1f) * 100f;
            if (Mathf.Abs(meleeDodge) > 0.01f)
            {
                sb.AppendLine("MX_NL_ShieldBuffMeleeDodge".Translate(meleeDodge.ToString("F0")).ToString());
            }

            float rangedDodge = profile.rangedDodgeBonusPct * 100f;
            if (Mathf.Abs(rangedDodge) > 0.01f)
            {
                sb.AppendLine("MX_NL_ShieldBuffRangedDodge".Translate(rangedDodge.ToString("F0")).ToString());
            }

            return sb.ToString().TrimEnd();
        }
    }

    public static class MXNeiyuShieldUtility
    {
        public static bool TryGetShieldComp(Pawn pawn, out HediffComp_MXNeiyuCountShield shield)
        {
            shield = null;
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return false;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                HediffComp_MXNeiyuCountShield comp = hediffs[i].TryGetComp<HediffComp_MXNeiyuCountShield>();
                if (comp != null)
                {
                    shield = comp;
                    return true;
                }
            }
            return false;
        }

        public static Pawn TryGetEquipmentOwnerPawn(Thing equipmentThing)
        {
            if (equipmentThing == null)
            {
                return null;
            }

            Pawn_EquipmentTracker equipmentTracker = equipmentThing.ParentHolder as Pawn_EquipmentTracker;
            if (equipmentTracker != null)
            {
                return equipmentTracker.pawn;
            }

            return null;
        }
    }

    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class Patch_MXNeiyuShield_Draw
    {
        private static readonly AccessTools.FieldRef<PawnRenderer, Pawn> PawnRef =
            AccessTools.FieldRefAccess<PawnRenderer, Pawn>("pawn");

        private static readonly Dictionary<string, Material> ShieldMaterialByPath = new Dictionary<string, Material>();

        [HarmonyPostfix]
        public static void Postfix(PawnRenderer __instance, Vector3 drawLoc, Rot4? rotOverride = null, bool neverAimWeapon = false)
        {
            if (__instance == null)
            {
                return;
            }

            Pawn pawn = PawnRef(__instance);
            HediffComp_MXNeiyuCountShield shield;
            if (pawn == null || !MXNeiyuShieldUtility.TryGetShieldComp(pawn, out shield) || !shield.ShouldDrawActiveShield)
            {
                return;
            }

            string texPath = shield.Props.activeShieldTexPath;
            if (texPath.NullOrEmpty())
            {
                return;
            }

            Material shieldMat = GetShieldMaterial(texPath, shield.Props.activeShieldAlpha);
            if (shieldMat == null)
            {
                return;
            }

            float pulseScale = shield.ActiveShieldPulseScale;
            Vector2 drawSize = shield.Props.activeShieldDrawSize;

            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            pos += Altitudes.AltIncVect * shield.Props.activeShieldAltitudeOffset;

            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.identity,
                new Vector3(drawSize.x * pulseScale, 1f, drawSize.y * pulseScale));

            Graphics.DrawMesh(MeshPool.plane10, matrix, shieldMat, 0);
        }

        private static Material GetShieldMaterial(string texPath, float alpha)
        {
            string cacheKey = texPath + "|" + Mathf.Clamp01(alpha).ToString("F3");
            Material shieldMat;
            if (ShieldMaterialByPath.TryGetValue(cacheKey, out shieldMat))
            {
                return shieldMat;
            }

            shieldMat = MaterialPool.MatFrom(texPath, ShaderDatabase.Transparent, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            ShieldMaterialByPath[cacheKey] = shieldMat;
            return shieldMat;
        }
    }


    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_MXNeiyuShield_PreApplyDamage
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (absorbed)
            {
                return;
            }

            HediffComp_MXNeiyuCountShield shield;
            if (!MXNeiyuShieldUtility.TryGetShieldComp(__instance, out shield))
            {
                return;
            }

            shield.TryAbsorb(ref dinfo, ref absorbed);
        }
    }


    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedMeleeDamageAmount), new Type[] { typeof(Tool), typeof(Pawn), typeof(Thing), typeof(HediffComp_VerbGiver) })]
    public static class Patch_MXNeiyuShield_MeleeDamage
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn attacker, ref float __result)
        {
            if (attacker == null)
            {
                return;
            }

            HediffComp_MXNeiyuCountShield shield;
            if (!MXNeiyuShieldUtility.TryGetShieldComp(attacker, out shield))
            {
                return;
            }

            MXNeiyuStage3Profile profile;
            if (!shield.TryGetStage3Profile(out profile))
            {
                return;
            }

            if (Mathf.Abs(profile.outgoingDamageFactor - 1f) > 0.0001f)
            {
                __result *= profile.outgoingDamageFactor;
            }
        }
    }


    [HarmonyPatch(typeof(ProjectileProperties), nameof(ProjectileProperties.GetDamageAmount), new Type[] { typeof(Thing), typeof(StringBuilder) })]
    public static class Patch_MXNeiyuShield_RangedDamage
    {
        [HarmonyPostfix]
        public static void Postfix(Thing weapon, ref int __result)
        {
            Pawn ownerPawn = MXNeiyuShieldUtility.TryGetEquipmentOwnerPawn(weapon);
            if (ownerPawn == null)
            {
                return;
            }

            HediffComp_MXNeiyuCountShield shield;
            if (!MXNeiyuShieldUtility.TryGetShieldComp(ownerPawn, out shield))
            {
                return;
            }

            MXNeiyuStage3Profile profile;
            if (!shield.TryGetStage3Profile(out profile))
            {
                return;
            }

            if (Mathf.Abs(profile.outgoingDamageFactor - 1f) <= 0.0001f)
            {
                return;
            }

            __result = Mathf.Max(1, Mathf.RoundToInt(__result * profile.outgoingDamageFactor));
        }
    }


    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
    public static class Patch_MXNeiyuShield_GetStatValue
    {
        [HarmonyPostfix]
        public static void Postfix(Thing thing, StatDef stat, bool applyPostProcess, int cacheStaleAfterTicks, ref float __result)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null)
            {
                return;
            }

            HediffComp_MXNeiyuCountShield shield;
            if (!MXNeiyuShieldUtility.TryGetShieldComp(pawn, out shield))
            {
                return;
            }

            MXNeiyuStage3Profile profile;
            bool hasStage3Buff = shield.TryGetStage3Profile(out profile);

            float weakMoveSpeedFactor;
            float weakRestFallRateFactor;
            float weakWorkSpeedGlobalFactor;
            bool hasWeakPenalty = shield.TryGetWeakPenaltyFactors(out weakMoveSpeedFactor, out weakRestFallRateFactor, out weakWorkSpeedGlobalFactor);

            if (!hasStage3Buff && !hasWeakPenalty)
            {
                return;
            }

            if (hasStage3Buff && stat == StatDefOf.AimingDelayFactor)
            {
                __result *= profile.aimingDelayFactor;
            }
            else if (hasStage3Buff && stat == StatDefOf.IncomingDamageFactor)
            {
                __result *= profile.incomingDamageFactor;
            }
            else if (hasStage3Buff && stat == StatDefOf.MoveSpeed)
            {
                __result *= profile.moveSpeedFactor;
            }
            else if (hasStage3Buff && stat == StatDefOf.InjuryHealingFactor)
            {
                __result *= profile.injuryHealingFactor;
            }
            else if (hasStage3Buff && stat == StatDefOf.MeleeDodgeChance)
            {
                __result *= profile.meleeDodgeChanceFactor;
            }

            if (hasWeakPenalty)
            {
                if (stat == StatDefOf.MoveSpeed)
                {
                    __result *= weakMoveSpeedFactor;
                }
                else if (stat == StatDefOf.RestFallRateFactor)
                {
                    __result *= weakRestFallRateFactor;
                }
                else if (stat == StatDefOf.WorkSpeedGlobal)
                {
                    __result *= weakWorkSpeedGlobalFactor;
                }
            }
        }
    }


    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedArmorPenetration), new Type[] { typeof(Verb), typeof(Pawn) })]
    public static class Patch_MXNeiyuShield_MeleeArmorPen
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn attacker, ref float __result)
        {
            if (attacker == null)
            {
                return;
            }

            HediffComp_MXNeiyuCountShield shield;
            if (!MXNeiyuShieldUtility.TryGetShieldComp(attacker, out shield))
            {
                return;
            }

            MXNeiyuStage3Profile profile;
            if (!shield.TryGetStage3Profile(out profile))
            {
                return;
            }

            __result *= profile.meleeArmorPenetrationFactor;
        }
    }


    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
    public static class Patch_MXNeiyuShield_RangedDodge
    {
        private static readonly FieldInfo ShotReportTargetField = AccessTools.Field(typeof(ShotReport), "target");
        private static readonly FieldInfo ShotReportFactorFromTargetSizeField = AccessTools.Field(typeof(ShotReport), "factorFromTargetSize");

        [HarmonyPostfix]
        public static void Postfix(ref ShotReport __result)
        {
            if (ShotReportTargetField == null || ShotReportFactorFromTargetSizeField == null)
            {
                return;
            }

            object boxed = __result;

            TargetInfo targetInfo;
            try
            {
                targetInfo = (TargetInfo)ShotReportTargetField.GetValue(boxed);
            }
            catch
            {
                return;
            }

            Pawn targetPawn = targetInfo.Thing as Pawn;
            if (targetPawn == null)
            {
                return;
            }

            HediffComp_MXNeiyuCountShield shield;
            if (!MXNeiyuShieldUtility.TryGetShieldComp(targetPawn, out shield))
            {
                return;
            }

            MXNeiyuStage3Profile profile;
            if (!shield.TryGetStage3Profile(out profile) || profile.rangedDodgeBonusPct <= 0f)
            {
                return;
            }

            float sizeFactor;
            try
            {
                sizeFactor = (float)ShotReportFactorFromTargetSizeField.GetValue(boxed);
            }
            catch
            {
                return;
            }

            float hitChanceFactor = Mathf.Clamp01(1f - profile.rangedDodgeBonusPct);
            sizeFactor *= hitChanceFactor;

            ShotReportFactorFromTargetSizeField.SetValue(boxed, sizeFactor);
            __result = (ShotReport)boxed;
        }
    }
}
