using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Draws a "sprite" stroke: one quad per particle, all of them batched into a single mesh so a
    /// hundred petals cost one submission.
    ///
    /// The renderer knows no effect. It reads the particles the emitter laid down (position, size,
    /// rotation, colour, atlas frame) and the params the content declared, and that is all.
    /// </summary>
    internal sealed class SpriteRenderer : IVfxRenderer
    {
        internal static readonly SpriteRenderer Instance = new();

        private readonly VfxMeshWriter writer = new();

        /// <summary>Reused per-instance row; the draw pass runs every frame and must not allocate.</summary>
        private readonly Vector4[] instanceRow = new Vector4[VfxInstanceBatch.MaxVectors];
        public void Render(VfxEmitter emitter, VfxPass pass, int passIndex, in VfxRenderContext context)
        {
            VfxSpriteParams sprite = pass?.Sprite;
            if (sprite == null || emitter.Count <= 0)
            {
                return;
            }

            Material material = context.Assets.Material(pass);
            if (material == null)
            {
                return;
            }

            // Instanced sprite: one shared quad, one row per particle. The geometry never changes, so
            // the shader owns orientation, rotation and animation — it has the clock and each
            // particle's spawn time. Rows:
            //   row0 = (center.x, center.z, halfSize, spawnSeconds)
            //   row1 = (center.y, rotation0, rotationRate, atlasFrame)
            //   row2 = colour rgba, written only when the pass asks for three rows
            if (pass.InstanceVectors > 0 && pass.Mesh != null)
            {
                RenderInstanced(emitter, pass, sprite, context, material);
                return;
            }

            MaterialPropertyBlock block = context.Engine.Block;
            block.Clear();
            for (int i = 0; i < sprite.Bindings.Count; i++)
            {
                VfxUniformBinding.Apply(block, emitter, sprite.Bindings[i], context.Engine.Frame);
            }

            writer.Clear();
            float alpha = emitter.Alpha;
            for (int i = 0; i < emitter.Count; i++)
            {
                VfxParticle particle = emitter.Particles[i];
                if (!particle.Alive || particle.Color.a * alpha <= 0.001f)
                {
                    continue;
                }
                if (pass.ParticleGroup >= 0 && particle.ParticleGroup != pass.ParticleGroup)
                {
                    continue;
                }

                Vector2 size = sprite.QuadSize;
                if (sprite.UseParticleSize)
                {
                    size *= particle.Size;
                }
                if (size.x <= 0.001f || size.y <= 0.001f)
                {
                    continue;
                }

                VfxQuadOptions options = VfxQuadOptions.Of(size);
                options.RotationDegrees = sprite.UseParticleRotation ? particle.RotationDegrees : 0f;
                options.Color = sprite.UseParticleColor ? particle.Color : Color.white;
                options.Color.a *= alpha * sprite.Tint.a;
                options.Color.r *= sprite.Tint.r;
                options.Color.g *= sprite.Tint.g;
                options.Color.b *= sprite.Tint.b;

                AtlasUv(emitter, i, sprite, out Vector2 uvMin, out Vector2 uvMax);
                options.UvMin = uvMin;
                options.UvMax = uvMax;
                options.FlipU = sprite.FlipU;
                options.FlipV = sprite.FlipV;
                Vector3 center = particle.Position;
                center.y = sprite.UseAltitude ? sprite.Altitude : center.y + sprite.AltitudeOffset;

                VfxQuad.Add(writer, center, options);
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

        /// <summary>
        /// Appends one instance row per particle instead of building vertices. The geometry is the
        /// shared quad, so nothing is uploaded per particle and the shader can animate from the clock.
        /// </summary>
        private void RenderInstanced(VfxEmitter emitter, VfxPass pass, VfxSpriteParams sprite,
            in VfxRenderContext context, Material material)
        {
            MaterialPropertyBlock constants = context.Engine.BlockFor(pass);
            if (constants == null)
            {
                return;
            }

            constants.Clear();
            float aspect = sprite.QuadSize.x > 1e-4f ? sprite.QuadSize.y / sprite.QuadSize.x : 1f;
            constants.SetVector(VfxShaderIds.QuadSize, new Vector4(sprite.QuadSize.x, sprite.QuadSize.y, aspect, 0f));
            for (int i = 0; i < sprite.Bindings.Count; i++)
            {
                VfxUniformBinding.Apply(constants, emitter, sprite.Bindings[i], context.Engine.Frame);
            }

            float alpha = emitter.Alpha;
            float rotationRate = sprite.RotationRate;
            int vectors = pass.InstanceVectors;

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

                float size = sprite.QuadSize.x * (sprite.UseParticleSize ? particle.Size : 1f);
                if (size <= 0.001f)
                {
                    continue;
                }

                float y = sprite.UseAltitude ? sprite.Altitude : particle.Position.y + sprite.AltitudeOffset;
                instanceRow[0] = new Vector4(
                    particle.Position.x, particle.Position.z, size * 0.5f, particle.SpawnSeconds);
                instanceRow[1] = new Vector4(
                    y,
                    sprite.UseParticleRotation ? particle.RotationDegrees : 0f,
                    rotationRate,
                    sprite.FrameChannel >= 0 ? Mathf.FloorToInt(emitter.Channel(i, sprite.FrameChannel)) : 0f);

                if (vectors >= 3)
                {
                    Color color = sprite.UseParticleColor ? particle.Color : Color.white;
                    color.a *= alpha * sprite.Tint.a;
                    color.r *= sprite.Tint.r;
                    color.g *= sprite.Tint.g;
                    color.b *= sprite.Tint.b;
                    instanceRow[2] = new Vector4(color.r, color.g, color.b, color.a);
                }

                context.Engine.AddInstance(pass, Matrix4x4.identity, instanceRow, 0);
            }
        }

        /// <summary>The uv rect of the particle's atlas frame, or the whole texture when there is no atlas.</summary>
        private static void AtlasUv(VfxEmitter emitter, int particleIndex, VfxSpriteParams sprite,
            out Vector2 uvMin, out Vector2 uvMax)
        {
            if (sprite.UseUvRect)
            {
                uvMin = sprite.UvMin;
                uvMax = sprite.UvMax;
                return;
            }

            uvMin = Vector2.zero;
            uvMax = Vector2.one;

            int columns = Mathf.Max(1, sprite.AtlasColumns);
            int rows = Mathf.Max(1, sprite.AtlasRows);
            if (columns == 1 && rows == 1)
            {
                return;
            }

            int frame = sprite.FrameChannel >= 0 ? Mathf.FloorToInt(emitter.Channel(particleIndex, sprite.FrameChannel)) : 0;
            frame = ((frame % (columns * rows)) + columns * rows) % (columns * rows);

            int column = frame % columns;
            int row = frame / columns;

            // Atlas row 0 is the top of the image, so v counts down from 1.
            float width = 1f / columns;
            float height = 1f / rows;
            uvMin = new Vector2(column * width, 1f - (row + 1) * height);
            uvMax = new Vector2(uvMin.x + width, uvMin.y + height);
        }
    }
}
