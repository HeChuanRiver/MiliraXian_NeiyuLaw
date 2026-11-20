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

            Camera camera = sprite.Plane == VfxPlane.Billboard ? Find.Camera : null;
            if (sprite.Plane == VfxPlane.Billboard && camera == null)
            {
                return;
            }

            MaterialPropertyBlock block = context.Engine.Block;
            block.Clear();
            for (int i = 0; i < sprite.Bindings.Count; i++)
            {
                VfxUniformBinding.Apply(block, emitter, sprite.Bindings[i], 0, context.Engine.Frame);
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
                options.Plane = sprite.Plane;
                options.RotationDegrees = sprite.UseParticleRotation ? particle.RotationDegrees : 0f;
                options.Color = sprite.UseParticleColor ? particle.Color : Color.white;
                options.Color.a *= alpha * sprite.Tint.a;
                options.Color.r *= sprite.Tint.r;
                options.Color.g *= sprite.Tint.g;
                options.Color.b *= sprite.Tint.b;

                AtlasUv(emitter, sprite, out Vector2 uvMin, out Vector2 uvMax);
                options.UvMin = uvMin;
                options.UvMax = uvMax;
                Vector3 center = particle.Position;
                if (sprite.Altitude > 0f)
                {
                    center.y = sprite.Altitude;
                }

                if (camera != null)
                {
                    VfxQuad.AddBillboard(writer, center, camera.transform.right, camera.transform.up, options);
                }
                else
                {
                    VfxQuad.Add(writer, center, options);
                }
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

        /// <summary>The uv rect of the particle's atlas frame, or the whole texture when there is no atlas.</summary>
        private static void AtlasUv(VfxEmitter emitter, VfxSpriteParams sprite, out Vector2 uvMin, out Vector2 uvMax)
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

            int frame = sprite.FrameChannel >= 0 ? Mathf.FloorToInt(emitter.Channel(0, sprite.FrameChannel)) : 0;
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
