using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.Emitter;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli;

[DefOf]
public static class ZhaoliVisualDefOf
{
    public static VfxDef MX_ZL_DuanzhanTrail;
    public static VfxDef MX_ZL_Minghuo;
    public static VfxDef MX_ZL_Guiyi;
    public static VfxDef MX_ZL_DeathField;
    public static VfxDef MX_ZL_MinshenWarmup;
    public static VfxDef MX_ZL_MinshenImpact;

    static ZhaoliVisualDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(ZhaoliVisualDefOf));
}

public sealed class ZhaoliAnimationClip : DefModExtension
{
    public CharacterVfxDef clip;
}

// Each def shares a sampler for the original curves, hierarchy and sprite frames.
// Sample() fully restores the authored pose before evaluating the requested time.
// Per-cast geometry and clocks stay on separate host emitters.
internal sealed class ZhaoliVisualEffect : VfxEffect, IVfxRenderer
{
    private readonly Dictionary<(Texture, bool), int> groups = new();
    private CharacterVfxAnimation animation;
    private bool attempted;
    private float lastPhase = -1f;
    private readonly CharacterVfxDef clip;

    internal ZhaoliVisualEffect(VfxDef def) : base(def)
    {
        clip = def.GetModExtension<ZhaoliAnimationClip>()?.clip;
        if (clip != null)
        {
            // Keep the exported clip authoritative for both frame textures and layer count.
            VfxPass surface = Passes.Find(pass => pass.Name == "Surface");
            VfxPass glow = Passes.Find(pass => pass.Name == "Glow");
            Passes.Clear();
            Capacity = 0;
            foreach (CharacterVfxPart part in clip.parts)
            {
                if (part.texture.NullOrEmpty()) continue;
                Capacity += 2;
                AddMaterial(part.texture, part.glow, part.glow ? glow : surface);
            }
            foreach (CharacterVfxSpriteFrame frame in clip.spriteFrames)
            {
                bool additive = clip.parts[frame.part].glow;
                AddMaterial(frame.texture, additive, additive ? glow : surface);
            }
        }
        Passes.Insert(0, new VfxPass { Name = "sample", Renderer = this });
    }

    private void AddMaterial(string path, bool glow, VfxPass template)
    {
        Material material = VfxMaterialCache.FromTexture(path, template.ShaderDefName);
        if (material?.mainTexture is not { } texture || !groups.TryAdd((texture, glow), Passes.Count)) return;
        Passes.Add(new VfxPass
        {
            Name = path, Renderer = template.Renderer, Ribbon = template.Ribbon, Material = material,
            ParticleGroup = Passes.Count, Layer = template.Layer
        });
    }

    internal bool Ready
    {
        get
        {
            if (!attempted)
            {
                attempted = true;
                animation = clip?.Instantiate();
            }
            return animation is { Sprites.Length: > 0 };
        }
    }

    public void Render(VfxEmitter emitter, VfxPass pass, int index, in VfxRenderContext context)
    {
        if (emitter is ZhaoliDuanzhanTrail trail)
        {
            trail.Populate(context.Engine.Map);
            return;
        }
        var playback = (ZhaoliClipEmitter)emitter;
        playback.Count = 0;
        if (!playback.TryFrame(context.Engine.Map, out Vector3 anchor, out float phase) || !Ready) return;
        if (phase != lastPhase)
        {
            animation.Sample(phase);
            lastPhase = phase;
        }
        anchor.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        foreach (CharacterVfxSprite sprite in animation.Sprites)
        {
            if (!sprite.visible || sprite.color.a <= .001f || !sprite.transform.gameObject.activeInHierarchy
                || sprite.texture == null || !groups.TryGetValue((sprite.texture, sprite.glow), out int group)) continue;
            Transform transform = sprite.transform;
            Vector3 scale = transform.localToWorldMatrix.lossyScale;
            float width = Mathf.Abs(sprite.size.x * scale.x) * playback.Scale;
            float height = Mathf.Abs(sprite.size.y * scale.y) * playback.Scale;
            if (width <= .001f || height <= .001f) continue;
            Vector3 offset = transform.position;
            Vector3 center = anchor + new Vector3(offset.x, 0f, offset.y) * playback.Scale
                + Altitudes.AltIncVect * (.01f + Mathf.Max(0, sprite.sortingOrder) * .004f);
            Quaternion rotation = Quaternion.AngleAxis(Mathf.DeltaAngle(0f, transform.eulerAngles.z), Vector3.up);
            Vector3 right = rotation * Vector3.right * width * .5f;
            Vector3 back = rotation * Vector3.back * height * .5f;
            if (sprite.flipX ^ (scale.x < 0f)) right = -right;
            if (sprite.flipY ^ (scale.y < 0f)) back = -back;
            Color tint = sprite.color;
            tint.a = Mathf.Clamp01(tint.a);
            playback.Quad(center, right, back, tint, group);
        }
    }

