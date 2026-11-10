using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.SweepDamage
{
    public enum AreaDamageShape
    {
        Circle,
        Capsule,
    }

    public sealed class AreaDamageBurst : IExposable
    {
        public SweepDamageDef def;
        public Pawn caster;
        public ThingDef weaponDef;
        public DamageDef damageDef;
        public AreaDamageShape shape = AreaDamageShape.Circle;
        public Vector3 center;
        /// <summary>Capsule only: the other end of the segment from <see cref="center"/>.</summary>
        public Vector3 end;
        public float radius;
        public float damage;
        public float armorPenetration;
        public float posture;

        private int dueTick = -1;
        private List<Thing> targets = new();

        private static readonly List<Thing> TmpTargets = new();

        internal bool Due(int now) => now >= dueTick;

        public int Fire(Map map, List<Thing> struck = null, List<Thing> ignore = null)
        {
            TmpTargets.Clear();
            CollectTargets(map, TmpTargets);
            if (ignore != null)
            {
                TmpTargets.RemoveAll(ignore.Contains);
            }
            int count = TmpTargets.Count;
            if (count == 0)
            {
                return 0;
            }
            struck?.AddRange(TmpTargets);
            if (def.damageDelayTicks > 0)
            {
                targets = new List<Thing>(TmpTargets);
                dueTick = Find.TickManager.TicksGame + def.damageDelayTicks;
                MapComponent_SweepDamage.AddBurst(map, this);
            }
            else
            {
                Apply(TmpTargets);
            }
            TmpTargets.Clear();
            return count;
        }

        public void CollectTargets(Map map, List<Thing> into)
        {
            SweepDamageSource source = ToSource();
            bool capsule = shape == AreaDamageShape.Capsule;
            Vector3 mid = capsule ? (center + end) * 0.5f : center;
            float reach = capsule ? radius + (end - center).Yto0().magnitude * 0.5f : radius;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(mid.ToIntVec3(), map, reach, useCenter: true))
            {
                IntVec3 c = capsule ? ClosestOnSegment(GenThing.TrueCenter(t)).ToIntVec3() : center.ToIntVec3();
                if (t.OccupiedRect().ClosestCellTo(c).DistanceTo(c) > radius || !def.Worker.CanTarget(source, t) || into.Contains(t))
                {
                    continue;
                }
                if (def.requireLineOfSight && !SweepTargeting.InSight(c, t, map))
                {
                    continue;
                }
                into.Add(t);
            }
        }

        private Vector3 ClosestOnSegment(Vector3 p)
        {
            Vector3 a = center.Yto0();
            Vector3 ab = end.Yto0() - a;
            float len = ab.sqrMagnitude;
            float u = len > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p.Yto0() - a, ab) / len) : 0f;
            return a + ab * u;
        }

        internal void ApplyDue(Map map)
        {
            targets.RemoveAll(t => t.Destroyed || t.MapHeld != map || t is Pawn { Dead: true });
            Apply(targets);
            targets.Clear();
        }

        private void Apply(List<Thing> list)
        {
            SweepDamageSource source = ToSource();
            foreach (Thing t in list)
            {
                // An earlier hit of the same burst can kill or destroy a later target.
                if (t.Destroyed)
                {
                    continue;
                }
                if (!SweepHitInterceptors.TryIntercept(source, t))
                {
                    def.Worker.ApplyHit(source, t);
                }
            }
        }

        private SweepDamageSource ToSource()
        {
            return new SweepDamageSource
            {
                def = def,
                caster = caster,
                weaponDef = weaponDef,
                damageDef = damageDef,
                center = new Vector3(center.x, 0f, center.z),
                radius = radius,
                damage = damage,
                armorPenetration = armorPenetration,
                posture = posture,
                startTick = Find.TickManager.TicksGame,
            };
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref caster, "caster");
            Scribe_Defs.Look(ref weaponDef, "weaponDef");
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref shape, "shape");
            Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref end, "end");
            Scribe_Values.Look(ref radius, "radius");
            Scribe_Values.Look(ref damage, "damage");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref posture, "posture");
            Scribe_Values.Look(ref dueTick, "dueTick", -1);
            Scribe_Collections.Look(ref targets, "targets", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                targets.RemoveAll(t => t == null);
            }
        }
    }
}
