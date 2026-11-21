using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Instanced submission state for one bucket: the per-instance matrices, the per-instance data
    /// rows, and the buffer that carries those rows to the shader.
    ///
    /// This is what makes "many instances of the same shape with different parameters" a single
    /// draw call: the geometry is shared, and everything that differs rides in the instance data.
    ///
    /// The row stride is the batch's own <see cref="Vectors"/>, not a global maximum: a stroke that
    /// needs two vec4 per instance pays for two, and only the stroke that really carries seven pays
    /// for seven. <see cref="MaxVectors"/> is merely the legal upper bound.
    /// </summary>
    internal sealed class VfxInstanceBatch
    {
        /// <summary>How many vec4 one instance may carry at most. Not the stride.</summary>
        internal const int MaxVectors = 16;

        private Matrix4x4[] matrices = System.Array.Empty<Matrix4x4>();
        private Vector4[] data = System.Array.Empty<Vector4>();
        private ComputeBuffer buffer;
        private MaterialPropertyBlock block;

        internal int Count { get; private set; }

        /// <summary>Vec4 per instance for this batch; the shader's stride constant must equal it.</summary>
        internal int Vectors { get; private set; }

        /// <summary>Bytes per instance, derived from <see cref="Vectors"/>.</summary>
        internal int Stride => Mathf.Max(1, Vectors) * 16;

        internal Matrix4x4[] Matrices => matrices;

        internal ComputeBuffer Buffer => buffer;

        /// <summary>
        /// The batch's own property block: its uniforms are written by the stroke at append time and
        /// must still be there when the batch is submitted at the end of the frame. A block shared with
        /// the engine's per-emitter draws would be cleared long before that.
        /// </summary>
        internal MaterialPropertyBlock Block => block ??= new MaterialPropertyBlock();

        /// <summary>Starts a frame with a layout of <paramref name="vectors"/> vec4 per instance.</summary>
        internal void Begin(int vectors)
        {
            int wanted = Mathf.Clamp(vectors, 1, MaxVectors);
            if (wanted != Vectors)
            {
                Vectors = wanted;
                Relayout(matrices.Length);
            }
            Count = 0;
        }

        /// <summary>Appends one instance; returns false when the batch is full.</summary>
        internal bool Add(in Matrix4x4 matrix, Vector4[] row, int rowOffset)
        {
            if (Count >= matrices.Length && !Grow())
            {
                return false;
            }

            matrices[Count] = matrix;
            int target = Count * Vectors;
            for (int i = 0; i < Vectors; i++)
            {
                data[target + i] = row[rowOffset + i];
            }
            Count++;
            return true;
        }

        /// <summary>Uploads the rows for this frame's instances. Nothing is uploaded when empty.</summary>
        internal void Upload()
        {
            if (Count <= 0)
            {
                return;
            }
            EnsureBuffer();
            buffer.SetData(data);
        }

        /// <summary>
        /// Allocates the instance buffer for up to <paramref name="capacity"/> instances ahead of time,
        /// so the first stroke drawn does not pay for a GPU buffer allocation mid-frame.
        /// </summary>
        internal void Reserve(int capacity, int vectors)
        {
            Begin(vectors);
            int size = Mathf.Max(1, capacity);
            if (matrices.Length < size)
            {
                Relayout(size);
            }
            EnsureBuffer();
        }

        /// <summary>Empties the frame's instance list without releasing the buffer.</summary>
        internal void ClearInstances()
        {
            Count = 0;
        }

        internal void Dispose()
        {
            buffer?.Release();
            buffer = null;
            block?.Clear();
            matrices = System.Array.Empty<Matrix4x4>();
            data = System.Array.Empty<Vector4>();
            Count = 0;
        }

        private void EnsureBuffer()
        {
            if (buffer != null && buffer.count == matrices.Length && buffer.stride == Stride)
            {
                return;
            }
            buffer?.Release();
            buffer = new ComputeBuffer(Mathf.Max(1, matrices.Length), Stride);
        }

        private bool Grow()
        {
            int size = matrices.Length == 0 ? 64 : matrices.Length * 2;
            if (size > 4096)
            {
                return false;
            }
            Relayout(size);
            return true;
        }

        /// <summary>Resizes the instance count and keeps the row array exactly <c>count * Vectors</c> long.</summary>
        private void Relayout(int instances)
        {
            int size = Mathf.Max(1, instances);
            if (matrices.Length != size)
            {
                System.Array.Resize(ref matrices, size);
            }
            int need = size * Mathf.Max(1, Vectors);
            if (data.Length != need)
            {
                System.Array.Resize(ref data, need);
            }
        }
    }
}
