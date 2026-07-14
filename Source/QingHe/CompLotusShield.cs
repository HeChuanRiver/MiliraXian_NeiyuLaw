using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_LotusShield : CompProperties
    {
        public float maxEnergy = 100f;

        // Damage absorbed per one shield point.
        public float baseDamagePerShieldPoint = 0.6f;
        public float bonusDamagePerShieldPointAtMaxTempest = 1.4f;

        // Shield regeneration per second.
        public float baseRegenPerSecond = 0.8f;
        public float bonusRegenPerSecondAtMaxElegance = 4.2f;

        // After breaking, shield is disabled for these ticks.
        public int breakDisabledTicks = 600;
        public bool breakOnEmp = true;
        public float tempestGainOnBreak = 20f;

        // Shield hit VFX settings aligned with Neiyu shield implementation.
        public string absorbFleckDefName = "ExplosionFlash";
        public List<string> hurtFleckDefNames = new List<string>();
        public float absorbFleckScale = 1.2f;
        public string absorbEffecterDefName = null;

        // Visual display rule:
        // - Show while shield is not full.
        // - After shield becomes full, keep showing and fade out across these ticks.
        public int fullEnergyFadeOutTicks = 90;
        public string activeShieldTexPath = "MiliraXianNeiyu/Effect/Neiyu_Shield/Shield";
        public Vector2 activeShieldDrawSize = new Vector2(1.9f, 1.9f);
        public float activeShieldAlpha = 0.45f;
        public float activeShieldAltitudeOffset = 0f;

        public float minDrawScale = 1.0f;
        public float maxDrawScale = 1.2f;

        public CompProperties_LotusShield()
        {
            compClass = typeof(CompLotusShield);
        }
    }

    /// <summary>
    /// Recoverable Lotus Shield for QingHe.
    /// </summary>
    public class CompLotusShield : ThingComp
    {
        private const int RegenUpdateIntervalTicks = 15;

        private float energy = 100f;
        // Retained for compatibility with existing saves; new runtime logic uses resetAtTick.
        private int ticksToReset = -1;
        private int resetAtTick = -1;
        private int lastRegenUpdateTick = -1;

        private int fullEnergyAccumulatedTicks = 0;
        private HediffComp_Elegance cachedElegance;
        private HediffComp_Tempest cachedTempest;

        public CompProperties_LotusShield Props => (CompProperties_LotusShield)props;

        private Pawn PawnOwner => parent as Pawn;

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        public float MaxEnergy => Mathf.Max(1f, Props.maxEnergy);

        public float Energy => Mathf.Clamp(energy, 0f, MaxEnergy);

        public bool InBreak => resetAtTick > CurrentTick;

        public int BreakTicksLeft => Mathf.Max(0, resetAtTick - CurrentTick);

        public float CurrentDamagePerShieldPoint => ResolveDamagePerShieldPoint();

        public float CurrentRegenPerSecond => ResolveRegenPerSecond();

        private bool ShouldDisplayFx
        {
            get
            {
                Pawn pawn = PawnOwner;
                return pawn != null
                       && pawn.Spawned
                       && !pawn.Dead
                       && !InBreak
                       && (Energy < MaxEnergy - 0.0001f || fullEnergyAccumulatedTicks < Mathf.Max(1, Props.fullEnergyFadeOutTicks));
            }
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            energy = MaxEnergy;
            lastRegenUpdateTick = CurrentTick;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref energy, "mx_qh_lotus_energy", 100f);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                ticksToReset = BreakTicksLeft;
            }

            Scribe_Values.Look(ref ticksToReset, "mx_qh_lotus_ticksToReset", -1);
            Scribe_Values.Look(ref resetAtTick, "mx_qh_lotus_resetAtTick", -1);
            Scribe_Values.Look(ref fullEnergyAccumulatedTicks, "mx_qh_lotus_fullEnergyAccumulatedTicks", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (resetAtTick < 0 && ticksToReset > 0)
                {
                    resetAtTick = CurrentTick + ticksToReset;
                }

                lastRegenUpdateTick = CurrentTick;
                cachedElegance = null;
                cachedTempest = null;
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (PawnOwner == null)
            {
                energy = 0f;
                return;
            }

            int currentTick = CurrentTick;
            if (InBreak)
            {
                fullEnergyAccumulatedTicks = 0;
                lastRegenUpdateTick = currentTick;
                return;
            }

            int elapsedTicks = lastRegenUpdateTick < 0 ? RegenUpdateIntervalTicks : currentTick - lastRegenUpdateTick;
            if (elapsedTicks < RegenUpdateIntervalTicks)
            {
                return;
            }

            ApplyAccumulatedRegen(currentTick, elapsedTicks);
        }

        private void ApplyAccumulatedRegen(int currentTick, int elapsedTicks)
        {
            float gain = ResolveRegenPerSecond() * Mathf.Max(1, elapsedTicks) / 60f;
            if (gain > 0f)
            {
                energy = Mathf.Min(MaxEnergy, energy + gain);
            }

            lastRegenUpdateTick = currentTick;
            if (Energy < MaxEnergy - 0.0001f)
            {
                fullEnergyAccumulatedTicks = 0;
            }
            else
            {
                fullEnergyAccumulatedTicks = Mathf.Min(fullEnergyAccumulatedTicks + Mathf.Max(1, elapsedTicks), 1000000);
            }
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;

            Pawn owner = PawnOwner;
            if (owner == null || owner.Dead)
            {
                return;
            }

            int currentTick = CurrentTick;
            if (!InBreak && lastRegenUpdateTick >= 0 && currentTick > lastRegenUpdateTick)
            {
                ApplyAccumulatedRegen(currentTick, currentTick - lastRegenUpdateTick);
            }

            if (dinfo.Amount <= 0f)
            {
                return;
            }

            if (InBreak)
            {
                return;
            }

            if (dinfo.Def == DamageDefOf.EMP && Props.breakOnEmp)
            {
                Break();
                return;
            }

            if (dinfo.Def.ignoreShields)
            {
                return;
            }

            float dpsp = ResolveDamagePerShieldPoint();
            if (dpsp <= 0f || energy <= 0f)
            {
                return;
            }

            float incoming = Mathf.Max(0f, dinfo.Amount);
            if (incoming <= 0f)
            {
                return;
            }

            float shieldCost = incoming / dpsp;
            if (shieldCost >= energy - 0.0001f)
            {
                energy = 0f;
            }
            else
            {
                energy -= shieldCost;
            }

            OnAbsorbedDamage();
            dinfo.SetAmount(0f);
            absorbed = true;

            if (energy <= 0.0001f)
            {
                Break();
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();
            DrawShield();
        }

        public override bool CompAllowVerbCast(Verb verb)
        {
            return true;
        }

        private void OnAbsorbedDamage()
        {
            Pawn owner = PawnOwner;
            if (owner == null || !owner.Spawned || owner.Map == null)
            {
                return;
            }

            SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(owner.Position, owner.Map));

            FleckDef fleck = ResolveAbsorbFleck();
            if (fleck != null)
            {
                FleckMaker.Static(owner.TrueCenter(), owner.Map, fleck, Mathf.Max(0.1f, Props.absorbFleckScale));
            }

            if (!Props.absorbEffecterDefName.NullOrEmpty())
            {
                EffecterDef effecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail(Props.absorbEffecterDefName);
                if (effecterDef != null)
                {
                    Effecter effecter = effecterDef.Spawn(owner.Position, owner.Map);
                    TargetInfo t = new TargetInfo(owner.Position, owner.Map);
                    effecter.EffectTick(t, t);
                    effecter.Cleanup();
                }
            }
        }

        private void Break()
        {
            energy = 0f;
            resetAtTick = CurrentTick + Mathf.Max(1, Props.breakDisabledTicks);
            ticksToReset = 0;
            lastRegenUpdateTick = CurrentTick;

            Pawn owner = PawnOwner;
                if (owner != null && Props.tempestGainOnBreak > 0f && MX_QHDefOf.MX_QH_Tempest != null)
                {
                    TempestUtility.AddTempest(owner, Props.tempestGainOnBreak);
                }

            if (owner == null || !owner.Spawned || owner.Map == null)
            {
                return;
            }

            float ratio = Mathf.Clamp01(Energy / MaxEnergy);
            float scale = Mathf.Lerp(Props.minDrawScale, Props.maxDrawScale, ratio);
            EffecterDefOf.Shield_Break.SpawnAttached(parent, parent.MapHeld, scale);
            FleckMaker.Static(owner.TrueCenter(), owner.Map, FleckDefOf.ExplosionFlash, 8f);
        }

        private void DrawShield()
        {
            Pawn owner = PawnOwner;
            if (owner == null || !ShouldDisplayFx)
            {
                return;
            }

            if (Props.activeShieldTexPath.NullOrEmpty())
            {
                return;
            }

            Vector3 pos = owner.Drawer.DrawPos;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            pos += Altitudes.AltIncVect * Props.activeShieldAltitudeOffset;
            
            Vector2 drawSize = Props.activeShieldDrawSize;
            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.identity,
                new Vector3(drawSize.x, 1f, drawSize.y));

            Color tint = ResolveShieldTintColor();
            tint.a = ResolveDrawAlpha();
            MiliraXian.Characters.MXShieldRenderUtility.Draw(Props.activeShieldTexPath, matrix, tint);
        }

        private float ResolveDrawAlpha()
        {
            float shieldFactor = Mathf.Clamp01(Energy / MaxEnergy);
            float fullFadeFactor = 1f;
            if (Energy >= MaxEnergy - 0.0001f)
            {
                int fadeTicks = Mathf.Max(1, Props.fullEnergyFadeOutTicks);
                fullFadeFactor = Mathf.Clamp01(1f - fullEnergyAccumulatedTicks / (float)fadeTicks);
            }

            return Mathf.Clamp01(Props.activeShieldAlpha * (0.35f + 0.65f * shieldFactor) * fullFadeFactor);
        }

        private Color ResolveShieldTintColor()
        {
            float eleganceFactor = GetEleganceComp()?.ValuePercent ?? 0f;
            float springFactor = GetTempestComp()?.ValuePercent ?? 0f;

            Color baseColor = new Color(0.82f, 0.92f, 1f, 1f);
            Color pinkColor = new Color(1f, 0.66f, 0.88f, 1f);
            Color hue = Color.Lerp(baseColor, pinkColor, eleganceFactor);

            float brightness = Mathf.Lerp(0.68f, 1f, springFactor);
            return new Color(
                Mathf.Clamp01(hue.r * brightness),
                Mathf.Clamp01(hue.g * brightness),
                Mathf.Clamp01(hue.b * brightness),
                1f);
        }

        private float ResolveDamagePerShieldPoint()
        {
            float factor = GetTempestComp()?.ValuePercent ?? 0f;
            return Mathf.Max(0.01f, Props.baseDamagePerShieldPoint + Props.bonusDamagePerShieldPointAtMaxTempest * factor);
        }

        private float ResolveRegenPerSecond()
        {
            float factor = GetEleganceComp()?.ValuePercent ?? 0f;
            return Mathf.Max(0f, Props.baseRegenPerSecond + Props.bonusRegenPerSecondAtMaxElegance * factor);
        }

        private HediffComp_Elegance GetEleganceComp()
        {
            Pawn owner = PawnOwner;
            if (cachedElegance == null || cachedElegance.Pawn != owner)
            {
                cachedElegance = EleganceUtility.GetComp(owner);
            }

            return cachedElegance;
        }

        private HediffComp_Tempest GetTempestComp()
        {
            Pawn owner = PawnOwner;
            if (cachedTempest == null || cachedTempest.Pawn != owner)
            {
                cachedTempest = TempestUtility.GetComp(owner);
            }

            return cachedTempest;
        }

        public string BuildShieldTooltip()
        {
            string status = InBreak
                ? "MX_QH_LotusShieldStatusDown".Translate(Mathf.CeilToInt(BreakTicksLeft / 60f)).ToString()
                : "MX_QH_LotusShieldStatusActive".Translate().ToString();

            return "ShieldPersonalTip".Translate().Resolve() + "\n\n"
                   + status + "\n"
                   + "MX_QH_LotusShieldDamagePerPoint".Translate(CurrentDamagePerShieldPoint.ToString("F2")) + "\n"
                   + "MX_QH_LotusShieldRegen".Translate(CurrentRegenPerSecond.ToString("F2"));
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
    }
}
