using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx.Emitter;
using UnityEngine;

namespace MiliraXian.Characters.Mingyuan;

// Separate shader/material and bloom profiles have separate, pooled pass meshes.
internal enum MingyuanFireLayer { Fire, FireGlow, TimeGlow, Takeoff, Dive, Sword, SwordGlow, Sparks, CombustionSigil, CombustionImpact, CombustionBody, Count }

internal sealed class MingyuanFireBatch
{
    private readonly VfxMeshWriter geometry = new();
    private readonly List<Vector4> frames = new();
    private readonly List<Vector4> shapes = new();
    private readonly List<Vector2> glow = new();
    internal bool HasGeometry => !geometry.IsEmpty;
    internal bool HasGlow { get; private set; }

    internal void Clear()
    {
        geometry.Clear();
        frames.Clear();
        shapes.Clear();
        glow.Clear();
        HasGlow = false;
    }

    internal void Add(Matrix4x4 matrix, Vector4 frame, Vector4 shape, float intensity)
    {
        Vector3 center = matrix.MultiplyPoint3x4(Vector3.zero);
        Vector3 right = matrix.MultiplyVector(Vector3.right) * 0.5f;
        Vector3 forward = matrix.MultiplyVector(Vector3.forward) * 0.5f;
        geometry.AddQuad(center - right - forward, center - right + forward, center + right + forward,
            center + right - forward, Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Color.white);
        for (int i = 0; i < 4; i++)
        {
            frames.Add(frame);
            shapes.Add(shape);
            glow.Add(new Vector2(intensity, 0f));
        }
        HasGlow |= intensity > 0f;
    }

    internal void Upload(Mesh mesh)
    {
        geometry.Upload(mesh);
        mesh.SetUVs(1, glow);
        mesh.SetUVs(2, frames);
        mesh.SetUVs(3, shapes);
    }
}

internal sealed class MingyuanFireLayerRenderer(MingyuanFireLayer layer) : IVfxRenderer
{
    private static readonly int Batched = Shader.PropertyToID("_MxBatched");
    private static readonly int CombustionCapture = Shader.PropertyToID("_CombustionCapture");

    public void Render(VfxEmitter emitter, VfxPass pass, int passIndex, in VfxRenderContext context)
    {
        var batch = ((MingyuanVisualEmitter)emitter).ExistingBatch(layer);
        if (batch?.HasGeometry != true || pass.Material == null) return;
        Mesh mesh = emitter.MeshAt(passIndex);
        batch.Upload(mesh);
        var block = context.Engine.Block;
        block.Clear();
        block.SetFloat(Batched, 1f);
        if (batch.HasGlow)
        {
            block.SetFloat(CombustionCapture, 1f);
            context.Engine.Bloom(context.Assets.Bloom(pass), mesh, pass.Material);
            block.SetFloat(CombustionCapture, 0f);
        }
        context.Engine.Draw(mesh, pass.Material, pass.Layer);
        block.Clear();
    }
}
