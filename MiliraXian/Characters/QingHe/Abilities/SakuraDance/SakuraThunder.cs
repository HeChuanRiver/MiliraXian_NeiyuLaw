using MiliraXian.Characters.Common.SweepDamage;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>Settings of the Lightning Reversal sweep, on its <see cref="SweepDamageDef"/>.</summary>
    public class SakuraThunderSweepExtension : DefModExtension
    {
        public int stunTicks = 120;
    }

    /// <summary>The release cut of the Lightning Reversal: normal sweep damage, then a stun.</summary>
    public class SakuraThunderSweepWorker : SweepDamageWorker
    {
        public override void ApplyHit(SweepDamageSource source, Thing target)
        {
            base.ApplyHit(source, target);
            int ticks = def.GetModExtension<SakuraThunderSweepExtension>()?.stunTicks ?? 0;
            if (ticks > 0 && target is Pawn { Dead: false, Spawned: true } pawn)
            {
                pawn.stances?.stunner?.StunFor(ticks, source.caster);
            }
        }
    }

    /// <summary>
    /// Sky flash of the caught thunder: the vanilla lightning flash (sky colour and shadow) with a
    /// short fixed duration and the on-map thunder at the caster. It does no damage; the vanilla
    /// strike (<c>WeatherEvent_LightningStrike.DoStrike</c>) is not used because it explodes in flame.
    /// </summary>
    public class WeatherEvent_SakuraThunderFlash : WeatherEvent_LightningFlash
    {
        private readonly IntVec3 cell;
        private readonly SoundDef sound;

        public WeatherEvent_SakuraThunderFlash(Map map, IntVec3 cell, SoundDef sound, int durationTicks) : base(map)
        {
            this.cell = cell;
            this.sound = sound;
            duration = durationTicks;
        }

        public override void FireEvent()
        {
            (sound ?? SoundDefOf.Thunder_OnMap).PlayOneShot(SoundInfo.InMap(new TargetInfo(cell, map)));
        }
    }

    public static class SakuraThunderUtility
    {
        /// <summary>
        /// Open sky at the caster and one of: rain at full rate (the weather change ramps it over
        /// 4000 ticks), a fully set-in weather with lightning events (thunderstorms, rainy or dry),
        /// or the clear-sky chance (rolled from <paramref name="seed"/>).
        /// </summary>
        public static bool CanCatch(Pawn pawn, CompProperties_AbilitySakuraDance props, int seed)
        {
            Map map = pawn?.Map;
            if (map == null || props == null || map.roofGrid.Roofed(pawn.Position))
            {
                return false;
            }
            WeatherManager weather = map.weatherManager;
            if (weather.RainRate >= props.thunderMinRainRate - 1e-4f)
            {
                return true;
            }
            if (props.thunderInStorms && weather.TransitionLerpFactor >= 1f && HasLightning(weather.curWeather))
            {
                return true;
            }
            return props.thunderClearSkyChance > 0f && Rand.ChanceSeeded(props.thunderClearSkyChance, seed);
        }

        /// <summary>The weather makes lightning flashes or strikes (vanilla or modded subclasses).</summary>
        public static bool HasLightning(WeatherDef weather)
        {
            if (weather?.eventMakers == null)
            {
                return false;
            }
            foreach (WeatherEventMaker maker in weather.eventMakers)
            {
                if (maker?.eventClass != null && typeof(WeatherEvent_LightningFlash).IsAssignableFrom(maker.eventClass))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Map draw position of a body-relative world offset (the blade renderer's mapping, without depth).</summary>
        public static Vector3 BodyOffsetToMap(Vector3 bodyCenter, Vector3 offset, float heightToScreen)
        {
            return new Vector3(bodyCenter.x + offset.x, bodyCenter.y, bodyCenter.z + offset.z + offset.y * heightToScreen);
        }
    }
}
