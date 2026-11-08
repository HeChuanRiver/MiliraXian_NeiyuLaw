using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>
    /// The aim of Sakura Dance (the vanilla ability warmup, <see cref="Stance_Warmup"/>): the pawn spins
    /// into the crouch and lays the blade behind its head. The warmup clip is
    /// stretched over the actual warmup ticks and ends in the pose the dance starts from. Purely
    /// visual: the stance and the cast are vanilla. Played by <see cref="BladeMotionPlayer"/>.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class SakuraDanceWarmup
    {
        static SakuraDanceWarmup()
        {
            MotionSources.Register(TryGetPlayback);
        }

        private static bool TryGetPlayback(Pawn pawn, out MotionPlayback playback)
        {
            playback = default;
            if (pawn.stances?.curStance is not Stance_Warmup warmup
                || warmup.verb is not Verb_CastAbility cast
                || cast.Ability?.CompOfType<CompAbilityEffect_SakuraDance>()?.Props is not { warmup: not null } props)
            {
                return false;
            }
            Vector3 dir = warmup.focusTarg.CenterVector3 - pawn.Position.ToVector3Shifted();
            dir.y = 0f;
            // Whole warmup = ticks already spent + ticks left; progress on the visual clock, so the
            // pose moves smoothly between ticks.
            float total = Mathf.Max(1f, Find.TickManager.TicksGame - warmup.startedTick + warmup.ticksLeft);
            float done = Mathf.Clamp(VisualClock.Ticks - warmup.startedTick, 0f, total);
            playback = new MotionPlayback
            {
                clip = props.warmup,
                seconds = done / total * props.warmup.durationSeconds,
                aim = dir.sqrMagnitude > 0.001f ? dir.normalized : pawn.Rotation.FacingCell.ToVector3(),
                projection = props.Projection,
                source = props.warmup,
                sourceId = warmup.startedTick,
            };
            return true;
        }
    }
}
