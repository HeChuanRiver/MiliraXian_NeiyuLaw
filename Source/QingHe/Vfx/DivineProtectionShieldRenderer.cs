using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Vfx
{
    public class DivineProtectionShieldGlowProperties
    {
        public string texPath;
        public Color color = Color.white;
        public float alpha = 0.45f;
        public float sizeMultiplier = 1f;
        public float breathAmplitude;
        public float breathSpeed = 1f;
        public int durationTicks = 60;
        public Vector3 drawOffset = Vector3.zero;
    }

    public class DivineProtectionShieldVisualProperties
    {
        public static readonly DivineProtectionShieldVisualProperties Default = new DivineProtectionShieldVisualProperties();

        public int fullEnergyFadeOutTicks = 90;
        public string texPath = "MiliraXianNeiyu/Effect/Neiyu_Shield/Shield";
        public ShaderTypeDef shaderType = ShaderTypeDefOf.Transparent;
        public List<ShaderParameter> shaderParameters;
        public Vector2 drawSize = new Vector2(1.9f, 1.9f);
        public float alpha = 0.45f;
        public Vector3 drawOffset = Vector3.zero;
        public Vector2 breakScale = new Vector2(1.0f, 1.2f);
        public string absorbFleckDefName = "ExplosionFlash";
        public List<string> hurtFleckDefNames = new List<string>();
        public float absorbFleckScale = 1.2f;
        public string absorbEffecterDefName = null;
        public DivineProtectionShieldGlowProperties farGlow = new DivineProtectionShieldGlowProperties
        {
            texPath = "Things/Mote/FireGlow",
            color = new Color(1f, 0.8117647f, 0.9294118f, 1f),
            alpha = 0.45f,
            sizeMultiplier = 2.8f,
            breathAmplitude = 0.25f,
            breathSpeed = 1f
        };
        public DivineProtectionShieldGlowProperties hitGlow = new DivineProtectionShieldGlowProperties
        {
            texPath = "Things/Mote/PsychicDistortionRing",
            color = new Color(1f, 0.72f, 0.92f, 1f),
            alpha = 0.45f,
            sizeMultiplier = 1.12f,
            durationTicks = 60,
            drawOffset = new Vector3(0f, 0f, -0.08f)
        };
    }

    public class DivineProtectionShieldRenderer
    {
        private readonly CompDivineProtectionShield shield;
        private bool visibleLastFrame;
        private float startRealTime;
        private int lastAbsorbTick = -1;

        public DivineProtectionShieldRenderer(CompDivineProtectionShield shield)
        {
            this.shield = shield;
        }

        private DivineProtectionShieldVisualProperties Visual => shield.Props.visual ?? DivineProtectionShieldVisualProperties.Default;

        public void NotifyHidden()
        {
            visibleLastFrame = false;
        }

        public void NotifyAbsorbed(Pawn owner, int currentTick)
        {
            lastAbsorbTick = currentTick;

            if (owner == null || !owner.Spawned || owner.Map == null)
            {
                return;
            }

            SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(owner.Position, owner.Map));

            FleckDef fleck = null;
            if (Visual.hurtFleckDefNames != null && Visual.hurtFleckDefNames.Count > 0)
            {
                int index = Rand.RangeInclusive(0, Visual.hurtFleckDefNames.Count - 1);
                string fleckName = Visual.hurtFleckDefNames[index];
                if (!fleckName.NullOrEmpty())
                {
                    fleck = DefDatabase<FleckDef>.GetNamedSilentFail(fleckName);
                }
            }
            if (fleck == null && !Visual.absorbFleckDefName.NullOrEmpty())
            {
                fleck = DefDatabase<FleckDef>.GetNamedSilentFail(Visual.absorbFleckDefName);
            }
            if (fleck == null)
            {
                fleck = FleckDefOf.ExplosionFlash;
            }

            if (fleck != null)
            {
                FleckMaker.Static(owner.TrueCenter(), owner.Map, fleck, Mathf.Max(0.1f, Visual.absorbFleckScale));
            }

            if (!Visual.absorbEffecterDefName.NullOrEmpty())
            {
                EffecterDef effecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail(Visual.absorbEffecterDefName);
                if (effecterDef != null)
                {
                    Effecter effecter = effecterDef.Spawn(owner.Position, owner.Map);
                    TargetInfo t = new TargetInfo(owner.Position, owner.Map);
                    effecter.EffectTick(t, t);
                    effecter.Cleanup();
                }
            }
        }

        public void NotifyBroken(Pawn owner, Thing parent, float energyRatio)
        {
            if (owner == null || !owner.Spawned || owner.Map == null)
            {
                return;
            }

            Vector2 breakScale = Visual.breakScale;
            EffecterDefOf.Shield_Break.SpawnAttached(
                parent,
                parent.MapHeld,
                Mathf.Lerp(breakScale.x, breakScale.y, Mathf.Clamp01(energyRatio)));
            FleckMaker.Static(owner.TrueCenter(), owner.Map, FleckDefOf.ExplosionFlash, 8f);
        }

        public void Draw(Pawn owner)
        {
            if (!visibleLastFrame)
            {
                startRealTime = Time.realtimeSinceStartup;
                visibleLastFrame = true;
            }

            float shieldFactor = Mathf.Clamp01(shield.Energy / shield.MaxEnergy);
            float fullFadeFactor = 1f;
            if (shield.Energy >= shield.MaxEnergy - 0.0001f)
            {
                int fadeTicks = Mathf.Max(1, Visual.fullEnergyFadeOutTicks);
                fullFadeFactor = Mathf.Clamp01(1f - shield.FullEnergyAccumulatedTicks / (float)fadeTicks);
            }

            float drawAlpha = Mathf.Clamp01(Visual.alpha * (0.35f + 0.65f * shieldFactor) * fullFadeFactor);
            if (Visual.texPath.NullOrEmpty())
            {
                return;
            }

            Color tintColor = new Color(0.82f, 0.92f, 1f, Mathf.Clamp01(drawAlpha));
            ShaderTypeDef shaderType = Visual.shaderType ?? ShaderTypeDefOf.Transparent;
            MaterialRequest request = new MaterialRequest(ContentFinder<Texture2D>.Get(Visual.texPath), shaderType.Shader, tintColor)
            {
                shaderParameters = Visual.shaderParameters
            };
            Material shieldMat = MaterialPool.MatFrom(request);
            if (shieldMat == null)
            {
                return;
            }

            float effectTime = Mathf.Max(0f, Time.realtimeSinceStartup - startRealTime);
            shieldMat.SetFloat("_EffectTime", effectTime);

            Vector3 pos = owner.Drawer.DrawPos;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            pos += Visual.drawOffset;

            DrawGlow(Visual.farGlow, pos, Visual.drawSize, drawAlpha, effectTime, 1f);

            float hitGlowFactor = 0f;
            if (lastAbsorbTick >= 0)
            {
                int durationTicks = Mathf.Max(1, Visual.hitGlow?.durationTicks ?? 60);
                int elapsedTicks = (Find.TickManager != null ? Find.TickManager.TicksGame : 0) - lastAbsorbTick;
                if (elapsedTicks >= 0 && elapsedTicks < durationTicks)
                {
                    hitGlowFactor = Mathf.Clamp01(1f - elapsedTicks / (float)durationTicks);
                }
            }
            DrawGlow(Visual.hitGlow, pos, Visual.drawSize, drawAlpha, effectTime, hitGlowFactor);

            MaterialPropertyBlock propertyBlock = MX_QHRenderStatics.SharedPropertyBlock;
            propertyBlock.Clear();
            if (!Visual.shaderParameters.NullOrEmpty())
            {
                for (int i = 0; i < Visual.shaderParameters.Count; i++)
                {
                    Visual.shaderParameters[i].Apply(propertyBlock);
                }
            }
            propertyBlock.SetFloat("_EffectTime", effectTime);

            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.identity,
                new Vector3(Visual.drawSize.x, 1f, Visual.drawSize.y));
            Graphics.DrawMesh(MeshPool.plane10, matrix, shieldMat, 0, null, 0, propertyBlock);
            propertyBlock.Clear();
        }

        public int FullEnergyFadeOutTicks => Mathf.Max(1, Visual.fullEnergyFadeOutTicks);

        public float AbsorbFlashPercent(int currentTick)
        {
            if (lastAbsorbTick < 0)
            {
                return 0f;
            }

            int elapsed = currentTick - lastAbsorbTick;
            const float decayTicks = 40f;
            return Mathf.Clamp01(1f - elapsed / decayTicks);
        }

        private static void DrawGlow(DivineProtectionShieldGlowProperties glow, Vector3 pos, Vector2 shieldDrawSize, float shieldAlpha, float effectTime, float factor)
        {
            if (glow == null || factor <= 0.001f || glow.texPath.NullOrEmpty() || shieldAlpha <= 0.001f || glow.alpha <= 0.001f)
            {
                return;
            }

            Texture2D texture = ContentFinder<Texture2D>.Get(glow.texPath, reportFailure: false);
            if (texture == null)
            {
                return;
            }

            Color glowColor = glow.color;
            float breath = 0.5f - 0.5f * Mathf.Cos(Mathf.Max(0f, effectTime) * Mathf.Max(0f, glow.breathSpeed) * 6.2831855f);
            float breathFactor = Mathf.Lerp(1f - Mathf.Clamp01(glow.breathAmplitude), 1f + Mathf.Clamp01(glow.breathAmplitude), breath);
            glowColor.a = Mathf.Clamp01(shieldAlpha * glow.alpha * factor * breathFactor);
            Material glowMat = MaterialPool.MatFrom(texture, ShaderDatabase.MoteGlow, glowColor);
            if (glowMat == null)
            {
                return;
            }

            Vector2 drawSize = shieldDrawSize * Mathf.Max(0.01f, glow.sizeMultiplier);
            Matrix4x4 matrix = Matrix4x4.TRS(
                pos + glow.drawOffset,
                Quaternion.identity,
                new Vector3(drawSize.x, 1f, drawSize.y));

            Graphics.DrawMesh(MeshPool.plane10, matrix, glowMat, 0);
        }
    }
}
