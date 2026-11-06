using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Brutal.VulkanApi;
using RimWorld.Planet;
using UnityStubs.Rendering;
using Verse;
using Waylith;
using Waylith.Graphics;
using Waylith.Rendering;
using Waylith.Rendering.PostFx;

namespace MiliraXian.Characters.Common.Vfx
{
    /// <summary>
    /// Records the screen bloom inside Waylith's own frame pipeline instead of going through the
    /// Unity compatibility layer (spare camera, Graphics.Blit, a composite mesh). Nothing here
    /// touches RenderTexture.active or any other emulated Unity state, and the whole effect is part
    /// of the frame's command buffer, so it costs no blocking GPU submits.
    /// </summary>
    /// <remarks>
    /// Main thread (<see cref="CaptureFrame"/>): picks this frame's bloom draws out of the mesh
    /// submission snapshot, turns them into draw items and copies every group parameter into the
    /// payload. Render thread, before the backbuffer: each group's draws are rendered into a
    /// half-float scene image, then blurred with a mip pyramid (Jimenez, "Next Generation Post
    /// Processing in Call of Duty: Advanced Warfare"): a thresholded base level at scene size,
    /// halved level by level with a 13-tap filter, and accumulated back up with a 3x3 tent, each
    /// level mixed with the one below by <see cref="ScreenBloomGroupDef.scatter"/>. Render thread,
    /// inside the backbuffer: the base of the accumulation is tent-filtered once more and added on
    /// top. Every magnification is a filtered 2x step, so no level shows its texel grid. The
    /// feature sits right after the game's PostFx composite, so the halo covers the map and stays
    /// under the UI. Targets are per frame in flight, like the game's scene targets, so the GPU
    /// never reads an image the next frame is writing.
    /// </remarks>
    internal sealed class ScreenBloomFeature : IRenderFeature, IDisposable
    {
        private const int MinTargetSize = 16;
        private const int MaxLevels = 8;
        private const VkFormat SceneFormat = VkFormat.R16G16B16A16SFloat;

        [StructLayout(LayoutKind.Sequential)]
        private struct PrefilterParams
        {
            public float threshold;
            public float knee;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UpsampleParams
        {
            public float scatter;
            public float radius;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CompositeParams
        {
            public float gain;
            public float radius;
        }

        /// <summary>Everything one group needs on the render thread, copied on the main thread.</summary>
        private sealed class GroupFrame
        {
            public ScreenBloomGroupDef group;
            public int sceneDivisor;
            public int levels;
            public PrefilterParams prefilter;
            public UpsampleParams upsample;
            public CompositeParams composite;
            public readonly List<FinalDrawItem> items = new();
        }

        private sealed class BloomFrame : IDisposable
        {
            public int frameNumber;
            public CameraView view;
            public readonly List<GroupFrame> groups = new();

            public void Dispose()
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    List<FinalDrawItem> items = groups[i].items;
                    for (int j = 0; j < items.Count; j++)
                    {
                        FinalDrawItem.Return(items[j]);
                    }
                    items.Clear();
                }
            }
        }

        /// <summary>One frame-in-flight slot of a group's images.</summary>
        private sealed class SlotImages
        {
            public Waylith.Rendering.RenderTexture scene;
            /// <summary>down[0] is the thresholded base level (scene size); down[i] is half of down[i-1].</summary>
            public Waylith.Rendering.RenderTexture[] down;
            /// <summary>up[i] has the size of down[i] and holds levels i..n accumulated.</summary>
            public Waylith.Rendering.RenderTexture[] up;

            public void Dispose()
            {
                scene?.Dispose();
                for (int i = 0; i < down.Length; i++)
                {
                    down[i].Dispose();
                }
                for (int i = 0; i < up.Length; i++)
                {
                    up[i].Dispose();
                }
            }
        }