    internal override void Release()
    {
        animation?.Dispose();
        animation = null;
        base.Release();
    }
}

internal struct ZhaoliClipHandle
{
    internal VfxEmitterHandle Handle;
    internal ZhaoliClipEmitter Emitter;
    internal bool Live => Emitter != null && Handle.Host?.Resolve(Handle) == Emitter;

    internal void End()
    {
        if (Live)
        {
            Emitter.DetachWarmup();
            Handle.Host.Destroy(Handle);
        }
        this = default;
    }
}

internal sealed class ZhaoliClipEmitter : VfxEmitter
{
    private int start, duration, stripSlot;
    private bool loop;
    private Stance_Warmup warmup;
    internal float Scale;

    private static bool Start(VfxDef def, Map map, VfxAnchor anchor, int start, int ticks, float scale,
        bool loop, Stance_Warmup stance, out ZhaoliClipHandle binding)
    {
        binding = default;
        if (map == null || def == null || VfxDefCompiler.EffectOf(def) is not ZhaoliVisualEffect { Ready: true }) return false;
        var host = map.GetComponent<MapComponent_VfxEmitterHost>();
        VfxEmitterHandle handle = host.Spawn(def, anchor);
        if (!host.TryGetEmitter(handle, out ZhaoliClipEmitter emitter)) return false;
        binding = new ZhaoliClipHandle { Handle = handle, Emitter = emitter };
        emitter.start = start;
        emitter.duration = Mathf.Max(1, ticks);
        emitter.Scale = Mathf.Max(.01f, scale);
        emitter.loop = loop;
        emitter.warmup = stance;
        emitter.stripSlot = emitter.Effect.ParticleFloatSlot("strip");
        return true;
    }

    internal static bool Play(VfxDef def, Map map, VfxAnchor anchor, int ticks, float scale = 1f) =>
        Start(def, map, anchor, Find.TickManager.TicksGame, ticks, scale, false, null, out _);

    // The owning comp retains the handle; a stale slot or an anchor on another map cannot be reused.
    internal static bool Maintain(ref ZhaoliClipHandle binding, VfxDef def, Map map, VfxAnchor anchor,
        int start, int ticks, float scale, Stance_Warmup stance = null)
    {
        ZhaoliClipEmitter current = binding.Emitter;
        if (binding.Live && binding.Handle.Host.Map == map && current.Effect.Def == def
            && current.warmup == stance && current.Anchor.Pawn == anchor.Pawn)
        {
            current.Anchor = anchor;
            current.Scale = Mathf.Max(.01f, scale);
            binding.Handle.Host.Maintain(binding.Handle);
            return true;
        }
        binding.End();
        return Start(def, map, anchor, start, ticks, scale, stance == null, stance, out binding);
    }

    internal void DetachWarmup() => warmup = null;

    private bool Playing(int now)
    {
        if (warmup != null) return warmup.verb?.CasterPawn?.stances?.curStance == warmup;
        return loop || now - start <= duration;
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        Count = 0;
        if (!loop) LastMaintainTick = nowTick;
        if (Playing(nowTick) && base.Advance(deltaSeconds, nowTick, map)) return true;
        warmup = null;
        return false;
    }

    internal bool TryFrame(Map map, out Vector3 position, out float phase)
    {
        int now = Find.TickManager.TicksGame;
        phase = 0f;
        if (!Anchor.TryResolve(map, out position) || !Playing(now) || Anchor.Pawn?.Dead == true) return false;
        phase = warmup != null ? warmup.verb.WarmupProgress : loop
            ? Mathf.Repeat((now - start) / (float)duration, 1f) : Mathf.Clamp01((now - start) / (float)duration);
        return true;
    }

    internal void Quad(Vector3 center, Vector3 right, Vector3 back, Color tint, int group)
    {
        int strip = Count;
        Edge(center - right, back, tint, group, strip);
        Edge(center + right, back, tint, group, strip);
    }

    private void Edge(Vector3 position, Vector3 side, Color color, int group, int strip)
    {
        int index = Emit(position, 0f, 1f, color, group);
        if (index < 0) return;
        Particles[index].Side = side;
        SetChannel(index, stripSlot, strip);
    }
}
