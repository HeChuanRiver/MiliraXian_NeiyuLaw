using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    // All motion is a function of the saved controller's age, never wall time or Rand.
    internal static class MingyuanBowVisualDrawer
    {
        private const int AlphaSteps = 16;
        private static readonly Color Ember = new(0.55f, 0.045f, 0.015f);
        private static readonly Material[] PreviewEmber = new Material[AlphaSteps];
        private static readonly Color Gold = new(1f, 0.81f, 0.38f);
        private static readonly Material[] PreviewGold = new Material[AlphaSteps];

        public static Vector3 EmitterPosition(Vector3 origin, Vector3 direction)
        {
            return Raised(origin + direction.normalized * 0.58f);
        }

        public static void DrawFocusCharge(MingyuanVisualEmitter layers, Vector3 origin, Vector3 direction, float progress)
        {
            direction = direction.Yto0().normalized;
            Vector3 emitter = EmitterPosition(origin, direction);
            float charge = Ease(progress);
            // The feather aperture contracts while the arrow is drawn back into it.
            Sprite(layers, MingyuanSprite.Crown, emitter, Rotate(direction, 65f * (1f - charge)),
                1.7f - charge * 0.65f, 1.7f - charge * 0.65f, 0.35f + charge * 0.55f);
            Vector3 nock = emitter - direction * (charge * 0.3f);
            Sprite(layers, MingyuanSprite.Arrow, nock, direction, 1.1f + charge * 0.5f, 1.55f + charge * 0.65f, 0.4f + charge * 0.6f);
            for (int i = 0; i < 6; i++)
            {
                float t = Mathf.Repeat(progress * 2.5f + i / 6f, 1f);
                Vector3 ray = Rotate(direction, i * 60f + progress * 45f);
                Vector3 point = emitter + ray * Mathf.Lerp(1.55f, 0.18f, t);
                Sprite(layers, MingyuanSprite.Wake, point, -ray, 0.45f, 0.6f, Mathf.Sin(t * Mathf.PI) * (0.3f + charge * 0.5f));
            }
        }

        public static void DrawFocusAim(MingyuanVisualEmitter layers, Vector3 origin, Vector3 target, Vector3 direction,
            float progress = 0f, float ageSeconds = -1f)
        {
            Vector3 emitter = EmitterPosition(origin, direction);
            target = Raised(target);
            Vector3 path = (target - emitter).Yto0();
            float distance = path.magnitude;
            if (distance < 0.05f) return;
            direction = path / distance;
            float charge = Ease(progress);
            Stroke(layers, emitter, target, 0.055f + charge * 0.025f, 0.42f + charge * 0.22f);
            if (ageSeconds < 0f) return;
            Sprite(layers, MingyuanSprite.Crown, target, Rotate(direction, -progress * 80f),
                1.65f - charge * 0.65f, 1.65f - charge * 0.65f, 0.5f + charge * 0.45f);
            // Small packets travel toward the target without obscuring intervening pawns.
            int count = Mathf.Clamp(Mathf.CeilToInt(distance / 5f), 2, 8);
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Repeat(i / (float)count + ageSeconds * 8f / distance, 1f);
                Sprite(layers, MingyuanSprite.Wake, Vector3.Lerp(emitter, target, t), direction, 0.3f, 0.55f,
                    Mathf.Sin(t * Mathf.PI) * charge * 0.6f);
            }
        }

        public static void DrawFocusArrow(MingyuanVisualEmitter layers, Vector3 start, Vector3 end, float progress)
        {
            start = Raised(start);
            end = Raised(end);
            Vector3 path = (end - start).Yto0();
            float distance = path.magnitude;
            if (distance < 0.05f || progress >= 1f) return;
            Vector3 direction = path / distance;
            float fade = 1f - Ease(progress);
            // Focus resolves immediately. The full streak and impact appear on release;
            // the fast arrow afterimage does not imply a delayed second damage event.
            Stroke(layers, start, end, (0.09f + 0.2f * fade) * fade, fade * 0.9f);
            float travel = Mathf.Clamp01(progress * 6f);
            if (travel < 1f)
            {
                Vector3 head = Vector3.Lerp(start, end, travel);
                Sprite(layers, MingyuanSprite.Arrow, head, direction, 1.9f, 2.55f, 1f - travel * 0.25f);
                DrawWake(layers, head, direction, Mathf.Min(distance * travel, 4f), 1.5f, fade);
            }
            float recoil = Mathf.Clamp01(progress * 3f);
            Sprite(layers, MingyuanSprite.Crown, start, Rotate(direction, recoil * 35f),
                1f + recoil, 1f + recoil, (1f - recoil) * 0.85f);
            DrawImpact(layers, end, direction, progress, 1f);
        }

        public static void DrawImpact(MingyuanVisualEmitter layers, Vector3 center, Vector3 direction, float progress, float strength)
        {
            if (progress >= 1f) return;
            center = Raised(center);
            float bloom = Ease(Mathf.Clamp01(progress * 2f));
            float fade = 1f - Ease(progress);
            float diameter = (0.65f + bloom * 1.6f) * strength;
            Sprite(layers, MingyuanSprite.Crown, center, Rotate(direction, progress * 45f), diameter, diameter, fade * 0.9f);
            int count = strength < 0.8f ? 4 : 8;
            for (int i = 0; i < count; i++)
            {
                Vector3 ray = Rotate(direction, i * 360f / count + 22.5f);
                Vector3 point = center + ray * ((0.12f + bloom * 0.7f) * strength);
                Sprite(layers, MingyuanSprite.Wake, point, ray, (0.8f - progress * 0.35f) * strength,
                    (0.55f + bloom * 0.65f) * strength, fade);
            }
        }

        public static void DrawSectorWarning(Vector3 origin, Vector3 direction, float radius, float arcDegrees, float alpha)
        {
            origin = Raised(origin);
            direction = direction.Yto0().normalized;
            Stroke(null, origin, origin + Rotate(direction, -arcDegrees * 0.5f) * radius, 0.075f, alpha);
            Stroke(null, origin, origin + Rotate(direction, arcDegrees * 0.5f) * radius, 0.075f, alpha);
            Arc(null, origin, direction, radius, arcDegrees, 24, 0.075f, alpha);
        }

        private static void DrawWake(MingyuanVisualEmitter layers, Vector3 head, Vector3 direction, float length, float width, float alpha)
        {
            if (length < 0.05f) return;
            // The generated texture has clear margins; its hot tip sits at 90% V.
            Sprite(layers, MingyuanSprite.Wake, head - direction * length * 0.5f, direction, width, length / 0.8f, alpha);
        }

        private static void Arc(MingyuanVisualEmitter layers, Vector3 origin, Vector3 direction, float radius, float arc, int segments, float width, float alpha)
        {
            if (radius < 0.05f) return;
            Vector3 previous = origin + Rotate(direction, -arc * 0.5f) * radius;
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = origin + Rotate(direction, arc * (i / (float)segments - 0.5f)) * radius;
                Stroke(layers, previous, next, width, alpha);
                previous = next;
            }
        }

        private static void Stroke(MingyuanVisualEmitter layers, Vector3 start, Vector3 end, float width, float alpha)
        {
            if (alpha < 0.02f || width < 0.005f || (end - start).sqrMagnitude < 0.0001f) return;
            if (layers != null)
            {
                float opacity = Opacity(alpha);
                layers.Line(MingyuanSprite.BowEmber, start, end, width, Ember.WithAlpha(opacity));
                Vector3 offset = Vector3.up * 0.004f;
                layers.Line(MingyuanSprite.BowGold, start + offset, end + offset, width * 0.3f,
                    Gold.WithAlpha(opacity));
                return;
            }
            GenDraw.DrawLineBetween(start, end, PreviewMaterial(PreviewEmber, Ember, alpha), width);
            GenDraw.DrawLineBetween(start + Vector3.up * 0.004f, end + Vector3.up * 0.004f, PreviewMaterial(PreviewGold, Gold, alpha), width * 0.3f);
        }

        private static void Sprite(MingyuanVisualEmitter layers, MingyuanSprite group, Vector3 position, Vector3 direction, float width, float length, float alpha)
        {
            if (alpha < 0.02f || width <= 0f || length <= 0f || direction.sqrMagnitude < 0.001f) return;
            position.y = AltitudeLayer.MoteOverheadLow.AltitudeFor()
                + (group == MingyuanSprite.Arrow ? 0.06f : group == MingyuanSprite.Crown ? 0.02f : 0.04f);
            Quaternion rotation = Quaternion.LookRotation(direction);
            layers.Quad(group, position, rotation * Vector3.right * (width * 0.5f),
                rotation * Vector3.forward * (length * 0.5f), Color.white.WithAlpha(Opacity(alpha)));
        }

        private static Vector3 Raised(Vector3 point)
        {
            point.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();
            return point;
        }

        private static Vector3 Rotate(Vector3 direction, float degrees)
        {
            float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
            float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);
            return new Vector3(direction.x * cos + direction.z * sin, 0f, direction.z * cos - direction.x * sin);
        }

        private static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        private static float Opacity(float alpha) => Mathf.Clamp(Mathf.CeilToInt(alpha * AlphaSteps), 1, AlphaSteps) / (float)AlphaSteps;

        private static Material PreviewMaterial(Material[] cache, Color tint, float alpha)
        {
            int step = Mathf.Clamp(Mathf.CeilToInt(alpha * AlphaSteps), 1, AlphaSteps);
            return cache[step - 1] ??= MaterialPool.MatFrom("UI/Overlays/ThingLine", ShaderDatabase.Transparent,
                tint.WithAlpha(step / (float)AlphaSteps));
        }
    }
}
