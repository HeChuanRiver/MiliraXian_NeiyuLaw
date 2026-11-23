using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.Emitter;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.Zantetsuken
{
    /// <summary>
    /// Visual emitter for one Guren Zantetsuken cast: the ground lotus with rising light bars,
    /// the expanding elliptical shock rings on landing and gaeshi click, and the landing flash.
    /// Reads only immutable cast parameters; the job driver keeps it alive via Maintain.
    /// </summary>
    internal sealed class ZantetsukenEmitter : VfxEmitter
    {
        private const float AlphaScale = 4f;
        private const float RingLife = 0.35f;
        private const float RingWidth = 0.24f;
        private const int RingSegments = 48;
        private const int RingVerts = RingSegments + 1;
        private const int LandRingOffset = 0;
        private const int GaeshiRingOffset = RingVerts;
        private const int FlashIndex = RingVerts * 2;
        private const int TotalParticles = FlashIndex + 1;

        private CompProperties_AbilityZantetsuken props;
        private ZantetsukenVisualProps vis;
        private int kickTick;
        private bool gaeshi;

        private int quadSlot;
        private int lotusSlot;
        private int lotus2Slot;
        private int rimWidthSlot;
        private int glowSlot;
        private int whiteSlot;
        private int fillSlot;
        private int uSlot;
        private int ribbonIdSlot;

        public ZantetsukenEmitter()
        {
        }

        internal static VfxEmitterHandle Spawn(Map map, CompProperties_AbilityZantetsuken props, int kickTick,
            IntVec3 lotusCell, bool gaeshi)
        {
            MapComponent_VfxEmitterHost host = map?.GetComponent<MapComponent_VfxEmitterHost>();
            if (host == null || VfxDefOf.MX_Vfx_Zantetsuken == null || props == null)
            {
                return default;
            }
            VfxEmitterHandle handle = host.Spawn(VfxDefOf.MX_Vfx_Zantetsuken, VfxAnchor.At(lotusCell.ToVector3Shifted()));
            if (host.TryGetEmitter(handle, out ZantetsukenEmitter emitter))
            {
                emitter.Configure(props, kickTick, gaeshi);
            }
            return handle;
        }

        internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
        {
            base.Reset(effect, anchor, version, nowTick, position);

            props = null;
            vis = null;
            kickTick = nowTick;
            gaeshi = false;

            quadSlot = effect.RequireVectorSlot("lotusQuad");
            lotusSlot = effect.RequireVectorSlot("lotusParams");
            lotus2Slot = effect.RequireVectorSlot("lotusStyle");
            rimWidthSlot = effect.RequireEmitterFloatSlot("lotusRimWidth");
            glowSlot = effect.RequireVectorSlot("lotusGlow");
            whiteSlot = effect.RequireVectorSlot("lotusWhite");
            fillSlot = effect.RequireVectorSlot("lotusFill");
            uSlot = effect.RequireParticleFloatSlot("u");
            ribbonIdSlot = effect.RequireParticleFloatSlot("ribbonId");

            if (Particles == null || Particles.Length < TotalParticles)
            {
                return;
            }

            // Pre-allocate ring strip vertices and the landing flash slot so Sync updates in place without GC.
            for (int i = 0; i < RingVerts * 2; i++)
            {
                int idx = Emit(position, 0f, 1f, Color.clear, particleGroup: 0);
                if (idx >= 0)
                {
                    // Sample the middle column of the circular glow texture for a soft-edged line.
                    SetChannel(idx, uSlot, 0.5f);
                    SetChannel(idx, ribbonIdSlot, i < RingVerts ? 0f : 1f);
                    Particles[idx].Alive = false;
                }
            }
            int flashIdx = Emit(position, 0f, 0f, Color.clear, particleGroup: 1);
            if (flashIdx >= 0)
            {
                Particles[flashIdx].Alive = false;
            }
        }

        private void Configure(CompProperties_AbilityZantetsuken castProps, int castKickTick, bool castGaeshi)
        {
            props = castProps;
            vis = castProps.visuals;
            kickTick = castKickTick;
            gaeshi = castGaeshi;
            Sync(Seconds);
        }

        private float Seconds => VisualClock.TicksSince(kickTick) / 60f;

        private float LifeSeconds => gaeshi
            ? Mathf.Max(props.GaeshiEnd, props.ClickTime + props.burstSeconds)
            : props.HoldEnd + props.plainFadeSeconds;

        internal override bool Advance(float deltaSeconds, int nowTick, Map map)
        {
            if (!base.Advance(deltaSeconds, nowTick, map))
            {
                return false;
            }
            if (props == null || vis == null)
            {
                return false;
            }

            float t = Seconds;
            if (t > LifeSeconds)
            {
                return false;
            }

            Sync(t);
            return true;
        }

        private void Sync(float t)
        {
            if (t < 0f)
            {
                SetVectorChannel(quadSlot, Vector4.zero);
                HideRing(LandRingOffset);
                HideRing(GaeshiRingOffset);
                HideFlash();
                return;
            }

            float k = BlueK(t);
            Color glow = Color.Lerp(vis.pink, vis.blue, k);
            Color white = Color.Lerp(vis.white, vis.blueWhite, k);
            Color fill = Color.Lerp(vis.red, vis.blueDeep, k);

            UpdateLotus(t, glow, white, fill);
            UpdateRing(LandRingOffset, t, props.landSeconds, 1f, glow, white);
            if (gaeshi)
            {
                UpdateRing(GaeshiRingOffset, t, props.ClickTime, 0.8f, glow, white);
            }
            else
            {
                HideRing(GaeshiRingOffset);
            }
            UpdateFlash(t, props.landSeconds, 1.4f, 0.05f, 1f, white);
        }

        private float BlueK(float t) => gaeshi && t >= props.HoldEnd ? Smooth((t - props.HoldEnd) / vis.blueRamp) : 0f;

        private void LotusState(float t, out float bright, out float scale, out float rise)
        {
            bright = scale = 1f;
            rise = 0f;
            if (t < props.takeoffSeconds)
            {
                bright = 0f;
                return;
            }
            bright = vis.lotusBase * Smooth((t - props.takeoffSeconds) / (props.apexSeconds - props.takeoffSeconds));
            float h = vis.curtainHeight;
            if (t >= props.landSeconds)
            {
                float u = (t - props.landSeconds) / vis.landFlareSeconds;
                if (u < 1f)
                {
                    bright = vis.lotusBase + 0.9f * Mathf.Exp(-u * 3f);
                    rise = h * Mathf.Pow(1f - u, 0.6f);
                }
            }
            if (gaeshi && t >= props.HoldEnd)
            {
                float kk = Smooth((t - props.HoldEnd) / (props.ScoopEnd - props.HoldEnd));
                bright = Mathf.Max(bright, vis.lotusBase + 0.55f * kk);
                rise = Mathf.Max(rise, h * kk);
                if (t >= props.ClickTime)
                {
                    float u = Mathf.Clamp01((t - props.ClickTime) / props.burstSeconds);
                    scale = 1f + 0.3f * EaseOut(u);
                    bright = Mathf.Pow(1f - u, 1.3f);
                    rise = h * (1f - u) * (1f - u);
                }
            }
            if (!gaeshi && t >= props.HoldEnd)
            {
                bright *= 1f - Mathf.Clamp01((t - props.HoldEnd) / props.plainFadeSeconds);
            }
        }

        private void UpdateLotus(float t, Color glow, Color white, Color fill)
        {
            LotusState(t, out float bright, out float scale, out float rise);
            if (bright <= 0.005f)
            {
                SetVectorChannel(quadSlot, Vector4.zero);
                return;
            }

            float depth = props.depthToScreen;
            float r = props.lotusRadius * scale * 1.1f;
            float top = rise * vis.curtainLift;

            SetVectorChannel(quadSlot, new Vector4(r, r * depth, top, 0f));
            SetVectorChannel(lotusSlot, new Vector4(bright, scale, top, props.lotusRadius));
            SetVectorChannel(lotus2Slot, new Vector4(depth, 0.07f, vis.rimStrength, 0.25f * vis.rimStrength));
            SetEmitterChannel(rimWidthSlot, vis.rimWidth);
            SetVectorChannel(glowSlot, glow);
            SetVectorChannel(whiteSlot, white);
            SetVectorChannel(fillSlot, fill);
        }

        private void UpdateRing(int offset, float t, float t0, float strength, Color glow, Color white)
        {
            if (Particles == null || offset + RingVerts > Count)
            {
                return;
            }

            float u = (t - t0) / RingLife;
            if (u < 0f || u > 1f)
            {
                HideRing(offset);
                return;
            }

            float r = props.lotusRadius * 1.1f * EaseOut(u);
            float depth = props.depthToScreen;
            float halfWidth = RingWidth * 0.5f;
            Color c = WithIntensity(Color.Lerp(glow, white, 0.5f), 0.65f * Mathf.Pow(1f - u, 1.5f) * strength);

            for (int i = 0; i < RingVerts; i++)
            {
                float a = i / (float)RingSegments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);
                // Analytical ellipse normal keeps the seam at i = 0 and i = RingSegments seamless.
                Vector3 normal = new Vector3(-cos * depth, 0f, -sin).normalized * halfWidth;

                VfxParticle p = Particles[offset + i];
                p.Position = new Vector3(Position.x + cos * r, Position.y, Position.z + sin * r * depth);
                p.Side = normal;
                p.Color = c;
                p.Alive = true;
                Particles[offset + i] = p;
            }
        }

        private void HideRing(int offset)
        {
            if (Particles == null || offset + RingVerts > Count)
            {
                return;
            }
            for (int i = 0; i < RingVerts; i++)
            {
                Particles[offset + i].Alive = false;
            }
        }

        private void UpdateFlash(float t, float t0, float size, float life, float strength, Color white)
        {
            if (Particles == null || FlashIndex >= Count)
            {
                return;
            }
            if (t < t0 - 0.01f)
            {
                HideFlash();
                return;
            }
            float x = (t - t0) / life;
            float k = Mathf.Exp(-x * x) * strength;
            if (k < 0.01f)
            {
                HideFlash();
                return;
            }

            VfxParticle p = Particles[FlashIndex];
            p.Position = Position;
            p.Size = size * 2f;
            p.Color = WithIntensity(white, k * 2f);
            p.Alive = true;
            Particles[FlashIndex] = p;
        }

        private void HideFlash()
        {
            if (Particles != null && FlashIndex < Count)
            {
                Particles[FlashIndex].Alive = false;
            }
        }

        /// <summary>Vertex alpha is stored divided by <see cref="AlphaScale"/>, so intensities up to 4 survive the 8-bit colour.</summary>
        private static Color WithIntensity(Color c, float intensity) => new(c.r, c.g, c.b, Mathf.Clamp01(intensity / AlphaScale));

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float EaseOut(float u) => 1f - (1f - u) * (1f - u);
    }
}
