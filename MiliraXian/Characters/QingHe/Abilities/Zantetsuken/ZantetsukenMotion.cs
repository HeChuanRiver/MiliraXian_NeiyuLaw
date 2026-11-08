using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.Zantetsuken
{
    /// <summary>
    /// Clip second 0 is the arrival at the dash end; keys are in
    /// the body frame for an east aim, which the player turns and mirrors for other aims.
    /// </summary>
    internal static class ZantetsukenMotion
    {
        private static readonly Vector3 Shoulder = new(0.12f, 0.25f, 0f);
        private const float GripToTip = 0.976f;
        private const float ScabbardLength = 1.03f;
        private const float IaiUp = 0.15f;
        private const float IaiForward = 0.25f;
        private const float IaiHilt0 = 0.95f, IaiMouth0 = -0.15f, IaiEnd = 0.5f;

        // MX_QH_Weapon_Sword's BladeGraphicExtension waist scabbard.
        private static readonly Vector3 RestMouth = new(-0.240f, 0.020f, 0.100f);
        private static readonly Vector3 RestEnd = new(-0.318f, -0.718f, -0.133f);

        /// <summary>
        /// The Gaeshi ends facing south: counter-clockwise from the east half, mirrored from the west;
        /// a south aim turns all the way round.
        /// </summary>
        public static float TurnTotal(Vector3 aim)
        {
            float a = aim.AngleFlat();
            float amount = aim.x < 0f ? Mathf.Repeat(180f - a, 360f) : Mathf.Repeat(a - 180f, 360f);
            if (amount < 1f)
            {
                amount = 360f;
            }
            return -amount;
        }

        public static BladeMotionClip Build(CompProperties_AbilityZantetsuken p, bool gaeshi, float apex, float turnTotal)
        {
            float end = p.EndSeconds(gaeshi);
            BladeMotionClip clip = new()
            {
                durationSeconds = end,
                turnCarriesBlade = true,
                blade = new BladeKeyframes { shoulder = Shoulder, armLength = 0.55f, armPitchShare = 0.5f },
                height = Sample(end, t => Height(p, gaeshi, apex, t)),
                sheathed = Sample(end, t => Sheathed(p, gaeshi, t)),
                turn = new SimpleCurve(),
                flashTime = gaeshi ? p.ClickTime : p.PlainSheatheEnd,
            };
            if (gaeshi)
            {
                clip.trailStart = p.HoldEnd;
                clip.trailEnd = p.HoldEnd + p.scoopSwingSeconds * 0.95f;
            }

            float turnW = p.HoldEnd + p.scoopSwingSeconds;
            float turnS = turnW + p.turnBackSeconds;
            clip.turn.Add(0f, 0f);
            if (gaeshi)
            {
                clip.turn.Add(turnW, 0f);
                clip.turn.Add(turnS, -Mathf.Min(180f, Mathf.Abs(turnTotal)));
                clip.turn.Add(turnS + p.turnSouthSeconds, turnTotal);
            }
            clip.turn.Add(end, gaeshi ? turnTotal : 0f);

            List<BladeKey> k = clip.blade.keys;
            Key(k, 0f, 180f, -30f, 90f, new Vector3(0.15f, -0.1f, -0.1f));
            Key(k, p.takeoffSeconds, 180f, -30f, 90f, new Vector3(0.15f, -0.1f, -0.1f));
            Key(k, Mathf.Lerp(p.takeoffSeconds, p.apexSeconds, 0.5f), 180f, 40f, 90f, new Vector3(0.12f, 0.35f, -0.08f));
            Key(k, p.apexSeconds, 180f, 80f, 90f, new Vector3(0.1f, 0.45f, -0.05f));
            // Through the vertical the yaw flips by 180 with the edge, so the keys straddle it.
            float top = Mathf.Lerp(p.apexSeconds, p.fallSeconds, 0.4f);
            Key(k, top, 180f, 90f, 90f, new Vector3(0.05f, 0.42f, 0.05f));
            Key(k, top + 0.005f, 0f, 89.5f, -90f, new Vector3(0.05f, 0.42f, 0.05f));
            Key(k, p.fallSeconds, 0f, 0f, -90f, new Vector3(0f, 0.15f, 0.35f));
            Key(k, p.landSeconds - 0.02f, 0f, -5f, -90f, new Vector3(0f, 0.15f, 0.35f));
            Key(k, p.landSeconds + 0.05f, 0f, -25f, -90f, new Vector3(0f, -0.15f, 0.38f));
            Key(k, p.HoldEnd, 0f, -25f, -90f, new Vector3(0f, -0.15f, 0.38f));

            Vector3 restDir = (RestEnd - RestMouth).normalized;
            float restYaw = Mathf.Atan2(restDir.x, restDir.z) * Mathf.Rad2Deg;
            float restPitch = Mathf.Asin(restDir.y) * Mathf.Rad2Deg;
            if (!gaeshi)
            {
                float mid = p.HoldEnd + p.plainSheatheSeconds * 0.5f;
                Key(k, mid, restYaw * 0.8f, restPitch * 0.8f, -40f, RestMouth - restDir * 0.45f);
                Key(k, p.PlainSheatheEnd, restYaw, restPitch, 0f, RestMouth);
                Scabbard(clip, 0f, RestMouth, RestEnd);
                Scabbard(clip, end, RestMouth, RestEnd);
                return clip;
            }

            Key(k, p.HoldEnd + 0.04f, 0f, -20f, 90f, new Vector3(0f, -0.1f, 0.4f));
            float up = p.HoldEnd + p.scoopSwingSeconds * 0.5f;
            Key(k, up, 0f, 89.5f, 90f, new Vector3(0.05f, 0.4f, 0.15f));
            Key(k, up + 0.005f, 180f, 89.5f, -90f, new Vector3(0.05f, 0.4f, 0.15f));
            Key(k, turnW, 180f, -20f, -90f, new Vector3(0.1f, 0.05f, -0.3f));
            Key(k, turnS, 0f, -20f, 90f, new Vector3(0.1f, 0.05f, 0.3f));
            Key(k, p.ScoopEnd, -35f, -20f, 90f, new Vector3(0.1f, 0.05f, 0.3f));
            float align = p.ScoopEnd + p.sheatheSeconds * p.sheatheAlignShare;
            Key(k, align, -90f, 0f, 90f, new Vector3(IaiHilt0, IaiUp, IaiForward));
            Key(k, p.ClickTime, -90f, 0f, 90f, new Vector3(IaiEnd, IaiUp, IaiForward));
            Key(k, end, -90f, 0f, 90f, new Vector3(IaiEnd, IaiUp, IaiForward));

            Scabbard(clip, 0f, RestMouth, RestEnd);
            Scabbard(clip, p.ScoopEnd, RestMouth, RestEnd);
            Vector3 m0 = new(IaiMouth0, IaiUp, IaiForward);
            Vector3 m1 = new(IaiEnd, IaiUp, IaiForward);
            Vector3 along = new(-ScabbardLength, 0f, 0f);
            Scabbard(clip, align, m0, m0 + along);
            // Scabbard keys are linear: the eased slide is sampled so it stays on the blade.
            for (int i = 1; i <= 8; i++)
            {
                float u = i / 8f;
                Vector3 m = Vector3.Lerp(m0, m1, Smooth(u));
                Scabbard(clip, Mathf.Lerp(align, p.ClickTime, u), m, m + along);
            }
            Scabbard(clip, end, m1, m1 + along);
            return clip;
        }

        private static void Key(List<BladeKey> keys, float time, float yaw, float pitch, float roll, Vector3 grip)
        {
            keys.Add(new BladeKey { time = time, yaw = yaw, pitch = pitch, roll = roll, reach = 0f, offset = grip - Shoulder });
        }

        private static void Scabbard(BladeMotionClip clip, float time, Vector3 mouth, Vector3 end)
        {
            clip.scabbardKeys.Add(new BladeScabbardKey { time = time, mouth = mouth, end = end });
        }

        private static SimpleCurve Sample(float end, System.Func<float, float> f)
        {
            SimpleCurve c = new();
            const float step = 1f / 60f;
            for (float t = 0f; t < end; t += step)
            {
                c.Add(t, f(t), sort: false);
            }
            c.Add(end, f(end), sort: false);
            return c;
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float EaseOut(float u) => 1f - (1f - u) * (1f - u);

        private static float Height(CompProperties_AbilityZantetsuken p, bool gaeshi, float apex, float t)
        {
            if (t < p.takeoffSeconds)
            {
                return -0.12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / p.takeoffSeconds));
            }
            if (t < p.apexSeconds)
            {
                return apex * EaseOut((t - p.takeoffSeconds) / (p.apexSeconds - p.takeoffSeconds));
            }
            if (t < p.fallSeconds)
            {
                return apex;
            }
            if (t < p.landSeconds)
            {
                float u = (t - p.fallSeconds) / (p.landSeconds - p.fallSeconds);
                return apex * (1f - u * u);
            }
            const float crouch = -0.18f;
            if (t < p.HoldEnd)
            {
                return crouch * Mathf.Clamp01((t - p.landSeconds) / 0.06f);
            }
            if (!gaeshi)
            {
                return crouch * (1f - Smooth((t - p.HoldEnd) / p.plainSheatheSeconds));
            }
            if (t < p.ScoopEnd)
            {
                return Mathf.Lerp(crouch, p.gaeshiHeight, EaseOut((t - p.HoldEnd) / p.scoopRiseSeconds));
            }
            if (t < p.ClickTime)
            {
                return p.gaeshiHeight + 0.04f * Mathf.Sin(Mathf.PI * (t - p.ScoopEnd) / p.sheatheSeconds);
            }
            if (t < p.DropStart)
            {
                return p.gaeshiHeight;
            }
            float d = Mathf.Clamp01((t - p.DropStart) / p.dropSeconds);
            return p.gaeshiHeight * (1f - d * d);
        }

        private static float Sheathed(CompProperties_AbilityZantetsuken p, bool gaeshi, float t)
        {
            if (!gaeshi)
            {
                return Smooth((t - (p.HoldEnd + p.plainSheatheSeconds * 0.5f)) / (p.plainSheatheSeconds * 0.5f));
            }
            float align = p.ScoopEnd + p.sheatheSeconds * p.sheatheAlignShare;
            if (t <= align)
            {
                return 0f;
            }
            float s = Smooth((t - align) / (p.ClickTime - align));
            float hilt = Mathf.Lerp(IaiHilt0, IaiEnd, s);
            float mouth = Mathf.Lerp(IaiMouth0, IaiEnd, s);
            // Level across the body, the tip lies at hilt - GripToTip on the right axis.
            return Mathf.Clamp01((mouth - (hilt - GripToTip)) / GripToTip);
        }
    }
}
