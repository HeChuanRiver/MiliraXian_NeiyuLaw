using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>
    /// The bell's notes (RangedRework.md 2.9 "音效", owner 11-11): the pentatonic scale gong, shang,
    /// jue, zhi, yu (1, 9/8, 5/4, 3/2, 5/3) on gong = 1047 Hz. One sample (the bell tree note,
    /// measured at 1317 Hz) is shifted to every degree with <see cref="SoundInfo.pitchFactor"/>.
    /// A degree counts up the scale from the lowest gong; five degrees make an octave.
    /// </summary>
    public static class FlowerBellChimes
    {
        private const float SampleHz = 1317.1f;
        private const float GongHz = 1046.5f;
        private static readonly float[] Ratios = { 1f, 9f / 8f, 5f / 4f, 3f / 2f, 5f / 3f };
        /// <summary>The release arpeggio runs over this (owner 11-11: 0.2 s).</summary>
        public const int ArpeggioTicks = 12;
        /// <summary>A dropped tier: the same note a little lower (and the duller sample).</summary>
        private const float DullPitch = 0.94f;

        private static SoundDef note;
        private static SoundDef knock;
        private static SoundDef dull;

        private static SoundDef Note => note ??= DefDatabase<SoundDef>.GetNamedSilentFail("MX_QH_BellNote");
        private static SoundDef Knock => knock ??= DefDatabase<SoundDef>.GetNamedSilentFail("MX_QH_BellKnock");
        private static SoundDef DullNote => dull ??= DefDatabase<SoundDef>.GetNamedSilentFail("MX_QH_BellDull");

        public static float Pitch(int degree)
        {
            degree = Mathf.Max(0, degree);
            return GongHz / SampleHz * Ratios[degree % 5] * Mathf.Pow(2f, degree / 5);
        }

        public static void Ring(Map map, IntVec3 at, int degree, float volume = 1f)
        {
            Play(Note, map, at, Pitch(degree), volume);
        }

        public static void Dull(Map map, IntVec3 at, int degree)
        {
            Play(DullNote ?? Note, map, at, Pitch(degree) * DullPitch, 0.8f);
        }

        /// <summary>The release: the strike, then the run's notes rising over <see cref="ArpeggioTicks"/>.</summary>
        public static void Arpeggio(Map map, IntVec3 at, int firstDegree, int count)
        {
            Play(Knock, map, at, 1f, 1f);
            for (int i = 0; i < count; i++)
            {
                int delay = count <= 1 ? 0 : Mathf.RoundToInt(ArpeggioTicks * i / (float)(count - 1));
                FlowerBellChargeTracker.Schedule(map, at, firstDegree + i, delay);
            }
        }

        private static void Play(SoundDef def, Map map, IntVec3 at, float pitch, float volume)
        {
            if (def == null || map == null || !Find.Maps.Contains(map))
            {
                return;
            }
            SoundInfo info = SoundInfo.InMap(new TargetInfo(at, map));
            info.pitchFactor = pitch;
            info.volumeFactor = volume;
            def.PlayOneShot(info);
        }
    }

    /// <summary>
    /// Ticks the bells that hold a charge (to hear the decay drop tier by tier, the charge itself is
    /// computed lazily) and plays the delayed notes of a release arpeggio. Neither is saved: a
    /// loaded charge registers itself again, a cut arpeggio is just not heard.
    /// </summary>
    public sealed class FlowerBellChargeTracker : GameComponent
    {
        private struct Pending
        {
            public Map map;
            public IntVec3 at;
            public int degree;
            public int tick;
        }

        private static readonly List<CompFlowerBellCharge> Tracked = new();
        private static readonly List<Pending> Notes = new();

        public FlowerBellChargeTracker(Game game)
        {
        }

        internal static void Track(CompFlowerBellCharge comp)
        {
            if (!Tracked.Contains(comp))
            {
                Tracked.Add(comp);
            }
        }

        internal static void Schedule(Map map, IntVec3 at, int degree, int delayTicks)
        {
            if (delayTicks <= 0)
            {
                FlowerBellChimes.Ring(map, at, degree, 0.85f);
                return;
            }
            Notes.Add(new Pending { map = map, at = at, degree = degree, tick = Find.TickManager.TicksGame + delayTicks });
        }

        public override void StartedNewGame()
        {
            Tracked.Clear();
            Notes.Clear();
        }

        public override void LoadedGame()
        {
            // Comps loaded with a charge registered themselves in PostExposeData; drop anything older.
            Tracked.RemoveAll(c => c?.parent == null || c.parent.Destroyed);
            Notes.Clear();
        }

        public override void GameComponentTick()
        {
            for (int i = Tracked.Count - 1; i >= 0; i--)
            {
                CompFlowerBellCharge c = Tracked[i];
                if (c?.parent == null || c.parent.Destroyed || !c.TickTracked())
                {
                    Tracked.RemoveAt(i);
                }
            }
            int now = Find.TickManager.TicksGame;
            for (int i = Notes.Count - 1; i >= 0; i--)
            {
                if (Notes[i].tick <= now)
                {
                    Pending p = Notes[i];
                    Notes.RemoveAt(i);
                    FlowerBellChimes.Ring(p.map, p.at, p.degree, 0.85f);
                }
            }
        }
    }
}
