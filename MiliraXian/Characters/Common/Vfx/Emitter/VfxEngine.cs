using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// The only place a renderer may talk to the engine: draw calls, the per-submission property
    /// block, and the bloom copy. It holds no assets and no meshes.
    ///
    /// Everything a renderer submits goes through here, so batching, ordering and the deferred-draw
    /// rules have exactly one implementation.
    /// </summary>
    internal sealed class VfxEngine
    {
        private readonly MapComponent_VfxEmitterHost host;
        private readonly MaterialPropertyBlock block = new();
        private readonly VfxFrameUniforms frame = new();

        internal VfxEngine(MapComponent_VfxEmitterHost host)
        {
            this.host = host;
        }

        /// <summary>Camera-derived values shared by every stroke this frame.</summary>
        internal VfxFrameUniforms Frame => frame;

        internal Map Map => host.Map;

        /// <summary>Scratch block for one submission. Clear it before writing; it is copied on submit.</summary>
        internal MaterialPropertyBlock Block => block;

        /// <summary>A one-off draw of geometry that stays in world space (identity matrix).</summary>
        internal void Draw(Mesh mesh, Material material, int layer)
        {
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, layer, null, 0, block);
        }

        internal void Draw(Mesh mesh, Matrix4x4 matrix, Material material, int layer)
        {
            Graphics.DrawMesh(mesh, matrix, material, layer, null, 0, block);
        }

        /// <summary>
        /// The property block of an instanced stroke's batch, or null when the stroke is not instanced.
        /// Uniforms written here belong to that batch and survive until the batch is submitted at the
        /// end of the frame; the shared <see cref="Block"/> must not be used for them.
        /// </summary>
        internal MaterialPropertyBlock BlockFor(VfxPass pass)
        {
            return pass != null && pass.InstanceVectors > 0 ? host.InstancedBlock(pass) : block;
        }

        /// <summary>
        /// Adds one instance of a shared mesh to this stroke's batch; the batch is submitted once at
        /// the end of the frame, so many instances cost a single draw call.
        /// </summary>
        internal void AddInstance(VfxPass pass, in Matrix4x4 matrix, Vector4[] row, int rowOffset)
        {
            host.AddInstance(pass, matrix, row, rowOffset);
        }

        /// <summary>
        /// Submits the halo copy into the bloom layer. Call it before <see cref="Draw"/>: it writes
        /// the tint into the block and restores it, so the main draw has to come after it.
        /// </summary>
        internal void Bloom(ScreenBloomProperties bloom, Mesh mesh, Material material)
        {
            if (bloom?.group == null)
            {
                return;
            }
            ScreenBloom.Submit(bloom, mesh, Matrix4x4.identity, material, block);
        }
    }
}
