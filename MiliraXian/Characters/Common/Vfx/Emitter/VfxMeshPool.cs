using System.Collections.Generic;
using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Mesh ownership for the emitter system: a pool to reuse, and a retirement queue that waits a
    /// frame before actually destroying.
    ///
    /// The queue is what makes "destroy something that was drawn this frame" impossible: this
    /// engine defers drawing to the end of the frame, and it runs the map component's draw pass
    /// <em>before</em> its update pass, so a mesh queued during a draw is only safe to destroy once
    /// a later frame has begun.
    /// </summary>
    internal sealed class VfxMeshPool
    {
        private struct Retiring
        {
            internal Mesh Mesh;
            internal int Frame;
        }

        private struct Pooled
        {
            internal Mesh Mesh;
            internal int ReturnedFrame;
        }

        private readonly Dictionary<string, Stack<Pooled>> pools = new();
        private readonly List<Retiring> retiring = new();

        internal int PooledCount { get; private set; }

        internal int PendingCount => retiring.Count;

        /// <summary>
        /// A mesh for this bucket. A mesh returned during the current frame is not handed out again
        /// until a later frame: the frame's draw commands are consumed at its end, so reusing one
        /// now would overwrite vertices a pending command still points at.
        /// </summary>
        internal Mesh Rent(string name)
        {
            if (pools.TryGetValue(name, out Stack<Pooled> stack))
            {
                int now = Time.frameCount;
                while (stack.Count > 0)
                {
                    Pooled pooled = stack.Pop();
                    PooledCount--;
                    if (pooled.Mesh == null || !pooled.Mesh)
                    {
                        continue;
                    }
                    if (pooled.ReturnedFrame >= now)
                    {
                        // Too fresh: park it back and take a fresh mesh instead.
                        stack.Push(pooled);
                        PooledCount++;
                        break;
                    }
                    return pooled.Mesh;
                }
            }
            Mesh mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            return mesh;
        }

        /// <summary>Returns a mesh to the pool; it becomes rentable again on a later frame.</summary>
        internal void Give(Mesh mesh, string name)
        {
            if (mesh == null || !mesh)
            {
                return;
            }
            if (!pools.TryGetValue(name, out Stack<Pooled> stack))
            {
                stack = new Stack<Pooled>();
                pools[name] = stack;
            }
            stack.Push(new Pooled { Mesh = mesh, ReturnedFrame = Time.frameCount });
            PooledCount++;
        }

        /// <summary>Queues a mesh for destruction; it is destroyed on a later frame, never now.</summary>
        internal void Retire(Mesh mesh)
        {
            if (mesh == null || !mesh)
            {
                return;
            }
            retiring.Add(new Retiring { Mesh = mesh, Frame = Time.frameCount });
        }

        /// <summary>
        /// Destroys the meshes queued on an earlier frame. Call it from the map component's update
        /// pass; anything queued during the current frame's draw is left alone on purpose.
        /// </summary>
        internal void Sweep()
        {
            int now = Time.frameCount;
            for (int i = retiring.Count - 1; i >= 0; i--)
            {
                Retiring item = retiring[i];
                if (item.Frame >= now)
                {
                    continue;
                }
                retiring.RemoveAt(i);
                if (item.Mesh != null && item.Mesh)
                {
                    Object.Destroy(item.Mesh);
                }
            }
        }

        /// <summary>Destroys everything now. Only valid when no frame will follow (map teardown).</summary>
        internal void Dispose()
        {
            foreach (Stack<Pooled> stack in pools.Values)
            {
                foreach (Pooled pooled in stack)
                {
                    Mesh mesh = pooled.Mesh;
                    if (mesh != null && mesh)
                    {
                        Object.Destroy(mesh);
                    }
                }
                stack.Clear();
            }
            pools.Clear();
            PooledCount = 0;

            for (int i = 0; i < retiring.Count; i++)
            {
                Mesh mesh = retiring[i].Mesh;
                if (mesh != null && mesh)
                {
                    Object.Destroy(mesh);
                }
            }
            retiring.Clear();
        }
    }
}
