using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Skeletal2D
{
    public struct Skeletal2DBonePose
    {
        public float rotationDeg;
        public Vector2 scale;

        public static readonly Skeletal2DBonePose Rest = new() { rotationDeg = 0f, scale = Vector2.one };

        public static Skeletal2DBonePose Lerp(Skeletal2DBonePose a, Skeletal2DBonePose b, float t)
        {
            return new Skeletal2DBonePose
            {
                rotationDeg = Mathf.Lerp(a.rotationDeg, b.rotationDeg, t),
                scale = Vector2.Lerp(a.scale, b.scale, t),
            };
        }
    }

    /// <summary>
    /// 2D affine transform mapping a bone's rest-space position to its posed position:
    /// posed = origin + colX * p.x + colY * p.y (evaluated in the vertex shader).
    /// </summary>
    public struct Skeletal2DBoneTransform
    {
        public Vector2 origin;
        public Vector2 colX;
        public Vector2 colY;
    }

    /// <summary>
    /// Plays Skeletal2DClipDefs on a rig: samples Bezier tracks at fractional ticks,
    /// crossfades poses on clip switches (including same-clip retriggers), then runs
    /// 2D FK into per-bone rest-to-posed transforms for skinning. Time is real seconds;
    /// clip tracks are keyed in ticks (60 = 1 second).
    /// FK matches Blender's default Inherit Scale = Full: a bone's posed frame is
    /// parentFrame * restRelative * R(rotation) * diag(scaleAlong, scaleAcross), so a
    /// parent's scale (including non-uniform scale, which shears children) propagates
    /// down the whole chain.
    /// </summary>
    public sealed class Skeletal2DPlayer
    {
        private readonly Skeletal2DRigDef rig;
        private readonly int boneCount;
        private readonly int[] parentIndex;
        private readonly int[] evalOrder;
        private readonly Dictionary<string, int> boneIndexByName;
        private readonly Vector2[] restHead;
        private readonly float[] restAngleDeg;
        private readonly float[] restCos;
        private readonly float[] restSin;
        private readonly float[] restLocalAngleDeg;
        /// <summary>Child head in the parent's rest frame (origin = parent head, x = along parent).</summary>
        private readonly Vector2[] restLocalOffset;

        private readonly Skeletal2DBonePose[] pose;
        private readonly Skeletal2DBoneTransform[] transforms;
        // Posed bone frames in rig space: origin = head, colX = along axis, colY = across axis.
        // Columns carry accumulated scale and shear, so children inherit them exactly.
        private readonly Vector2[] frameOrigin;
        private readonly Vector2[] frameColX;
        private readonly Vector2[] frameColY;
        private Skeletal2DBonePose[] blendFromPose;
        private Skeletal2DBoneTrack[] tracksByBone;
        private int resolvedDataVersion = -1;
        private Skeletal2DClipDef clip;
        private float clipStartSeconds;
        private float playbackSpeed = 1f;
        private float blendStartSeconds;
        private float blendDurationSeconds = 0.0001f;

        public Skeletal2DPlayer(Skeletal2DRigDef rig)
        {
            this.rig = rig;
            boneCount = rig.bones.Count;
            parentIndex = new int[boneCount];
            boneIndexByName = new Dictionary<string, int>(boneCount);
            restHead = new Vector2[boneCount];
            restAngleDeg = new float[boneCount];
            restCos = new float[boneCount];
            restSin = new float[boneCount];
            restLocalAngleDeg = new float[boneCount];
            restLocalOffset = new Vector2[boneCount];
            pose = new Skeletal2DBonePose[boneCount];
            transforms = new Skeletal2DBoneTransform[boneCount];
            frameOrigin = new Vector2[boneCount];
            frameColX = new Vector2[boneCount];
            frameColY = new Vector2[boneCount];

            for (int i = 0; i < boneCount; i++)
            {
                boneIndexByName[rig.bones[i].name] = i;
            }
            for (int i = 0; i < boneCount; i++)
            {
                Skeletal2DBone bone = rig.bones[i];
                int p = -1;
                if (!bone.parent.NullOrEmpty() && !boneIndexByName.TryGetValue(bone.parent, out p))
                {
                    Log.ErrorOnce(
                        $"[MiliraXian] Skeletal2D rig '{rig.defName}' bone '{bone.name}' has unknown parent '{bone.parent}'.",
                        GenText.StableStringHash(rig.defName + bone.name));
                    p = -1;
                }
                parentIndex[i] = p;
                Vector2 d = bone.restTail - bone.restHead;
                restHead[i] = bone.restHead;
                restAngleDeg[i] = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                restCos[i] = Mathf.Cos(restAngleDeg[i] * Mathf.Deg2Rad);
                restSin[i] = Mathf.Sin(restAngleDeg[i] * Mathf.Deg2Rad);
            }
            for (int i = 0; i < boneCount; i++)
            {
                int p = parentIndex[i];
                if (p < 0)
                {
                    restLocalAngleDeg[i] = restAngleDeg[i];
                    restLocalOffset[i] = Vector2.zero;
                }
                else
                {
                    // Head-to-head offset in the parent's rest frame, like Blender's
                    // parent.matrix_local^-1 @ child.matrix_local: the parent's length is part
                    // of this offset, so scaling the parent along its axis carries the child
                    // head out to the scaled tail.
                    restLocalAngleDeg[i] = restAngleDeg[i] - restAngleDeg[p];
                    restLocalOffset[i] = Rotate(rig.bones[i].restHead - rig.bones[p].restHead, -restAngleDeg[p]);
                }
            }
            evalOrder = TopologicalOrder();
        }

        /// <param name="speed">Playback rate multiplier; 0.5 plays the clip at half speed.</param>
        /// <param name="startTick">Clip tick to start at (the crossfade still blends from the current pose).</param>
        public void Play(Skeletal2DClipDef newClip, int transitionTicks, float nowSeconds, float speed = 1f, float startTick = 0f)
        {
            if (clip != null && transitionTicks > 0)
            {
                // Capture the outgoing pose at the outgoing clip's speed before switching.
                EvaluatePose(nowSeconds);
                blendFromPose = (Skeletal2DBonePose[])pose.Clone();
            }
            else
            {
                blendFromPose = null;
            }
            playbackSpeed = Mathf.Max(0.0001f, speed);
            clip = newClip;
            // Back-date the clip start so sampling begins at startTick.
            clipStartSeconds = nowSeconds - Mathf.Max(0f, startTick) / 60f / playbackSpeed;
            blendStartSeconds = nowSeconds;
            blendDurationSeconds = Mathf.Max(0.0001f, transitionTicks / 60f);
            ResolveTracks();
        }

        /// <summary>
        /// Evaluates the current pose (with crossfade applied) and FKs it into per-bone
        /// rest-to-posed transforms, bone order matching the rig. The returned array is
        /// reused every call; consume it before the next call.
        /// </summary>
        public Skeletal2DBoneTransform[] EvaluateWorldTransforms(float nowSeconds)
        {
            EvaluatePose(nowSeconds);
            for (int n = 0; n < boneCount; n++)
            {
                int i = evalOrder[n];
                int p = parentIndex[i];

                // Local part: R(restLocal + rotation) * diag(scaleAlong, scaleAcross).
                float rad = (restLocalAngleDeg[i] + pose[i].rotationDeg) * Mathf.Deg2Rad;
                float c = Mathf.Cos(rad);
                float s = Mathf.Sin(rad);
                Vector2 localX = new Vector2(c, s) * pose[i].scale.x;
                Vector2 localY = new Vector2(-s, c) * pose[i].scale.y;

                Vector2 origin;
                Vector2 colX;
                Vector2 colY;
                if (p < 0)
                {
                    origin = restHead[i];
                    colX = localX;
                    colY = localY;
                }
                else
                {
                    // Full inheritance: everything local is expressed through the parent's
                    // posed frame, whose columns already carry its accumulated scale/shear.
                    Vector2 px = frameColX[p];
                    Vector2 py = frameColY[p];
                    origin = frameOrigin[p] + px * restLocalOffset[i].x + py * restLocalOffset[i].y;
                    colX = px * localX.x + py * localX.y;
                    colY = px * localY.x + py * localY.y;
                }
                frameOrigin[i] = origin;
                frameColX[i] = colX;
                frameColY[i] = colY;

                // Rest-to-posed skinning transform: posedFrame * restFrame^-1, where the rest
                // frame is the unit rotation R(restAngle) placed at restHead.
                float c0 = restCos[i];
                float s0 = restSin[i];
                Vector2 skinX = colX * c0 - colY * s0;
                Vector2 skinY = colX * s0 + colY * c0;
                transforms[i] = new Skeletal2DBoneTransform
                {
                    colX = skinX,
                    colY = skinY,
                    origin = origin - skinX * restHead[i].x - skinY * restHead[i].y,
                };
            }
            return transforms;
        }

        private void EvaluatePose(float nowSeconds)
        {
            float tick = 0f;
            if (clip != null)
            {
                // Clip data is hot-reloadable; re-resolve bone tracks when it changes.
                if (clip.DataVersion != resolvedDataVersion)
                {
                    ResolveTracks();
                }
                float duration = Mathf.Max(1, clip.DurationTicks);
                float t = Mathf.Max(0f, nowSeconds - clipStartSeconds) * 60f * playbackSpeed;
                tick = clip.Loop ? Mathf.Repeat(t, duration) : Mathf.Min(t, duration);
            }
            for (int i = 0; i < boneCount; i++)
            {
                Skeletal2DBoneTrack track = tracksByBone?[i];
                pose[i] = track == null
                    ? Skeletal2DBonePose.Rest
                    : new Skeletal2DBonePose
                    {
                        rotationDeg = Skeletal2DClipDef.Sample(track.rotationKeys, tick, 0f),
                        scale = new Vector2(
                            Skeletal2DClipDef.Sample(track.scaleAlongKeys, tick, 1f),
                            Skeletal2DClipDef.Sample(track.scaleAcrossKeys, tick, 1f)),
                    };
            }
            if (blendFromPose != null)
            {
                float blendT = Mathf.Clamp01((nowSeconds - blendStartSeconds) / blendDurationSeconds);
                for (int i = 0; i < boneCount; i++)
                {
                    pose[i] = Skeletal2DBonePose.Lerp(blendFromPose[i], pose[i], blendT);
                }
                if (blendT >= 1f)
                {
                    blendFromPose = null;
                }
            }
        }

        private void ResolveTracks()
        {
            tracksByBone = new Skeletal2DBoneTrack[boneCount];
            if (clip == null)
            {
                resolvedDataVersion = -1;
                return;
            }
            List<Skeletal2DBoneTrack> clipTracks = clip.Tracks;
            resolvedDataVersion = clip.DataVersion;
            for (int i = 0; i < clipTracks.Count; i++)
            {
                Skeletal2DBoneTrack track = clipTracks[i];
                if (track == null)
                {
                    continue;
                }
                if (!boneIndexByName.TryGetValue(track.bone ?? "", out int index))
                {
                    Log.ErrorOnce(
                        $"[MiliraXian] Skeletal2D clip '{clip.defName}' has a track for unknown bone '{track.bone}'.",
                        GenText.StableStringHash(clip.defName + track.bone));
                    continue;
                }
                tracksByBone[index] = track;
            }
        }

        private int[] TopologicalOrder()
        {
            var order = new int[boneCount];
            var placed = new bool[boneCount];
            int placedCount = 0;
            for (int pass = 0; pass < boneCount && placedCount < boneCount; pass++)
            {
                for (int i = 0; i < boneCount; i++)
                {
                    if (placed[i])
                    {
                        continue;
                    }
                    int p = parentIndex[i];
                    if (p < 0 || placed[p])
                    {
                        placed[i] = true;
                        order[placedCount++] = i;
                    }
                }
            }
            if (placedCount < boneCount)
            {
                Log.Error($"[MiliraXian] Skeletal2D rig '{rig.defName}' has a parent cycle; using file order.");
                for (int i = 0; i < boneCount; i++)
                {
                    order[i] = i;
                }
            }
            return order;
        }

        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
