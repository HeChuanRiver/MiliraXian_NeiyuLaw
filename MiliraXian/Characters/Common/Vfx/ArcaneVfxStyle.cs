using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Verse;
using NativeMeshes = Waylith.Graphics.Meshes;

namespace MiliraXian.Characters.Common.Vfx
{
    public enum ArcaneVfxQuality { Low, Medium, High }

    // Visual parameters only. No cast state, damage or saved progress belongs here.
    public sealed class ArcaneVfxStyleDef : Def
    {
        public float strokeExpansion = 1.35f;
        public float coreWidth = 0.58f;
        public float glowRadius = 5.5f;
        public float glowIntensity = 0.32f;
        public float flowIntensity = 0.28f;
        public float circleOpacity = 0.88f;
        public float pulseIntensity = 0.48f;
        public float beamIntensity = 0.82f;
        public float trailWidth = 1f;
        public float particleDensity = 1f;
        public float particleSizeMin = 0.075f;
        public float particleSizeMax = 0.21f;
        public float dustLifetime = 1.6f;
        public float explosionParticleDensity = 1f;
        public float chantStageLengthening = 1.3f;
        public float chantBeatInterval = 0.75f;
        public float chantBeatLengthening = 2.4f;
        public float chantSealFraction = 0.05f;
        public float chantConvergeFraction = 0.08f;
        public float chantAfterglowFraction = 0.04f;
    }

    // A sample of the existing job progress, never a second clock or saved cast state.
    internal struct ArcaneChantFrame
    {
        internal float ClipProgress, Seconds, Construction, BeatPhase, PhaseProgress;
        internal int Stage, Beat;
        internal Vector4 Finish;
    }

    internal static class ArcaneVfxStyle
    {
        // Boundaries in the authored circle clip; its geometry and tracks stay authoritative.
        private static readonly float[] ChantClipStages = { 0f, 0.15f, 0.35f, 0.60f, 0.85f, 0.96f };
        internal static ArcaneVfxStyleDef Tuning => DefDatabase<ArcaneVfxStyleDef>.GetNamed("MX_ArcaneVfxStyle");
        internal static ArcaneVfxQuality Quality => NeiyuLawMod.Instance?.Settings?.ArcaneVisualQuality ?? ArcaneVfxQuality.Medium;
        internal static int ParticleBudget => Quality switch { ArcaneVfxQuality.Low => 144, ArcaneVfxQuality.High => 448, _ => 288 };
        internal static float Density => Mathf.Clamp(Tuning.particleDensity, 0f, 2f)
            * (Quality == ArcaneVfxQuality.Low ? 0.45f : Quality == ArcaneVfxQuality.High ? 1f : 0.7f);
        internal static float Glow => Quality == ArcaneVfxQuality.Low ? 0.65f : Quality == ArcaneVfxQuality.High ? 1f : 0.85f;

