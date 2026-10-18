using System.Collections.Generic;
using MiliraXian.Characters.Common.Skeletal2D;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Vfx
{
    public class AuraShieldVisualProperties
    {
        public static readonly AuraShieldVisualProperties Default = new();

        public int fullEnergyFadeOutTicks = 90;
        public string texPath = "MiliraXianNeiyu/Effect/Neiyu_Shield/Shield";
        public ShaderTypeDef shaderType;
        public ShaderDef petalShader;
        public ShieldAnimationDef petalAnimation;
        public List<ShaderParameter> shaderParameters;
        public Vector2 drawSize = new(1.9f, 1.9f);
        public float alpha = 0.45f;
        public Vector3 drawOffset = Vector3.zero;
        public Vector2 breakScale = new(1.0f, 1.2f);
        public string absorbFleckDefName = "ExplosionFlash";
        public List<string> hurtFleckDefNames = new();
        public float absorbFleckScale = 1.2f;
        public string absorbEffecterDefName = null;
        // Bloom group the shield glows into; absent means no halo.
        public ScreenBloomGroupDef bloomGroup;
    }

    [StaticConstructorOnStartup]
    public class AuraShieldRenderer
    {
        private static readonly int EffectTimeProperty = Shader.PropertyToID("_EffectTime");
        private static readonly int DissolveProperty = Shader.PropertyToID("_Dissolve");
        private static readonly int BreakProgressProperty = Shader.PropertyToID("_BreakProgress");
        private static readonly int BoneTransformsProperty = Shader.PropertyToID("_BoneTransforms");

        private readonly CompAuraShield shield;
        private Material shieldMaterial;
        private AuraShieldAnimator petalAnimator;
        private Vector4[] boneData;
        private bool visibleLastFrame;
        private float startRealTime;
        private int lastAbsorbTick = -1;

        public AuraShieldRenderer(CompAuraShield shield)
        {
            this.shield = shield;
        }

        private AuraShieldVisualProperties Visual => shield.Props.visual ?? AuraShieldVisualProperties.Default;

        private bool UsePetalShield => Visual.petalShader != null && Visual.petalAnimation != null
            && Visual.petalAnimation.rig != null
            && Visual.petalAnimation.idle?.clip != null
            && Visual.petalAnimation.hit?.clip != null
            && Visual.petalAnimation.breakState?.clip != null;

        // During break the comp stops drawing by default; with the petal pipeline the break
        // animation keeps rendering until its dissolve finishes. Before the animator exists we
        // must draw once so the state machine can observe the break.
        public bool BreakAnimActive => UsePetalShield && (petalAnimator?.BreakActive ?? true);

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
                    TargetInfo t = new(owner.Position, owner.Map);
                    effecter.EffectTick(t, t);
                    effecter.Cleanup();
                }
            }
        }

        public void NotifyBroken(Pawn owner, Thing parent, float energyRatio)
        {
            petalAnimator?.ForceBreak((Find.TickManager?.TicksGame ?? 0) / 60f);
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
            if (!shield.DevKeepVisible && shield.Energy >= shield.MaxEnergy - 0.0001f)
            {
                int fadeTicks = Mathf.Max(1, Visual.fullEnergyFadeOutTicks);
                fullFadeFactor = Mathf.Clamp01(1f - shield.FullEnergyAccumulatedTicks / (float)fadeTicks);
            }

            float drawAlpha = Mathf.Clamp01(Visual.alpha * (0.35f + 0.65f * shieldFactor) * fullFadeFactor);
            if (Visual.texPath.NullOrEmpty())
            {
                return;
            }

            Material shieldMat = ResolveShieldMaterial();
            if (shieldMat == null)
            {
                return;
            }

            float effectTime = Mathf.Max(0f, Time.realtimeSinceStartup - startRealTime);

            Vector3 pos = owner.Drawer.DrawPos;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            Quaternion rotation = PawnBodyRenderRotation(owner);
            pos += RotateDrawOffset(Visual.drawOffset, rotation);

            // 骨骼动画走游戏 tick：暂停冻结、随游戏速度缩放，与世界其他视觉一致。
            // 显示级缩放只作用于护盾网格本体；辉光等附属特效保持原尺寸与原位置，
            // 需要缩放时另写各自的动画。
            float gameSeconds = (Find.TickManager?.TicksGame ?? 0) / 60f;
            float displayScale = 1f;
            Vector3 shieldPos = pos;
            if (UsePetalShield)
            {
                petalAnimator ??= new AuraShieldAnimator(Visual.petalAnimation, gameSeconds);
                petalAnimator.Update(shield, lastAbsorbTick, gameSeconds);
                displayScale = Mathf.Max(0f, petalAnimator.Scale);
                // 绕 scalePivot 缩放：把网格绘制中心向轴心方向移 (1-s)·pivot·drawSize，
                // 轴心在屏幕上保持不动。网格局部 (x, y) 对应世界 (x, 0, z)。
                Vector2 pivot = Visual.petalAnimation.scalePivot;
                float k = 1f - displayScale;
                shieldPos += rotation * new Vector3(
                    pivot.x * Visual.drawSize.x * k,
                    0f,
                    pivot.y * Visual.drawSize.y * k);
            }
            Vector2 shieldDrawSize = Visual.drawSize * displayScale;

            MaterialPropertyBlock propertyBlock = MX_RenderStatics.SharedPropertyBlock;
            propertyBlock.Clear();
            propertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(0.82f, 0.92f, 1f, Mathf.Clamp01(drawAlpha)));
            if (!Visual.shaderParameters.NullOrEmpty())
            {
                for (int i = 0; i < Visual.shaderParameters.Count; i++)
                {
                    Visual.shaderParameters[i].Apply(propertyBlock);
                }
            }
            propertyBlock.SetFloat(EffectTimeProperty, effectTime);

            Mesh shieldMesh = MeshPool.plane10;
            Mesh skinnedMesh = null;
            if (UsePetalShield)
            {
                // GPU 蒙皮：网格静态共享只上传一次，每帧只传骨骼变换给顶点 shader。
                skinnedMesh = Skeletal2DGpuSkin.GetMesh(Visual.petalAnimation.rig);
                if (skinnedMesh != null)
                {
                    Skeletal2DBoneTransform[] bones = petalAnimator.EvaluateWorldTransforms(gameSeconds);
                    int vectorCount = bones.Length * Skeletal2DGpuSkin.VectorsPerBone;
                    if (boneData == null || boneData.Length != vectorCount)
                    {
                        boneData = new Vector4[vectorCount];
                    }
                    Skeletal2DGpuSkin.PackBones(bones, boneData);
                    propertyBlock.SetVectorArray(BoneTransformsProperty, boneData);
                    propertyBlock.SetFloat(DissolveProperty, petalAnimator.Dissolve);
                    propertyBlock.SetFloat(BreakProgressProperty, petalAnimator.BreakProgress);
                    shieldMesh = skinnedMesh;
                }
            }

            Matrix4x4 matrix = Matrix4x4.TRS(
                shieldPos,
                rotation,
                new Vector3(shieldDrawSize.x, 1f, shieldDrawSize.y));
            // The glow is a second submission of the same mesh to the bloom group's layer, with the
            // same property block (bones, colour and fade included). The bloom pass renders every
            // submission of the frame at once, so a shield costs nothing beyond this call.
            if (Visual.bloomGroup != null && drawAlpha > 0.001f)
            {
                ScreenBloom.Submit(Visual.bloomGroup, shieldMesh, matrix, shieldMat, propertyBlock);
            }
            Graphics.DrawMesh(shieldMesh, matrix, shieldMat, 0, null, 0, propertyBlock);
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

        private Material ResolveShieldMaterial()
        {
            if (shieldMaterial != null)
            {
                return shieldMaterial;
            }

            Texture2D texture = ContentFinder<Texture2D>.Get(Visual.texPath, reportFailure: false);
            if (texture == null)
            {
                return null;
            }

            Shader shader = UsePetalShield
                ? Visual.petalShader.Shader
                : (Visual.shaderType ?? ShaderTypeDefOf.Transparent).Shader;
            MaterialRequest request = new(texture, shader, Color.white)
            {
                shaderParameters = Visual.shaderParameters
            };
            shieldMaterial = MaterialPool.MatFrom(request);
            return shieldMaterial;
        }

        private static Quaternion PawnBodyRenderRotation(Pawn pawn)
        {
            if (pawn?.Drawer?.renderer == null)
            {
                return Quaternion.identity;
            }

            return Quaternion.AngleAxis(pawn.Drawer.renderer.BodyAngle(PawnRenderFlags.None), Vector3.up);
        }

        private static Vector3 RotateDrawOffset(Vector3 offset, Quaternion rotation)
        {
            if (offset == Vector3.zero)
            {
                return Vector3.zero;
            }

            return rotation * offset;
        }
    }
}
