using System;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.Emitter;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu;

[DefOf]
public static class NeiyuVisualDefOf
{
    public static VfxDef MX_NY_Execution;
    public static VfxDef MX_NY_Thunder;
    public static VfxDef MX_NY_ThunderWarmup;
    public static VfxDef MX_NY_SwordFlashes;
    public static VfxDef MX_NY_WarpFeather;

    static NeiyuVisualDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(NeiyuVisualDefOf));
}

internal interface INeiyuVisualSource
{
    bool ActiveOn(Map map);
    void Populate(NeiyuVisualEmitter emitter);
    NeiyuQuadBatch CachedLayer(NeiyuLayer layer) => null;
}

internal enum NeiyuLayer { Glow, Cross, Sword, Glyph, Cut, Wave, MultiWave, Feather, Ring, Flashes, Energy, Sparks, Plate }

internal struct NeiyuVisualBinding
{
    private VfxEmitterHandle handle;

    internal void Bind(INeiyuVisualSource owner, Map map, VfxDef def, VfxAnchor anchor)
    {
        if (map == null || def == null) return;
        if (handle.Host?.Resolve(handle) is NeiyuVisualEmitter live && live.Source == owner && handle.Host.Map == map) return;
        End(owner);
        try
        {
            handle = map.GetComponent<MapComponent_VfxEmitterHost>().Spawn(def, anchor);
            if (handle.Host?.Resolve(handle) is NeiyuVisualEmitter visual) visual.Source = owner;
        }
        catch (Exception ex)
        {
            // A render asset failure must not cancel the job or replay a committed strike.
            Log.ErrorOnce($"[霓羽] 无法创建特效 {def.defName}。{ex}", GenText.StableStringHash(def.defName));
        }
    }

    internal void End(INeiyuVisualSource owner)
    {
        if (handle.Host?.Resolve(handle) is NeiyuVisualEmitter live && live.Source == owner)
        {
            live.Ring?.Begin();
            live.Source = null;
            handle.Host.Destroy(handle);
        }
        handle = default;
    }
}

internal sealed class NeiyuVisualEffect : VfxEffect
{
    internal VfxPass Plate;
    internal NeiyuVisualEffect(VfxDef def) : base(def) { }

    internal override void Build(VfxDef def)
    {
        base.Build(def);
        foreach (VfxPass pass in Passes)
        {
            NeiyuLayer layer = Enum.Parse<NeiyuLayer>(pass.Name);
            if (layer is NeiyuLayer.MultiWave or NeiyuLayer.Ring)
                pass.Material ??= VfxMaterialCache.FromTexture(pass.TexturePath, "Transparent");
            if ((layer is NeiyuLayer.Energy or NeiyuLayer.Sparks) && !NeiyuThunderVfx.ShadersReady)
                pass.Material = VfxMaterialCache.FromTexture("Common/WorldArcaneStrike/neiyu_halo_vfx/SoftGlow", "MoteGlow");
            if (layer == NeiyuLayer.Plate)
            {
                Plate = pass;
                pass.Renderer = null; // Instances are queued while preparing, then flushed by the host.
            }
            else if (layer is NeiyuLayer.Ring or NeiyuLayer.Flashes or NeiyuLayer.Energy or NeiyuLayer.Sparks)
                pass.Renderer = new BatchPass(layer);
        }
        Passes.Insert(0, new VfxPass { Name = "prepare", Renderer = Prepare.Instance });
    }

    private sealed class Prepare : IVfxRenderer
    {
        internal static readonly Prepare Instance = new();
        public void Render(VfxEmitter emitter, VfxPass pass, int index, in VfxRenderContext context) =>
            ((NeiyuVisualEmitter)emitter).Prepare(context.Engine);
    }