        internal static ArcaneChantFrame ChantFrame(float progress, float clipDuration, float chantDuration)
        {
            ArcaneVfxStyleDef tuning = Tuning;
            progress = Mathf.Clamp01(progress);
            float seal = Mathf.Clamp(tuning.chantSealFraction, 0.01f, 0.1f);
            float converge = Mathf.Clamp(tuning.chantConvergeFraction, 0.02f, 0.15f);
            float afterglow = Mathf.Clamp(tuning.chantAfterglowFraction, 0.01f, 0.1f);
            float constructionEnd = 1f - seal - converge - afterglow;
            ArcaneChantFrame frame = new() { Construction = Mathf.Clamp01(progress / constructionEnd) };
            if (progress < constructionEnd)
            {
                float growth = Mathf.Clamp(tuning.chantStageLengthening, 1f, 2f);
                float total = 0f, weight = 1f;
                for (int i = 0; i < 5; i++, weight *= growth) total += weight;
                float position = frame.Construction * total;
                weight = 1f;
                while (frame.Stage < 4 && position >= weight)
                {
                    position -= weight;
                    weight *= growth;
                    frame.Stage++;
                }
                frame.PhaseProgress = Mathf.Clamp01(position / weight);
                frame.ClipProgress = Mathf.Lerp(ChantClipStages[frame.Stage], ChantClipStages[frame.Stage + 1], frame.PhaseProgress);
            }
            else if (progress < constructionEnd + seal)
            {
                frame.Stage = 5;
                frame.PhaseProgress = (progress - constructionEnd) / seal;
                frame.ClipProgress = 0.96f;
                frame.Finish.x = Mathf.Sin(frame.PhaseProgress * Mathf.PI);
            }
            else if (progress < 1f - afterglow)
            {
                frame.Stage = 6;
                frame.PhaseProgress = (progress - constructionEnd - seal) / converge;
                float pull = Mathf.SmoothStep(0f, 1f, frame.PhaseProgress);
                frame.ClipProgress = Mathf.Lerp(0.9725f, 0.998f, pull);
                frame.Finish.y = pull;
            }
            else
            {
                frame.Stage = 7;
                frame.PhaseProgress = Mathf.InverseLerp(1f - afterglow, 1f, progress);
                frame.ClipProgress = Mathf.Lerp(0.998f, 1f, frame.PhaseProgress);
                frame.Finish.y = 1f;
                frame.Finish.z = 1f - frame.PhaseProgress;
                frame.Finish.w = 1f;
            }
            frame.Seconds = (frame.ClipProgress <= 0.96f ? frame.ClipProgress
                : frame.ClipProgress < 0.9725f ? 0.96f : frame.ClipProgress - 0.0125f) * clipDuration;
            // Integrate a linearly increasing interval: later beats take longer,
            // and sampling after pause/load/speed changes needs no accumulated timer.
            float interval = Mathf.Clamp(tuning.chantBeatInterval, 0.3f, 2f);
            float lengthening = Mathf.Clamp(tuning.chantBeatLengthening, 1f, 4f) - 1f;
            float elapsed = frame.Construction * constructionEnd * chantDuration;
            float beats = lengthening < 0.001f ? elapsed / interval
                : constructionEnd * chantDuration / (interval * lengthening) * Mathf.Log(1f + lengthening * frame.Construction);
            frame.Beat = Mathf.FloorToInt(beats);
            frame.BeatPhase = beats - frame.Beat;
            return frame;
        }

        internal static void Properties(MaterialPropertyBlock block, float seconds, float seed = 0f, float profile = 0f)
        {
            ArcaneVfxStyleDef tuning = Tuning;
            block.SetVector("_ArcaneClock", new Vector4(seconds, seed, profile, Glow));
            block.SetVector("_Stroke", new Vector4(Mathf.Clamp(tuning.strokeExpansion, 0f, 3f),
                Mathf.Clamp(tuning.coreWidth, 0.2f, 1f), Mathf.Clamp(tuning.glowRadius, 2f, 9f),
                Mathf.Clamp(tuning.glowIntensity, 0f, 0.65f)));
            block.SetVector("_Flow", new Vector4(Mathf.Clamp(tuning.flowIntensity, 0f, 0.6f),
                tuning.pulseIntensity, 0f, 0f));
        }

        internal static bool KeepParticle(int index, int layer, float density) =>
            Mathf.Repeat(index * 0.618034f + layer * 0.381966f, 1f) < Mathf.Clamp01(density);

        // The compatibility vertices/colors setters rebuild native storage. These
        // meshes have fixed layouts: update their public native backing in place.
        internal static void UploadMesh(Mesh mesh, Vector3[] vertices, Color[] colors = null)
        {
            NativeMeshes.CpuMeshData storage = mesh.EngineMesh.CpuStorage;
            storage.Layout.TryGetAttribute(NativeMeshes.VertexAttributeSemantic.Position, out var position);
            Span<byte> positions = storage.GetVertexStream(position.Stream).AsSpan();
            int stride = storage.Layout.GetStreamStride(position.Stream);
            Bounds bounds = new(vertices[0], Vector3.zero);
            for (int i = 0; i < vertices.Length; i++)
            {
                MemoryMarshal.Write(positions.Slice(i * stride + position.OffsetBytes), in vertices[i]);
                bounds.Encapsulate(vertices[i]);
            }
            if (colors != null && storage.Layout.TryGetAttribute(NativeMeshes.VertexAttributeSemantic.Color, out var color))
            {
                Span<byte> target = storage.GetVertexStream(color.Stream).AsSpan();
                int colorStride = storage.Layout.GetStreamStride(color.Stream);
                for (int i = 0; i < colors.Length; i++)
                {
                    Color32 value = colors[i];
                    MemoryMarshal.Write(target.Slice(i * colorStride + color.OffsetBytes), in value);
                }
            }
            mesh.bounds = bounds;
            for (int i = 0; i < storage.SubMeshes.Length; i++)
            {
                NativeMeshes.SubMesh sub = storage.SubMeshes[i];
                storage.SetSubMesh(i, new NativeMeshes.SubMesh(sub.IndexStart, sub.IndexCount, sub.Topology,
                    sub.BaseVertex, mesh.EngineMesh.Bounds));
            }
            mesh.EngineMesh.MarkCpuStorageChanged();
        }
    }

