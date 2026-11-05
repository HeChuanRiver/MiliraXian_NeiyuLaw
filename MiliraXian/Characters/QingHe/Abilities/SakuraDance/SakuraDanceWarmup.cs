using HarmonyLib;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>
    /// The aim of Sakura Dance (the vanilla ability warmup, <see cref="Stance_Warmup"/>): the pawn spins
    /// into the crouch and lays the blade behind its head (Sekiro 316700). The warmup motion is
    /// stretched over the actual warmup ticks and ends in the pose the dance starts from. Purely
    /// visual: the stance and the cast are vanilla.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class SakuraDanceWarmup
    {
        static SakuraDanceWarmup()
        {
            BladePoseProviders.Register(TryGetBladePose);
        }

        /// <summary>The Sakura Dance warmup of <paramref name="pawn"/>: its props, aim and 0..1 progress.</summary>
        private static bool TryGet(Pawn pawn, out CompProperties_AbilitySakuraDance props, out Vector3 aim, out float seconds)
        {
            props = null;
            aim = Vector3.right;
            seconds = 0f;
            if (pawn?.stances?.curStance is not Stance_Warmup warmup
                || warmup.verb is not Verb_CastAbility cast
                || cast.Ability?.CompOfType<CompAbilityEffect_SakuraDance>()?.Props is not { warmup: not null } p)
            {
                return false;
            }
            props = p;
            Vector3 dir = warmup.focusTarg.CenterVector3 - pawn.Position.ToVector3Shifted();
            dir.y = 0f;
            aim = dir.sqrMagnitude > 0.001f ? dir.normalized : pawn.Rotation.FacingCell.ToVector3();
            // Whole warmup = ticks already spent + ticks left; progress on the visual clock, so the
            // pose moves smoothly between ticks.
            float total = Mathf.Max(1f, Find.TickManager.TicksGame - warmup.startedTick + warmup.ticksLeft);
            float done = Mathf.Clamp(VisualClock.Ticks - warmup.startedTick, 0f, total);
            seconds = done / total * p.warmup.durationSeconds;
            return true;
        }

        private static bool TryGetBladePose(Pawn pawn, out BladePose pose)
        {
            pose = default;
            if (!TryGet(pawn, out CompProperties_AbilitySakuraDance props, out Vector3 aim, out float seconds) || props.warmup.blade == null)
            {
                return false;
            }
            SakuraRingFrame.BodyBasis(aim, out Vector3 right, out Vector3 up, out Vector3 forward);
            float spin = MonotoneCurve.Evaluate(props.warmup.spin, seconds) * Mathf.Deg2Rad;
            Vector3 f = forward * Mathf.Cos(spin) + right * Mathf.Sin(spin);
            Vector3 r = right * Mathf.Cos(spin) - forward * Mathf.Sin(spin);
            props.warmup.blade.Evaluate(seconds, out Vector3 grip, out Vector3 dir, out Vector3 edge);
            pose.grip = SakuraRingFrame.BodyToWorld(grip, r, up, f);
            pose.dir = SakuraRingFrame.BodyToWorld(dir, r, up, f);
            pose.edge = SakuraRingFrame.BodyToWorld(edge, r, up, f);
            pose.heightToScreen = props.heightToScreen;
            return true;
        }

        /// <summary>Body facing during the warmup spin.</summary>
        internal static void ModifyDraw(Pawn pawn, ref Rot4? rotOverride)
        {
            if (!TryGet(pawn, out CompProperties_AbilitySakuraDance props, out Vector3 aim, out float seconds) || props.warmup.spin == null)
            {
                return;
            }
            float spin = MonotoneCurve.Evaluate(props.warmup.spin, seconds);
            float facing = aim.AngleFlat() + (aim.x < 0f ? -spin : spin);
            rotOverride = Rot4.FromAngleFlat(facing);
        }
    }

    /// <summary>Turns the body of a pawn aiming Sakura Dance.</summary>
    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    internal static class Patch_PawnRenderer_SakuraDanceWarmup
    {
        private static void Prefix(Pawn ___pawn, ref Rot4? rotOverride)
        {
            if (___pawn?.stances?.curStance is Stance_Warmup)
            {
                SakuraDanceWarmup.ModifyDraw(___pawn, ref rotOverride);
            }
        }
    }
}
