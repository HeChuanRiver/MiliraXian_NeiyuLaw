using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Instanced submission state for one bucket: the per-instance matrices, the per-instance data
    /// rows, and the buffer that carries those rows to the shader.
    ///
    /// This is what makes "many instances of the same shape with different parameters" a single
    /// draw call: the geometry is shared, and everything that differs rides in the instance data.
    /// </summary>
    internal sealed class VfxInstanceBatch
    {
        /// <summary>How many vec4 a shader may read per instance.</summary>
        internal const int MaxVectors = 4;

        private const int Stride = MaxVectors * 16;

        private Matrix4x4[] matrices = System.Array.Empty<Matrix4x4>();
        private Vector4[] data = System.Array.Empty<Vector4>();
        private ComputeBuffer buffer;
        private MaterialPropertyBlock block;

        internal int Count { get; private set; }

        internal int Vectors { get; private set; }

        /// <summary>
        /// The batch's own property block: its uniforms are written by the stroke at append time and
        /// must still be there when the batch is submitted at the end of the frame. A block shared with
        /// the engine's per-emitter draws would be cleared long before that.
        /// </summary>
        internal MaterialPropertyBlock Block => block ??= new MaterialPropertyBlock();

        internal Matrix4x4[] Matrices => matrices;

        internal ComputeBuffer Buffer => buffer;

        internal Vector4[] Data => data;

        /// <summary>
        /// Allocates the instance buffer for up to <paramref name="capacity"/> instances ahead of time,
        /// so the first stroke drawn does not pay for a GPU buffer allocation mid-frame.
        /// </summary>
        internal void Reserve(int capacity, int vectors)
        {
            int size = Mathf.Max(1, capacity);
            if (matrices.Length >= size && buffer != null)
            {
                return;
            }
            if (matrices.Length < size)
            {
                System.Array.Resize(ref matrices, size);
                System.Array.Resize(ref data, size * MaxVectors);
            }
            buffer?.Release();
            buffer = new ComputeBuffer(size, Stride);
            Vectors = Mathf.Clamp(vectors, 1, MaxVectors);
        }

        /// <summary>Empties the frame's instance list without releasing the buffer.</summary>
        internal void ClearInstances()
        {
            Count = 0;
        }

        /// <summary>Starts a frame with a layout of <paramref name="vectors"/> vec4 per instance.</summary>
        internal void Begin(int vectors)
        {
            Count = 0;
            Vectors = Mathf.Clamp(vectors, 1, MaxVectors);
        }

        /// <summary>Appends one instance; returns false when the batch is full.</summary>
        internal bool Add(in Matrix4x4 matrix, Vector4[] row, int rowOffset)
        {
            if (Count >= matrices.Length && !Grow())
            {
                return false;
            }

            matrices[Count] = matrix;
            int target = Count * MaxVectors;
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
            if (buffer == null || buffer.count < matrices.Length)
            {
                buffer?.Release();
                buffer = new ComputeBuffer(Mathf.Max(1, matrices.Length), Stride);
            }
            buffer.SetData(data);
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

        private bool Grow()
        {
            int size = matrices.Length == 0 ? 64 : matrices.Length * 2;
            if (size > 4096)
            {
                return false;
            }
            System.Array.Resize(ref matrices, size);
            System.Array.Resize(ref data, size * MaxVectors);
            return true;
        }
    }
}
