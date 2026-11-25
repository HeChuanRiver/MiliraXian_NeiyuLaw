using System;
using MiliraXian.Characters.Common.Vfx.Emitter;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

[DefOf]
public static class MingyuanVisualDefOf
{
    public static VfxDef MX_MY_SkillBurst;
    public static VfxDef MX_MY_RebirthRings;
    public static VfxDef MX_MY_Bow;
    public static VfxDef MX_MY_WhiteFlame;
    public static VfxDef MX_MY_Fire;
}

internal interface IMingyuanVisualSource
{
    void AttachVisual(VfxDef def);
    void PopulateVisual(MingyuanVisualEmitter emitter);
}

// The owner keeps this transient handle. Its saved simulation clock remains authoritative.
internal struct MingyuanVisualBinding
{
    private VfxEmitterHandle handle;

    internal static void AfterSpawn(Thing owner, VfxDef def)
    {
        // Restored things may be spawned by the loading thread before map initialization finishes.
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            if (owner.Spawned) ((IMingyuanVisualSource)owner).AttachVisual(def);
        });
    }

    internal void Maintain(Thing owner, VfxDef def)
    {
        if (!owner.Spawned) return;
        if (handle.Host?.Resolve(handle) is { } live && live.Anchor.Thing == owner && handle.Host.Map == owner.Map)
        {
            handle.Host.Maintain(handle);
            return;
        }
        handle = owner.Map.GetComponent<MapComponent_VfxEmitterHost>().Spawn(def, VfxAnchor.Attached(owner));
    }

    internal void End(Thing owner)
    {
        if (handle.Host?.Resolve(handle)?.Anchor.Thing == owner)
            handle.Host.Destroy(handle);
        handle = default;
    }
}

// Prepare geometry in the host's draw phase, after simulation, even when the game is paused.
internal sealed class MingyuanVisualEffect : VfxEffect
{
    internal MingyuanVisualEffect(VfxDef def) : base(def) { }

    internal VfxPass[] FirePasses { get; private set; }

    internal override void Build(VfxDef def)
    {
        base.Build(def);
        foreach (VfxPass pass in Passes)
        {
            if (!Enum.TryParse(pass.Name, out MingyuanFireLayer layer)) continue;
            FirePasses ??= new VfxPass[(int)MingyuanFireLayer.Count];
            FirePasses[(int)layer] = pass;
            if (layer >= MingyuanFireLayer.CombustionSigil)
                pass.Renderer = new MingyuanFireLayerRenderer(layer);
            else
            {
                if (layer == MingyuanFireLayer.Sparks) pass.Mesh = MingyuanFireVfx.Particles;
                // Submitted to the host's shared instance batch while the owner prepares its frame.
                pass.Renderer = null;
            }
        }
        Passes.Insert(0, new VfxPass { Name = "prepare", Renderer = Prepare.Instance });
    }

    private sealed class Prepare : IVfxRenderer
    {
        internal static readonly Prepare Instance = new();
        public void Render(VfxEmitter emitter, VfxPass pass, int passIndex, in VfxRenderContext context) =>
            ((MingyuanVisualEmitter)emitter).Populate(context.Engine);
    }
}

internal enum MingyuanSprite { GoldLine, RedLine, BowEmber, BowGold, Crown, Wake, Arrow, ArrowGlow, Incoming, Halo, Core, Inner }

