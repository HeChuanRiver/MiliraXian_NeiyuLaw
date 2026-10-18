using System;
using System.Collections.Generic;
using HarmonyLib;
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
        /// <summary>The scene image is 1/N of the screen, so the blur radius is in screen pixels.</summary>
        public int sceneDivisor = 2;
        public float threshold = 0.02f;
        public float knee = 0.02f;
        /// <summary>
        /// Scales the blur kernel, which is sampled on a halo image 1/4 of the scene image. At 1 its
        /// standard deviation is about 1.15 halo pixels, i.e. 4.6 * sceneDivisor screen pixels.
        /// </summary>
        public float blurRadius = 1f;
        public float gain = 1f;

        internal int layer = -1;
    }

    /// <summary>
    /// Screen-space bloom for self-drawn glowing objects. Every object submits its mesh once more
    /// to its group's layer; after all dynamic things of the frame are drawn, each group that
    /// received anything is rendered once into a screen-sized scene image, reduced 4x in a single
    /// pass, and drawn back as one additive quad over the whole view whose shader does the blur.
    /// The cost depends on the size of those images, not on how many objects glow.
    /// </summary>
    /// <remarks>
    /// The group layer is routed to the engine's capture-only pass, so the submitted meshes never
    /// appear in the main view themselves; only the spare camera renders them. Every render and
    /// every blit is a blocking GPU submit in this engine, and every new render target costs a depth
    /// image, a framebuffer and one more blocking submit, so the targets are persistent and a frame
    /// issues exactly two submits per group. The scene image is only read by the blit inside the
    /// same flush, so it is single; the halo image is read by the deferred composite quad, which
    /// the GPU may still be executing while the next frame's flush writes, so it is double buffered
    /// by frame parity.
    /// </remarks>
    [StaticConstructorOnStartup]
    public static class ScreenBloom
    {
        private const int MinTargetSize = 16;
        private const int HaloReduction = 4;
        private const string LayerNamePrefix = "MX_ScreenBloom_";

        private sealed class GroupState
        {
            public RenderTexture scene;
            public readonly RenderTexture[] halo = new RenderTexture[2];
        }

        private static readonly Dictionary<ScreenBloomGroupDef, GroupState> States = new();
        private static readonly List<ScreenBloomGroupDef> Pending = new();
        private static readonly MaterialPropertyBlock CompositeBlock = new();

        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int KneeId = Shader.PropertyToID("_Knee");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int GainId = Shader.PropertyToID("_Gain");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");

        private static Camera spareCamera;
        private static Material reduceMaterial;
        private static Material compositeMaterial;
        private static bool layersReady;
        private static bool disabled;
        private static int pendingFrame = -1;
        private static int lastFlushFrame = -1;
        private static bool warnedLateSubmit;

        /// <summary>
        /// Draws <paramref name="mesh"/> into the group's layer so the bloom pass picks it up. Call it
        /// right next to the object's normal draw, while <paramref name="block"/> still holds its
        /// colour/alpha and any per-draw data; the block is copied by the call.
        /// </summary>
        public static void Submit(
            ScreenBloomGroupDef group,
            Mesh mesh,
            Matrix4x4 matrix,
            Material material,
            MaterialPropertyBlock block)
        {
            // The gravship capture camera draws every layer the main camera does, so a halo
            // submitted while it renders would end up inside the gravship image.
            if (group == null || mesh == null || material == null
                || GravshipCapturer.IsGravshipRenderInProgress || !EnsureLayers() || group.layer < 0)
            {
                return;
            }

            Graphics.DrawMesh(mesh, matrix, material, group.layer, null, 0, block);

            int frame = Time.frameCount;
            if (pendingFrame != frame)
            {
                // Submissions of an earlier frame are gone with that frame's draw commands.
                Pending.Clear();
                pendingFrame = frame;
            }
            if (lastFlushFrame == frame && !warnedLateSubmit)
            {
                // The flush hook already ran this frame, so this halo cannot show up. It means the
                // object draws outside DrawDynamicThings and needs another hook.
                warnedLateSubmit = true;
                Log.Warning("[MiliraXian] Screen bloom: a glow was submitted after the frame's bloom pass already ran; it will not show.");
            }
            if (!Pending.Contains(group))
            {
                Pending.Add(group);
            }
        }

        /// <summary>Renders and composites every group that received submissions this frame.</summary>
        internal static void FlushFrame()
        {
            lastFlushFrame = Time.frameCount;
            if (pendingFrame != Time.frameCount)
            {
                // Anything still pending was submitted in an earlier frame and cannot be rendered.
                Pending.Clear();
            }
            if (Pending.Count == 0)
            {
                return;
            }

            try
            {
                Camera main = Find.Camera;
                if (main != null && !disabled && TryResolveMaterials())
                {
                    for (int i = 0; i < Pending.Count; i++)
                    {
                        RenderGroup(Pending[i], main);
                    }
                }
            }
            catch (Exception ex)
            {
                // A per-frame hook that throws would flood the log; report once and stop.
                disabled = true;
                Log.Error("[MiliraXian] Screen bloom failed and is disabled: " + ex);
            }
            finally
            {
                Pending.Clear();
            }
        }

        private static void RenderGroup(ScreenBloomGroupDef group, Camera main)
        {
            if (!States.TryGetValue(group, out GroupState state))
            {
                state = new GroupState();
                States[group] = state;
            }

            int height = Mathf.Max(MinTargetSize, Mathf.RoundToInt(Screen.height / (float)Mathf.Max(1, group.sceneDivisor)));
            int width = Mathf.Max(MinTargetSize, Mathf.RoundToInt(height * main.aspect));

            // Camera -> 8-bit target and Blit -> half-float target are what the probe proved; a
            // camera rendering straight into a half-float target was never resolved.
            EnsureTarget(ref state.scene, width, height, RenderTextureFormat.ARGB32, depthBuffer: true);
            ref RenderTexture halo = ref state.halo[Time.frameCount & 1];
            EnsureTarget(ref halo, Mathf.Max(1, width / HaloReduction), Mathf.Max(1, height / HaloReduction), RenderTextureFormat.ARGBHalf, depthBuffer: false);

            RenderScene(group, main, state.scene);

            reduceMaterial.SetFloat(ThresholdId, group.threshold);
            reduceMaterial.SetFloat(KneeId, group.knee);
            Graphics.Blit(state.scene, halo, reduceMaterial);

            Composite(group, main, halo);
        }

        private static void RenderScene(ScreenBloomGroupDef group, Camera main, RenderTexture target)
        {
            if (spareCamera == null)
            {
                // One spare camera for the whole mod: a camera object per group per frame would be churn.
                GameObject go = new("MX_ScreenBloomCamera");
                UnityEngine.Object.DontDestroyOnLoad(go);
                spareCamera = go.AddComponent<Camera>();
                spareCamera.enabled = false;
            }

            Camera cam = spareCamera;
            cam.CopyFrom(main);
            cam.transform.position = main.transform.position;
            cam.transform.rotation = main.transform.rotation;
            cam.orthographic = true;
            cam.orthographicSize = main.orthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << group.layer;
            cam.enabled = false;
            cam.targetTexture = target;
            cam.Render();
            cam.targetTexture = null;
        }

        private static void Composite(ScreenBloomGroupDef group, Camera main, RenderTexture halo)
        {
            // The map camera looks straight down with screen-up = world +z, so a quad aligned to
            // world x/z and sized to the orthographic view covers exactly what the camera sees.
            float height = main.orthographicSize * 2f;
            float width = height * main.aspect;
            Vector3 cameraPos = main.transform.position;
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(cameraPos.x, AltitudeLayer.MoteOverhead.AltitudeFor(), cameraPos.z),
                Quaternion.identity,
                new Vector3(width, 1f, height));

            CompositeBlock.Clear();
            CompositeBlock.SetTexture(MainTexId, halo);
            CompositeBlock.SetColor(ShaderPropertyIDs.Color, Color.white);
            CompositeBlock.SetFloat(GainId, group.gain);
            CompositeBlock.SetFloat(RadiusId, group.blurRadius);
            Graphics.DrawMesh(MeshPool.plane10, matrix, compositeMaterial, 0, null, 0, CompositeBlock);
            CompositeBlock.Clear();
        }

        // Targets live until the window size changes. The replaced image is released immediately:
        // for the halo, the slot being rebuilt is the one of two frames ago, whose composite has
        // already been drawn.
        private static void EnsureTarget(ref RenderTexture target, int width, int height, RenderTextureFormat format, bool depthBuffer)
        {
            if (target != null && target.width == width && target.height == height)
            {
                return;
            }
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }

            target = new RenderTexture(width, height, depthBuffer ? 24 : 0, format, RenderTextureReadWrite.Linear)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

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
                        mask |= 1 << layer;
                    }
                }
                if (mask != 0)
                {
                    Waylith.Rendering.LayerPassRouter.RegisterLayerMask(
                        mask,
                        Waylith.Rendering.RenderPassRegistry.Register("CaptureOnly"));
                }
            }
            catch (Exception ex)
            {
                disabled = true;
                Log.Error("[MiliraXian] Screen bloom could not allocate its layers and is disabled: " + ex);
            }
            return !disabled;
        }

        private static bool TryResolveMaterials()
        {
            if (compositeMaterial != null)
            {
                return true;
            }

            Shader reduce = ShaderOf("MX_ScreenBloomPrefilter");
            Shader composite = ShaderOf("MX_ScreenBloomComposite");
            if (reduce == null || composite == null)
            {
                return false;
            }

            reduceMaterial = MaterialPool.MatFrom(reduce);
            compositeMaterial = MaterialPool.MatFrom(composite);
            return true;
        }

        private static Shader ShaderOf(string defName)
        {
            Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail(defName)?.Shader;
            if (shader == null)
            {
                Log.ErrorOnce(
                    $"[MiliraXian] Screen bloom shader '{defName}' is missing; the halo is disabled.",
                    GenText.StableStringHash("MXScreenBloom" + defName));
            }
            return shader;
        }
    }

    /// <summary>
    /// Pawns, buildings and most motes draw inside <c>DrawDynamicThings</c>; running the bloom
    /// right after it means every glowing object of the frame has been submitted.
    /// </summary>
    [HarmonyPatch(typeof(DynamicDrawManager), nameof(DynamicDrawManager.DrawDynamicThings))]
    internal static class Patch_ScreenBloom_FlushAfterDynamicDraw
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            ScreenBloom.FlushFrame();
        }
    }
}
