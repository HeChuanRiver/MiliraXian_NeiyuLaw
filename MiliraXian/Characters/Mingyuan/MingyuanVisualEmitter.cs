using System;
using System.Collections.Generic;
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

// Handles are transient. Each owner writes its visual data on the main thread.
internal struct MingyuanVisualBinding
{
    private VfxEmitterHandle handle;

    internal MingyuanVisualEmitter Get(Thing owner, VfxDef def)
    {
        if (!owner.Spawned) return null;
        var host = owner.Map.GetComponent<MapComponent_VfxEmitterHost>();
        if (handle.Host != host || !host.TryGetEmitter(handle, out MingyuanVisualEmitter live))
        {
            handle.Host?.Destroy(handle);
            handle = host.Spawn(def, VfxAnchor.Attached(owner));
            return host.TryGetEmitter(handle, out live) ? live : null;
        }
        host.Maintain(handle);
        return live;
    }

    internal MingyuanVisualEmitter BeginFrame(Thing owner, VfxDef def)
    {
        var emitter = Get(owner, def);
        emitter?.BeginFrame();
        return emitter;
    }

    internal void End()
    {
        handle.Host?.Destroy(handle);
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
                // Submitted to the host's shared instance batch during the host's prepare pass.
                pass.Renderer = null;
            }
        }
        Passes.Insert(0, new VfxPass { Name = "prepare", Renderer = Prepare.Instance });
    }

    private sealed class Prepare : IVfxRenderer
    {
        internal static readonly Prepare Instance = new();
        public void Render(VfxEmitter emitter, VfxPass pass, int passIndex, in VfxRenderContext context) =>
            ((MingyuanVisualEmitter)emitter).PrepareFrame(context.Engine);
    }
}

internal enum MingyuanSprite { GoldLine, RedLine, BowEmber, BowGold, Crown, Wake, Arrow, ArrowGlow, Incoming, Halo, Core, Inner }

internal class MingyuanVisualEmitter : VfxEmitter
{
    private int stripSlot;
    private int stripId;
    private readonly MingyuanFireBatch[] batches = new MingyuanFireBatch[(int)MingyuanFireLayer.Count - (int)MingyuanFireLayer.CombustionSigil];

    private readonly record struct Instance(MingyuanFireLayer Layer, Matrix4x4 Matrix, Vector4 Frame, Vector4 Shape);
    private List<Instance> instances;
    private readonly Vector4[] instanceRow = new Vector4[2];
    private int pushedFrame = -1;

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position);
        stripSlot = effect.ParticleFloatSlot("strip");
        pushedFrame = -1;
        ClearGeometry();
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        // Owner-supplied ribbon edges and sprites already have final positions for this frame.
        int count = Count;
        Count = 0;
        bool alive = base.Advance(deltaSeconds, nowTick, map);
        Count = count;
        return alive;
    }

    private void ClearGeometry()
    {
        Count = stripId = 0;
        instances?.Clear();
        foreach (var batch in batches) batch?.Clear();
    }

    internal void BeginFrame()
    {
        ClearGeometry();
        pushedFrame = Time.frameCount;
    }

    internal void AddInstance(MingyuanFireLayer layer, Matrix4x4 matrix, Vector4 frame, Vector4 shape) =>
        (instances ??= new()).Add(new Instance(layer, matrix, frame, shape));

    internal virtual void PrepareFrame(VfxEngine engine)
    {
        // MapInterface may draw before this frame's dynamic things. Consume the latest
        // completed owner sample and discard it when the owner stops being drawn.
        if (pushedFrame < 0 || Time.frameCount - pushedFrame > 1 || !DebugViewSettings.drawThingsDynamic
            || !Anchor.TryResolve(engine.Map, out _))
        {
            ClearGeometry();
            return;
        }
        if (instances == null) return;
        var passes = ((MingyuanVisualEffect)Effect).FirePasses;
        foreach (var item in instances)
        {
            instanceRow[0] = item.Frame;
            instanceRow[1] = item.Shape;
            engine.AddInstance(passes[(int)item.Layer], item.Matrix, instanceRow, 0);
        }
    }

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

internal abstract class MingyuanTimedEmitter : MingyuanVisualEmitter
{
    internal override void PrepareFrame(VfxEngine engine)
    {
        BeginFrame();
        var cell = Position.ToIntVec3();
        if (!DebugViewSettings.drawThingsDynamic || !cell.InBounds(engine.Map) || cell.Fogged(engine.Map)) return;
        var view = Find.CameraDriver.CurrentViewRect.ExpandedBy(1);
        if (WorldComponent_GravshipController.GravshipRenderInProgess)
            view = view.Encapsulate(WorldComponent_GravshipController.GravshipRenderBounds);
        if (view.Contains(cell)) PopulateBurst();
    }

    protected abstract void PopulateBurst();
}
