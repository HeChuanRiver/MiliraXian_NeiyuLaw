using MiliraXian.Characters.Common.Skeletal2D;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Things;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Vfx
{
    /// <summary>
    /// Per-frame driver for the lotus aura shield petal deformation. Picks one of the three
    /// states (idle / hit / break) from the shield comp and plays the matching
    /// skeletal clip through a Skeletal2DPlayer, which handles Bezier sampling at
    /// fractional ticks and pose crossfades on state changes and hit re-triggers.
    /// </summary>
    public class AuraShieldAnimator
    {
        private enum ShieldAnimState
        {
            Idle,
            Hit,
            Break,
        }

        private readonly ShieldAnimationDef def;
        private readonly Skeletal2DPlayer player;
        private ShieldAnimState state = ShieldAnimState.Idle;
        private float stateStartTime;
        private int lastSeenAbsorbTick = -1;

        public AuraShieldAnimator(ShieldAnimationDef def, float startSeconds)
        {
            this.def = def;
            stateStartTime = startSeconds;
            player = new Skeletal2DPlayer(def.rig);
            player.Play(def.idle.clip, 0, startSeconds, def.idle.speed);
        }

        public float Dissolve { get; private set; }

        /// <summary>Display-level uniform scale of the whole shield, crossfaded across state changes.</summary>
        public float Scale { get; private set; } = 1f;

        public bool BreakActive { get; private set; }

        private float scaleFrom = 1f;
        private float scaleBlendStartTime;
        private float scaleBlendSeconds = 0.0001f;
        // Seconds of the current state's clip skipped by a retrigger start offset; the hit window
        // is shortened by the same amount so it still ends when the clip does.
        private float stateStartOffsetSeconds;

        /// <summary>
        /// Break is an event, not a pollable state: the comp's draw gate reads BreakAnimActive
        /// before Update would ever run on the break frame, so the break must be pushed in here.
        /// </summary>
        public void ForceBreak(float nowSeconds)
        {
            PlayState(ShieldAnimState.Break, nowSeconds);
            BreakActive = true;
        }

        public Skeletal2DBoneTransform[] EvaluateWorldTransforms(float nowSeconds)
        {
            return player.EvaluateWorldTransforms(nowSeconds);
        }

        public void Update(CompAuraShield shield, int lastAbsorbTick, float nowSeconds)
        {
            bool newHit = lastAbsorbTick != lastSeenAbsorbTick;
            lastSeenAbsorbTick = lastAbsorbTick;
            if (newHit && state == ShieldAnimState.Hit && !shield.InBreak)
            {
                // Hit while already reacting to a hit: restart from retriggerStartTick, not frame 0.
                PlayState(ShieldAnimState.Hit, nowSeconds, retrigger: true);
            }
            else
            {
                ShieldAnimState target = SelectState(shield, lastAbsorbTick);
                if (target != state)
                {
                    PlayState(target, nowSeconds, retrigger: false);
                }
            }

            ShieldAnimStateParams stateDef = StateDef(state);
            float duration = DurationSeconds(stateDef);
            float elapsed = nowSeconds - stateStartTime;
            float stateTime = stateDef.clip.Loop ? Mathf.Repeat(elapsed, duration) / duration : Mathf.Clamp01(elapsed / duration);
            BreakActive = state == ShieldAnimState.Break && !stateDef.clip.Loop && elapsed < duration;
            Dissolve = stateDef.dissolve?.Evaluate(stateTime) ?? 0f;

            float targetScale = stateDef.scale?.Evaluate(stateTime) ?? 1f;
            float blendT = Mathf.Clamp01((nowSeconds - scaleBlendStartTime) / scaleBlendSeconds);
            Scale = Mathf.Lerp(scaleFrom, targetScale, blendT);
        }

        private void PlayState(ShieldAnimState target, float nowSeconds, bool retrigger = false)
        {
            ShieldAnimStateParams targetDef = StateDef(target);
            float startTick = retrigger ? Mathf.Clamp(targetDef.retriggerStartTick, 0f, targetDef.clip.DurationTicks) : 0f;
            float speed = Mathf.Max(0.0001f, targetDef.speed);
            // Scale crossfades from wherever the previous state left it (e.g. a hit pulse
            // interrupted mid-swell by another hit), same window as the pose crossfade.
            scaleFrom = Scale;
            scaleBlendStartTime = nowSeconds;
            scaleBlendSeconds = Mathf.Max(0.0001f, targetDef.transitionTicks / 60f);
            player.Play(targetDef.clip, targetDef.transitionTicks, nowSeconds, targetDef.speed, startTick);
            state = target;
            // Back-date the state start so scale/dissolve curves line up with the clip offset.
            stateStartOffsetSeconds = startTick / 60f / speed;
            stateStartTime = nowSeconds - stateStartOffsetSeconds;
        }

        /// <summary>Real playback length of a state's clip, with its speed multiplier applied.</summary>
        private static float DurationSeconds(ShieldAnimStateParams stateDef)
        {
            return Mathf.Max(1 / 60f, stateDef.clip.DurationTicks / 60f / Mathf.Max(0.0001f, stateDef.speed));
        }

        private ShieldAnimState SelectState(CompAuraShield shield, int lastAbsorbTick)
        {
            if (shield.InBreak)
            {
                return ShieldAnimState.Break;
            }
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            float hitOffset = state == ShieldAnimState.Hit ? stateStartOffsetSeconds : 0f;
            if (lastAbsorbTick >= 0 && (currentTick - lastAbsorbTick) / 60f + hitOffset < DurationSeconds(def.hit))
            {
                return ShieldAnimState.Hit;
            }
            return ShieldAnimState.Idle;
        }

        private ShieldAnimStateParams StateDef(ShieldAnimState target)
        {
            return target switch
            {
                ShieldAnimState.Hit => def.hit,
                ShieldAnimState.Break => def.breakState,
                _ => def.idle,
            };
        }
    }
}
