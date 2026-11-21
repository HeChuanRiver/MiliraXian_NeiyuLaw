using UnityEngine;
using Verse;
using MiliraXian.Characters.Common.Vfx.Emitter;

namespace MiliraXian.Characters.QingHe.Things
{
    /// <summary>
    /// The rain wall's particles, as an emitter's code path.
    ///
    /// It reproduces the old hand-written loops exactly: the same per-wall LCG, the same placement,
    /// speed, phase and length, the same fade envelope and the same lean. What changed is ownership:
    /// the strands are particles of group 0 and their landings are particles of group 1, both produced
    /// by one pass over the same random stream. That is what makes "the splash sits exactly under its
    /// strand and is born when it lands" a local fact of this loop instead of a convention spread
    /// across two draw methods.
    ///
    /// The wall's own geometry (axis, band, seconds, strength) is published as vec4 channels, which
    /// the strokes bind as uniforms; nothing is handed across from the caller.
    /// </summary>
    internal sealed class RainWallEmitter : VfxEmitter
    {
        /// <summary>Strand cross-section: white, alpha = exp(-0.5·(2v−1)²) across, solid along.</summary>
        internal const float Width = 0.018f;

        /// <summary>Height of the sheet the drops fall through, in cells.</summary>
        internal const float SheetHeight = 2.20f;

        /// <summary>Half-extent of one splash quad, in cells; the shader reads uv as cell offsets.</summary>
        internal const float SplashQuadHalf = 0.45f;

        /// <summary>The splash sits just above the water so it is not clipped by it.</summary>
        internal const float SplashLift = 0.015f;

        /// <summary>
        /// How far the quad extends past the band so its soft edge is not cut off mid-slope. Matches
        /// the old renderer's padding, which the shaders' falloff was tuned against.
        /// </summary>
        internal const float QuadPadding = 0.4f;

        private const float FallSpeedLo = 1.70f;
        private const float FallSpeedHi = 2.60f;
        private const float StrandLenLo = 0.85f;
        private const float StrandLenHi = 1.45f;
        private const float StrandTilt = 0.10f;
        private const float StrandFadeIn = 0.18f;
        private const float StrandFadeOut = 0.22f;
        private const float StrandTailDim = 0.75f;
        private const float StrandDensity = 300f / 27f;
        private const float StrandGain = 0.48f * 0.48f;

        private static readonly Color StrandWhite = new(0.88f, 0.96f, 1f);

        /// <summary>How many strands (and therefore landings) one wall carries.</summary>
        internal static int CountFor(CompProperties_LotusRainWall props)
        {
            return Mathf.Max(1, Mathf.CeilToInt(StrandDensity * 4f * props.halfLength * props.halfThickness));
        }

        private CompLotusRainWall wall;
        private int strands;
        private int splashes;

        internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
        {
            base.Reset(effect, anchor, version, nowTick, position);

            wall = anchor.Kind == VfxAnchorKind.ThingComp ? anchor.Comp as CompLotusRainWall : null;
            strands = wall == null ? 0 : CountFor(wall.Props);
            strands = Mathf.Min(strands, Particles.Length / 2);
            splashes = strands;

            // Group 0: one particle per falling strand. Group 1: one per landing.
            for (int i = 0; i < strands; i++)
            {
                Emit(position, 0f, 0f, StrandWhite, 0);
            }
            for (int i = 0; i < splashes; i++)
            {
                Emit(position, 0f, 0f, Color.clear, 1);
            }
        }

