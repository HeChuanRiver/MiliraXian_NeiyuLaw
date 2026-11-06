using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx
{
    /// <summary>
    /// One bloom group: a layer of its own plus one set of blur parameters. Glowing objects only
    /// name a group; they never carry bloom parameters themselves.
    /// </summary>
    public class ScreenBloomGroupDef : Def
    {
        /// <summary>The scene image is 1/N of the screen, so the halo size is in screen pixels.</summary>
        public int sceneDivisor = 2;
        public float threshold = 0.02f;
        public float knee = 0.02f;
        /// <summary>
        /// Pyramid levels, the base included. Level i is 1/2^i of the scene image, so the widest
        /// part of the halo reaches about 2^levels * sceneDivisor screen pixels.
        /// </summary>
        public int levels = 6;
        /// <summary>
        /// How much of each coarser level is mixed into the finer one on the way up (0..1). Higher
        /// moves the light outward into a wider, softer halo; lower keeps it tight around the
        /// object. The total brightness does not change.
        /// </summary>
        public float scatter = 0.7f;
        /// <summary>Spread of the upsample tent in coarse texels; 1 is the standard kernel.</summary>
        public float blurRadius = 1f;
        public float gain = 1f;

        internal int layer = -1;
    }

    /// <summary>
    /// Screen-space bloom for self-drawn glowing objects. Every object submits its mesh once more
    /// to its group's layer; <see cref="ScreenBloomFeature"/> then renders each group that received
    /// anything into a screen-sized scene image, reduces it 4x in a single pass and adds the blurred
    /// halo over the whole map. The cost depends on the size of those images, not on how many
    /// objects glow.
    /// </summary>
    /// <remarks>
    /// The group layers are routed to the engine's capture-only pass, so the submitted meshes never
    /// appear in the main view themselves; the render feature picks them out of the frame's draw
    /// snapshot by layer.
    /// </remarks>
    [StaticConstructorOnStartup]
    public static class ScreenBloom
    {
        private const string LayerNamePrefix = "MX_ScreenBloom_";

        private static readonly int BloomTintId = Shader.PropertyToID("_BloomTint");
        private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly ScreenBloomGroupDef[] GroupsByLayer = new ScreenBloomGroupDef[32];

        private static bool layersReady;
        private static bool disabled;
        private static int pendingFrame = -1;
        private static int pendingMask;

        /// <summary>
        /// Draws <paramref name="mesh"/> into the group's layer so the bloom pass picks it up. Call it
        /// right next to the object's normal draw, while <paramref name="block"/> still holds its
        /// colour/alpha and any per-draw data; the block is copied by the call.
        /// </summary>
        /// <remarks>
        /// <paramref name="bloom"/>'s tint and intensity are written into the block just for this
        /// submission and restored afterwards, so the object's own draw is unaffected. That is safe
        /// because the draw call copies the block: whatever it holds when the call is made is what
        /// the halo is rendered with.
        /// </remarks>
        public static void Submit(
            ScreenBloomProperties bloom,
            Mesh mesh,
            Matrix4x4 matrix,
            Material material,
            MaterialPropertyBlock block)
        {
            // The mod setting turns the whole feature off: with nothing submitted, no layer is
            // allocated and the render feature stays inactive.
            if (NeiyuLawMod.Instance?.Settings?.EnableScreenBloom == false)
            {
                return;
            }

            // The gravship capture camera draws every layer the main camera does, so a halo
            // submitted while it renders would end up inside the gravship image.
            ScreenBloomGroupDef group = bloom?.group;
            if (group == null || mesh == null || material == null
                || GravshipCapturer.IsGravshipRenderInProgress || !EnsureLayers() || group.layer < 0)
            {
                return;
            }

            block.SetColor(BloomTintId, bloom.tint);
            block.SetFloat(BloomIntensityId, bloom.intensity);
            Graphics.DrawMesh(mesh, matrix, material, group.layer, null, 0, block);
            block.SetColor(BloomTintId, Color.white);
            block.SetFloat(BloomIntensityId, 1f);

            int frame = Time.frameCount;
            if (pendingFrame != frame)
            {
                // Submissions of an earlier frame are gone with that frame's draw commands.
                pendingFrame = frame;
                pendingMask = 0;
            }
            pendingMask |= 1 << group.layer;
        }

        /// <summary>
        /// Called by the render feature on the main thread when the frame is captured: returns the
        /// layers that received glows this frame and clears them.
        /// </summary>
        internal static bool ConsumeSubmissions(out int layerMask)
        {
            layerMask = pendingFrame == Time.frameCount ? pendingMask : 0;
            pendingMask = 0;
            pendingFrame = -1;
            return layerMask != 0 && !disabled;
        }

        internal static ScreenBloomGroupDef GroupForLayer(int layer) =>
            (uint)layer < (uint)GroupsByLayer.Length ? GroupsByLayer[layer] : null;

        // Layers are allocated for every group up front and registered with one call: the router
        // has only 8 slots and does not deduplicate, so one slot per group would waste them.
        private static bool EnsureLayers()
        {
            if (layersReady)
            {
                return !disabled;
            }
            layersReady = true;

            try
            {
                int mask = 0;
                foreach (ScreenBloomGroupDef def in DefDatabase<ScreenBloomGroupDef>.AllDefs)
                {
                    int layer = LayerMask.NameToLayer(LayerNamePrefix + def.defName);
                    if (layer >= 0)
                    {
                        def.layer = layer;
                        GroupsByLayer[layer] = def;
                        mask |= 1 << layer;
                    }
                }
                if (mask != 0)
                {
                    RouteToCaptureOnly(mask);
                }
            }
            catch (Exception ex)
            {
                disabled = true;
                Log.Error("[MiliraXian] Screen bloom could not allocate its layers and is disabled: " + ex);
            }
            return !disabled;
        }

        /// <remarks>
        /// Kept out of <see cref="EnsureLayers"/> so that a changed Waylith member fails when this
        /// is called, inside the try there, instead of when EnsureLayers itself is compiled, which
        /// would throw into the glowing object's draw code.
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RouteToCaptureOnly(int mask) =>
            Waylith.Rendering.LayerPassRouter.RegisterLayerMask(mask, Waylith.Rendering.RenderPassRegistry.Register("CaptureOnly"));
    }
}
