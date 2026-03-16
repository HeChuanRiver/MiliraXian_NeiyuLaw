// File: D:\RimWorldModForMe\MiliraXian_NeiyuLaw\Source\Hediff_NeiyuCountShield.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.NeiyuLaw
{
    public struct MXNeiyuStage3Profile
    {
        public float outgoingDamageFactor;        // 近/远程伤害系数
        public float aimingDelayFactor;           // 瞄准时间系数（<1更快）
        public float incomingDamageFactor;        // 承伤系数（<1更硬）
        public float moveSpeedFactor;             // 移速系数
        public float injuryHealingFactor;         // 愈合速度系数
        public float meleeDodgeChanceFactor;      // 近战闪避系数
        public float rangedDodgeBonusPct;         // 远程闪避加成（0.10 = +10%）
        public float meleeArmorPenetrationFactor; // 近战护甲穿透系数
    }

    public class HediffCompProperties_MXNeiyuCountShield : HediffCompProperties
    {
        // 二阶段阈值：原版常见子弹伤害多在 6~30，集中约 10~18，默认 18
        public float phase2Threshold = 18f;

        public int phase2MaxChargesNormal = 1000;
        public int phase2MaxChargesWeak = 250;

        // 二阶段在 12h 内点数不变 -> 回到一阶段
        public int phase2RecoverTicksNoChange = 30000; // 12h

        // 三阶段：前2h蓄伤（无敌）+后22h增益（可受伤）
        // 兼容旧配置：保留 stage3DurationTicks 作为兜底。
        public int stage3AbsorbTicks = 5000;  // 2h
        public int stage3BuffTicks = 55000;   // 22h
        public int stage3DurationTicks = 60000; // legacy fallback
        public int weakDurationTicks = 300000;  // 5 days

        // 三阶段存伤分段
        public float stage3TierA_MaxDamage = 100f;
        public float stage3TierB_MaxDamage = 500f;
        public float stage3TierC_MaxDamage = 1000f;
        public float stage3TierD_ExtraStepDamage = 500f; // >1000后每500一档

        // 三阶段结束时血液消耗（通过 BloodLoss severity）
        public float bloodLossTierB = 0.10f;
        public float bloodLossTierC = 0.30f;
        public float bloodLossTierD = 0.50f;

        // 吸收反馈（可替换为你后续自定义资源）
        public string absorbFleckDefName = "ExplosionFlash";
        public List<string> hurtFleckDefNames = new List<string>();
        public float absorbFleckScale = 1.2f;
        public string absorbEffecterDefName = null; // TODO: 你自己的 EffecterDef defName

        // 常态护盾显示：阶段1、阶段2、阶段3前半段显示
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
        // 1=一阶段，2=二阶段，3=三阶段
        private int stage = 1;

        private int phase2Charges;
        private int phase2LastChargeChangeTick = -1;

        private float phase3StoredDamage;
        private int phase3AbsorbUntilTick = -1;
        private int phase3EndTick = -1;
        private bool stage3BuffPhaseAnnounced;

        private int weakUntilTick = -1;
        private bool weakWasActive;

        // 二阶段最近5次受击日志（用于Hediff面板展示）
        private List<string> phase2RecentHitLogs = new List<string>();

        public HediffCompProperties_MXNeiyuCountShield Props => (HediffCompProperties_MXNeiyuCountShield)props;

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        private bool InWeak => weakUntilTick > CurrentTick;

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
                    if (Pawn != null)
                    {
                        Messages.Message("[" + Pawn.LabelShort + "] 护身进入增益阶段：不再蓄伤、也不再无敌。", Pawn, MessageTypeDefOf.NeutralEvent);
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
                    // 三阶段前2小时：蓄伤+无敌
                    phase3StoredDamage += dinfo.Amount;
                    absorbed = true;
                    PlayAbsorbFx(dinfo);
                    return true;
                }

                // 三阶段后22小时：仅保留buff，不再蓄伤，不再无敌
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
                        // 虚弱期：二阶段耗尽后直接正常受伤
                        phase2Charges = 0;
                        phase2LastChargeChangeTick = now;
                        RecordPhase2Hit(dinfo.Amount, before, before, 0);
                        return false;
                    }

                    absorbed = true;
                    PlayAbsorbFx(dinfo);
                    phase2Charges = 0;
                    phase2LastChargeChangeTick = now;
                    RecordPhase2Hit(dinfo.Amount, before, before, 0);
                    EnterStage3(now);
                    return true;
                }

                phase2Charges = before - cost;
                phase2LastChargeChangeTick = now;
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

        // stage==3 时始终返回 true（A档也返回，只是增益为0）
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

            // A: 0~100 无增益
            if (d <= Props.stage3TierA_MaxDamage)
            {
                return true;
            }

            // B: 100~500
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

            // C: 500~1000
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

            // D: >1000，每多500再叠一档
            int stacks = 1 + Mathf.FloorToInt((d - Props.stage3TierC_MaxDamage) / Mathf.Max(1f, Props.stage3TierD_ExtraStepDamage));
            profile.outgoingDamageFactor = 1f + 0.35f * stacks;
            profile.moveSpeedFactor = 1f + 0.10f * stacks;
            profile.injuryHealingFactor = 1f + 0.35f * stacks;
            profile.incomingDamageFactor = Mathf.Max(0.10f, 1f - 0.10f * stacks);
            profile.meleeArmorPenetrationFactor = 1f + 0.10f * stacks;
            profile.meleeDodgeChanceFactor = 1f + 0.10f * stacks;
            profile.rangedDodgeBonusPct = 0.10f * stacks;
            // D段瞄准保持1（按你当前设定）

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

            // 新增虚弱惩罚：
            // 1) 移速降低50% => x0.5
            // 2) 休息值下降速率提高50% => RestFallRateFactor x1.5
            // 3) 全局工作效率降低80% => WorkSpeedGlobal x0.2
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

                string txt = "护身";
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
                        txt += " III-蓄伤 蓄伤:" + phase3StoredDamage.ToString("F0")
                            + " 剩余:" + (remainAbsorb / 2500f).ToString("F1") + "h";
                    }
                    else
                    {
                        int remainBuff = Math.Max(0, phase3EndTick - now);
                        txt += " III-增益 档位:" + GetStage3TierLabel()
                            + " 剩余:" + (remainBuff / 2500f).ToString("F1") + "h";
                    }
                }

                if (InWeak)
                {
                    int weakRemain = Math.Max(0, weakUntilTick - CurrentTick);
                    txt += " [虚弱 " + (weakRemain / 2500f).ToString("F1") + "h]";
                }

                return txt;
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("阶段: " + stage + (InWeak ? " (虚弱中)" : ""));

                if (stage == 2)
                {
                    sb.AppendLine("二阶段次数: " + phase2Charges + "/" + (InWeak ? Props.phase2MaxChargesWeak : Props.phase2MaxChargesNormal));
                    sb.AppendLine("阈值: " + Props.phase2Threshold.ToString("F1"));
                    sb.AppendLine("最近5次受击(伤害 -> 消耗次数):");
                    EnsureRecentLogs();
                    if (phase2RecentHitLogs.Count == 0)
                    {
                        sb.AppendLine("无");
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
                        sb.AppendLine("阶段3-蓄伤(无敌)");
                        sb.AppendLine("蓄伤剩余: " + (remainAbsorb / 2500f).ToString("F1") + "h");
                        sb.AppendLine("当前蓄伤: " + phase3StoredDamage.ToString("F1"));
                        sb.AppendLine("增益将于蓄伤结束后开启。");
                    }
                    else
                    {
                        int remainBuff = Math.Max(0, phase3EndTick - now);
                        sb.AppendLine("阶段3-增益(可受伤)");
                        sb.AppendLine("增益剩余: " + (remainBuff / 2500f).ToString("F1") + "h");
                        sb.AppendLine("锁定蓄伤: " + phase3StoredDamage.ToString("F1"));
                        sb.AppendLine("当前档位: " + GetStage3TierLabel());

                        MXNeiyuStage3Profile profile;
                        if (TryGetStage3Profile(out profile))
                        {
                            string buffs = BuildStage3BuffLines(profile);
                            sb.AppendLine("当前增益:");
                            if (buffs.NullOrEmpty())
                            {
                                sb.AppendLine("无");
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
                    sb.AppendLine("虚弱剩余: " + (weakRemain / 2500f).ToString("F1") + "h");
                    sb.AppendLine("虚弱惩罚: 移速-50%，休息下降+50%，全局工作效率-80%");
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
                // 虚弱期禁止进入三阶段
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
                Messages.Message("[" + Pawn.LabelShort + "] 护身第三阶段结束，进入虚弱。", Pawn, MessageTypeDefOf.CautionInput);
            }
        }

        private void EnterWeak(int now)
        {
            weakUntilTick = now + Mathf.Max(1, Props.weakDurationTicks);
            weakWasActive = true;

            // 虚弱开始后恢复到一阶段待机；二阶段上限在进入二阶段时限制为250
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
                // 虚弱结束，回到一阶段
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
                bloodLoss = Props.bloodLossTierD; // >1000
            }
            else if (d > Props.stage3TierB_MaxDamage)
            {
                bloodLoss = Props.bloodLossTierC; // 500~1000
            }
            else if (d > Props.stage3TierA_MaxDamage)
            {
                bloodLoss = Props.bloodLossTierB; // 100~500
            }

            if (bloodLoss > 0f)
            {
                HealthUtility.AdjustSeverity(Pawn, HediffDefOf.BloodLoss, bloodLoss);
            }
        }

        private int CalculatePhase2Cost(float damageAmount)
        {
            float t = Mathf.Max(0.1f, Props.phase2Threshold);

            if (damageAmount < t * (2f / 3f))
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

            // 15倍以上：消耗所有剩余点数
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

            // 此时在 PreApplyDamage 后置阶段，dinfo.Amount 已经过原版前置调整
            float projectedSeverity = dinfo.Amount;

            bool wouldDie = Pawn.health.WouldDieAfterAddingHediff(incomingHediff, part, projectedSeverity);
            if (wouldDie)
            {
                return true;
            }

            bool wouldDown = Pawn.health.WouldBeDownedAfterAddingHediff(incomingHediff, part, projectedSeverity);
            return wouldDown;
        }

        private void PlayAbsorbFx(DamageInfo dinfo)
        {
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

            // TODO: 你后续可在 XML 填 absorbEffecterDefName，挂自定义材质动画
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

            string damageType = lethalOrDowning ? "致命/倒地" : "非致命";
            string text = "[" + Pawn.LabelShort + "] 护身触发：判定为" + damageType + "伤害，进入第" + targetStage + "阶段。";
            Messages.Message(text, Pawn, MessageTypeDefOf.NeutralEvent);
        }

        private string GetStage3TierLabel()
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

            // 兼容旧存档或异常状态：自动重建阶段3时间窗，避免“蓄伤一直不结束”或“增益不生效”。
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

            // 旧配置兜底：按24h总时长拆成4h蓄伤+20h增益
            int total = Mathf.Max(1, Props.stage3DurationTicks);
            return Mathf.Clamp(total / 6, 1, total);
        }

        private int ResolveStage3BuffTicks(int absorbTicks)
        {
            if (Props.stage3BuffTicks > 0)
            {
                return Props.stage3BuffTicks;
            }

            // 旧配置兜底
            int total = Mathf.Max(absorbTicks + 1, Props.stage3DurationTicks);
            return Mathf.Max(1, total - absorbTicks);
        }

        private string BuildStage3BuffLines(MXNeiyuStage3Profile profile)
        {
            StringBuilder sb = new StringBuilder();

            float outgoing = (profile.outgoingDamageFactor - 1f) * 100f;
            if (Mathf.Abs(outgoing) > 0.01f)
            {
                sb.AppendLine("近战/远程伤害 +" + outgoing.ToString("F0") + "%");
            }

            float aimReduce = (1f - profile.aimingDelayFactor) * 100f;
            if (Mathf.Abs(aimReduce) > 0.01f)
            {
                sb.AppendLine("瞄准时间 " + (aimReduce >= 0f ? "-" : "+") + Mathf.Abs(aimReduce).ToString("F0") + "%");
            }

            float incomingReduce = (1f - profile.incomingDamageFactor) * 100f;
            if (Mathf.Abs(incomingReduce) > 0.01f)
            {
                sb.AppendLine("承伤系数 " + (incomingReduce >= 0f ? "-" : "+") + Mathf.Abs(incomingReduce).ToString("F0") + "%");
            }

            float move = (profile.moveSpeedFactor - 1f) * 100f;
            if (Mathf.Abs(move) > 0.01f)
            {
                sb.AppendLine("移速 +" + move.ToString("F0") + "%");
            }

            float heal = (profile.injuryHealingFactor - 1f) * 100f;
            if (Mathf.Abs(heal) > 0.01f)
            {
                sb.AppendLine("愈合速度 +" + heal.ToString("F0") + "%");
            }

            float armorPen = (profile.meleeArmorPenetrationFactor - 1f) * 100f;
            if (Mathf.Abs(armorPen) > 0.01f)
            {
                sb.AppendLine("近战护甲穿透 +" + armorPen.ToString("F0") + "%");
            }

            float meleeDodge = (profile.meleeDodgeChanceFactor - 1f) * 100f;
            if (Mathf.Abs(meleeDodge) > 0.01f)
            {
                sb.AppendLine("近战闪避 +" + meleeDodge.ToString("F0") + "%");
            }

            float rangedDodge = profile.rangedDodgeBonusPct * 100f;
            if (Mathf.Abs(rangedDodge) > 0.01f)
            {
                sb.AppendLine("远程闪避 +" + rangedDodge.ToString("F0") + "%");
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

    // 防御入口：受击前吸收
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

    // 近战伤害入口：挂在 VerbProperties 的真实近战伤害计算链
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

    // 远程伤害入口：挂在 ProjectileProperties.GetDamageAmount(weapon) 链
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

    // 三阶段动态属性：瞄准、承伤、移速、愈合、近战闪避
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

    // 近战护甲穿透：走 VerbProperties 真实计算链
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

    // 远程闪避：在 ShotReport.HitReportFor 生成时，直接压低 target size 命中系数
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
