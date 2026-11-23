using System.Collections.Generic;
using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Draws one or more continuous strips from ordered particles in the pass's particle group.
    /// A dead particle or a change in <see cref="VfxRibbonParams.RibbonIdChannel"/> ends the current strip
    /// and starts a new one. When a particle provides a non-zero <see cref="VfxParticle.Side"/> vector it is
    /// used directly as the half-width lateral vector; otherwise a perpendicular vector is derived on XZ.
    /// </summary>
    internal sealed class RibbonRenderer : IVfxRenderer
    {
        internal static readonly RibbonRenderer Instance = new();

        private readonly VfxMeshWriter writer = new();
        private readonly List<int> segment = new();

        public void Render(VfxEmitter emitter, VfxPass pass, int passIndex, in VfxRenderContext context)
        {
            VfxRibbonParams ribbon = pass?.Ribbon;
            if (ribbon == null || emitter.Count < 2)
            {
                return;
            }

            Material material = context.Assets.Material(pass);
            if (material == null)
            {
                return;
            }

            MaterialPropertyBlock block = context.Engine.Block;
            block.Clear();
            for (int i = 0; i < ribbon.Bindings.Count; i++)
            {
                VfxUniformBinding.Apply(block, emitter, ribbon.Bindings[i], context.Engine.Frame);
            }

            ResolveVRange(emitter, ribbon, out float vMin, out float vMax);

            writer.Clear();
            segment.Clear();
            float alpha = emitter.Alpha;
            bool hasPrevId = false;
            int prevId = 0;

            for (int i = 0; i < emitter.Count; i++)
            {
                VfxParticle particle = emitter.Particles[i];
                if (!particle.Alive)
                {
                    FlushSegment(emitter, ribbon, alpha, vMin, vMax);
                    hasPrevId = false;
                    continue;
                }
                if (pass.ParticleGroup >= 0 && particle.ParticleGroup != pass.ParticleGroup)
                {
                    continue;
                }

                if (ribbon.RibbonIdChannel >= 0)
                {
                    int id = Mathf.FloorToInt(emitter.Channel(i, ribbon.RibbonIdChannel));
                    if (hasPrevId && id != prevId)
                    {
                        FlushSegment(emitter, ribbon, alpha, vMin, vMax);
                    }
                    prevId = id;
                    hasPrevId = true;
                }

                segment.Add(i);
            }
            FlushSegment(emitter, ribbon, alpha, vMin, vMax);

            if (writer.IsEmpty)
            {
                block.Clear();
                return;
            }

            Mesh mesh = emitter.MeshAt(passIndex);
            if (mesh == null)
            {
                block.Clear();
                return;
            }
            writer.Upload(mesh);

            ScreenBloomProperties bloom = context.Assets.Bloom(pass);
            if (bloom != null)
            {
                context.Engine.Bloom(bloom, mesh, material);
            }
            context.Engine.Draw(mesh, material, pass.Layer);
            block.Clear();
        }

        private void FlushSegment(VfxEmitter emitter, VfxRibbonParams ribbon, float emitterAlpha, float vMin, float vMax)
        {
            int count = segment.Count;
            if (count < 2)
            {
                segment.Clear();
                return;
            }

            float dist = 0f;
            Vector3 prevPos = Vector3.zero;
            float invCount = 1f / (count - 1);

            for (int k = 0; k < count; k++)
            {
                int idx = segment[k];
                VfxParticle p = emitter.Particles[idx];
                Vector3 pos = p.Position;
                pos.y = ribbon.UseAltitude ? ribbon.Altitude : pos.y + ribbon.AltitudeOffset;

                if (k > 0)
                {
                    dist += Vector3.Distance(prevPos, pos);
                }
                prevPos = pos;

                Vector3 side = p.Side;
                if (side.sqrMagnitude <= 1e-8f)
                {
                    Vector3 tangent = ComputeTangent(emitter, k, count);
                    float width = ribbon.Width * (ribbon.UseParticleSize ? p.Size : 1f);
                    float halfWidth = Mathf.Max(0f, width) * 0.5f;
                    Vector3 flat = new Vector3(tangent.x, 0f, tangent.z);
                    side = flat.sqrMagnitude > 1e-8f
                        ? new Vector3(-flat.z, 0f, flat.x).normalized * halfWidth
                        : new Vector3(halfWidth, 0f, 0f);
                }

                float u = ribbon.UChannel >= 0
                    ? emitter.Channel(idx, ribbon.UChannel)
                    : (ribbon.NormalizeU ? k * invCount : dist) * ribbon.UScale;

                Color color = ribbon.UseParticleColor ? p.Color : Color.white;
                color.r *= ribbon.Tint.r;
                color.g *= ribbon.Tint.g;
                color.b *= ribbon.Tint.b;
                color.a *= ribbon.Tint.a * emitterAlpha;

                writer.AddStripPair(pos - side, pos + side, u, vMin, vMax, color, k > 0);
            }

            segment.Clear();
        }

        private Vector3 ComputeTangent(VfxEmitter emitter, int k, int count)
        {
            Vector3 cur = emitter.Particles[segment[k]].Position;
            if (k == 0)
            {
                return emitter.Particles[segment[1]].Position - cur;
            }
            if (k == count - 1)
            {
                return cur - emitter.Particles[segment[k - 1]].Position;
            }
            Vector3 span = emitter.Particles[segment[k + 1]].Position - emitter.Particles[segment[k - 1]].Position;
            return span.sqrMagnitude > 1e-8f ? span : cur - emitter.Particles[segment[k - 1]].Position;
        }

        private static void ResolveVRange(VfxEmitter emitter, VfxRibbonParams ribbon, out float vMin, out float vMax)
        {
            int frames = Mathf.Max(1, ribbon.VFrames);
            if (frames <= 1)
            {
                vMin = ribbon.VMin;
                vMax = ribbon.VMax;
                return;
            }

            int f = ribbon.FrameChannel >= 0 ? Mathf.FloorToInt(emitter.EmitterChannel(ribbon.FrameChannel)) : 0;
            f = ((f % frames) + frames) % frames;
            vMin = 1f - (f + 1f) / frames;
            vMax = 1f - (float)f / frames;
        }
    }
}