        internal override bool Advance(float deltaSeconds, int nowTick, Map map)
        {
            if (!base.Advance(deltaSeconds, nowTick, map))
            {
                return false;
            }

            CompLotusRainWall owner = wall;
            if (owner == null || owner.parent == null || owner.parent.Destroyed || owner.parent.Map != map)
            {
                return false;
            }

            float strength = owner.Strength;
            float seconds = owner.SecondsSinceStart;
            float halfLength = owner.HalfLength;
            float halfThickness = owner.HalfThickness;
            Vector2 along = owner.AxisLength;
            Vector2 across = owner.AxisThickness;
            Vector3 origin = new(owner.parent.Position.x + 0.5f, 0f, owner.parent.Position.z + 0.5f);

            // The strokes read these as uniforms; they are the wall's own geometry, not per particle.
            SetVectorChannel(RainWallChannels.Axis, new Vector4(along.x, along.y, across.x, across.y));
            SetVectorChannel(RainWallChannels.Band, new Vector4(halfLength, halfThickness, seconds, strength));

            // The axis-aligned quad that covers the band, padding included: the band is rotated, so
            // its footprint depends on the wall's direction and cannot be a pass constant.
            float halfX = halfLength * Mathf.Abs(along.x) + halfThickness * Mathf.Abs(across.x) + QuadPadding;
            float halfZ = halfLength * Mathf.Abs(along.y) + halfThickness * Mathf.Abs(across.y) + QuadPadding;
            SetVectorChannel(RainWallChannels.Quad, new Vector4(halfX, halfZ, 0f, 0f));

            if (strength <= 0.01f)
            {
                for (int i = 0; i < strands + splashes; i++)
                {
                    VfxParticle p = Particles[i];
                    p.Color = new Color(StrandWhite.r, StrandWhite.g, StrandWhite.b, 0f);
                    Particles[i] = p;
                }
                return true;
            }

            float strandY = AltitudeLayer.MoteOverhead.AltitudeFor();
            float splashY = AltitudeLayer.MoteLow.AltitudeFor() + SplashLift;

            uint state = (uint)(owner.parent.thingIDNumber * 2654435761u + 97u);
            for (int i = 0; i < strands; i++)
            {
                float alongUnit = NextRand(ref state) * 2f - 1f;
                float acrossUnit = NextRand(ref state) * 2f - 1f;
                float speed = Mathf.Lerp(FallSpeedLo, FallSpeedHi, NextRand(ref state));
                float phase = NextRand(ref state);
                float length = Mathf.Lerp(StrandLenLo, StrandLenHi, NextRand(ref state));

                float u = (seconds * speed / SheetHeight + phase) % 1f;
                float envelope = FallEnvelope(u);
                float head = (1f - u) * SheetHeight;
                float bx = origin.x + along.x * alongUnit * halfLength + across.x * acrossUnit * halfThickness;
                float bz = origin.z + along.y * alongUnit * halfLength + across.y * acrossUnit * halfThickness;
                float slide = StrandTilt * 0.5f * head;

                // The strand: head at the drop, tail trailing up and fading.
                Vector3 a = new(bx - slide, strandY, bz + head);
                Vector3 b = new(bx - slide - StrandTilt * length * 0.5f, strandY, bz + head + length);
                float intensity = envelope * strength * StrandGain;

                VfxParticle strand = Particles[i];
                strand.Position = a;
                strand.Velocity = b - a;
                strand.Size = length;
                strand.Color = new Color(StrandWhite.r, StrandWhite.g, StrandWhite.b, intensity);
                strand.Alive = true;
                Particles[i] = strand;

                // Its landing: same place, same clock. Group 1 carries exactly what the old vertex
                // colour carried — the raw phase and the normalised speed — because the shader
                // recomputes the fall cycle itself: cycle = fract(t·speed/SHEET_H + phase). Passing
                // the current progress instead of the phase would double the period and drift.
                VfxParticle splash = Particles[strands + i];
                splash.Position = new Vector3(bx, splashY, bz);
                splash.Size = 1f;
                splash.Color = new Color(
                    phase,
                    Mathf.InverseLerp(FallSpeedLo, FallSpeedHi, speed),
                    speed >= (FallSpeedLo + FallSpeedHi) * 0.5f ? 1f : 0f,
                    1f);
                splash.Alive = true;
                Particles[strands + i] = splash;
            }
            return true;
        }

        private static float NextRand(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state >> 8) / 16777216f;
        }

        /// <summary>Fades the drop in at the top and out before landing, so the recycle is invisible.</summary>
        private static float FallEnvelope(float u)
        {
            if (u < StrandFadeIn)
            {
                return u / StrandFadeIn;
            }
            float left = 1f - u;
            return left < StrandFadeOut ? left / StrandFadeOut : 1f;
        }

        /// <summary>The streak's tail fade, matching <see cref="StrandTailDim"/>.</summary>
        internal const float TailAlpha = 1f - StrandTailDim;
    }
}
