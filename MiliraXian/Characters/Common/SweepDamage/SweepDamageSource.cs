using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.SweepDamage
{
    /// <summary>
    /// One active sector sweep on the ground plane. The blade direction for every tick since the
    /// start is stored as a heading (degrees, counter-clockwise from +x = east, unwrapped), so the
    /// visual and the damage read the same curve. Each tick, the sector swept since the previous
    /// tick is tested against every candidate's current position; the first contact with a target
    /// locks it in, and its damage lands after <see cref="SweepDamageDef.damageDelayTicks"/>.
    /// </summary>
    public sealed class SweepDamageSource : IExposable
    {
        private sealed class PendingHit : IExposable
        {
            public Thing target;
            public int dueTick;

            public void ExposeData()
            {
                Scribe_References.Look(ref target, "target");
                Scribe_Values.Look(ref dueTick, "dueTick");
            }
        }

        public SweepDamageDef def;
        public Pawn caster;
        public ThingDef weaponDef;
        /// <summary>Sweep centre on the ground (y ignored).</summary>
        public Vector3 center;
        public float radius;
        public float damage;
        public float armorPenetration;
        public int startTick;
        /// <summary>Heading at tick offsets 0..Count-1. The sweep ends after the last entry.</summary>
        public List<float> headings = new();

        private int lastProcessedOffset = -1;
        private List<Thing> hit = new();
        private List<PendingHit> pending = new();

        private static readonly List<Thing> TmpCandidates = new();
        private static readonly HashSet<Thing> TmpSeen = new();

        public bool Finished => lastProcessedOffset >= headings.Count - 1 && pending.Count == 0;

        /// <summary>Linear heading curve: <paramref name="startHeading"/> + <paramref name="degreesPerSecond"/> * t for <paramref name="durationTicks"/>.</summary>
        public static List<float> LinearHeadings(float startHeading, float degreesPerSecond, int durationTicks)
        {
            List<float> list = new(durationTicks + 1);
            for (int i = 0; i <= durationTicks; i++)
            {
                list.Add(startHeading + degreesPerSecond * i / 60f);
            }
            return list;
        }

        public static float HeadingOf(Vector3 v)
        {
            return Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg;
        }

        public void Tick(Map map, int now)
        {
            int offset = Mathf.Min(now - startTick, headings.Count - 1);
            if (offset > lastProcessedOffset && offset >= 1 && caster != null && caster.Spawned && caster.MapHeld == map)
            {
                // Catch up every tick interval not yet tested (normally one).
                int from = Mathf.Max(lastProcessedOffset, 0);
                CollectCandidates(map);
                for (int i = from; i < offset; i++)
                {
                    TestInterval(map, headings[i], headings[i + 1], now);
                }
            }
            if (offset > lastProcessedOffset)
            {
                lastProcessedOffset = offset;
            }

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingHit p = pending[i];
                if (now < p.dueTick)
                {
                    continue;
                }
                pending.RemoveAt(i);
                // Decided at contact: only a target that no longer exists on this map is skipped.
                Thing t = p.target;
                if (t == null || t.Destroyed || !t.Spawned || t.MapHeld != map || t is Pawn { Dead: true })
                {
                    continue;
                }
                def.Worker.ApplyHit(this, t);
            }
        }

        private void CollectCandidates(Map map)
        {
            TmpCandidates.Clear();
            TmpSeen.Clear();
            IntVec3 c = center.ToIntVec3();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(c, radius + 1.5f, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (TmpSeen.Add(t) && !hit.Contains(t) && def.Worker.CanTarget(this, t))
                    {
                        TmpCandidates.Add(t);
                    }
                }
            }
        }

        private void TestInterval(Map map, float h0, float h1, int now)
        {
            float lo = Mathf.Min(h0, h1);
            float span = Mathf.Abs(h1 - h0);
            for (int i = 0; i < TmpCandidates.Count; i++)
            {
                Thing t = TmpCandidates[i];
                if (hit.Contains(t))
                {
                    continue;
                }
                Vector3 rel = (t is Pawn ? t.DrawPos : GenThing.TrueCenter(t)) - center;
                rel.y = 0f;
                float bodyRadius = BodyRadius(t);
                float dist = rel.magnitude;
                if (dist > radius + bodyRadius)
                {
                    continue;
                }
                bool inside;
                if (dist <= bodyRadius || span >= 360f)
                {
                    inside = true;
                }
                else
                {
                    float pad = Mathf.Asin(Mathf.Clamp01(bodyRadius / dist)) * Mathf.Rad2Deg;
                    float d = Mathf.Repeat(HeadingOf(rel) - lo + pad, 360f);
                    inside = d <= span + 2f * pad;
                }
                if (!inside)
                {
                    continue;
                }
                if (def.requireLineOfSight && !GenSight.LineOfSight(caster.Position, t.Position, map, skipFirstCell: true))
                {
                    continue;
                }
                hit.Add(t);
                pending.Add(new PendingHit { target = t, dueTick = now + Mathf.Max(0, def.damageDelayTicks) });
            }
        }

        private static float BodyRadius(Thing t)
        {
            if (t is Pawn p)
            {
                return Mathf.Clamp(0.3f * Mathf.Sqrt(p.BodySize), 0.2f, 1.2f);
            }
            IntVec2 size = t.def.size;
            return 0.5f * Mathf.Max(1, Mathf.Min(size.x, size.z));
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref caster, "caster");
            Scribe_Defs.Look(ref weaponDef, "weaponDef");
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref radius, "radius");
            Scribe_Values.Look(ref damage, "damage");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref lastProcessedOffset, "lastProcessedOffset", -1);
            Scribe_Collections.Look(ref headings, "headings", LookMode.Value);
            Scribe_Collections.Look(ref hit, "hit", LookMode.Reference);
            Scribe_Collections.Look(ref pending, "pending", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                headings ??= new List<float>();
                hit ??= new List<Thing>();
                hit.RemoveAll(t => t == null);
                pending ??= new List<PendingHit>();
                pending.RemoveAll(p => p?.target == null);
            }
        }
    }
}