        /// <summary>GPU resources of one group; only the render thread touches them.</summary>
        /// <remarks>
        /// Each pass object is recorded at most once per frame: <see cref="PostEffectPass.Record"/>
        /// resets the pass's descriptor pool, which would free the sets of an earlier recording of
        /// the same frame before the GPU has run them. Hence one pass object per pyramid level.
        /// </remarks>
        private sealed class GroupTargets : IDisposable
        {
            public PostEffectPass prefilter;
            public PostEffectPass[] downsample = Array.Empty<PostEffectPass>();
            public PostEffectPass[] upsample = Array.Empty<PostEffectPass>();
            public PostEffectPass composite;
            public SlotImages[] slots = Array.Empty<SlotImages>();
            public PassAttachment sceneAttachment;
            public int width;
            public int height;
            public int levels;

            public void DisposeTargets()
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    slots[i].Dispose();
                }
                slots = Array.Empty<SlotImages>();
                sceneAttachment = null;
                width = height = levels = 0;
            }

            public void DisposePasses()
            {
                prefilter?.Dispose();
                for (int i = 0; i < downsample.Length; i++)
                {
                    downsample[i].Dispose();
                }
                for (int i = 0; i < upsample.Length; i++)
                {
                    upsample[i].Dispose();
                }
                composite?.Dispose();
                prefilter = composite = null;
                downsample = upsample = Array.Empty<PostEffectPass>();
            }

