using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>
    /// JSON data of the Sakura Dance cuts (the ring trail layers, petals and thunder effects of
    /// each of the three cuts): a pure art asset, self-contained numbers, colours and texture
    /// paths, no cross-references. Loaded by <see cref="CompProperties_AbilitySakuraDance"/> from
    /// its cutsDataPath (relative to the mod root) and reloaded by the "Reload JSON assets" debug
    /// action. JSON shape:
    /// { "cuts": [ { "time": 0.033, "offset": [x, y, z], "tiltX": 22.5, ..., "layers": [
    ///   { "texPath": "...", "radialOffset": -0.345, "width": 0.9, "alphaCurve": [[t, v], ...],
    ///     "color": [r, g, b, a], ... }, ... ], "petals": [...], "thunderDischarge": {...},
    ///   "thunderLayers": [...], "updraft": {...} }, ... ] }
    /// Missing fields keep the defaults of the corresponding props class.
    /// </summary>
    internal static class SakuraCutData
    {
        internal sealed class CutsData
        {
            public CutData[] cuts { get; set; }
        }

        internal sealed class CutData
        {
            public float? time { get; set; }
            public float[] offset { get; set; }
            public float? tiltX { get; set; }
            public float? tiltZ { get; set; }
            public float? startAngle { get; set; }
            public float? angularSpeed { get; set; }
            public float? spinSeconds { get; set; }
            public float? radius { get; set; }
            public float? damage { get; set; }
            public float? armorPenetration { get; set; }
            public float? petalStartAngle { get; set; }
            public float? petalRadius { get; set; }
            public LayerData[] layers { get; set; }
            public PetalData[] petals { get; set; }
            public LayerData[] thunderLayers { get; set; }
            public bool? thunderRelease { get; set; }
            public float? thunderRadiusFactor { get; set; }
            public float? thunderDamageFactor { get; set; }
            public float? posture { get; set; }
            public float? thunderPosture { get; set; }
            public DischargeData thunderDischarge { get; set; }
            public UpdraftData updraft { get; set; }
        }

        internal sealed class LayerData
        {
            public string texPath { get; set; }
            public bool? additive { get; set; }
            public bool? bloom { get; set; }
            public float? radialOffset { get; set; }
            public float? width { get; set; }
            public float[][] widthCurve { get; set; }
            public float? segmentDuration { get; set; }
            public float? startFade { get; set; }
            public float[][] startFadeCurve { get; set; }
            public float? endFade { get; set; }
            public float[][] endFadeCurve { get; set; }
            public float? alpha { get; set; }
            public float[][] alphaCurve { get; set; }
            public float? brightness { get; set; }
            public float[][] brightnessCurve { get; set; }
            public float? textureFraction { get; set; }
            public float? segmentInterval { get; set; }
            public float? speedU { get; set; }
            public float[][] speedUCurve { get; set; }
            public bool? attachedUV { get; set; }
            public bool? flipV { get; set; }
            public float? tailWidth { get; set; }
            public float? taperAnchor { get; set; }
            public float? tailAlpha { get; set; }
            public float? headBoost { get; set; }
            public float? headLength { get; set; }
            public float? widthNoise { get; set; }
            public float? alphaNoise { get; set; }
            public bool? liveFade { get; set; }
            public bool? clampAlpha { get; set; }
            public float[] color { get; set; }
            public int? frames { get; set; }
            public float? frameRate { get; set; }
            public bool? distortion { get; set; }
            public float? distortionIntensity { get; set; }
            public float[][] distortionIntensityCurve { get; set; }
            public string normalTexPath { get; set; }
            public float? shadeDarkness { get; set; }
        }

        internal sealed class PetalData
        {
            public float? lifetime { get; set; }
            public float? spacing { get; set; }
            public float? speed { get; set; }
            public float? turnDegrees { get; set; }
        }

        internal sealed class DischargeData
        {
            public float? delay { get; set; }
            public float? duration { get; set; }
            public float? falloff { get; set; }
            public float? interval { get; set; }
            public int? perBurst { get; set; }
            public float[] length { get; set; }
            public float? width { get; set; }
            public float? life { get; set; }
            public float? spreadDegrees { get; set; }
            public float? outward { get; set; }
            public int? branches { get; set; }
            public float? jitter { get; set; }
        }

        internal sealed class UpdraftData
        {
            public float? delay { get; set; }
            public float[] startOffset { get; set; }
            public float[] endOffset { get; set; }
            public float? riseSeconds { get; set; }
            public float? angularSpeed { get; set; }
            public float? spiralRadius { get; set; }
            public int? strands { get; set; }
            public float? emitSeconds { get; set; }
            public LayerData[] layers { get; set; }
            public string dustTexPath { get; set; }
            public float[] dustOffset { get; set; }
            public int? dustCount { get; set; }
            public float? dustInterval { get; set; }
            public float? dustLife { get; set; }
            public float? dustStartSize { get; set; }
            public float? dustEndSize { get; set; }
            public float? dustAlpha { get; set; }
            public float? dustSquash { get; set; }
            public float[] dustColor { get; set; }
        }

        /// <summary>Builds the runtime cut list from the parsed data; null/empty input gives an empty list.</summary>
        internal static List<SakuraCutProps> Map(CutsData data)
        {
            var result = new List<SakuraCutProps>(data?.cuts?.Length ?? 0);
            if (data?.cuts == null)
            {
                return result;
            }
            foreach (CutData d in data.cuts)
            {
                var cut = new SakuraCutProps();
                if (d.time.HasValue) cut.time = d.time.Value;
                if (d.offset != null) cut.offset = Vec3(d.offset);
                if (d.tiltX.HasValue) cut.tiltX = d.tiltX.Value;
                if (d.tiltZ.HasValue) cut.tiltZ = d.tiltZ.Value;
                if (d.startAngle.HasValue) cut.startAngle = d.startAngle.Value;
                if (d.angularSpeed.HasValue) cut.angularSpeed = d.angularSpeed.Value;
                if (d.spinSeconds.HasValue) cut.spinSeconds = d.spinSeconds.Value;
                if (d.radius.HasValue) cut.radius = d.radius.Value;
                if (d.damage.HasValue) cut.damage = d.damage.Value;
                if (d.armorPenetration.HasValue) cut.armorPenetration = d.armorPenetration.Value;
                if (d.petalStartAngle.HasValue) cut.petalStartAngle = d.petalStartAngle.Value;
                if (d.petalRadius.HasValue) cut.petalRadius = d.petalRadius.Value;
                cut.layers = MapLayers(d.layers);
                cut.petals = MapPetals(d.petals);
                cut.thunderLayers = MapLayers(d.thunderLayers);
                if (d.thunderRelease.HasValue) cut.thunderRelease = d.thunderRelease.Value;
                if (d.thunderRadiusFactor.HasValue) cut.thunderRadiusFactor = d.thunderRadiusFactor.Value;
                if (d.thunderDamageFactor.HasValue) cut.thunderDamageFactor = d.thunderDamageFactor.Value;
                if (d.posture.HasValue) cut.posture = d.posture.Value;
                if (d.thunderPosture.HasValue) cut.thunderPosture = d.thunderPosture.Value;
                if (d.thunderDischarge != null) cut.thunderDischarge = MapDischarge(d.thunderDischarge);
                if (d.updraft != null) cut.updraft = MapUpdraft(d.updraft);
                result.Add(cut);
            }
            return result;
        }

        private static List<SakuraTrailLayer> MapLayers(LayerData[] data)
        {
            var result = new List<SakuraTrailLayer>(data?.Length ?? 0);
            if (data == null)
            {
                return result;
            }
            foreach (LayerData d in data)
            {
                var layer = new SakuraTrailLayer();
                if (d.texPath != null) layer.texPath = d.texPath;
                if (d.additive.HasValue) layer.additive = d.additive.Value;
                if (d.bloom.HasValue) layer.bloom = d.bloom.Value;
                if (d.radialOffset.HasValue) layer.radialOffset = d.radialOffset.Value;
                if (d.width.HasValue) layer.width = d.width.Value;
                if (d.widthCurve != null) layer.widthCurve = Curve(d.widthCurve);
                if (d.segmentDuration.HasValue) layer.segmentDuration = d.segmentDuration.Value;
                if (d.startFade.HasValue) layer.startFade = d.startFade.Value;
                if (d.startFadeCurve != null) layer.startFadeCurve = Curve(d.startFadeCurve);
                if (d.endFade.HasValue) layer.endFade = d.endFade.Value;
                if (d.endFadeCurve != null) layer.endFadeCurve = Curve(d.endFadeCurve);
                if (d.alpha.HasValue) layer.alpha = d.alpha.Value;
                if (d.alphaCurve != null) layer.alphaCurve = Curve(d.alphaCurve);
                if (d.brightness.HasValue) layer.brightness = d.brightness.Value;
                if (d.brightnessCurve != null) layer.brightnessCurve = Curve(d.brightnessCurve);
                if (d.textureFraction.HasValue) layer.textureFraction = d.textureFraction.Value;
                if (d.segmentInterval.HasValue) layer.segmentInterval = d.segmentInterval.Value;
                if (d.speedU.HasValue) layer.speedU = d.speedU.Value;
                if (d.speedUCurve != null) layer.speedUCurve = Curve(d.speedUCurve);
                if (d.attachedUV.HasValue) layer.attachedUV = d.attachedUV.Value;
                if (d.flipV.HasValue) layer.flipV = d.flipV.Value;
                if (d.tailWidth.HasValue) layer.tailWidth = d.tailWidth.Value;
                if (d.taperAnchor.HasValue) layer.taperAnchor = d.taperAnchor.Value;
                if (d.tailAlpha.HasValue) layer.tailAlpha = d.tailAlpha.Value;
                if (d.headBoost.HasValue) layer.headBoost = d.headBoost.Value;
                if (d.headLength.HasValue) layer.headLength = d.headLength.Value;
                if (d.widthNoise.HasValue) layer.widthNoise = d.widthNoise.Value;
                if (d.alphaNoise.HasValue) layer.alphaNoise = d.alphaNoise.Value;
                if (d.liveFade.HasValue) layer.liveFade = d.liveFade.Value;
                if (d.clampAlpha.HasValue) layer.clampAlpha = d.clampAlpha.Value;
                if (d.color != null) layer.color = Col(d.color);
                if (d.frames.HasValue) layer.frames = d.frames.Value;
                if (d.frameRate.HasValue) layer.frameRate = d.frameRate.Value;
                if (d.distortion.HasValue) layer.distortion = d.distortion.Value;
                if (d.distortionIntensity.HasValue) layer.distortionIntensity = d.distortionIntensity.Value;
                if (d.distortionIntensityCurve != null) layer.distortionIntensityCurve = Curve(d.distortionIntensityCurve);
                if (d.normalTexPath != null) layer.normalTexPath = d.normalTexPath;
                if (d.shadeDarkness.HasValue) layer.shadeDarkness = d.shadeDarkness.Value;
                result.Add(layer);
            }
            return result;
        }

        private static List<SakuraPetalEmitter> MapPetals(PetalData[] data)
        {
            var result = new List<SakuraPetalEmitter>(data?.Length ?? 0);
            if (data == null)
            {
                return result;
            }
            foreach (PetalData d in data)
            {
                var petal = new SakuraPetalEmitter();
                if (d.lifetime.HasValue) petal.lifetime = d.lifetime.Value;
                if (d.spacing.HasValue) petal.spacing = d.spacing.Value;
                if (d.speed.HasValue) petal.speed = d.speed.Value;
                if (d.turnDegrees.HasValue) petal.turnDegrees = d.turnDegrees.Value;
                result.Add(petal);
            }
            return result;
        }

        private static SakuraDischargeProps MapDischarge(DischargeData d)
        {
            var p = new SakuraDischargeProps();
            if (d.delay.HasValue) p.delay = d.delay.Value;
            if (d.duration.HasValue) p.duration = d.duration.Value;
            if (d.falloff.HasValue) p.falloff = d.falloff.Value;
            if (d.interval.HasValue) p.interval = d.interval.Value;
            if (d.perBurst.HasValue) p.perBurst = d.perBurst.Value;
            if (d.length != null) p.length = new FloatRange(d.length[0], d.length[1]);
            if (d.width.HasValue) p.width = d.width.Value;
            if (d.life.HasValue) p.life = d.life.Value;
            if (d.spreadDegrees.HasValue) p.spreadDegrees = d.spreadDegrees.Value;
            if (d.outward.HasValue) p.outward = d.outward.Value;
            if (d.branches.HasValue) p.branches = d.branches.Value;
            if (d.jitter.HasValue) p.jitter = d.jitter.Value;
            return p;
        }

        private static SakuraUpdraftProps MapUpdraft(UpdraftData d)
        {
            var p = new SakuraUpdraftProps();
            if (d.delay.HasValue) p.delay = d.delay.Value;
            if (d.startOffset != null) p.startOffset = Vec3(d.startOffset);
            if (d.endOffset != null) p.endOffset = Vec3(d.endOffset);
            if (d.riseSeconds.HasValue) p.riseSeconds = d.riseSeconds.Value;
            if (d.angularSpeed.HasValue) p.angularSpeed = d.angularSpeed.Value;
            if (d.spiralRadius.HasValue) p.spiralRadius = d.spiralRadius.Value;
            if (d.strands.HasValue) p.strands = d.strands.Value;
            if (d.emitSeconds.HasValue) p.emitSeconds = d.emitSeconds.Value;
            p.layers = MapLayers(d.layers);
            if (d.dustTexPath != null) p.dustTexPath = d.dustTexPath;
            if (d.dustOffset != null) p.dustOffset = Vec3(d.dustOffset);
            if (d.dustCount.HasValue) p.dustCount = d.dustCount.Value;
            if (d.dustInterval.HasValue) p.dustInterval = d.dustInterval.Value;
            if (d.dustLife.HasValue) p.dustLife = d.dustLife.Value;
            if (d.dustStartSize.HasValue) p.dustStartSize = d.dustStartSize.Value;
            if (d.dustEndSize.HasValue) p.dustEndSize = d.dustEndSize.Value;
            if (d.dustAlpha.HasValue) p.dustAlpha = d.dustAlpha.Value;
            if (d.dustSquash.HasValue) p.dustSquash = d.dustSquash.Value;
            if (d.dustColor != null) p.dustColor = Col(d.dustColor);
            return p;
        }

        internal static string Validate(CutsData data)
        {
            if (data?.cuts == null || data.cuts.Length == 0)
            {
                return "missing cuts";
            }
            foreach (CutData cut in data.cuts)
            {
                string error = ValidateLayers(cut.layers) ?? ValidateLayers(cut.thunderLayers)
                    ?? (cut.updraft != null ? ValidateLayers(cut.updraft.layers) : null);
                if (error != null)
                {
                    return error;
                }
                if (cut.offset != null && cut.offset.Length != 3)
                {
                    return "a cut offset is not 3 numbers";
                }
                if (cut.thunderDischarge?.length != null && cut.thunderDischarge.length.Length != 2)
                {
                    return "a discharge length is not 2 numbers";
                }
            }
            return null;
        }

        private static string ValidateLayers(LayerData[] layers)
        {
            if (layers == null)
            {
                return null;
            }
            foreach (LayerData layer in layers)
            {
                if (layer == null || layer.texPath.NullOrEmpty())
                {
                    return "a layer has no texPath";
                }
                foreach (float[][] curve in new[] { layer.widthCurve, layer.startFadeCurve, layer.endFadeCurve,
                    layer.alphaCurve, layer.brightnessCurve, layer.speedUCurve, layer.distortionIntensityCurve })
                {
                    if (curve == null)
                    {
                        continue;
                    }
                    foreach (float[] p in curve)
                    {
                        if (p == null || p.Length != 2)
                        {
                            return "a layer curve point is not 2 numbers";
                        }
                    }
                }
                if (layer.color != null && layer.color.Length != 4)
                {
                    return "a layer color is not 4 numbers";
                }
            }
            return null;
        }

        private static SimpleCurve Curve(float[][] points)
        {
            if (points == null || points.Length == 0)
            {
                return null;
            }
            var curve = new SimpleCurve();
            foreach (float[] p in points)
            {
                curve.Add(p[0], p[1], sort: false);
            }
            curve.SortPoints();
            return curve;
        }

        private static Vector3 Vec3(float[] a)
        {
            return new Vector3(a[0], a[1], a[2]);
        }

        private static Color Col(float[] a)
        {
            return new Color(a[0], a[1], a[2], a[3]);
        }
    }
}