    // One owned dynamic mesh per particle texture, with fixed topology and bounded
    // storage. No GameObject, material instance or draw call per particle.
    internal sealed class ArcaneParticleBatch : IDisposable
    {
        private const int Capacity = 512;
        private readonly Vector3[] vertices = new Vector3[Capacity * 4];
        private readonly Color[] colors = new Color[Capacity * 4];
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly MaterialPropertyBlock properties = new();
        private int count;

        internal ArcaneParticleBatch(Material material)
        {
            this.material = material;
            Vector2[] uv = new Vector2[Capacity * 4];
            int[] triangles = new int[Capacity * 6];
            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4, t = i * 6;
                uv[v] = new(0, 0); uv[v + 1] = new(1, 0); uv[v + 2] = new(0, 1); uv[v + 3] = new(1, 1);
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh = new Mesh { name = "MX_ArcaneParticleBatch" };
            NativeMeshes.CpuMeshData storage = mesh.EngineMesh.CpuStorage;
            storage.SetVertexLayout(new NativeMeshes.VertexLayout(new[]
            {
                new NativeMeshes.VertexAttribute(NativeMeshes.VertexAttributeSemantic.Position, NativeMeshes.VertexAttributeFormat.Float32, 3, 0),
                new NativeMeshes.VertexAttribute(NativeMeshes.VertexAttributeSemantic.Color, NativeMeshes.VertexAttributeFormat.Float32, 4, 1),
                new NativeMeshes.VertexAttribute(NativeMeshes.VertexAttributeSemantic.TexCoord0, NativeMeshes.VertexAttributeFormat.Float32, 2, 2)
            }), Capacity * 4);
            storage.SetIndexBufferParams(triangles.Length, NativeMeshes.MeshIndexFormat.UInt32);
            triangles.AsSpan().CopyTo(storage.GetIndexBuffer().AsSpan<int>());
            uv.AsSpan().CopyTo(storage.GetVertexStream(2).AsSpan<Vector2>());
            storage.SetSubMeshCount(1);
            storage.SetSubMesh(0, new NativeMeshes.SubMesh(0, 0));
            mesh.MarkDynamic();
        }

        internal void Begin() => count = 0;

        internal void Add(Vector3 position, Vector3 right, Vector3 up, float width, float height, Color color)
        {
            if (count >= Mathf.Min(Capacity, ArcaneVfxStyle.ParticleBudget) || color.a <= 0.001f || width <= 0.001f) return;
            int v = count++ * 4;
            right *= width * 0.5f; up *= height * 0.5f;
            vertices[v] = position - right - up; vertices[v + 1] = position + right - up;
            vertices[v + 2] = position - right + up; vertices[v + 3] = position + right + up;
            colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
        }

        internal void Upload()
        {
            if (count == 0) return;
            NativeMeshes.CpuMeshData storage = mesh.EngineMesh.CpuStorage;
            vertices.AsSpan(0, count * 4).CopyTo(storage.GetVertexStream(0).AsSpan<Vector3>());
            colors.AsSpan(0, count * 4).CopyTo(storage.GetVertexStream(1).AsSpan<Color>());
            Bounds bounds = new(vertices[0], Vector3.zero);
            for (int i = 1; i < count * 4; i++) bounds.Encapsulate(vertices[i]);
            mesh.bounds = bounds;
            storage.SetSubMesh(0, new NativeMeshes.SubMesh(0, count * 6, bounds: mesh.EngineMesh.Bounds));
            mesh.EngineMesh.MarkCpuStorageChanged();
        }

        internal void Draw(float seconds, int layer, Camera camera)
        {
            if (count == 0) return;
            properties.Clear();
            properties.SetColor(ShaderPropertyIDs.Color, Color.white);
            ArcaneVfxStyle.Properties(properties, seconds, profile: 3f);
            ArcaneEnergyResources.SubmitMesh(mesh, Matrix4x4.identity, material, layer, camera, properties);
        }

        public void Dispose() => UnityEngine.Object.Destroy(mesh);
    }
}
