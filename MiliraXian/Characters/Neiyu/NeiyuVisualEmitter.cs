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
    public static VfxDef MX_NY_ExecBlade;
    public static VfxDef MX_NY_ExecVolley;
    public static VfxDef MX_NY_ExecGlyph;
    public static VfxDef MX_NY_ExecSlash;
    public static VfxDef MX_NY_ExecMultiSlash;
    public static VfxDef MX_NY_ExecPierce;
    public static VfxDef MX_NY_ExecRing;
    public static VfxDef MX_NY_Thunder;
    public static VfxDef MX_NY_ThunderWarmup;
    public static VfxDef MX_NY_SwordFlashes;
    public static VfxDef MX_NY_WarpFeather;

    static NeiyuVisualDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(NeiyuVisualDefOf));
}

internal enum NeiyuLayer { Glow = 0, Feather = 7, Energy = 8, Sparks = 9, Plate = 10 }

internal struct NeiyuVisualBinding
{
    private VfxEmitterHandle handle;

    internal NeiyuVisualEmitter Get(Map map, VfxDef def, VfxAnchor anchor)
    {
        if (map == null || def == null) return null;
        var host = map.GetComponent<MapComponent_VfxEmitterHost>();
        if (handle.Host == host && host.TryGetEmitter(handle, out NeiyuVisualEmitter live))
        {
            host.Maintain(handle);
            return live;
        }
        End();
        try
        {
            handle = host.Spawn(def, anchor);
            return host.TryGetEmitter(handle, out live) ? live : null;
        }
        catch (Exception ex)
        {
            Log.ErrorOnce($"[霓羽] 无法创建特效 {def.defName}。{ex}", GenText.StableStringHash(def.defName));
            return null;
        }
    }

    internal void Maintain() => handle.Host?.Maintain(handle);

    internal void End()
    {
        handle.Host?.Destroy(handle);
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
            if ((layer is NeiyuLayer.Energy or NeiyuLayer.Sparks) && !NeiyuThunderVfx.ShadersReady)
                pass.Material = VfxMaterialCache.FromTexture("Common/WorldArcaneStrike/neiyu_halo_vfx/SoftGlow", "MoteGlow");
            if (layer == NeiyuLayer.Plate)
            {
                Plate = pass;
                pass.Renderer = null; // Instances are queued while preparing, then flushed by the host.
            }
            else if (layer is NeiyuLayer.Energy or NeiyuLayer.Sparks)
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
            NeiyuQuadBatch batch = layer switch
            {
                NeiyuLayer.Energy => visual.Thunder?.Ribbons,
                NeiyuLayer.Sparks => visual.Thunder?.Particles,
                _ => null
            };
            if (batch == null || batch.Writer.IsEmpty || pass.Material == null) return;
            Mesh mesh = emitter.MeshAt(index);
            batch.UploadIfNeeded(mesh);
            MaterialPropertyBlock block = context.Engine.Block;
            block.Clear();
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
    internal NeiyuThunderVfx Thunder;
    internal NeiyuWarpFeatherVisual Feather;
    internal float WarmupProgress;
    internal int WarmupSeed;
    internal bool Visible;
    private VfxEngine engine;
    private int stripSlot, stripId;
    private readonly Vector4[] plateRow = new Vector4[2];

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int now, Vector3 position)
    {
        base.Reset(effect, anchor, version, now, position);
        Thunder = null;
        Feather = null;
        Visible = false;
        stripSlot = effect.ParticleFloatSlot("strip");
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        if (Feather is { Ending: true } feather)
        {
            if (!feather.Alive(nowTick)) { Feather = null; return false; }
            LastMaintainTick = nowTick;
        }
        // Pushed geometry has no particle simulation. Retain it across frames and pause.
        int count = Count;
        Count = 0;
        bool alive = base.Advance(deltaSeconds, nowTick, map);
        Count = count;
        return alive;
    }

    internal void Prepare(VfxEngine drawEngine)
    {
        Visible = !Find.UIRoot.HideMotes && !Find.ScreenshotModeHandler.Active && Find.Camera != null;
        Count = stripId = 0;
        if (!Visible) return;
        engine = drawEngine;
        try
        {
            if (Feather != null) Feather.Draw(this, drawEngine.Map);
            else if (Thunder != null) Thunder.Draw(this, Find.TickManager.TicksGame);
            else if (Effect.Def == NeiyuVisualDefOf.MX_NY_ThunderWarmup && DebugViewSettings.drawThingsDynamic
                && Anchor.TryResolve(drawEngine.Map, out Vector3 at) && at.ToIntVec3().InBounds(drawEngine.Map)
                && !at.ToIntVec3().Fogged(drawEngine.Map) && Find.CameraDriver.CurrentViewRect.ExpandedBy(1).Contains(at.ToIntVec3()))
                NeiyuThunderVfx.DrawWarmup(this, at, WarmupProgress, WarmupSeed);
        }
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
