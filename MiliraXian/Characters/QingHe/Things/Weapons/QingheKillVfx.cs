using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.QingHe.Abilities.SakuraDance;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>
    /// The word over an enemy Qinghe's execution killed, and the darkening around it: both fade in
    /// quickly, hold, then the word dissolves through a noise texture while the darkening fades.
    /// Map units and seconds.
    /// </summary>
    public class QingheKillVfxProps
    {
        public string texPath = "MiliraXianQinghe/Effect/Execution/Luohua";
        /// <summary>Height of the word, map units; its width follows the texture.</summary>
        public float height = 2.6f;
        public Vector2 offset;
        public Color color = Color.white;
        public float brightness = 1f;
        public ScreenBloomProperties bloom;

        public float fadeInSeconds = 0.1f;
        public float holdSeconds = 1.2f;
        public float dissolveSeconds = 0.8f;
        public string noiseTexPath = "MiliraXianQinghe/Effect/lotus_shield_dissolve_noise";
        public float noiseScale = 1f;
        public float edgeWidth = 0.08f;
        public Color edgeColor = new(1f, 0.6f, 0.7f, 1f);

        /// <summary>A soft black disc around the kill, at most this opaque at its centre.</summary>
        public float darkenRadius = 7f;
        public float darkenAlpha = 0.6f;
        /// <summary>Above 1 widens the fully dark core of the soft disc (the texture's alpha is scaled by it, then clamped).</summary>
        public float darkenFlatness = 1f;
        public string darkenTexPath = "MiliraXianQinghe/Effect/SakuraDance/Glow";

        public SoundDef sound;

        public float LifeSeconds => fadeInSeconds + holdSeconds + dissolveSeconds;
    }

    /// <summary>Draws the execution kill words of the map. Visual only and not saved (a couple of seconds each).</summary>
    public sealed class MapComponent_QingheKillVfx : MapComponent
    {
        private sealed class Kill
        {
            public QingheKillVfxProps props;
            public Vector3 at;
            public float startTicks;
        }

        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int AlphaMaxId = Shader.PropertyToID("_AlphaMax");

        private readonly List<Kill> kills = new();

        public MapComponent_QingheKillVfx(Map map) : base(map)
        {
        }

        internal static void Spawn(Map map, QingheKillVfxProps props, Vector3 at)
        {
            if (map == null || props == null)
            {
                return;
            }
            props.sound?.PlayOneShot(new TargetInfo(at.ToIntVec3(), map));
            map.GetComponent<MapComponent_QingheKillVfx>()?.kills.Add(new Kill
            {
                props = props,
                at = at,
                startTicks = Find.TickManager.TicksGame,
            });
        }

        public override void MapComponentDraw()
        {
            if (kills.Count == 0 || Find.CurrentMap != map)
            {
                return;
            }
            float ticks = VisualClock.Ticks;
            kills.RemoveAll(k => (ticks - k.startTicks) / 60f > k.props.LifeSeconds);
            try
            {
                foreach (Kill k in kills)
                {
                    float t = (ticks - k.startTicks) / 60f;
                    if (t >= 0f)
                    {
                        Draw(k, t);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce("[MiliraXian] Qinghe execution kill effect failed: " + ex, 0x51c7a8);
                kills.Clear();
            }
        }

        private static void Draw(Kill k, float t)
        {
            QingheKillVfxProps p = k.props;
            float fadeIn = p.fadeInSeconds > 0f ? Mathf.Clamp01(t / p.fadeInSeconds) : 1f;
            float dissolve = p.dissolveSeconds > 0f
                ? Mathf.Clamp01((t - p.fadeInSeconds - p.holdSeconds) / p.dissolveSeconds)
                : 0f;
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;

            // The darkening: a soft black disc, faded in with the word and out as it dissolves.
            Material dark = MapComponent_SakuraDanceVisuals.Material(p.darkenTexPath, "MX_QH_SakuraTrailAlpha");
            if (dark != null && p.darkenRadius > 0f && p.darkenAlpha > 0f)
            {
                block.Clear();
                block.SetColor(MapComponent_SakuraDanceVisuals.ColorId, Color.black);
                block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 1f);
                // The glow texture falls off from its centre; scaling its alpha (clamped to the
                // darkening's own) widens the dark core and keeps a soft rim.
                block.SetFloat(MapComponent_SakuraDanceVisuals.AlphaScaleId, Mathf.Max(1f, p.darkenFlatness));
                block.SetFloat(AlphaMaxId, p.darkenAlpha * fadeIn * (1f - dissolve));
                Vector3 pos = new(k.at.x, AltitudeLayer.MetaOverlays.AltitudeFor(), k.at.z);
                float d = p.darkenRadius * 2f;
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(d, 1f, d)), dark, 0, null, 0, block);
                block.Clear();
            }

            Material text = MapComponent_SakuraDanceVisuals.Material(p.texPath, "MX_QH_ExecutionText");
            Texture tex = text?.mainTexture;
            if (text == null || tex == null || dissolve >= 1f)
            {
                return;
            }
            float h = p.height;
            float w = h * tex.width / Mathf.Max(1f, tex.height);
            Vector3 at = new(k.at.x + p.offset.x, AltitudeLayer.MetaOverlays.AltitudeFor() + 0.01f, k.at.z + p.offset.y);
            // The texture loads with its rows the other way up from the plane's v: mirror the quad
            // north-south (not a half turn, which would also mirror it east-west).
            Matrix4x4 m = Matrix4x4.TRS(at, Quaternion.identity, new Vector3(w, 1f, -h));
            block.Clear();
            Color c = p.color;
            c.a *= fadeIn;
            block.SetColor(MapComponent_SakuraDanceVisuals.ColorId, c);
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, p.brightness);
            block.SetFloat(DissolveId, dissolve);
            block.SetFloat(NoiseScaleId, p.noiseScale);
            block.SetFloat(EdgeWidthId, p.edgeWidth);
            block.SetColor(EdgeColorId, p.edgeColor);
            Texture2D noise = p.noiseTexPath.NullOrEmpty() ? null : ContentFinder<Texture2D>.Get(p.noiseTexPath, reportFailure: false);
            if (noise != null)
            {
                block.SetTexture(NoiseTexId, noise);
            }
            if (p.bloom != null)
            {
                ScreenBloom.Submit(p.bloom, MeshPool.plane10, m, text, block);
            }
            Graphics.DrawMesh(MeshPool.plane10, m, text, 0, null, 0, block);
            block.Clear();
        }
    }
}
