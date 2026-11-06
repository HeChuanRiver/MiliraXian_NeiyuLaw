using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.BladeMotion
{
    // The body side of every motion, whatever plays it (job, ability warmup, weapon attack):
    // one patch per vanilla hook, each reading the one player.

    /// <summary>
    /// Turns the drawn body by the playing motion's turn track. A tilted body is not drawn from the
    /// texture atlas cache, whose cached path ignores the body angle of standing pawns.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    internal static class Patch_PawnRenderer_MotionFacing
    {
        private static void Prefix(Pawn ___pawn, ref Rot4? rotOverride, ref bool disableCache)
        {
            if (!BladeMotionPlayer.TryGetBody(___pawn, out MotionBody body))
            {
                return;
            }
            if (rotOverride == null && body.hasFacing)
            {
                rotOverride = body.facing;
            }
            if (body.tilt != 0f)
            {
                disableCache = true;
            }
        }
    }

    /// <summary>Leans the drawn body by the playing motion's tilt track (vanilla draws standing pawns at angle 0).</summary>
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    internal static class Patch_PawnRenderer_MotionTilt
    {
        private static void Prefix(Pawn ___pawn, ref float angle, PawnRenderFlags flags)
        {
            if (flags.FlagSet(PawnRenderFlags.Portrait) || flags.FlagSet(PawnRenderFlags.Cache)
                || ___pawn.GetPosture() != PawnPosture.Standing
                || !BladeMotionPlayer.TryGetBody(___pawn, out MotionBody body))
            {
                return;
            }
            angle += body.tilt;
        }
    }

    /// <summary>
    /// Drops the lean of the flight animation while a motion plays. Milira's flight animation
    /// (Milira Race, Defs/AnimationDefs/Milira_Fly.xml) turns the Root node 8 degrees (east; -8 west)
    /// and the Head back the other way; it is picked by pawn.Rotation, not by the facing a motion
    /// draws. During a motion that angle stacked on the motion's tilt, and with a turn track it leaned
    /// a sprite drawn facing north or south. Only the rotation is removed: the wing frames and the
    /// small up-down bob stay.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNode), nameof(PawnRenderNode.TryGetAnimationRotation))]
    internal static class Patch_PawnRenderNode_MotionFlightLean
    {
        private const string MiliraFlyPrefix = "Milira_Fly";

        private static void Postfix(PawnDrawParms parms, ref float offset)
        {
            if (offset == 0f)
            {
                return;
            }
            Pawn pawn = parms.pawn;
            AnimationDef anim = pawn?.Drawer?.renderer?.CurAnimation;
            if (anim == null || !IsFlightAnimation(pawn, anim) || !MotionSources.TryGet(pawn, out _))
            {
                return;
            }
            offset = 0f;
        }

        private static bool IsFlightAnimation(Pawn pawn, AnimationDef anim)
        {
            return anim.defName.StartsWith(MiliraFlyPrefix)
                || (pawn.Flying && anim == Pawn_FlightTracker.GetBestFlyAnimation(pawn));
        }
    }

    /// <summary>
    /// Moves the drawn body by the playing motion's height and ground tracks, in <c>DrawPos</c> like
    /// the vanilla job offset: everything drawn at the pawn (shield, halo, weapon, overlays) follows
    /// the body. Position, selection and the shadow stay on the ground.
    /// On the DrawPos getter, not the small private OffsetForcedByJob, which the JIT may inline.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    internal static class Patch_PawnDrawTracker_MotionLift
    {
        private static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (BladeMotionPlayer.TryGetBody(___pawn, out MotionBody body))
            {
                __result.x += body.shift.x + body.travel.x;
                __result.z += body.shift.z + body.lift + body.travel.z;
            }
        }
    }

    /// <summary>
    /// Keeps the shadow of a lifted body on the ground, under the shifted body: the offset is in
    /// <c>DrawPos</c>, which both shadow paths start from. The flying branch is redrawn here because
    /// it reads <c>pawn.DrawPos</c> itself instead of its argument.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "DrawShadowInternal")]
    internal static class Patch_PawnRenderer_MotionShadow
    {
        private static bool Prefix(PawnRenderer __instance, Pawn ___pawn, ref Vector3 drawLoc)
        {
            if (!BladeMotionPlayer.TryGetBody(___pawn, out MotionBody body) || body.lift == 0f)
            {
                return true;
            }
            if (___pawn.Flying)
            {
                Vector3 pos = ___pawn.DrawPos;
                pos.z -= body.lift;
                pos.y = AltitudeLayer.Filth.AltitudeFor();
                pos -= new Vector3(0f, 0f, ___pawn.flight.PositionOffsetFactor);
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.75f), __instance.FlightShadowMaterial, 0);
                return false;
            }
            drawLoc.z -= body.lift;
            return true;
        }
    }
}
