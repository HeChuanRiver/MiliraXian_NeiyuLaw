using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public enum MingyuanSkillVisualKind : byte { OverburnRelease, TimeErosion, Absorb, Rebirth }

    [StaticConstructorOnStartup]
    public static class MingyuanSkillVfx
    {
        private static readonly Material[] Gold = new Material[12];
        private static readonly Material[] Red = new Material[12];
        static MingyuanSkillVfx()
        {
            for (int i = 0; i < Gold.Length; i++)
            {
                float alpha = (i + 1f) / Gold.Length;
                Gold[i] = MaterialPool.MatFrom("UI/Overlays/ThingLine", ShaderDatabase.MoteGlow, new Color(1f, 0.88f, 0.5f, alpha));
                Red[i] = MaterialPool.MatFrom("UI/Overlays/ThingLine", ShaderDatabase.Transparent, new Color(0.85f, 0.16f, 0.045f, alpha));
            }
        }

        public static void Play(Map map, Vector3 origin, MingyuanSkillVisualKind kind, float radius, Vector3 end = default)
        {
            if (map == null || MX_MingyuanDefOf.MX_Mingyuan_SkillVisual == null || !origin.ToIntVec3().InBounds(map)) return;
            var visual = (Thing_MingyuanSkillVisual)ThingMaker.MakeThing(MX_MingyuanDefOf.MX_Mingyuan_SkillVisual);
            visual.Init(origin, end, kind, radius);
            GenSpawn.Spawn(visual, origin.ToIntVec3(), map);
        }

        public static void Line(Vector3 a, Vector3 b, float width, float alpha, bool gold = true)
        {
            if (alpha <= 0.01f || (a - b).sqrMagnitude < 0.0001f) return;
            a.y = b.y = AltitudeLayer.MoteOverheadLow.AltitudeFor() + (gold ? 0.02f : 0f);
            int index = Mathf.Clamp(Mathf.CeilToInt(alpha * 12f) - 1, 0, 11);
            GenDraw.DrawLineBetween(a, b, (gold ? Gold : Red)[index], width);
        }

        public static Vector3 Radial(float angle) => new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

        public static void Ring(Vector3 origin, float radius, float phase, float alpha, float fraction = 1f)
        {
            for (int i = 0; i < 36; i++)
            {
                if (i / 36f >= fraction) break;
                float a = phase + i * Mathf.PI / 18f;
                float b = phase + Mathf.Min((i + 1) / 36f, fraction) * Mathf.PI * 2f;
                Line(origin + Radial(a) * radius, origin + Radial(b) * radius, 0.08f, alpha);
            }
        }
    }

    public class Thing_MingyuanSkillVisual : Thing
    {
        private Vector3 origin, end;
        private MingyuanSkillVisualKind kind;
        private float radius;
        private int startTick;
        private const int Duration = 42;
        public void Init(Vector3 origin, Vector3 end, MingyuanSkillVisualKind kind, float radius)
        {
            this.origin = origin; this.end = end; this.kind = kind; this.radius = radius;
            startTick = Find.TickManager.TicksGame;
        }
        protected override void Tick()
        {
            if (Find.TickManager.TicksGame - startTick >= Duration) Destroy();
        }
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float t = Mathf.Clamp01((Find.TickManager.TicksGame - startTick) / (float)Duration);
            float fade = 1f - Mathf.SmoothStep(0.35f, 1f, t);
            if (kind == MingyuanSkillVisualKind.Absorb)
            {
                Vector3 delta = (end - origin).Yto0();
                Vector3 side = new Vector3(delta.z, 0f, -delta.x).normalized;
                for (int stream = 0; stream < 3; stream++)
                {
                    float head = Mathf.Clamp01(t * 1.25f - stream * 0.07f);
                    Vector3 previous = origin;
                    for (int i = 0; i <= 8; i++)
                    {
                        float u = Mathf.Lerp(Mathf.Max(0f, head - 0.28f), head, i / 8f);
                        Vector3 point = Vector3.Lerp(origin, end, u) + side * (Mathf.Sin(u * Mathf.PI) * Mathf.Sin(u * 5f + stream * 2f) * 0.6f);
                        if (i > 0) MingyuanSkillVfx.Line(previous, point, 0.045f + i * 0.009f, fade * i / 8f);
                        previous = point;
                    }
                }
                MingyuanSkillVfx.Ring(end, 0.25f + t * 0.45f, t * 3f, fade);
                return;
            }
            bool inward = kind == MingyuanSkillVisualKind.TimeErosion;
            float front = inward ? radius * (1f - t * 0.8f) : radius * Mathf.Sin(t * Mathf.PI * 0.5f);
            MingyuanSkillVfx.Ring(origin, front, t * 2f, fade, inward ? 0.78f : 1f);
            for (int i = 0; i < 16; i++)
            {
                Vector3 ray = MingyuanSkillVfx.Radial(i * Mathf.PI / 8f + t * (inward ? -2f : 0.3f));
                Vector3 point = origin + ray * front;
                MingyuanSkillVfx.Line(point - ray * (0.1f + fade * 0.25f), point, 0.12f * fade, fade, false);
                MingyuanSkillVfx.Line(point, point + ray * 0.09f, 0.055f, fade);
            }
        }
    }

    public class Thing_MingyuanRebirthMarker : Thing
    {
        private int returnTick;
        private int startTick;
        public void SetReturnTick(int tick)
        {
            if (startTick == 0) startTick = Find.TickManager.TicksGame;
            returnTick = tick;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref returnTick, "returnTick");
            Scribe_Values.Look(ref startTick, "startTick");
        }
        public override string GetInspectString() => "MX_Mingyuan_Rebirth_Countdown".Translate(Mathf.Max(0, Mathf.CeilToInt((returnTick - Find.TickManager.TicksGame) / 60f)));
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            int tick = Find.TickManager.TicksGame;
            float phase = (tick - startTick) / 60f;
            float remaining = Mathf.Clamp01((returnTick - tick) / (float)Mathf.Max(1, returnTick - startTick));
            MingyuanSkillVfx.Ring(drawLoc, 1.05f + Mathf.Sin(phase * 3f) * 0.08f, phase, 0.85f);
            MingyuanSkillVfx.Ring(drawLoc, 1.4f, 0f, 0.95f, remaining);
            for (int i = 0; i < 6; i++)
            {
                Vector3 ray = MingyuanSkillVfx.Radial(phase * 1.4f + i * Mathf.PI / 3f);
                Vector3 point = drawLoc + ray * (0.65f + Mathf.Sin(phase * 2f + i) * 0.12f);
                MingyuanSkillVfx.Line(point - ray * 0.16f, point + ray * 0.16f, 0.1f, 0.9f);
            }
        }
    }
}
