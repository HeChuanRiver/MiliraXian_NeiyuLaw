using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Draws a "streak" stroke: one thin quad per particle, from its head along its direction for
    /// its size, fading from head alpha to tail alpha. Every quad of the stroke is batched into one
    /// mesh, so hundreds of raindrops cost one submission.
    ///
    /// The renderer knows no effect: it reads the particles' position, direction and length.
    /// </summary>
    internal sealed class StreakRenderer : IVfxRenderer
    {
        internal static readonly StreakRenderer Instance = new();

        private readonly VfxMeshWriter writer = new();

        public void Render(VfxEmitter emitter, VfxPass pass, int passIndex, in VfxRenderContext context)
        {
            VfxStreakParams streak = pass?.Streak;
            if (streak == null || emitter.Count <= 0)
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
            for (int i = 0; i < streak.Bindings.Count; i++)
            {
                VfxUniformBinding.Apply(block, emitter, streak.Bindings[i], context.Engine.Frame);
            }

            writer.Clear();
            float emitterAlpha = emitter.Alpha;
            float halfWidth = Mathf.Max(1e-4f, streak.Width) * 0.5f;

            for (int i = 0; i < emitter.Count; i++)
            {
                VfxParticle particle = emitter.Particles[i];
                if (!particle.Alive)
                {
                    continue;
                }
                if (pass.ParticleGroup >= 0 && particle.ParticleGroup != pass.ParticleGroup)
                {
                    continue;
                }

                Color head = particle.Color;
                head.a *= emitterAlpha;
                if (head.a <= 0.001f || particle.Size <= 1e-4f)
                {
                    continue;
                }

                Vector3 a = particle.Position;
                if (streak.Altitude > 0f)
                {
                    a.y = streak.Altitude;
                }

                Vector3 direction = particle.Velocity;
                if (direction.sqrMagnitude < 1e-8f)
                {
                    direction = Vector3.up;
                }
                direction.Normalize();
                Vector3 b = a + direction * particle.Size;

                // The width axis is horizontal, perpendicular to the segment's ground direction; a
                // perfectly vertical streak falls back to east-west (the raindrop case).
                Vector3 flat = new Vector3(direction.x, 0f, direction.z);
                Vector3 side = flat.sqrMagnitude < 1e-8f
                    ? new Vector3(halfWidth, 0f, 0f)
                    : new Vector3(-flat.z, 0f, flat.x).normalized * halfWidth;

                Color tail = head;
                tail.a *= streak.TailAlpha;

                // u runs along the streak (0 at the head, 1 at the tail), v across its width.
                writer.AddQuad(
                    a - side, b - side, b + side, a + side,
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                    head, tail, tail, head);
            }

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
    }
}