    private sealed class BatchPass(NeiyuLayer layer) : IVfxRenderer
    {
        public void Render(VfxEmitter emitter, VfxPass pass, int index, in VfxRenderContext context)
        {
            var visual = (NeiyuVisualEmitter)emitter;
            if (!visual.Visible) return;
            NeiyuQuadBatch batch = layer == NeiyuLayer.Ring ? visual.Ring : visual.Source?.CachedLayer(layer);
            if (batch == null || batch.Writer.IsEmpty || pass.Material == null) return;
            Mesh mesh = emitter.MeshAt(index);
            batch.UploadIfNeeded(mesh);
            MaterialPropertyBlock block = context.Engine.Block;
            block.Clear();
            if (layer != NeiyuLayer.Ring)
                ArcaneVfxStyle.Properties(block, Find.TickManager.TicksGame / 60f, profile: 3f);
            block.SetColor(ShaderPropertyIDs.Color, Color.white);
            context.Engine.Draw(mesh, pass.Material, pass.Layer);
            block.Clear();
        }
    }
}

// Keep tick-stable billboard geometry on the host mesh while paused. The annular blade
// brush uses the same writer with authored per-corner UVs and a varying edge opacity.
internal sealed class NeiyuQuadBatch
{
    internal readonly VfxMeshWriter Writer = new();
    private Mesh uploaded;

    internal void Begin()
    {
        Writer.Clear();
        uploaded = null;
    }

    internal void Add(Vector3 center, Vector3 right, Vector3 up, float width, float height, Color tint)
    {
        if (width <= .001f || tint.a <= .001f || Writer.VertexCount / 4 >= Mathf.Min(512, ArcaneVfxStyle.ParticleBudget)) return;
        right *= width * .5f;
        up *= height * .5f;
        Writer.AddQuad(center - right - up, center - right + up, center + right + up, center + right - up,
            Vector2.zero, Vector2.up, Vector2.one, Vector2.right, tint);
    }

    internal void UploadIfNeeded(Mesh mesh)
    {
        if (mesh == uploaded) return;
        Writer.Upload(mesh);
        uploaded = mesh;
    }
}

internal sealed class NeiyuVisualEmitter : VfxEmitter
{
    internal INeiyuVisualSource Source;
    internal NeiyuQuadBatch Ring;
    internal bool Visible;
    private VfxEngine engine;
    private int stripSlot, stripId;
    private readonly Vector4[] plateRow = new Vector4[2];

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int now, Vector3 position)
    {
        base.Reset(effect, anchor, version, now, position);
        Source = null;
        Visible = false;
        stripSlot = effect.ParticleFloatSlot("strip");
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        Count = 0;
        LastMaintainTick = nowTick;
        if (Source?.ActiveOn(map) == true && base.Advance(deltaSeconds, nowTick, map)) return true;
        Ring?.Begin();
        Source = null;
        return false;
    }

    internal void Prepare(VfxEngine drawEngine)
    {
        Count = stripId = 0;
        Ring?.Begin();
        Visible = !Find.UIRoot.HideMotes && !Find.ScreenshotModeHandler.Active && Find.Camera != null
            && Source?.ActiveOn(drawEngine.Map) == true;
        if (!Visible) return;
        engine = drawEngine;
        try { Source.Populate(this); }
        finally { engine = null; }
    }

    internal void Plate(Matrix4x4 matrix, Vector4 clock, Vector4 state)
    {
        plateRow[0] = clock;
        plateRow[1] = state;
        engine.AddInstance(((NeiyuVisualEffect)Effect).Plate, matrix, plateRow, 0);
    }

    // Two ribbon edges preserve the shear of the rotated execution brush and rectangular feather artwork.
    internal void Quad(NeiyuLayer layer, Matrix4x4 matrix, Color color)
    {
        if (color.a <= .001f) return;
        Vector3 center = matrix.MultiplyPoint3x4(Vector3.zero);
        Vector3 right = matrix.MultiplyVector(Vector3.right * .5f);
        Vector3 back = matrix.MultiplyVector(Vector3.back * .5f);
        int strip = ++stripId;
        Edge(center - right, back, color, layer, strip);
        Edge(center + right, back, color, layer, strip);
    }

    private void Edge(Vector3 at, Vector3 side, Color color, NeiyuLayer layer, int strip)
    {
        int index = Emit(at, 0f, 1f, color, (int)layer);
        if (index < 0) return;
        Particles[index].Side = side;
        SetChannel(index, stripSlot, strip);
    }
}
