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
        private static readonly Dictionary<string, Material> ShieldMaterialByPath = new Dictionary<string, Material>();

        private float energy = 100f;
        private int ticksToReset = -1;

        private int fullEnergyAccumulatedTicks = 0;

        public CompProperties_LotusShield Props => (CompProperties_LotusShield)props;

        private Pawn PawnOwner => parent as Pawn;

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        public float MaxEnergy => Mathf.Max(1f, Props.maxEnergy);

        public float Energy => Mathf.Clamp(energy, 0f, MaxEnergy);

        public bool InBreak => ticksToReset > 0;

        public int BreakTicksLeft => Mathf.Max(0, ticksToReset);

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
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref energy, "mx_qh_lotus_energy", 100f);
            Scribe_Values.Look(ref ticksToReset, "mx_qh_lotus_ticksToReset", -1);
            Scribe_Values.Look(ref fullEnergyAccumulatedTicks, "mx_qh_lotus_fullEnergyAccumulatedTicks", 0);
        }

        public override void CompTick()
        {
            base.CompTick();

            if (PawnOwner == null)
            {
                energy = 0f;
                return;
            }

            if (ticksToReset > 0)
            {
                ticksToReset--;
                fullEnergyAccumulatedTicks = 0;
                return;
            }

            float gain = ResolveRegenPerSecond() / 60f;
            if (gain > 0f)
            {
                energy = Mathf.Min(MaxEnergy, energy + gain);
            }

            if (Energy < MaxEnergy - 0.0001f)
            {
                fullEnergyAccumulatedTicks = 0;
            }
            else
            {
                fullEnergyAccumulatedTicks = Mathf.Min(fullEnergyAccumulatedTicks + 1, 1000000);
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
            ticksToReset = Mathf.Max(1, Props.breakDisabledTicks);

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

            Material shieldMat = GetShieldMaterial(Props.activeShieldTexPath, ResolveDrawAlpha(), ResolveShieldTintColor());
            if (shieldMat == null)
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

            Graphics.DrawMesh(MeshPool.plane10, matrix, shieldMat, 0);
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

        private Material GetShieldMaterial(string texPath, float alpha, Color tintColor)
        {
            if (texPath.NullOrEmpty())
            {
                return null;
            }

            float finalAlpha = Mathf.Clamp01(alpha);
            Color finalColor = tintColor;
            finalColor.a = finalAlpha;

            string key = texPath
                         + "|" + finalColor.r.ToString("F2")
                         + "|" + finalColor.g.ToString("F2")
                         + "|" + finalColor.b.ToString("F2")
                         + "|" + finalColor.a.ToString("F2");
            if (!ShieldMaterialByPath.TryGetValue(key, out Material shieldMat))
            {
                shieldMat = MaterialPool.MatFrom(texPath, ShaderDatabase.Transparent, finalColor);
                ShieldMaterialByPath[key] = shieldMat;
            }

            return shieldMat;
        }

        private Color ResolveShieldTintColor()
        {
            float eleganceFactor = EleganceUtility.GetPercent(PawnOwner);
            float springFactor = TempestUtility.GetPercent(PawnOwner);

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

        private MiliraXian.Characters.HediffComp_PawnResourceScaling GetScaler()
        {
            var hediff = PawnOwner?.health?.hediffSet?.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_LotusShield);
            return hediff?.TryGetComp<MiliraXian.Characters.HediffComp_PawnResourceScaling>();
        }

        private float ResolveDamagePerShieldPoint()
        {
            var scaler = GetScaler();
            if (scaler?.Props.damagePerShieldPoint != null)
            {
                return Mathf.Max(0.01f, scaler.DamagePerShieldPoint);
            }

            Pawn owner = PawnOwner;
            float tempest = TempestUtility.GetCurrent(owner);
            float tempestMax = Mathf.Max(1f, TempestUtility.GetMax(owner));
            float factor = Mathf.Clamp01(tempest / tempestMax);
            return Mathf.Max(0.01f, Props.baseDamagePerShieldPoint + Props.bonusDamagePerShieldPointAtMaxTempest * factor);
        }

        private float ResolveRegenPerSecond()
        {
            var scaler = GetScaler();
            if (scaler?.Props.regenPerSecond != null)
            {
                return Mathf.Max(0f, scaler.RegenPerSecond);
            }

            Pawn owner = PawnOwner;
            float elegance = EleganceUtility.GetCurrent(owner);
            float eleganceMax = Mathf.Max(1f, EleganceUtility.GetMax(owner));
            float factor = Mathf.Clamp01(elegance / eleganceMax);
            return Mathf.Max(0f, Props.baseRegenPerSecond + Props.bonusRegenPerSecondAtMaxElegance * factor);
        }

        public string BuildShieldTooltip()
        {
            string status = InBreak
                ? "Status: down (" + Mathf.CeilToInt(BreakTicksLeft / 60f) + "s left)"
                : "Status: active";

            return "ShieldPersonalTip".Translate().Resolve() + "\n\n"
                   + status + "\n"
                   + "Damage per shield point: " + CurrentDamagePerShieldPoint.ToString("F2") + "\n"
                   + "Shield regen: " + CurrentRegenPerSecond.ToString("F2") + " /s";
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