            public void Dispose()
            {
                DisposeTargets();
                DisposePasses();
            }
        }

        /// <summary>Draws one group's halo into the open backbuffer render pass.</summary>
        private sealed class InlineCompositePass : PostEffectPass
        {
            public override bool PreBackbuffer => false;

            public InlineCompositePass(string name)
                : base(name, "MX_ScreenBloomComposite.frag.glsl", new int[] { 0 }, BlendState.Additive, 0, 1, 1)
            {
            }
        }

        private sealed class ScenePass : IRenderPass
        {
            private readonly ScreenBloomFeature owner;
            public ScenePass(ScreenBloomFeature owner) => this.owner = owner;
            public byte PassId { get; } = RenderPassRegistry.Register("MX_ScreenBloomScene");
            public PassAttachment Attachment => null;
            public bool PreBackbuffer => true;
            public void Record(in FrameContext ctx, object featurePayload) => owner.GuardedRecordScene(in ctx, featurePayload as BloomFrame);
            public void OnRenderTargetRebuilt(IRenderTarget target) { }
            public void Dispose() { }
        }

        private sealed class CompositeStage : IRenderPass
        {
            private readonly ScreenBloomFeature owner;
            public CompositeStage(ScreenBloomFeature owner) => this.owner = owner;
            public byte PassId { get; } = RenderPassRegistry.Register("MX_ScreenBloomComposite");
            public PassAttachment Attachment => null;
            public bool PreBackbuffer => false;
            public void Record(in FrameContext ctx, object featurePayload) => owner.GuardedRecordComposite(in ctx, featurePayload as BloomFrame);
            public void OnRenderTargetRebuilt(IRenderTarget target) { }
            public void Dispose() { }
        }

        private readonly IRenderPass[] passes;
        private readonly Dictionary<ScreenBloomGroupDef, GroupTargets> targets = new();
        private readonly Dictionary<int, GroupFrame> scratchByLayer = new();
        private MeshBatchRenderer renderer;
        private VkRenderPass sceneRenderPass;
        private VkFormat sceneDepthFormat = VkFormat.Undefined;
        private Brutal.VulkanApi.Abstractions.DeviceEx device;
        private bool isActive;
        private volatile bool failed;

        public bool IsActive => isActive;

        public IReadOnlyList<IRenderPass> Passes => passes;

        public ScreenBloomFeature()
        {
            passes = new IRenderPass[] { new ScenePass(this), new CompositeStage(this) };
        }

        // ---------------------------------------------------------------- engine entry points
        //
        // The engine calls these every frame. Each only forwards to a NoInlining worker inside a
        // try, so if a game update changes a Waylith member the worker uses, the JIT failure
        // (MissingMethodException, TypeLoadException, ...) is raised at the call inside the try:
        // the bloom reports once and turns itself off instead of taking down the frame loop.
        // Keep these bodies free of engine calls for the same reason.

        public object CaptureFrame(in FrameTiming timing)
        {
            if (failed)
            {
                isActive = false;
                return null;
            }
            try
            {
                return CaptureFrameCore(in timing);
            }
            catch (Exception ex)
            {
                isActive = false;
                Fail("capturing the frame", ex);
                return null;
            }
        }

        private void GuardedRecordScene(in FrameContext ctx, BloomFrame frame)
        {
            if (frame == null || failed)
            {
                return;
            }
            try
            {
                RecordScene(in ctx, frame);
            }
            catch (Exception ex)
            {
                Fail("recording the scene pass", ex);
            }
        }

        private void GuardedRecordComposite(in FrameContext ctx, BloomFrame frame)
        {
            if (frame == null || failed)
            {
                return;
            }
            try
            {
                RecordComposite(in ctx, frame);
            }
            catch (Exception ex)
            {
                Fail("recording the composite pass", ex);
            }
        }

        public void OnRenderTargetRebuilt(IRenderTarget renderTarget)
        {
            try
            {
                OnRenderTargetRebuiltCore(renderTarget);
            }
            catch (Exception ex)
            {
                Fail("handling a render target rebuild", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                DisposeCore();
            }
            catch (Exception ex)
            {
                // Shutdown: nothing to turn off any more, just leave a trace.
                Log.Warning("[MiliraXian] Screen bloom failed to release its GPU resources: " + ex);
            }
        }

        // ---------------------------------------------------------------- main thread

        [MethodImpl(MethodImplOptions.NoInlining)]
        private object CaptureFrameCore(in FrameTiming timing)
        {
            isActive = false;
            if (failed || !ScreenBloom.ConsumeSubmissions(out int layerMask))
            {
                return null;
            }

            UnityEngine.Camera camera = Find.Camera;
            if (camera == null || !WorldRendererUtility.DrawingMap || Find.CurrentMap == null)
            {
                return null;
            }

            DrawItemFactory maker = MeshDrawSubmission.DrawItemMaker;
            GraphicsDevice graphicsDevice = EngineApplication.Instance?.GraphicsDevice;
            if (maker == null || graphicsDevice == null)
            {
                return null;
            }

            BloomFrame frame = null;
            try
            {
                // The map feature has already taken this frame's snapshot; this returns the same one.
                MeshDrawSubmissionSnapshot snapshot = MeshDrawSubmission.CaptureFlushedSnapshot(graphicsDevice);
                MeshDrawCommand[] commands = snapshot.Commands;
                string keywordKey = UnityEngine.Shader.GlobalKeywordKey;

                frame = new BloomFrame
                {
                    frameNumber = timing.FrameNumber,
                    view = CameraViewFactory.GetView(camera),
                };
                scratchByLayer.Clear();
                for (int i = 0; i < snapshot.CommandCount; i++)
                {
                    int layer = commands[i].Layer;
                    if ((uint)layer >= 32u || (layerMask & (1 << layer)) == 0)
                    {
                        continue;
                    }
                    if (!scratchByLayer.TryGetValue(layer, out GroupFrame groupFrame))
                    {
                        ScreenBloomGroupDef group = ScreenBloom.GroupForLayer(layer);
                        if (group == null)
                        {
                            continue;
                        }
                        groupFrame = new GroupFrame
                        {
                            group = group,
                            sceneDivisor = Math.Max(1, group.sceneDivisor),
                            levels = Math.Clamp(group.levels, 1, MaxLevels),
                            prefilter = new PrefilterParams { threshold = group.threshold, knee = group.knee },
                            upsample = new UpsampleParams { scatter = Math.Clamp(group.scatter, 0f, 1f), radius = group.blurRadius },
                            composite = new CompositeParams { gain = group.gain, radius = 1f },
                        };
                        scratchByLayer[layer] = groupFrame;
                        frame.groups.Add(groupFrame);
                    }
                    FinalDrawItem item = maker.TryMake(in commands[i], snapshot.PropertyArena, keywordKey, i, null, 0);
                    if (item != null)
                    {
                        groupFrame.items.Add(item);
                    }
                }
                scratchByLayer.Clear();

                for (int i = frame.groups.Count - 1; i >= 0; i--)
                {
                    if (frame.groups[i].items.Count == 0)
                    {
                        frame.groups.RemoveAt(i);
                    }
                    else
                    {
                        frame.groups[i].items.Sort(DrawSortKeyComparer.Instance);
                    }
                }
                if (frame.groups.Count == 0)
                {
                    frame.Dispose();
                    return null;
                }
            }
            catch (Exception ex)
            {
                frame?.Dispose();
                Fail("capturing the frame", ex);
                return null;
            }

            isActive = true;
            return frame;
        }

        // ---------------------------------------------------------------- render thread

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RecordScene(in FrameContext ctx, BloomFrame frame)
        {
            renderer ??= new MeshBatchRenderer(ctx.GraphicsDevice, ctx.RenderContext);
            EnsureSceneRenderPass(in ctx);
            renderer.BeginFrame(in ctx, frame.frameNumber);

            int slot = ctx.SubmissionFrameIndex;
            for (int g = 0; g < frame.groups.Count; g++)
            {
                GroupFrame groupFrame = frame.groups[g];
                GroupTargets t = EnsureTargets(in ctx, groupFrame);
                SlotImages images = t.slots[slot % t.slots.Length];

                RecordSceneImage(in ctx, t.sceneAttachment, groupFrame.items, frame.view);
                images.scene.Color.SetMipLayout(0, VkImageLayout.ShaderReadOnlyOptimal);
                RecordSampleBarrier(ctx.CommandBuffer, images.scene);

                // Down: scene -> down[0] (threshold) -> down[1] -> ... -> down[n-1].
                RunPass(in ctx, t.prefilter, images.down[0], images.scene, null, in groupFrame.prefilter);
                for (int i = 1; i < t.levels; i++)
                {
                    RunPass(in ctx, t.downsample[i - 1], images.down[i], images.down[i - 1], null, default(PrefilterParams), writeUniforms: false);
                }

                // Up: up[i] = mix(down[i], tent(up[i+1]), scatter), starting from the coarsest
                // level, which is its own accumulation.
                for (int i = t.levels - 2; i >= 0; i--)
                {
                    Waylith.Rendering.RenderTexture coarse = i + 1 == t.levels - 1 ? images.down[i + 1] : images.up[i + 1];
                    RunPass(in ctx, t.upsample[i], images.up[i], coarse, images.down[i], in groupFrame.upsample);
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RecordComposite(in FrameContext ctx, BloomFrame frame)
        {
            int slot = ctx.SubmissionFrameIndex;
            for (int g = 0; g < frame.groups.Count; g++)
            {
                GroupFrame groupFrame = frame.groups[g];
                if (!targets.TryGetValue(groupFrame.group, out GroupTargets t) || t.slots.Length == 0)
                {
                    continue;
                }
                SlotImages images = t.slots[slot % t.slots.Length];
                Waylith.Rendering.RenderTexture halo = t.levels > 1 ? images.up[0] : images.down[0];
                t.composite.SetInput(0, halo.Color, halo.Sampler);
                t.composite.SetUniforms(in groupFrame.composite);
                t.composite.RecordInline(in ctx, ctx.RenderPass, ctx.SampleCount);
            }
        }

        /// <summary>
        /// Makes the scene image's colour writes visible to the prefilter's sampling.
        /// </summary>
        /// <remarks>
        /// The scene render pass already ends with an external dependency to fragment-shader reads
        /// (<see cref="PostFxPassHelpers.CreateColorRenderPass"/>), which suffices elsewhere; the game
        /// adds this explicit barrier on macOS only (SceneColorTargets.RecordSampleBarrier, also in
        /// PostEffectPass), presumably because MoltenVK does not honour the subpass dependency. The
        /// same is done here for the same image transition.
        /// </remarks>
        private static void RecordSampleBarrier(CommandBuffer cmd, Waylith.Rendering.RenderTexture target)
        {
            if (!Brutal.Platform.IsMacOS)
            {
                return;
            }
            VkImageMemoryBarrier barrier = new VkImageMemoryBarrier
            {
                SrcAccessMask = VkAccessFlags.ColorAttachmentWriteBit,
                DstAccessMask = VkAccessFlags.ShaderReadBit,
                OldLayout = VkImageLayout.ShaderReadOnlyOptimal,
                NewLayout = VkImageLayout.ShaderReadOnlyOptimal,
                SrcQueueFamilyIndex = -1,
                DstQueueFamilyIndex = -1,
                Image = target.Color.Image,
                SubresourceRange = new VkImageSubresourceRange
                {
                    AspectMask = VkImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
            };
            cmd.PipelineBarrier(VkPipelineStageFlags.ColorAttachmentOutputBit, VkPipelineStageFlags.FragmentShaderBit,
                VkDependencyFlags.None, default(ReadOnlySpan<VkMemoryBarrier>), default(ReadOnlySpan<VkBufferMemoryBarrier>),
                new ReadOnlySpan<VkImageMemoryBarrier>(ref barrier));
        }

        private unsafe void RecordSceneImage(in FrameContext main, PassAttachment attachment, List<FinalDrawItem> items, CameraView view)
        {
            FrameContext ctx = attachment.MakeFrameContext(in main);
            CommandBuffer cmd = ctx.CommandBuffer;
            VkClearValue[] clearValues = attachment.ClearValues;
            fixed (VkClearValue* clear = clearValues)
            {
                VkRenderPassBeginInfo begin = new VkRenderPassBeginInfo
                {
                    RenderPass = ctx.RenderPass,
                    Framebuffer = ctx.Framebuffer,
                    RenderArea = new VkRect2D(ctx.Extent),
                    ClearValueCount = clearValues.Length,
                    ClearValues = clear,
                };
                cmd.BeginRenderPass(in begin, VkSubpassContents.Inline);
            }
            VkExtent2D extent = ctx.Extent;
            VkViewport viewport = new VkViewport
            {
                X = 0f,
                Y = 0f,
                Width = extent.Width,
                Height = extent.Height,
                MinDepth = 0f,
                MaxDepth = 1f,
            };
            VkRect2D scissor = new VkRect2D(extent);
            cmd.SetViewport(0, new ReadOnlySpan<VkViewport>(ref viewport));
            cmd.SetScissor(0, new ReadOnlySpan<VkRect2D>(ref scissor));
            renderer.RecordBatch(in ctx, items, view);
            cmd.EndRenderPass();
        }

        /// <summary>
        /// Records one fullscreen pass from <paramref name="source"/> (and <paramref name="detail"/>,
        /// binding 1, if given) into <paramref name="output"/>.
        /// </summary>
        private static void RunPass<T>(in FrameContext ctx, PostEffectPass pass, Waylith.Rendering.RenderTexture output,
            Waylith.Rendering.RenderTexture source, Waylith.Rendering.RenderTexture detail, in T uniforms, bool writeUniforms = true)
            where T : unmanaged
        {
            pass.Output = output;
            pass.SetInput(0, source.Color, source.Sampler);
            if (detail != null)
            {
                pass.SetInput(1, detail.Color, detail.Sampler);
            }
            if (writeUniforms)
            {
                pass.SetUniforms(in uniforms);
            }
            pass.Record(in ctx, null);
        }

        private static Waylith.Rendering.RenderTexture ColorTarget(int width, int height, string name) =>
            new(width, height, SceneFormat, VkFormat.Undefined, RenderTextureFilter.Bilinear, RenderTextureWrap.Clamp, false, name);

        /// <summary>One pass object per pyramid level; see <see cref="GroupTargets"/> for why.</summary>
        private static void EnsureLevelPasses(GroupTargets t, ScreenBloomGroupDef group, int levels)
        {
            string name = group.defName;
            t.prefilter ??= new PostEffectPass("MX_ScreenBloomPrefilter_" + name, "MX_ScreenBloomPrefilter.frag.glsl", new int[] { 0 }, default, 0, 1, 1);
            t.composite ??= new InlineCompositePass("MX_ScreenBloomComposite_" + name);
            if (t.downsample.Length == levels - 1)
            {
                return;
            }
            for (int i = 0; i < t.downsample.Length; i++)
            {
                t.downsample[i].Dispose();
            }
            for (int i = 0; i < t.upsample.Length; i++)
            {
                t.upsample[i].Dispose();
            }
            t.downsample = new PostEffectPass[levels - 1];
            t.upsample = new PostEffectPass[levels - 1];
            for (int i = 0; i < levels - 1; i++)
            {
                t.downsample[i] = new PostEffectPass($"MX_ScreenBloomDown{i + 1}_{name}", "MX_ScreenBloomDownsample.frag.glsl", new int[] { 0 }, default, 0, 1, -1);
                t.upsample[i] = new PostEffectPass($"MX_ScreenBloomUp{i}_{name}", "MX_ScreenBloomUpsample.frag.glsl", new int[] { 0, 1 }, default, 0, 1, 2);
            }
        }

        private void EnsureSceneRenderPass(in FrameContext ctx)
        {
            VkFormat depthFormat = ctx.DepthFormat;
            if (!sceneRenderPass.IsNull() && depthFormat == sceneDepthFormat)
            {
                return;
            }
            device = ctx.Device;
            if (!sceneRenderPass.IsNull())
            {
                foreach (GroupTargets t in targets.Values)
                {
                    t.DisposeTargets();
                }
                device.DestroyRenderPass(sceneRenderPass, device.VkAllocator);
            }
            sceneDepthFormat = depthFormat;
            sceneRenderPass = PostFxPassHelpers.CreateColorRenderPass(device, SceneFormat, depthFormat, VkAttachmentLoadOp.Clear);
        }

        private GroupTargets EnsureTargets(in FrameContext ctx, GroupFrame groupFrame)
        {
            ScreenBloomGroupDef group = groupFrame.group;
            if (!targets.TryGetValue(group, out GroupTargets t))
            {
                t = new GroupTargets();
                targets[group] = t;
            }

            VkExtent2D screen = ctx.Extent;
            int height = Math.Max(MinTargetSize, (int)Math.Round(screen.Height / (double)groupFrame.sceneDivisor));
            int width = Math.Max(MinTargetSize, (int)Math.Round(screen.Width / (double)groupFrame.sceneDivisor));
            // Stop halving before a level gets thinner than a few texels; the tent would only smear
            // the clamped border then.
            int levels = 1;
            for (int w = width, h = height; levels < groupFrame.levels && Math.Min(w, h) / 2 >= 4; levels++)
            {
                w /= 2;
                h /= 2;
            }
            int frames = Math.Max(1, ctx.RenderContext.FramesInFlight);
            if (t.slots.Length == frames && t.width == width && t.height == height && t.levels == levels)
            {
                return t;
            }

            // Sizes only change after the swapchain was rebuilt, when the frames using the old
            // images have finished.
            t.DisposeTargets();
            EnsureLevelPasses(t, group, levels);
            t.width = width;
            t.height = height;
            t.levels = levels;
            t.slots = new SlotImages[frames];
            VkFramebuffer[] framebuffers = new VkFramebuffer[frames];
            VkImageView[] depthViews = new VkImageView[frames];
            for (int i = 0; i < frames; i++)
            {
                SlotImages images = new SlotImages
                {
                    scene = new Waylith.Rendering.RenderTexture(width, height, SceneFormat, sceneDepthFormat,
                        RenderTextureFilter.Bilinear, RenderTextureWrap.Clamp, false, $"MX_ScreenBloomScene_{group.defName}_{i}"),
                    down = new Waylith.Rendering.RenderTexture[levels],
                    up = new Waylith.Rendering.RenderTexture[Math.Max(0, levels - 1)],
                };
                int w = width, h = height;
                for (int level = 0; level < levels; level++)
                {
                    if (level > 0)
                    {
                        w = Math.Max(1, w / 2);
                        h = Math.Max(1, h / 2);
                    }
                    images.down[level] = ColorTarget(w, h, $"MX_ScreenBloomDown{level}_{group.defName}_{i}");
                    if (level < levels - 1)
                    {
                        images.up[level] = ColorTarget(w, h, $"MX_ScreenBloomUp{level}_{group.defName}_{i}");
                    }
                }
                t.slots[i] = images;
                framebuffers[i] = images.scene.GetFramebuffer(sceneRenderPass);
                depthViews[i] = images.scene.DepthView;
            }
            VkClearValue[] clear =
            {
                new VkClearColorValue { Float32 = new Brutal.Numerics.float4(0f, 0f, 0f, 0f) },
                new VkClearDepthStencilValue { Depth = 0f, Stencil = 0 },
            };
            t.sceneAttachment = new PassAttachment(sceneRenderPass, framebuffers, depthViews, new VkExtent2D(width, height),
                clear, VkSampleCountFlags._1Bit, SceneFormat, sceneDepthFormat);
            return t;
        }

        /// <remarks>
        /// The frame renderer notifies features, not their passes, and calls this while the render
        /// thread is idle, so the GPU resources can be touched here.
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OnRenderTargetRebuiltCore(IRenderTarget renderTarget)
        {
            if (renderTarget is IRenderContext renderContext)
            {
                renderer?.OnRenderContextRebuilt(renderContext);
            }
            foreach (GroupTargets t in targets.Values)
            {
                // The composite pipeline was built against the backbuffer render pass, which was
                // just replaced; the scene images are resized on the next frame.
                t.composite.Invalidate();
            }
        }

        private void Fail(string stage, Exception ex)
        {
            // A per-frame failure would flood the log; report once and stop drawing the halo.
            if (failed)
            {
                return;
            }
            failed = true;
            Log.Error($"[MiliraXian] Screen bloom failed while {stage} and is disabled: {ex}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DisposeCore()
        {
            foreach (GroupTargets t in targets.Values)
            {
                t.Dispose();
            }
            targets.Clear();
            renderer?.Dispose();
            renderer = null;
            if (!sceneRenderPass.IsNull() && device != null)
            {
                device.DestroyRenderPass(sceneRenderPass, device.VkAllocator);
                sceneRenderPass = default;
            }
        }

        object IRenderFeature.CaptureFrame(in FrameTiming timing) => CaptureFrame(in timing);
    }

    /// <summary>
    /// Registers the bloom feature once the engine's frame renderer is configured, right after the
    /// game's PostFx composite and therefore before the UI.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ScreenBloomFeatureRegistration
    {
        static ScreenBloomFeatureRegistration()
        {
            // Same reasoning as the feature's entry points: the engine calls live in a NoInlining
            // method so a changed Waylith member fails inside this try.
            try
            {
                Register();
            }
            catch (Exception ex)
            {
                Log.Error("[MiliraXian] Screen bloom could not register its render feature: " + ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register()
        {
            FrameRenderer frameRenderer = EngineApplication.Instance?.FrameRenderer;
            if (frameRenderer == null || EngineApplication.Instance.Options.Headless)
            {
                return;
            }

            int index = frameRenderer.GetFeatureCount();
            for (int i = 0; i < frameRenderer.GetFeatureCount(); i++)
            {
                if (frameRenderer.GetFeatureAt(i).GetType().Name == "PostFxFeature")
                {
                    index = i + 1;
                    break;
                }
            }
            frameRenderer.InsertFeature(index, new ScreenBloomFeature());
        }
    }
}
