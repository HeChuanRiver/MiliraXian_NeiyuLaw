using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using MiliraXian.Characters.QingHe.Hediffs;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_LotusShield : CompProperties
    {
        public float maxEnergy = 100f;

        // Shield regeneration per second.
        public float baseRegenPerSecond = 0.8f;

        public float shangMaxEnergyMultiplier = 2f;
        public float zhiRegenMultiplier = 3f;
        public int hitRegenDelayTicks = 120;
        public float gaoshanDamageCap = 40f;
        public float yuDamageCap = 10f;
        public int yuBreakDisabledTicks = 120;

        // After breaking, shield is disabled for these ticks.
        public int breakDisabledTicks = 600;
        public bool breakOnEmp = true;

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
        public ShaderTypeDef activeShieldShaderType = ShaderTypeDefOf.Transparent;
        public List<ShaderParameter> activeShieldShaderParameters;
        public Vector2 activeShieldDrawSize = new Vector2(1.9f, 1.9f);
        public float activeShieldAlpha = 0.45f;
        public string activeShieldFarGlowTexPath = "Things/Mote/FireGlow";
        public Color activeShieldFarGlowColor = new Color(1f, 0.8117647f, 0.9294118f, 1f);
        public float activeShieldFarGlowAlpha = 0.45f;
        public float activeShieldFarGlowSizeMultiplier = 2.8f;
        public float activeShieldFarGlowBreathAmplitude = 0.25f;
        public float activeShieldFarGlowBreathSpeed = 1f;
        public string activeShieldRingGlowTexPath = "Things/Mote/PsychicDistortionRing";
        public Color activeShieldRingGlowColor = new Color(1f, 0.72f, 0.92f, 1f);
        public float activeShieldRingGlowAlpha = 0.45f;
        public float activeShieldRingGlowSizeMultiplier = 1.12f;
        public int activeShieldRingGlowDurationTicks = 60;
        public float activeShieldRingGlowDrawOffsetZ = -0.08f;
        public float activeShieldAltitudeOffset = 0f;
        public float activeShieldDrawOffsetZ = 0f;

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
        private float energy = 100f;
        private int ticksToReset = -1;
        private int ticksToRegen = 0;

        private int fullEnergyAccumulatedTicks = 0;
        // Record the tick when damage was last absorbed, used for Gizmo hit-flash.
        private int lastAbsorbTick = -1;
        private bool shieldFxVisibleLastFrame = false;
        private float shieldFxStartRealTime = 0f;

        public CompProperties_LotusShield Props => (CompProperties_LotusShield)props;

        private Pawn PawnOwner => parent as Pawn;

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        public float MaxEnergy => Mathf.Max(1f, Props.maxEnergy * ResolveMaxEnergyMultiplier());

        public float Energy => Mathf.Clamp(energy, 0f, MaxEnergy);

        public bool InBreak => ticksToReset > 0;

        public int BreakTicksLeft => Mathf.Max(0, ticksToReset);

        public bool InRegenDelay => ticksToRegen > 0;

        public int RegenDelayTicksLeft => Mathf.Max(0, ticksToRegen);

        public float CurrentRegenPerSecond => ResolveRegenPerSecond();

        /// <summary>
        /// Flash intensity for the shield bar, decaying from 1 to 0 over ~40 ticks after absorbing damage.
        /// </summary>
        public float AbsorbFlashPercent
        {
            get
            {
                if (lastAbsorbTick < 0) return 0f;
                int elapsed = CurrentTick - lastAbsorbTick;
                const float decayTicks = 40f;
                return Mathf.Clamp01(1f - elapsed / decayTicks);
            }
        }

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
            Scribe_Values.Look(ref ticksToRegen, "mx_qh_lotus_ticksToRegen", 0);
            Scribe_Values.Look(ref fullEnergyAccumulatedTicks, "mx_qh_lotus_fullEnergyAccumulatedTicks", 0);
            Scribe_Values.Look(ref lastAbsorbTick, "mx_qh_lotus_lastAbsorbTick", -1);
        }

        public override void CompTick()
        {
            base.CompTick();

            if (PawnOwner == null)
            {
                energy = 0f;
                return;
            }

            energy = Mathf.Min(energy, MaxEnergy);

            if (ticksToReset > 0)
            {
                ticksToReset--;
                fullEnergyAccumulatedTicks = 0;
                return;
            }

            if (ticksToRegen > 0)
            {
                ticksToRegen--;
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

            if (energy <= 0f)
            {
                return;
            }

            float shieldDamage = ResolveShieldDamage(dinfo.Amount);
            if (shieldDamage <= 0f)
            {
                return;
            }

            if (shieldDamage >= energy - 0.0001f)
            {
                energy = 0f;
            }
            else
            {
                energy -= shieldDamage;
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
            lastAbsorbTick = CurrentTick;
            if (FlowerCourtUtility.GetFlowerDivination(PawnOwner)?.Active == true)
            {
                ticksToRegen = 0;
            }
            else
            {
                ticksToRegen = Mathf.Max(0, Props.hitRegenDelayTicks);
            }

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
            ticksToRegen = 0;
            ticksToReset = Mathf.Max(1, ResolveBreakDisabledTicks());

            Pawn owner = PawnOwner;
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
                shieldFxVisibleLastFrame = false;
                return;
            }

            if (!shieldFxVisibleLastFrame)
            {
                shieldFxStartRealTime = Time.realtimeSinceStartup;
                shieldFxVisibleLastFrame = true;
            }

            float drawAlpha = ResolveDrawAlpha();
            Material shieldMat = GetShieldMaterial(Props.activeShieldTexPath, drawAlpha, ResolveShieldTintColor());
            if (shieldMat == null)
            {
                return;
            }
            float effectTime = Mathf.Max(0f, Time.realtimeSinceStartup - shieldFxStartRealTime);
            shieldMat.SetFloat("_EffectTime", effectTime);

            Vector3 pos = owner.Drawer.DrawPos;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            pos.z += Props.activeShieldDrawOffsetZ;
            pos += Altitudes.AltIncVect * Props.activeShieldAltitudeOffset;
            
            Vector2 drawSize = Props.activeShieldDrawSize;
            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.identity,
                new Vector3(drawSize.x, 1f, drawSize.y));

            DrawShieldFarGlow(pos, drawSize, drawAlpha, effectTime);
            DrawShieldRingGlow(pos, drawSize, drawAlpha, effectTime);

            MaterialPropertyBlock propertyBlock = MX_QHRenderStatics.SharedPropertyBlock;
            propertyBlock.Clear();
            ApplyShieldShaderParameters(propertyBlock);
            propertyBlock.SetFloat("_EffectTime", effectTime);

            Graphics.DrawMesh(MeshPool.plane10, matrix, shieldMat, 0, null, 0, propertyBlock);
            propertyBlock.Clear();
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
            return GetShieldMaterial(texPath, alpha, tintColor, Props.activeShieldShaderParameters);
        }

        private Material GetShieldMaterial(string texPath, float alpha, Color tintColor, List<ShaderParameter> shaderParameters)
        {
            if (texPath.NullOrEmpty())
            {
                return null;
            }

            float finalAlpha = Mathf.Clamp01(alpha);
            Color finalColor = tintColor;
            finalColor.a = finalAlpha;

            ShaderTypeDef shaderType = Props.activeShieldShaderType ?? ShaderTypeDefOf.Transparent;
            Shader shieldShader = shaderType.Shader;

            MaterialRequest request = new MaterialRequest(ContentFinder<Texture2D>.Get(texPath), shieldShader, finalColor)
            {
                shaderParameters = shaderParameters
            };
            return MaterialPool.MatFrom(request);
        }

        private void DrawShieldFarGlow(Vector3 pos, Vector2 shieldDrawSize, float shieldAlpha, float effectTime)
        {
            if (Props.activeShieldFarGlowTexPath.NullOrEmpty() || shieldAlpha <= 0.001f || Props.activeShieldFarGlowAlpha <= 0.001f)
            {
                return;
            }

            Texture2D texture = ContentFinder<Texture2D>.Get(Props.activeShieldFarGlowTexPath, reportFailure: false);
            if (texture == null)
            {
                return;
            }

            Color glowColor = Props.activeShieldFarGlowColor;
            glowColor.a = Mathf.Clamp01(shieldAlpha * Props.activeShieldFarGlowAlpha * ResolveGlowBreath(effectTime));
            Material glowMat = MaterialPool.MatFrom(texture, ShaderDatabase.MoteGlow, glowColor);
            if (glowMat == null)
            {
                return;
            }

            float sizeMultiplier = Mathf.Max(0.01f, Props.activeShieldFarGlowSizeMultiplier);
            Vector2 drawSize = shieldDrawSize * sizeMultiplier;
            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.identity,
                new Vector3(drawSize.x, 1f, drawSize.y));

            Graphics.DrawMesh(MeshPool.plane10, matrix, glowMat, 0);
        }

        private void DrawShieldRingGlow(Vector3 pos, Vector2 shieldDrawSize, float shieldAlpha, float effectTime)
        {
            float hitGlowFactor = ResolveHitGlowFactor();
            if (hitGlowFactor <= 0.001f || Props.activeShieldRingGlowTexPath.NullOrEmpty() || shieldAlpha <= 0.001f || Props.activeShieldRingGlowAlpha <= 0.001f)
            {
                return;
            }

            Texture2D texture = ContentFinder<Texture2D>.Get(Props.activeShieldRingGlowTexPath, reportFailure: false);
            if (texture == null)
            {
                return;
            }

            Color glowColor = Props.activeShieldRingGlowColor;
            glowColor.a = Mathf.Clamp01(shieldAlpha * Props.activeShieldRingGlowAlpha * hitGlowFactor);
            Material glowMat = MaterialPool.MatFrom(texture, ShaderDatabase.MoteGlow, glowColor);
            if (glowMat == null)
            {
                return;
            }

            float sizeMultiplier = Mathf.Max(0.01f, Props.activeShieldRingGlowSizeMultiplier);
            Vector2 drawSize = shieldDrawSize * sizeMultiplier;
            Vector3 drawPos = pos;
            drawPos.z += Props.activeShieldRingGlowDrawOffsetZ;
            Matrix4x4 matrix = Matrix4x4.TRS(
                drawPos,
                Quaternion.identity,
                new Vector3(drawSize.x, 1f, drawSize.y));

            Graphics.DrawMesh(MeshPool.plane10, matrix, glowMat, 0);
        }

        private float ResolveHitGlowFactor()
        {
            if (lastAbsorbTick < 0)
            {
                return 0f;
            }

            int durationTicks = Mathf.Max(1, Props.activeShieldRingGlowDurationTicks);
            int elapsedTicks = CurrentTick - lastAbsorbTick;
            if (elapsedTicks < 0 || elapsedTicks >= durationTicks)
            {
                return 0f;
            }

            return Mathf.Clamp01(1f - elapsedTicks / (float)durationTicks);
        }

        private float ResolveGlowBreath(float effectTime)
        {
            return ResolveGlowBreath(effectTime, Props.activeShieldFarGlowBreathAmplitude, Props.activeShieldFarGlowBreathSpeed);
        }

        private static float ResolveGlowBreath(float effectTime, float amplitude, float speed)
        {
            float breath = 0.5f - 0.5f * Mathf.Cos(Mathf.Max(0f, effectTime) * Mathf.Max(0f, speed) * 6.2831855f);
            return Mathf.Lerp(
                1f - Mathf.Clamp01(amplitude),
                1f + Mathf.Clamp01(amplitude),
                breath);
        }

        private void ApplyShieldShaderParameters(MaterialPropertyBlock propertyBlock)
        {
            ApplyShieldShaderParameters(propertyBlock, Props.activeShieldShaderParameters);
        }

        private void ApplyShieldShaderParameters(MaterialPropertyBlock propertyBlock, List<ShaderParameter> shaderParameters)
        {
            if (propertyBlock == null || shaderParameters.NullOrEmpty())
            {
                return;
            }

            for (int i = 0; i < shaderParameters.Count; i++)
            {
                shaderParameters[i].Apply(propertyBlock);
            }
        }

        private Color ResolveShieldTintColor()
        {
            Color baseColor = new Color(0.82f, 0.92f, 1f, 1f);
            return new Color(
                Mathf.Clamp01(baseColor.r),
                Mathf.Clamp01(baseColor.g),
                Mathf.Clamp01(baseColor.b),
                1f);
        }

        private float ResolveMaxEnergyMultiplier()
        {
            return HasSkillNode(QingheSkillTreeSystem.NodeShang) ? Mathf.Max(0.01f, Props.shangMaxEnergyMultiplier) : 1f;
        }

        private float ResolveRegenPerSecond()
        {
            float multiplier = HasSkillNode(QingheSkillTreeSystem.NodeZhi) ? Mathf.Max(0f, Props.zhiRegenMultiplier) : 1f;
            return Mathf.Max(0f, Props.baseRegenPerSecond * multiplier);
        }

        private float ResolveShieldDamage(float incomingDamage)
        {
            float damage = Mathf.Max(0f, incomingDamage);
            float cap = ResolveDamageCap();
            return cap > 0f ? Mathf.Min(damage, cap) : damage;
        }

        private float ResolveDamageCap()
        {
            if (FlowerCourtUtility.GetFlowerDivination(PawnOwner)?.Active == true && HasSkillNode(QingheSkillTreeSystem.NodeYu))
            {
                return Mathf.Max(0f, Props.yuDamageCap);
            }

            return HasSkillNode(QingheSkillTreeSystem.NodeGaoshan) ? Mathf.Max(0f, Props.gaoshanDamageCap) : 0f;
        }

        private int ResolveBreakDisabledTicks()
        {
            if (FlowerCourtUtility.GetFlowerDivination(PawnOwner)?.Active == true && HasSkillNode(QingheSkillTreeSystem.NodeYu))
            {
                return Mathf.Max(1, Props.yuBreakDisabledTicks);
            }

            return Mathf.Max(1, Props.breakDisabledTicks);
        }

        private bool HasSkillNode(string nodeDefName)
        {
            return FlowerCourtUtility.EnsureSkillTreeState(PawnOwner)?.HasNode(nodeDefName) == true;
        }

        public string BuildShieldTooltip()
        {
            string status = InBreak
                ? "MX_QH_LotusShieldStatusDown".Translate(Mathf.CeilToInt(BreakTicksLeft / 60f)).ToString()
                : "MX_QH_LotusShieldStatusActive".Translate().ToString();

            return "花神护体\n\n"
                   + status + "\n"
                   + "护盾值：" + Energy.ToString("F0") + " / " + MaxEnergy.ToString("F0") + "\n"
                   + "护盾回复：" + CurrentRegenPerSecond.ToString("F2") + " /秒"
                   + (InRegenDelay ? "\n回复延迟：" + Mathf.CeilToInt(RegenDelayTicksLeft / 60f) + "秒" : "");
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
