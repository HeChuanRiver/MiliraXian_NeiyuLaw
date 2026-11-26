using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// The per-map owner of the emitter system: the emitter table, the particle stepping, the mesh
    /// pool and the frame-generation sweep.
    ///
    /// It is a <see cref="MapComponent"/> because that is exactly what it is: one per map, updated
    /// every frame for every map, drawn every frame for the current map, and disposed with the map.
    /// <see cref="Verse.Map.FillComponents"/> creates it; nothing has to register it.
    ///
    /// It knows nothing about any particular effect. It never decides what to draw, how many
    /// strokes an effect needs or which layer it belongs on — content and renderers do that.
    /// </summary>
    public sealed class MapComponent_VfxEmitterHost : MapComponent, IDisposable
    {
        private readonly List<VfxEmitter> slots = new();
        private readonly List<int> freeSlots = new();
        private readonly VfxMeshPool meshes = new();
        private readonly Dictionary<VfxPass, ScreenBloomProperties> blooms = new();
        private readonly Dictionary<VfxPass, VfxInstanceBatch> batches = new();

        private VfxEngine engine;
        private VfxAssets assets;
        private VfxRenderContext context;
        private float lastTicks;
        private bool hasTicks;
        private bool disposed;

        public MapComponent_VfxEmitterHost(Map map) : base(map)
        {
        }

        /// <summary>Live emitters, for diagnostics and for the acceptance checks.</summary>
        internal int EmitterCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && !slots[i].Released)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        internal int PendingDestroyCount => meshes.PendingCount;

        internal Map Map => map;

        /// <summary>
        /// Allocates the GPU instance buffers of every instanced stroke. Runs on the main thread from
        /// <see cref="MapComponentUpdate"/>, never from <c>FinalizeInit</c>: map generation happens on
        /// a background thread, where materials and buffers must not be created.
        /// </summary>
        private void ReserveInstanceBatches()
        {
            foreach (VfxDef def in DefDatabase<VfxDef>.AllDefsListForReading)
            {
                VfxEffect effect;
                try
                {
                    effect = VfxDefCompiler.EffectOf(def);
                }
                catch (Exception exception)
                {
                    Log.ErrorOnce("[MiliraXian] VFX '" + def.defName + "' failed to compile: " + exception,
                        GenText.StableStringHash("MX_Vfx_Compile_" + def.defName));
                    continue;
                }
                List<VfxPass> passes = effect?.Passes;
                if (passes == null)
                {
                    continue;
                }
                for (int i = 0; i < passes.Count; i++)
                {
                    VfxPass pass = passes[i];
                    if (pass != null && pass.InstanceVectors > 0 && pass.Mesh != null)
                    {
                        BatchFor(pass).Reserve(def.capacity, pass.InstanceVectors);
                    }
                }
            }
        }

        public override void MapComponentUpdate()
        {
            // Warm-up runs here, on the main thread's first frame: map component init happens during
            // background map generation, where creating materials is not allowed.
            if (VfxDefCompiler.PrewarmPending)
            {
                VfxDefCompiler.Prewarm();
                ReserveInstanceBatches();
            }

            // Step ages, expire emitters whose source stopped maintaining them, then destroy meshes
            // queued on earlier frames. Every map runs this every frame.
            TickFrame();

            // Draw here rather than in MapComponentDraw: MapComponentDraw runs in UIRootUpdate
            // before TickManagerUpdate and DrawDynamicThings, leaving attached or DrawAt-pushed
            // visuals one frame behind the bodies they follow.
            if (Find.CurrentMap == map && WorldRendererUtility.DrawingMap)
            {
                DrawFrame();
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            Dispose();
        }

        internal Mesh RentMesh(string name)
        {
            return meshes.Rent(name);
        }

        /// <summary>Hands a mesh back for reuse under the same bucket name.</summary>
        internal void GiveMesh(Mesh mesh, string name)
        {
            meshes.Give(mesh, name);
        }

        /// <summary>Queues a mesh for destruction on a later frame. Never destroys now.</summary>
        internal void RetireMesh(Mesh mesh)
        {
            meshes.Retire(mesh);
        }

        internal VfxEmitterHandle Spawn(VfxDef def, VfxAnchor anchor)
        {
            return Spawn(VfxDefCompiler.EffectOf(def), anchor);
        }

        private VfxEmitterHandle Spawn(VfxEffect effect, VfxAnchor anchor)
        {
            if (disposed || effect == null)
            {
                return VfxEmitterHandle.None;
            }

            int nowTick = TickNow();
            Vector3 position = Vector3.zero;
            bool resolved = anchor.TryResolve(map, out position);
            if (!resolved && anchor.Kind != VfxAnchorKind.None)
            {
                // The anchor is already stale: there is nothing to show.
                return VfxEmitterHandle.None;
            }

            int slot;
            if (freeSlots.Count > 0)
            {
                slot = freeSlots[freeSlots.Count - 1];
                freeSlots.RemoveAt(freeSlots.Count - 1);
                VfxEmitter reused = slots[slot];
                // A slot may have served a different def whose emitter class differs; in that case
                // the pooled object is useless and a fresh one takes its place.
                if (reused.GetType() != effect.EmitterType)
                {
                    reused = CreateEmitter(effect);
                    slots[slot] = reused;
                }
                reused.Reset(effect, anchor, reused.Version + 1, nowTick, position);
                return new VfxEmitterHandle(this, slot, reused.Version);
            }

            VfxEmitter emitter = CreateEmitter(effect);
            emitter.Reset(effect, anchor, 1, nowTick, position);
            slots.Add(emitter);
            return new VfxEmitterHandle(this, slots.Count - 1, emitter.Version);
        }

        /// <summary>The instance class the def named, or the plain emitter.</summary>
        private static VfxEmitter CreateEmitter(VfxEffect effect)
        {
            Type type = effect.EmitterType ?? typeof(VfxEmitter);
            if (type == typeof(VfxEmitter))
            {
                return new VfxEmitter();
            }
            try
            {
                return (VfxEmitter)Activator.CreateInstance(type);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[MiliraXian] VFX emitter class '" + type.Name + "' could not be created for '"
                    + effect.Name + "': " + exception, GenText.StableStringHash("MX_Vfx_Emitter_" + type.Name));
                return new VfxEmitter();
            }
        }

        /// <summary>The source's "still here" signal; one call per tick keeps the emitter alive.</summary>
        internal void Maintain(VfxEmitterHandle handle)
        {
            VfxEmitter emitter = Resolve(handle);
            if (emitter == null || (emitter.Effect != null && !emitter.Effect.RequireMaintain))
            {
                return;
            }
            emitter.LastMaintainTick = TickNow();
            emitter.FadeSeconds = 0f;
        }

        internal bool TryGetEmitter<T>(VfxEmitterHandle handle, out T typed) where T : VfxEmitter
        {
            typed = Resolve(handle) as T;
            return typed != null;
        }

        internal void SetChannel(VfxEmitterHandle handle, string channel, float value)
        {
            Resolve(handle)?.SetEmitterChannel(channel, value);
        }

        /// <summary>Writes one emitter-level vec4 channel (a wall's axis, a field's band).</summary>
        internal void SetVectorChannel(VfxEmitterHandle handle, string channel, Vector4 value)
        {
            Resolve(handle)?.SetVectorChannel(channel, value);
        }

        /// <summary>Writes one emitter-level dynamic texture channel (e.g. a captured RenderTexture).</summary>
        internal void SetTextureChannel(VfxEmitterHandle handle, string channel, Texture texture)
        {
            Resolve(handle)?.SetTextureChannel(channel, texture);
        }

        internal float GetChannel(VfxEmitterHandle handle, string channel)
        {
            VfxEmitter emitter = Resolve(handle);
            return emitter == null ? 0f : emitter.EmitterChannel(channel);
        }

        /// <summary>Releases an emitter now, without waiting for its fade.</summary>
        internal void Destroy(VfxEmitterHandle handle)
        {
            if (disposed || handle.Host != this || handle.Slot < 0 || handle.Slot >= slots.Count)
            {
                return;
            }
            VfxEmitter emitter = slots[handle.Slot];
            if (emitter == null || emitter.Version != handle.Version || emitter.Released)
            {
                return;
            }
            Release(handle.Slot);
        }

        internal VfxEmitter Resolve(VfxEmitterHandle handle)
        {
            if (disposed || handle.Host != this || handle.Slot < 0 || handle.Slot >= slots.Count)
            {
                return null;
            }
            VfxEmitter emitter = slots[handle.Slot];
            if (emitter == null || emitter.Version != handle.Version || emitter.Released)
            {
                return null;
            }
            return emitter;
        }

        /// <summary>
        /// Draws every live emitter, stroke by stroke. Runs from <see cref="MapComponentUpdate"/>
        /// for the current map after ticks and dynamic things have updated.
        /// </summary>
        internal void DrawFrame()
        {
            if (disposed)
            {
                return;
            }
            engine ??= new VfxEngine(this);
            assets ??= new VfxAssets(this);
            context = new VfxRenderContext(engine, assets, meshes);
            engine.Frame.Refresh();

            // A batch lives across frames, so its instance list has to be emptied before this frame's
            // strokes append to it; otherwise last frame's instances would be drawn again.
            foreach (VfxInstanceBatch started in batches.Values)
            {
                started.ClearInstances();
            }

            for (int i = 0; i < slots.Count; i++)
            {
                VfxEmitter emitter = slots[i];
                if (emitter == null || emitter.Released)
                {
                    continue;
                }

                VfxEffect effect = emitter.Effect;
                List<VfxPass> passes = effect?.Passes;
                if (passes == null)
                {
                    continue;
                }

                for (int p = 0; p < passes.Count; p++)
                {
                    VfxPass pass = passes[p];
                    if (pass?.Renderer == null)
                    {
                        continue;
                    }
                    try
                    {
                        pass.Renderer.Render(emitter, pass, p, context);
                    }
                    catch (Exception exception)
                    {
                        Log.ErrorOnce("[MiliraXian] VFX '" + effect.Name + "' stroke '" + pass.Name + "' failed to draw: " + exception,
                            GenText.StableStringHash("MX_Vfx_Draw_" + effect.Name + "_" + pass.Name));
                        Release(i);
                        break;
                    }
                }
            }

            // Instanced strokes accumulate across every emitter first; one submission per stroke.
            FlushInstances();
        }

        /// <summary>The batch of a stroke, created on first use.</summary>
        private VfxInstanceBatch BatchFor(VfxPass pass)
        {
            if (!batches.TryGetValue(pass, out VfxInstanceBatch batch))
            {
                batch = new VfxInstanceBatch();
                batches[pass] = batch;
            }
            return batch;
        }

        /// <summary>
        /// The property block of a stroke's instance batch: uniforms written here survive until the
        /// batch is submitted at the end of the frame. Null when the stroke is not instanced.
        /// </summary>
        internal MaterialPropertyBlock InstancedBlock(VfxPass pass)
        {
            if (disposed || pass == null || pass.InstanceVectors <= 0 || pass.Mesh == null)
            {
                return null;
            }
            return BatchFor(pass).Block;
        }

        /// <summary>
        /// Queues one instance of a stroke's shared mesh. Instances accumulate until the frame's draw
        /// pass ends, then every batch is uploaded and submitted once.
        /// </summary>
        internal void AddInstance(VfxPass pass, in Matrix4x4 matrix, Vector4[] row, int rowOffset)
        {
            if (disposed || pass == null)
            {
                return;
            }
            VfxInstanceBatch batch = BatchFor(pass);
            if (batch.Count == 0)
            {
                batch.Begin(pass.InstanceVectors);
            }
            batch.Add(matrix, row, rowOffset);
        }

        /// <summary>
        /// Uploads and submits every accumulated instance batch. Called once the frame's strokes have
        /// all been appended, so the whole batch is still one submission.
        /// </summary>
        private void FlushInstances()
        {
            foreach (KeyValuePair<VfxPass, VfxInstanceBatch> pair in batches)
            {
                VfxInstanceBatch batch = pair.Value;
                if (batch.Count <= 0)
                {
                    continue;
                }

                VfxPass pass = pair.Key;
                Material material = pass.Material;
                if (material == null || pass.Mesh == null)
                {
                    continue;
                }

                batch.Upload();

                MaterialPropertyBlock block = batch.Block;
                block.SetBuffer(InstanceDataId, batch.Buffer);

                // Bloom first: it writes the tint into the block and restores it, so the main draw
                // below sees the stroke's own values.
                ScreenBloomProperties bloom = BloomOf(pass);
                if (bloom != null)
                {
                    ScreenBloom.SubmitInstanced(bloom, pass.Mesh, batch.Matrices, batch.Count, material, block);
                }

                Graphics.DrawMeshInstanced(pass.Mesh, 0, material, batch.Matrices, batch.Count, block,
                    ShadowCastingMode.Off, false, pass.Layer);
            }
        }

        private static readonly int InstanceDataId = Shader.PropertyToID("_InstanceData");

        /// <summary>The bloom settings for one stroke, built once and reused.</summary>
        internal ScreenBloomProperties BloomOf(VfxPass pass)
        {
            if (pass.BloomGroup.NullOrEmpty())
            {
                return null;
            }
            if (blooms.TryGetValue(pass, out ScreenBloomProperties cached))
            {
                return cached;
            }

            ScreenBloomGroupDef group = DefDatabase<ScreenBloomGroupDef>.GetNamedSilentFail(pass.BloomGroup);
            if (group == null)
            {
                Log.ErrorOnce("[MiliraXian] VFX bloom group is missing: " + pass.BloomGroup,
                    GenText.StableStringHash("MX_Vfx_Bloom_" + pass.BloomGroup));
                blooms[pass] = null;
                return null;
            }

            ScreenBloomProperties bloom = new ScreenBloomProperties
            {
                group = group,
                tint = pass.BloomTint,
                intensity = pass.BloomIntensity,
            };
            blooms[pass] = bloom;
            return bloom;
        }

        /// <summary>
        /// One frame: step ages, expire emitters whose source stopped maintaining them, then destroy
        /// the meshes that were queued on an earlier frame.
        /// </summary>
        internal void TickFrame()
        {
            if (disposed)
            {
                return;
            }

            float nowTicks = VisualClock.Ticks;
            float delta = hasTicks ? Mathf.Clamp((nowTicks - lastTicks) / 60f, 0f, 0.1f) : 0f;
            lastTicks = nowTicks;
            hasTicks = true;
            int nowTick = TickNow();

            for (int i = 0; i < slots.Count; i++)
            {
                VfxEmitter emitter = slots[i];
                if (emitter == null || emitter.Released)
                {
                    continue;
                }
                if (!emitter.Advance(delta, nowTick, map))
                {
                    Release(i);
                }
            }

            meshes.Sweep();
        }

        /// <summary>
        /// Unconditionally releases every emitter and every mesh. Called when the map goes away
        /// (component dispose or map removal), where no further frame will run. Idempotent.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            for (int i = 0; i < slots.Count; i++)
            {
                VfxEmitter emitter = slots[i];
                if (emitter == null)
                {
                    continue;
                }
                Mesh[] meshesOf = emitter.Meshes;
                if (meshesOf != null)
                {
                    for (int m = 0; m < meshesOf.Length; m++)
                    {
                        if (meshesOf[m] != null)
                        {
                            UnityEngine.Object.Destroy(meshesOf[m]);
                            meshesOf[m] = null;
                        }
                    }
                }
                if (!emitter.Released)
                {
                    emitter.Clear();
                }
            }
            slots.Clear();
            freeSlots.Clear();
            meshes.Dispose();
            foreach (VfxInstanceBatch batch in batches.Values)
            {
                batch.Dispose();
            }
            batches.Clear();
            blooms.Clear();
            engine = null;
            assets = null;
        }

        private void Release(int slot)
        {
            VfxEmitter emitter = slots[slot];
            RetireMeshes(emitter);
            emitter.Clear();
            freeSlots.Add(slot);
        }

        /// <summary>
        /// Queues every scratch mesh of an emitter for destruction. They may have been drawn earlier
        /// in this frame and the frame's draw commands are only consumed at its end, so this must go
        /// through the generation queue rather than destroy now.
        /// </summary>
        private void RetireMeshes(VfxEmitter emitter)
        {
            Mesh[] meshesOf = emitter.Meshes;
            if (meshesOf == null)
            {
                return;
            }
            for (int i = 0; i < meshesOf.Length; i++)
            {
                if (meshesOf[i] != null)
                {
                    meshes.Retire(meshesOf[i]);
                    meshesOf[i] = null;
                }
            }
        }

        private static int TickNow()
        {
            return Find.TickManager?.TicksGame ?? 0;
        }
    }
}
