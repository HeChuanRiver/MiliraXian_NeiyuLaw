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

        private const int LotusCount = 4;
        private const float LotusOpenSeconds = 1.1f;
        private const float BloomAspect = 333f / 544f;
        private const float PadAspect = 231f / 373f;
        private const float ReflectionAlpha = 0.20f;
        private const int WindCount = 5;

        private static readonly Color StrandWhite = new(0.88f, 0.96f, 1f);

        /// <summary>How many strands (and therefore landings) one wall carries.</summary>
        internal static int CountFor(CompProperties_LotusRainWall props)
        {
            return Mathf.Max(1, Mathf.CeilToInt(StrandDensity * 4f * props.halfLength * props.halfThickness));
        }

        private CompLotusRainWall wall;
        private int strands;
        private int splashes;
        private int lotusStart;
        private readonly Vector4[] lotusBuffer = new Vector4[LotusCount];

        private int axisSlot;
        private int bandSlot;
        private int quadSlot;
        private readonly int[] lotusSlots = new int[LotusCount];
        private readonly int[] windSlots = new int[WindCount];

        internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
        {
            base.Reset(effect, anchor, version, nowTick, position);

            axisSlot = effect.RequireVectorSlot("rainWallAxis");
            bandSlot = effect.RequireVectorSlot("rainWallBand");
            quadSlot = effect.RequireVectorSlot("rainWallQuad");
            lotusSlots[0] = effect.RequireVectorSlot("rainWallLotusA");
            lotusSlots[1] = effect.RequireVectorSlot("rainWallLotusB");
            lotusSlots[2] = effect.RequireVectorSlot("rainWallLotusC");
            lotusSlots[3] = effect.RequireVectorSlot("rainWallLotusD");
            windSlots[0] = effect.RequireVectorSlot("rainWallWindA");
            windSlots[1] = effect.RequireVectorSlot("rainWallWindB");
            windSlots[2] = effect.RequireVectorSlot("rainWallWindC");
            windSlots[3] = effect.RequireVectorSlot("rainWallWindD");
            windSlots[4] = effect.RequireVectorSlot("rainWallWindE");

            wall = anchor.Kind == VfxAnchorKind.ThingComp ? anchor.Comp as CompLotusRainWall : null;
            int lotusParticleCount = LotusCount * 2;
            int available = Particles == null ? 0 : Mathf.Max(0, Particles.Length - lotusParticleCount);
            strands = wall == null ? 0 : CountFor(wall.Props);
            strands = Mathf.Min(strands, available / 2);
            splashes = strands;

            // Group 0: falling strands. Group 1: landing splashes.
            for (int i = 0; i < strands; i++)
            {
                Emit(position, 0f, 0f, StrandWhite, 0);
            }
            for (int i = 0; i < splashes; i++)
            {
                Emit(position, 0f, 0f, Color.clear, 1);
            }

            // Groups 2..5: lotus bloom reflection / bloom / pad reflection / pad.
            lotusStart = Count;
            for (int i = 0; i < LotusCount; i++)
            {
                bool isBloom = i % 2 == 0;
                Emit(position, 0f, 0f, Color.clear, isBloom ? 2 : 4);
                Emit(position, 0f, 0f, Color.clear, isBloom ? 3 : 5);
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
            SetVectorChannel(axisSlot, new Vector4(along.x, along.y, across.x, across.y));
            SetVectorChannel(bandSlot, new Vector4(halfLength, halfThickness, seconds, strength));

            // The axis-aligned quad that covers the band, padding included: the band is rotated, so
            // its footprint depends on the wall's direction and cannot be a pass constant.
            float halfX = halfLength * Mathf.Abs(along.x) + halfThickness * Mathf.Abs(across.x) + QuadPadding;
            float halfZ = halfLength * Mathf.Abs(along.y) + halfThickness * Mathf.Abs(across.y) + QuadPadding;
            SetVectorChannel(quadSlot, new Vector4(halfX, halfZ, 0f, 0f));

            UpdateLotusAndWindChannels(owner, halfLength, halfThickness);

            if (strength <= 0.01f)
            {
                for (int i = 0; i < Count; i++)
                {
                    VfxParticle p = Particles[i];
                    p.Color = new Color(p.Color.r, p.Color.g, p.Color.b, 0f);
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

                // Its landing: same place, same clock. Group 1 carries the raw phase and normalised
                // speed because the shader recomputes cycle = fract(t*speed/SHEET_H + phase).
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

            UpdateLotusParticles(origin, along, across, seconds, strength);
            return true;
        }

        private void UpdateLotusAndWindChannels(CompLotusRainWall owner, float halfLength, float halfThickness)
        {
            uint lotusState = (uint)(owner.parent.thingIDNumber * 64013 + 41);
            for (int i = 0; i < LotusCount; i++)
            {
                float baseAlong = Mathf.Lerp(-halfLength + 0.7f, halfLength - 0.7f, i / (float)(LotusCount - 1));
                float jitter = NextRand(ref lotusState) * 2f - 1f;
                float across = NextRand(ref lotusState) * 2f - 1f;
                float radius = Mathf.Lerp(0.34f, 0.52f, NextRand(ref lotusState));
                float start = 0.05f + 0.28f * i + NextRand(ref lotusState) * 0.12f;
                lotusBuffer[i] = new Vector4(baseAlong + jitter * 0.25f, across * 0.7f * halfThickness, radius, start);
            }
            for (int i = 0; i < LotusCount; i++)
            {
                SetVectorChannel(lotusSlots[i], lotusBuffer[i]);
            }

            uint windState = (uint)(owner.parent.thingIDNumber * 777 + 123);
            for (int i = 0; i < WindCount; i++)
            {
                float along = (NextRand(ref windState) * 2f - 1f) * halfLength;
                float across = (NextRand(ref windState) * 2f - 1f) * halfThickness;
                float lam = 1.0f + NextRand(ref windState) * 0.8f;
                float amp = 0.003f + NextRand(ref windState) * 0.002f;
                SetVectorChannel(windSlots[i], new Vector4(along, across, lam, amp));
            }
        }

        private void UpdateLotusParticles(Vector3 origin, Vector2 along, Vector2 across, float seconds, float strength)
        {
            for (int i = 0; i < LotusCount; i++)
            {
                int reflIdx = lotusStart + i * 2;
                int flowerIdx = reflIdx + 1;
                if (flowerIdx >= Count)
                {
                    break;
                }

                Vector4 f = lotusBuffer[i];
                float open = Mathf.Clamp01((seconds - f.w) / LotusOpenSeconds);
                if (open <= 0f)
                {
                    VfxParticle r0 = Particles[reflIdx];
                    r0.Color = Color.clear;
                    Particles[reflIdx] = r0;
                    VfxParticle f0 = Particles[flowerIdx];
                    f0.Color = Color.clear;
                    Particles[flowerIdx] = f0;
                    continue;
                }

                bool isBloom = i % 2 == 0;
                float halfW = f.z;
                float halfH = f.z * (isBloom ? BloomAspect : PadAspect);
                float x = origin.x + along.x * f.x + across.x * f.y;
                float z = origin.z + along.y * f.x + across.y * f.y;
                float alpha = Mathf.Min(1f, 0.35f + 0.65f * open) * strength;

                VfxParticle refl = Particles[reflIdx];
                refl.Position = new Vector3(x, 0f, z - halfH * 0.05f);
                refl.Size = halfW;
                refl.Color = new Color(1f, 1f, 1f, alpha * ReflectionAlpha);
                refl.Alive = true;
                Particles[reflIdx] = refl;

                VfxParticle flower = Particles[flowerIdx];
                flower.Position = new Vector3(x, 0f, z);
                flower.Size = halfW;
                flower.Color = new Color(1f, 1f, 1f, alpha);
                flower.Alive = true;
                Particles[flowerIdx] = flower;
            }
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
