using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.SweepDamage
{
    /// <summary>
    /// A ring that spreads out from a point on the ground along a given radius curve and hits
    /// everything it passes, once each. The radius for every tick since the start is stored, so the
    /// visual and the damage read the same curve. Saved with the map.
    /// </summary>
    public sealed class ExpandingRingDamage : IExposable
    {
        public SweepDamageDef def;
        public Pawn caster;
        public ThingDef weaponDef;
        public DamageDef damageDef;
        /// <summary>The projectile that made the ring, for workers that read its on-hit data.</summary>
        public ThingDef projectileDef;
        /// <summary>Ring centre on the ground (y ignored).</summary>
        public Vector3 center;
        public float damage;
        public float armorPenetration;
        public int startTick;
        /// <summary>Radius at tick offsets 0..Count-1. The ring ends after the last entry.</summary>
        public List<float> radii = new();
        /// <summary>Hit on the first tick wherever it stands (the thing the projectile struck).</summary>
        public Thing guaranteedTarget;
        /// <summary>The attacked thing: hit when the ring reaches it whatever its side (as a vanilla attack on a selected target).</summary>
        public Thing primaryTarget;
        /// <summary>Multiplier for workers that add build-ups on a hit (the Flower Bell's later notes).</summary>
        public float abnormalFactor = 1f;

        private int lastProcessedOffset = -1;
        private List<Thing> hit = new();

        private static readonly List<Thing> TmpTargets = new();

        public bool Finished => lastProcessedOffset >= radii.Count - 1;

        public void Tick(Map map, int now)
        {
            int offset = Mathf.Min(now - startTick, radii.Count - 1);
            if (offset <= lastProcessedOffset || offset < 0)
            {
                return;
            }
            lastProcessedOffset = offset;
            float r = radii[offset];
            TmpTargets.Clear();
            SweepDamageSource source = ToSource(r);
            if (guaranteedTarget != null && !hit.Contains(guaranteedTarget) && Alive(guaranteedTarget, map)
                && (guaranteedTarget == primaryTarget || def.Worker.CanTarget(source, guaranteedTarget)))
            {
                TmpTargets.Add(guaranteedTarget);
            }
            IntVec3 c = center.ToIntVec3();
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(c, map, r + 1.5f, useCenter: true))
            {
                if (hit.Contains(t) || TmpTargets.Contains(t) || (t != primaryTarget && !def.Worker.CanTarget(source, t)))
                {
                    continue;
                }
                Vector3 rel = (t is Pawn ? t.DrawPos : GenThing.TrueCenter(t)) - center;
                rel.y = 0f;
                if (rel.magnitude - BodyRadius(t) > r)
                {
                    continue;
                }
                if (def.requireLineOfSight && !SweepTargeting.InSight(c, t, map))
                {
                    continue;
                }
                TmpTargets.Add(t);
            }
            if (def.pierceCells > 0)
            {
                AddPierced(map, source);
            }
            foreach (Thing t in TmpTargets)
            {
                hit.Add(t);
                // An earlier hit of the same tick can kill or destroy a later target.
                if (!Alive(t, map))
                {
                    continue;
                }
                if (!SweepHitInterceptors.TryIntercept(source, t))
                {
                    def.Worker.ApplyHit(source, t);
                }
            }
            TmpTargets.Clear();
        }

        private static readonly List<Thing> TmpPierced = new();

        /// <summary>
        /// For each sight-blocking thing reached this tick, the things up to <see cref="SweepDamageDef.pierceCells"/>
        /// cells straight behind it (away from the centre), as if the wave went through. Only one wall deep:
        /// what is reached through a wall does not pierce again.
        /// </summary>
        private void AddPierced(Map map, SweepDamageSource source)
        {
            TmpPierced.Clear();
            Vector3 c = new(center.x, 0f, center.z);
            foreach (Thing wall in TmpTargets)
            {
                if (wall is Pawn || wall.def.Fillage != FillCategory.Full)
                {
                    continue;
                }
                Vector3 from = GenThing.TrueCenter(wall).Yto0();
                Vector3 dir = from - c;
                if (dir.sqrMagnitude < 1e-4f)
                {
                    continue;
                }
                dir.Normalize();
                CellRect rect = wall.OccupiedRect();
                // Step out of the wall's own cells first, then up to pierceCells more.
                Vector3 p = from;
                int beyond = 0;
                for (int step = 0; step < 8 + def.pierceCells * 2 && beyond < def.pierceCells; step++)
                {
                    p += dir * 0.5f;
                    IntVec3 cell = p.ToIntVec3();
                    if (rect.Contains(cell))
                    {
                        continue;
                    }
                    if (!cell.InBounds(map))
                    {
                        break;
                    }
                    beyond = Mathf.Max(beyond, Mathf.CeilToInt(rect.ClosestCellTo(cell).DistanceTo(cell) - 0.01f));
                    List<Thing> things = cell.GetThingList(map);
                    for (int i = 0; i < things.Count; i++)
                    {
                        Thing t = things[i];
                        if (t == caster || hit.Contains(t) || TmpTargets.Contains(t) || TmpPierced.Contains(t)
                            || (t != primaryTarget && !def.Worker.CanTarget(source, t)))
                        {
                            continue;
                        }
                        TmpPierced.Add(t);
                    }
                }
            }
            TmpTargets.AddRange(TmpPierced);
            TmpPierced.Clear();
        }

        private static bool Alive(Thing t, Map map) =>
            !t.Destroyed && t.Spawned && t.MapHeld == map && t is not Pawn { Dead: true };

        private SweepDamageSource ToSource(float r)
        {
            return new SweepDamageSource
            {
                def = def,
                caster = caster,
                weaponDef = weaponDef,
                damageDef = damageDef,
                projectileDef = projectileDef,
                center = new Vector3(center.x, 0f, center.z),
                radius = r,
                damage = damage,
                armorPenetration = armorPenetration,
                startTick = startTick,
                resourceFactor = abnormalFactor,
            };
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
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Defs.Look(ref projectileDef, "projectileDef");
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref damage, "damage");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Collections.Look(ref radii, "radii", LookMode.Value);
            Scribe_References.Look(ref guaranteedTarget, "guaranteedTarget");
            Scribe_References.Look(ref primaryTarget, "primaryTarget");
            Scribe_Values.Look(ref abnormalFactor, "abnormalFactor", 1f);
            Scribe_Values.Look(ref lastProcessedOffset, "lastProcessedOffset", -1);
            Scribe_Collections.Look(ref hit, "hit", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                radii ??= new List<float>();
                hit ??= new List<Thing>();
                hit.RemoveAll(t => t == null);
            }
        }
    }
}
