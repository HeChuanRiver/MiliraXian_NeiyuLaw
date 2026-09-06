using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    internal static class ZhaoliEffectUtility
    {
        private static readonly Lazy<ThingDef> GuiyiLinkLineMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiLinkLine"));
        private static readonly Lazy<ThingDef> GuiyiLinkPulseMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiLinkPulse"));
        private static readonly Lazy<ThingDef> GuiyiLinkStripeMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiLinkStripe"));
        private static readonly Lazy<ThingDef> GuiyiHealGlowMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiHealGlow"));
        private static readonly Lazy<ThingDef> DingshuLinkLineMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DingshuLinkLine"));
        private static readonly Lazy<ThingDef> DingshuLinkPulseMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DingshuLinkPulse"));
        private static readonly Lazy<ThingDef> DingshuLinkStripeMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DingshuLinkStripe"));
        private static readonly Lazy<ThingDef> DingshuReviveGlowMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DingshuReviveGlow"));
        private static readonly Lazy<ThingDef> DeathFieldAreaMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DeathFieldArea"));
        private static readonly Lazy<ThingDef> DeathFieldMarkMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DeathFieldMark"));
        private static readonly Lazy<ThingDef> SoulAbsorbPulseMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_SoulAbsorbPulse"));
        private static readonly Lazy<ThingDef> MinshenWarnAreaMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_MinshenWarnArea"));
        private static readonly Lazy<ThingDef>[] DeathFieldParticleMoteDefsLazy =
        {
            new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DeathFieldParticleA")),
            new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DeathFieldParticleB")),
            new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_DeathFieldParticleC"))
        };
        private static readonly Lazy<ThingDef>[] GuiyiHealFrameMoteDefsLazy =
        {
            new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiHealFrameA")),
            new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiHealFrameB")),
            new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_GuiyiHealFrameC"))
        };
        private static readonly Lazy<ThingDef>[] MinghuoAuraFrameMoteDefsLazy =
        {
            new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_MinghuoAuraFrameA")),
            new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_MinghuoAuraFrameB")),
            new(() => DefDatabase<ThingDef>.GetNamedSilentFail("MXZL_Mote_MinghuoAuraFrameC"))
        };
        private static readonly Lazy<ThingDef> GroundCrackHugeMoteDefLazy = new(() => DefDatabase<ThingDef>.GetNamedSilentFail("GroundCrackHuge"));
        private static readonly Lazy<FleckDef> DeathRefusalBubbleFleckDefLazy = new(() => DefDatabase<FleckDef>.GetNamedSilentFail("DeathRefusalBubble"));
        private static readonly Lazy<FleckDef> DeathRefusalPulseFleckDefLazy = new(() => DefDatabase<FleckDef>.GetNamedSilentFail("DeathRefusalPulse"));
        private static readonly Lazy<HediffDef> MinghuoHediffDefLazy = new(() => DefDatabase<HediffDef>.GetNamedSilentFail(ZhaoliMinghuoUtility.MinghuoHediffDefName));
        private static readonly Lazy<HediffDef> DormancyHediffDefLazy = new(() => DefDatabase<HediffDef>.GetNamedSilentFail(ZhaoliKarmaUtility.DormancyHediffDefName));

        public static ThingDef GuiyiLinkLineMoteDef => GuiyiLinkLineMoteDefLazy.Value;

        public static ThingDef GuiyiLinkPulseMoteDef => GuiyiLinkPulseMoteDefLazy.Value;

        public static ThingDef GuiyiLinkStripeMoteDef => GuiyiLinkStripeMoteDefLazy.Value;

        public static ThingDef GuiyiHealGlowMoteDef => GuiyiHealGlowMoteDefLazy.Value;

        public static ThingDef DingshuLinkLineMoteDef => DingshuLinkLineMoteDefLazy.Value;

        public static ThingDef DingshuLinkPulseMoteDef => DingshuLinkPulseMoteDefLazy.Value;

        public static ThingDef DingshuLinkStripeMoteDef => DingshuLinkStripeMoteDefLazy.Value;

        public static ThingDef DingshuReviveGlowMoteDef => DingshuReviveGlowMoteDefLazy.Value;

        public static ThingDef DeathFieldAreaMoteDef => DeathFieldAreaMoteDefLazy.Value;

        public static ThingDef DeathFieldMarkMoteDef => DeathFieldMarkMoteDefLazy.Value;

        public static ThingDef SoulAbsorbPulseMoteDef => SoulAbsorbPulseMoteDefLazy.Value;

        public static ThingDef MinshenWarnAreaMoteDef => MinshenWarnAreaMoteDefLazy.Value;

        public static ThingDef RandomDeathFieldParticleMoteDef => GetRandomDef(DeathFieldParticleMoteDefsLazy);

        public static ThingDef GuiyiHealFrameMoteDef(int frameIndex)
        {
            return GetFrameDef(GuiyiHealFrameMoteDefsLazy, frameIndex);
        }

        public static ThingDef MinghuoAuraFrameMoteDef(int frameIndex)
        {
            return GetFrameDef(MinghuoAuraFrameMoteDefsLazy, frameIndex);
        }

        public static ThingDef GroundCrackHugeMoteDef => GroundCrackHugeMoteDefLazy.Value;

        public static FleckDef DeathRefusalBubbleFleckDef => DeathRefusalBubbleFleckDefLazy.Value;

        public static FleckDef DeathRefusalPulseFleckDef => DeathRefusalPulseFleckDefLazy.Value;

        public static HediffDef MinghuoHediffDef => MinghuoHediffDefLazy.Value;

        public static HediffDef DormancyHediffDef => DormancyHediffDefLazy.Value;

        private static ThingDef GetFrameDef(Lazy<ThingDef>[] frames, int frameIndex)
        {
            if (frames == null || frames.Length == 0)
            {
                return null;
            }

            int startIndex = frameIndex % frames.Length;
            if (startIndex < 0)
            {
                startIndex += frames.Length;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                ThingDef frameDef = frames[(startIndex + i) % frames.Length].Value;
                if (frameDef != null)
                {
                    return frameDef;
                }
            }

            return null;
        }

        private static ThingDef GetRandomDef(Lazy<ThingDef>[] defs)
        {
            if (defs == null || defs.Length == 0)
            {
                return null;
            }

            int startIndex = Rand.Range(0, defs.Length);
            for (int i = 0; i < defs.Length; i++)
            {
                ThingDef def = defs[(startIndex + i) % defs.Length].Value;
                if (def != null)
                {
                    return def;
                }
            }

            return null;
        }
    }

    internal enum ZhaoliVisualAnimationKind
    {
        GuiyiHeal
    }

    internal static class ZhaoliVisualUtility
    {
        public static void QueueGuiyiHealAnimation(Pawn target, float scale)
        {
            if (target == null || !target.Spawned)
            {
                return;
            }

            if (MiliraXian.Characters.CharacterUnityVfxRuntime.TryPlayAttached(
                    MiliraXian.Characters.CharacterUnityVfxKind.ZhaoliGuiyi,
                    target,
                    Mathf.Max(0.1f, scale),
                    18))
            {
                return;
            }

            GameComponent_ZhaoliVisuals component = Current.Game?.GetComponent<GameComponent_ZhaoliVisuals>();
            if (component == null)
            {
                SpawnAttachedFrame(target, ZhaoliEffectUtility.GuiyiHealFrameMoteDef(0), Vector3.zero, scale);
                return;
            }

            component.QueueAttachedAnimation(target, ZhaoliVisualAnimationKind.GuiyiHeal, 3, 5, Vector3.zero, scale);
        }

        public static void SpawnMinghuoAuraFrame(Pawn target, int frameIndex, float scale)
        {
            SpawnAttachedFrame(target, ZhaoliEffectUtility.MinghuoAuraFrameMoteDef(frameIndex), Vector3.zero, scale, randomizeRotation: false);
        }

        internal static void SpawnAttachedFrame(Pawn target, ThingDef frameDef, Vector3 offset, float scale, bool randomizeRotation = true)
        {
            if (target == null || frameDef == null || !target.Spawned)
            {
                return;
            }

            Mote mote = MoteMaker.MakeAttachedOverlay(target, frameDef, offset, scale);
            if (mote != null && randomizeRotation)
            {
                mote.exactRotation = Rand.Range(0f, 360f);
            }
        }
    }

    public class GameComponent_ZhaoliVisuals : GameComponent
    {
        private List<ZhaoliPendingAttachedAnimation> pendingAnimations = new();
        private int nextDueTick = int.MaxValue;

        public GameComponent_ZhaoliVisuals(Game game)
        {
        }

        internal void QueueAttachedAnimation(Pawn target, ZhaoliVisualAnimationKind kind, int frameCount, int ticksPerFrame, Vector3 offset, float scale)
        {
            if (target == null || !target.Spawned || frameCount <= 0)
            {
                return;
            }

            pendingAnimations ??= new();

            int currentTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            pendingAnimations.Add(new ZhaoliPendingAttachedAnimation(target, kind, frameCount, Mathf.Max(1, ticksPerFrame), currentTick, offset, scale));
            nextDueTick = Mathf.Min(nextDueTick, currentTick);
        }

        public override void GameComponentTick()
        {
            if (pendingAnimations == null || pendingAnimations.Count == 0 || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick < nextDueTick)
            {
                return;
            }

            int newNextDueTick = int.MaxValue;
            for (int i = pendingAnimations.Count - 1; i >= 0; i--)
            {
                ZhaoliPendingAttachedAnimation animation = pendingAnimations[i];
                if (animation == null || animation.TargetInvalid)
                {
                    pendingAnimations.RemoveAt(i);
                    continue;
                }

                if (currentTick < animation.nextFrameTick)
                {
                    newNextDueTick = Mathf.Min(newNextDueTick, animation.nextFrameTick);
                    continue;
                }

                ThingDef frameDef = ResolveFrameDef(animation.kind, animation.frameIndex);
                ZhaoliVisualUtility.SpawnAttachedFrame(animation.target, frameDef, animation.offset, animation.scale);
                animation.frameIndex++;
                if (animation.frameIndex >= animation.frameCount)
                {
                    pendingAnimations.RemoveAt(i);
                    continue;
                }

                animation.nextFrameTick = currentTick + animation.ticksPerFrame;
                newNextDueTick = Mathf.Min(newNextDueTick, animation.nextFrameTick);
            }

            nextDueTick = newNextDueTick;
        }

        private static ThingDef ResolveFrameDef(ZhaoliVisualAnimationKind kind, int frameIndex)
        {
            return kind switch
            {
                ZhaoliVisualAnimationKind.GuiyiHeal => ZhaoliEffectUtility.GuiyiHealFrameMoteDef(frameIndex),
                _ => null,
            };
        }
    }

    internal class ZhaoliPendingAttachedAnimation
    {
        public Pawn target;
        public ZhaoliVisualAnimationKind kind;
        public int frameCount;
        public int ticksPerFrame;
        public int nextFrameTick;
        public int frameIndex;
        public Vector3 offset;
        public float scale;

        public ZhaoliPendingAttachedAnimation(Pawn target, ZhaoliVisualAnimationKind kind, int frameCount, int ticksPerFrame, int nextFrameTick, Vector3 offset, float scale)
        {
            this.target = target;
            this.kind = kind;
            this.frameCount = frameCount;
            this.ticksPerFrame = ticksPerFrame;
            this.nextFrameTick = nextFrameTick;
            this.offset = offset;
            this.scale = scale;
        }

        public bool TargetInvalid => target == null || target.Destroyed || target.Dead || !target.Spawned;
    }
}
