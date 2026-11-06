using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    // All motion is a function of the saved controller's age, never wall time or Rand.
    [StaticConstructorOnStartup]
    internal static class MingyuanBowVisualDrawer
    {
        private const int AlphaSteps = 16;
        private const string Root = "MiliraXianMingyuan/Effect/Bow/";
        private static readonly Material[] Crown = Materials(Root + "CinderCrown", Color.white);
        private static readonly Material[] Wake = Materials(Root + "FlameWake", Color.white);
        private static readonly Material[] Arrow = Materials("MiliraXianMingyuan/Projectile/RainbowArrow", Color.white);
        private static readonly Material[] Ember = Materials("UI/Overlays/ThingLine", new Color(0.55f, 0.045f, 0.015f));
        private static readonly Material[] Gold = Materials("UI/Overlays/ThingLine", new Color(1f, 0.81f, 0.38f));

        public static Vector3 EmitterPosition(Vector3 origin, Vector3 direction)
        {
            return Raised(origin + direction.normalized * 0.58f);
        }

        public static void DrawFocusCharge(Vector3 origin, Vector3 direction, float progress)
        {
            direction = direction.Yto0().normalized;
            Vector3 emitter = EmitterPosition(origin, direction);
            float charge = Ease(progress);
            // The feather aperture contracts while the arrow is drawn back into it.
            Sprite(Crown, emitter, Rotate(direction, 65f * (1f - charge)),
                1.7f - charge * 0.65f, 1.7f - charge * 0.65f, 0.35f + charge * 0.55f);
            Vector3 nock = emitter - direction * (charge * 0.3f);
            Sprite(Arrow, nock, direction, 1.1f + charge * 0.5f, 1.55f + charge * 0.65f, 0.4f + charge * 0.6f);
            for (int i = 0; i < 6; i++)
            {
                float t = Mathf.Repeat(progress * 2.5f + i / 6f, 1f);
                Vector3 ray = Rotate(direction, i * 60f + progress * 45f);
                Vector3 point = emitter + ray * Mathf.Lerp(1.55f, 0.18f, t);
                Sprite(Wake, point, -ray, 0.45f, 0.6f, Mathf.Sin(t * Mathf.PI) * (0.3f + charge * 0.5f));
            }
        }

        public static void DrawFocusAim(Vector3 origin, Vector3 target, Vector3 direction,
            float progress = 0f, float ageSeconds = -1f)
        {
            Vector3 emitter = EmitterPosition(origin, direction);
            target = Raised(target);
            Vector3 path = (target - emitter).Yto0();
            float distance = path.magnitude;
            if (distance < 0.05f) return;
            direction = path / distance;
            float charge = Ease(progress);
            Stroke(emitter, target, 0.055f + charge * 0.025f, 0.42f + charge * 0.22f);
            if (ageSeconds < 0f) return;
            Sprite(Crown, target, Rotate(direction, -progress * 80f),
                1.65f - charge * 0.65f, 1.65f - charge * 0.65f, 0.5f + charge * 0.45f);
            // Small packets travel toward the target without obscuring intervening pawns.
            int count = Mathf.Clamp(Mathf.CeilToInt(distance / 5f), 2, 8);
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Repeat(i / (float)count + ageSeconds * 8f / distance, 1f);
                Sprite(Wake, Vector3.Lerp(emitter, target, t), direction, 0.3f, 0.55f,
                    Mathf.Sin(t * Mathf.PI) * charge * 0.6f);
            }
        }

        public static void DrawFocusArrow(Vector3 start, Vector3 end, float progress)
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
            Stroke(start, end, (0.09f + 0.2f * fade) * fade, fade * 0.9f);
            float travel = Mathf.Clamp01(progress * 6f);
            if (travel < 1f)
            {
                Vector3 head = Vector3.Lerp(start, end, travel);
                Sprite(Arrow, head, direction, 1.9f, 2.55f, 1f - travel * 0.25f);
                DrawWake(head, direction, Mathf.Min(distance * travel, 4f), 1.5f, fade);
            }
            float recoil = Mathf.Clamp01(progress * 3f);
            Sprite(Crown, start, Rotate(direction, recoil * 35f),
                1f + recoil, 1f + recoil, (1f - recoil) * 0.85f);
            DrawImpact(end, direction, progress, 1f);
        }

        public static void DrawImpact(Vector3 center, Vector3 direction, float progress, float strength)
        {
            if (progress >= 1f) return;
            center = Raised(center);
            float bloom = Ease(Mathf.Clamp01(progress * 2f));
            float fade = 1f - Ease(progress);
            float diameter = (0.65f + bloom * 1.6f) * strength;
            Sprite(Crown, center, Rotate(direction, progress * 45f), diameter, diameter, fade * 0.9f);
            int count = strength < 0.8f ? 4 : 8;
            for (int i = 0; i < count; i++)
            {
                Vector3 ray = Rotate(direction, i * 360f / count + 22.5f);
                Vector3 point = center + ray * ((0.12f + bloom * 0.7f) * strength);
                Sprite(Wake, point, ray, (0.8f - progress * 0.35f) * strength,
                    (0.55f + bloom * 0.65f) * strength, fade);
            }
        }

        public static void DrawSectorWarning(Vector3 origin, Vector3 direction, float radius, float arcDegrees, float alpha)
        {
            origin = Raised(origin);
            direction = direction.Yto0().normalized;
            Stroke(origin, origin + Rotate(direction, -arcDegrees * 0.5f) * radius, 0.075f, alpha);
            Stroke(origin, origin + Rotate(direction, arcDegrees * 0.5f) * radius, 0.075f, alpha);
            Arc(origin, direction, radius, arcDegrees, 24, 0.075f, alpha);
        }

        private static void DrawWake(Vector3 head, Vector3 direction, float length, float width, float alpha)
        {
            if (length < 0.05f) return;
            // The generated texture has clear margins; its hot tip sits at 90% V.
            Sprite(Wake, head - direction * length * 0.5f, direction, width, length / 0.8f, alpha);
        }

        private static void Arc(Vector3 origin, Vector3 direction, float radius, float arc, int segments, float width, float alpha)
        {
            if (radius < 0.05f) return;
            Vector3 previous = origin + Rotate(direction, -arc * 0.5f) * radius;
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = origin + Rotate(direction, arc * (i / (float)segments - 0.5f)) * radius;
                Stroke(previous, next, width, alpha);
                previous = next;
            }
        }

        private static void Stroke(Vector3 start, Vector3 end, float width, float alpha)
        {
            if (alpha < 0.02f || width < 0.005f || (end - start).sqrMagnitude < 0.0001f) return;
            GenDraw.DrawLineBetween(start, end, Pick(Ember, alpha), width);
            GenDraw.DrawLineBetween(start + Vector3.up * 0.004f, end + Vector3.up * 0.004f, Pick(Gold, alpha), width * 0.3f);
        }

        private static void Sprite(Material[] materials, Vector3 position, Vector3 direction, float width, float length, float alpha)
        {
            if (alpha < 0.02f || width <= 0f || length <= 0f || direction.sqrMagnitude < 0.001f) return;
            position.y = AltitudeLayer.MoteOverheadLow.AltitudeFor()
                + (materials == Arrow ? 0.06f : materials == Crown ? 0.02f : 0.04f);
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(position, Quaternion.LookRotation(direction), new Vector3(width, 1f, length)),
                Pick(materials, alpha), 0);
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

        private static Material Pick(Material[] materials, float alpha)
        {
            return materials[Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(alpha) * AlphaSteps) - 1, 0, AlphaSteps - 1)];
        }

        private static Material[] Materials(string path, Color color)
        {
            Material[] result = new Material[AlphaSteps];
            for (int i = 0; i < result.Length; i++)
            {
                Color tint = color;
                tint.a = (i + 1f) / result.Length;
                result[i] = MaterialPool.MatFrom(path, ShaderDatabase.Transparent, tint);
            }
            return result;
        }
    }
}