internal class MingyuanVisualEmitter : VfxEmitter
{
    private int stripSlot;
    private int stripId;
    private readonly MingyuanFireBatch[] batches = new MingyuanFireBatch[(int)MingyuanFireLayer.Count - (int)MingyuanFireLayer.CombustionSigil];

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        // Geometry is regenerated from the owner's tick state; there are no simulated particles to advance.
        Count = 0;
        if (Anchor.Thing is { } owner && owner is not Thing_MingyuanRebirthMarker && owner.Spawned && owner.Map == map)
            LastMaintainTick = nowTick;
        return base.Advance(deltaSeconds, nowTick, map);
    }

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position);
        stripSlot = effect.ParticleFloatSlot("strip");
    }

    private VfxEngine engine;
    private readonly Vector4[] instanceRow = new Vector4[2];

    internal void AddInstance(MingyuanFireLayer layer, Matrix4x4 matrix, Vector4 frame, Vector4 shape)
    {
        var pass = ((MingyuanVisualEffect)Effect).FirePasses[(int)layer];
        instanceRow[0] = frame;
        instanceRow[1] = shape;
        engine.AddInstance(pass, matrix, instanceRow, 0);
    }

    internal void Populate(VfxEngine drawEngine)
    {
        Map map = drawEngine.Map;
        Count = 0;
        stripId = 0;
        foreach (var batch in batches) batch?.Clear();
        if (!DebugViewSettings.drawThingsDynamic) return;
        var view = Find.CameraDriver.CurrentViewRect.ExpandedBy(1);
        if (WorldComponent_GravshipController.GravshipRenderInProgess)
            view = view.Encapsulate(WorldComponent_GravshipController.GravshipRenderBounds);
        if (Anchor.Thing is { } owner)
        {
            if (!owner.Spawned || owner.Map != map || owner.Destroyed) return;
            if (!owner.Position.InBounds(map) || (!owner.def.seeThroughFog && owner.Position.Fogged(map))) return;
            if (owner.def.hideAtSnowOrSandDepth < 1f && map.snowGrid.GetDepth(owner.Position) > owner.def.hideAtSnowOrSandDepth) return;
            if (!owner.def.drawOffscreen && !view.Overlaps(owner.OccupiedDrawRect())) return;
            engine = drawEngine;
            try { ((IMingyuanVisualSource)owner).PopulateVisual(this); }
            finally { engine = null; }
        }
        else
        {
            var cell = Position.ToIntVec3();
            if (cell.InBounds(map) && !cell.Fogged(map) && view.Contains(cell))
                PopulateBurst();
        }
    }

    protected virtual void PopulateBurst() { }

    internal MingyuanFireBatch Batch(MingyuanFireLayer layer) =>
        batches[(int)layer - (int)MingyuanFireLayer.CombustionSigil] ??= new MingyuanFireBatch();

    internal MingyuanFireBatch ExistingBatch(MingyuanFireLayer layer) => batches[(int)layer - (int)MingyuanFireLayer.CombustionSigil];

    // A two-edge ribbon represents an arbitrarily proportioned, rotated quad. U runs across
    // the picture, V along it, matching plane10's four UVs even on non-square bow textures.
    internal void Quad(MingyuanSprite group, Vector3 center, Vector3 right, Vector3 forward, Color color)
    {
        if (color.a <= 0.001f) return;
        int id = ++stripId;
        Edge(group, center - right, forward, color, id);
        Edge(group, center + right, forward, color, id);
    }

    private void Edge(MingyuanSprite group, Vector3 point, Vector3 halfHeight, Color color, int id)
    {
        int i = Emit(point, 0f, 1f, color, (int)group);
        if (i < 0) return;
        Particles[i].Side = -halfHeight;
        SetChannel(i, stripSlot, id);
    }

    internal void Line(MingyuanSprite group, Vector3 a, Vector3 b, float width, Color color)
    {
        Vector3 length = (a - b).Yto0();
        if (Mathf.Abs(length.x) < 0.01f && Mathf.Abs(length.z) < 0.01f) return;
        Vector3 right = new Vector3(length.z, 0f, -length.x).normalized * (width * 0.5f);
        Quad(group, (a + b) * 0.5f, right, length * 0.5f, color);
    }

    internal void Sprite(MingyuanSprite group, Vector3 position, float size, float rotation, Color tint)
    {
        int i = Emit(position, 0f, size, tint, (int)group);
        if (i >= 0) Particles[i].RotationDegrees = -rotation;
    }
}
