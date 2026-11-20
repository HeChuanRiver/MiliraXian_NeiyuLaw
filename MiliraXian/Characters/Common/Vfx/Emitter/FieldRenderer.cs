using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Draws a "field" stroke: one quad whose pixels a shader computes (edge curtains, water
    /// stains, refraction, aura washes).
    ///
    /// The renderer knows none of those effects. It supplies the quad's geometry, its centre and
    /// edge length if the shader asks for them, and then applies the uniforms the stroke declared
    /// in its <see cref="VfxFieldParams"/>. Whether the result looks like a spring, a rain wall or
    /// a shield is entirely the shader's and the texture's business.
    ///
    /// It draws exactly one stroke: an effect that grows more strokes gets more passes, not a
    /// smarter renderer.
    /// </summary>
    internal sealed class FieldRenderer : IVfxRenderer
    {
        internal static readonly FieldRenderer Instance = new();

        private readonly VfxMeshWriter writer = new();

        /// <summary>Reused per-instance row; the draw pass runs every frame and must not allocate.</summary>
        private readonly Vector4[] instanceRow = new Vector4[VfxInstanceBatch.MaxVectors];

        public void Render(VfxEmitter emitter, VfxPass pass, int passIndex, in VfxRenderContext context)
        {
            VfxFieldParams field = pass?.Field;
            if (field == null || emitter.Count <= 0)
            {
                return;
            }

            Material material = context.Assets.Material(pass);
            if (material == null)
            {
                return;
            }

            // The field is one particle: pick the one this stroke claims.
            int particleIndex = emitter.FirstParticleOfGroup(pass.ParticleGroup);
            if (particleIndex < 0)
            {
                return;
            }

            // Resolve the anchor now rather than reusing the tick-pass position: the draw pass runs
            // before the update pass, so a followed pawn would otherwise trail one frame behind.
            Vector3 center = emitter.Anchor.TryResolve(context.Engine.Map, out Vector3 resolved)
                ? resolved
                : emitter.Particles[particleIndex].Position;

            Vector2 size = field.QuadSize;
            if (field.SizeFromRadius)
            {
                // Cell coverage is integral: the field occupies whole cells, so the quad must be
                // sized from the ceiling of the radius, exactly as the old renderer did.
                float side = Mathf.Ceil(emitter.Channel(particleIndex, VfxChannels.Radius)) * 2f + field.RadiusPadding;
                size = new Vector2(side, side);
            }
            size.x = Mathf.Max(0.01f, size.x);
            size.y = Mathf.Max(0.01f, size.y);

            // The shader rebuilds its geometry from the centre and the edge length, so the quad has
            // to line up with that formula: the extra curtain height spills north of the field.
            float height = size.y + field.ExtraHeight;

            // Instanced strokes share one built-in quad; the centre, extents, radius and strength
            // travel in the instance row, which is what lets every field of this kind batch.
            if (pass.InstanceVectors > 0 && pass.Mesh != null)
            {
                instanceRow[0] = new Vector4(center.x, center.z, size.x, field.ExtraHeight);
                instanceRow[1] = new Vector4(
                    field.RadiusChannel >= 0 ? emitter.Channel(particleIndex, field.RadiusChannel) : 0f,
                    field.StrengthChannel >= 0 ? emitter.Channel(particleIndex, field.StrengthChannel) : 1f,
                    field.Altitude,
                    0f);

                MaterialPropertyBlock constants = context.Engine.BlockFor(pass);
                if (constants != null)
                {
                    constants.Clear();
                    if (field.SizeUniform != 0)
                    {
                        constants.SetFloat(field.SizeUniform, size.x);
                    }
                    for (int i = 0; i < field.Bindings.Count; i++)
                    {
                        VfxUniformBinding.Apply(constants, emitter, field.Bindings[i], particleIndex,
                            context.Engine.Frame);
                    }
                }

                context.Engine.AddInstance(pass, Matrix4x4.identity, instanceRow, 0);
                return;
            }

            Vector3 quadCenter = new Vector3(
                center.x,
                field.Altitude > 0f ? field.Altitude : center.y,
                center.z + field.ExtraHeight * 0.5f);

            VfxQuadOptions options = VfxQuadOptions.Of(new Vector2(size.x, height));
            writer.Clear();
            VfxQuad.Add(writer, quadCenter, options);

            // One mesh per (emitter, stroke): drawing is deferred to the frame's end, so a mesh
            // shared between two strokes would show only the last one's vertices.
            Mesh mesh = emitter.MeshAt(passIndex);
            if (mesh == null)
            {
                return;
            }
            writer.Upload(mesh);

            MaterialPropertyBlock block = context.Engine.Block;
            block.Clear();
            if (field.CenterUniform != 0)
            {
                block.SetVector(field.CenterUniform, new Vector4(center.x, center.z, 0f, 0f));
            }
            if (field.SizeUniform != 0)
            {
                block.SetFloat(field.SizeUniform, size.x);
            }
            for (int i = 0; i < field.Bindings.Count; i++)
            {
                VfxUniformBinding.Apply(block, emitter, field.Bindings[i], particleIndex,
                    context.Engine.Frame);
            }

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
