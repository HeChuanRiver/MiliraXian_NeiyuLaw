
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters.Neiyu.Cultivation;

namespace MiliraXian.Characters.Neiyu
{
    public class CompProperties_AbilityNeiyuArrowBarrage : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;
        public ThingDef projectileDef;
        public int shotCount = 108;
        public int shotIntervalTicks = 2;
        public float maxDistance = 999f;
        public float lateralSpread = 5f;

        public CompProperties_AbilityNeiyuArrowBarrage()
        {
            compClass = typeof(CompAbilityEffect_NeiyuArrowBarrage);
        }
    }

    public class CompAbilityEffect_NeiyuArrowBarrage : CompAbilityEffect
    {
        private new CompProperties_AbilityNeiyuArrowBarrage Props
        {
            get { return (CompProperties_AbilityNeiyuArrowBarrage)props; }
        }

        public override bool ShouldHideGizmo
        {
            get
            {
                Pawn pawn = parent != null ? parent.pawn : null;
                if (pawn == null)
                {
                    return true;
                }

                if (Props.requiredWeapon == null)
                {
                    return false;
                }

                return pawn.equipment == null || pawn.equipment.Primary == null || pawn.equipment.Primary.def != Props.requiredWeapon;
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (NeiyuPowerBalance.AbilitiesDisabled)
            {
                reason = NeiyuPowerBalance.AbilitiesDisabledReason;
                return true;
            }

            if (!HasRequiredWeapon(parent != null ? parent.pawn : null))
            {
                reason = "MX_NL_NeedBowForm".Translate().ToString();
                return true;
            }

            if (Props.projectileDef == null)
            {
                reason = "MX_NL_ProjectileDefMissing".Translate().ToString();
                return true;
            }

            reason = null;
            return false;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (NeiyuPowerBalance.AbilitiesDisabled)
            {
                if (throwMessages)
                {
                    Messages.Message(NeiyuPowerBalance.AbilitiesDisabledReason, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            Pawn caster = parent != null ? parent.pawn : null;
            if (!HasRequiredWeapon(caster))
            {
                if (throwMessages)
                {
                    Messages.Message("MX_NL_NeedBowForm".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (!target.IsValid)
            {
                if (throwMessages)
                {
                    Messages.Message("MX_NL_NeedTargetCell".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (NeiyuPowerBalance.AbilitiesDisabled)
            {
                return;
            }

            base.Apply(target, dest);

            Pawn caster = parent != null ? parent.pawn : null;
            Map map = caster != null ? caster.MapHeld : null;
            if (caster == null || map == null)
            {
                return;
            }

            IntVec3 aimCell = target.IsValid ? target.Cell : caster.Position + caster.Rotation.FacingCell;
            NeiyuCombatMapComponent component = map.GetComponent<NeiyuCombatMapComponent>();
            if (component == null)
            {
                return;
            }

            component.ScheduleArrowBarrage(caster, aimCell, Props);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent != null ? parent.pawn : null;
            if (caster == null || caster.Map == null || !target.IsValid)
            {
                return;
            }

            GenDraw.DrawLineBetween(caster.DrawPos, target.Cell.ToVector3Shifted());
            GenDraw.DrawRadiusRing(target.Cell, 1.2f, Color.yellow);
        }

        private bool HasRequiredWeapon(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (Props.requiredWeapon == null)
            {
                return true;
            }

            return pawn.equipment != null && pawn.equipment.Primary != null && pawn.equipment.Primary.def == Props.requiredWeapon;
        }
    }

    public partial class NeiyuCombatMapComponent : MapComponent
    {
        private class BarrageTask : IExposable
        {
            public Pawn caster;
            public IntVec3 aimCell;
            public Vector3 fireDirection;
            public ThingDef projectileDef;
            public int totalShots;
            public int remainingShots;
            public int firedShots;
            public int spreadSeed;
            public int nextShotTick;
            public int shotIntervalTicks;
            public float maxDistance;
            public float lateralSpread;

            public void ExposeData()
            {
                Scribe_References.Look(ref caster, "caster");
                Scribe_Values.Look(ref aimCell, "aimCell");
                Scribe_Values.Look(ref fireDirection, "fireDirection");
                Scribe_Defs.Look(ref projectileDef, "projectileDef");
                Scribe_Values.Look(ref totalShots, "totalShots", 0);
                Scribe_Values.Look(ref remainingShots, "remainingShots", 0);
                Scribe_Values.Look(ref firedShots, "firedShots", 0);
                Scribe_Values.Look(ref spreadSeed, "spreadSeed", 0);
                Scribe_Values.Look(ref nextShotTick, "nextShotTick", 0);
                Scribe_Values.Look(ref shotIntervalTicks, "shotIntervalTicks", 2);
                Scribe_Values.Look(ref maxDistance, "maxDistance", 999f);
                Scribe_Values.Look(ref lateralSpread, "lateralSpread", 5f);
            }
        }

        private List<BarrageTask> barrageTasks = new();

        public NeiyuCombatMapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            ExposeThunderData();
            Scribe_Collections.Look(ref barrageTasks, "mxnl_barrageTasks", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                barrageTasks ??= new();
            }
        }

        public void ScheduleArrowBarrage(Pawn caster, IntVec3 aimCell, CompProperties_AbilityNeiyuArrowBarrage props)
        {
            if (caster == null || caster.MapHeld != map || props == null || props.projectileDef == null)
            {
                return;
            }

            BarrageTask task = new();
            task.caster = caster;
            task.aimCell = aimCell;
            task.fireDirection = GetFireDirection(caster, aimCell);
            task.projectileDef = props.projectileDef;
            task.totalShots = Cultivation.CultivationPower.BarrageCount(caster, props.shotCount);
            task.remainingShots = task.totalShots;
            task.firedShots = 0;
            task.spreadSeed = Rand.Int;
            task.nextShotTick = Find.TickManager.TicksGame;
            task.shotIntervalTicks = Mathf.Max(1, props.shotIntervalTicks);
            task.maxDistance = Mathf.Max(10f, props.maxDistance);
            task.lateralSpread = Mathf.Max(0.5f, props.lateralSpread);
            barrageTasks.Add(task);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (NeiyuPowerBalance.AbilitiesDisabled)
            {
                CancelPendingAbilities();
                return;
            }

            TickThunder();

            if (!barrageTasks.NullOrEmpty())
            {
                for (int i = barrageTasks.Count - 1; i >= 0; i--)
                {
                    if (barrageTasks[i] == null || !ProcessBarrageTask(barrageTasks[i]))
                    {
                        barrageTasks.RemoveAt(i);
                    }
                }
            }
        }

        private bool ProcessBarrageTask(BarrageTask task)
        {
            if (task.caster == null || task.caster.Destroyed || task.caster.Dead || !task.caster.Spawned || task.caster.MapHeld != map)
            {
                return false;
            }

            if (task.projectileDef == null)
            {
                return false;
            }

            int now = Find.TickManager.TicksGame;
            if (now < task.nextShotTick)
            {
                return true;
            }

            task.remainingShots = Mathf.Min(task.remainingShots, NeiyuPowerBalance.BarrageShotCap);

            LaunchBarrageProjectile(task);

            task.firedShots++;
            task.remainingShots--;
            if (task.remainingShots <= 0)
            {
                return false;
            }

            task.nextShotTick = now + task.shotIntervalTicks;
            return true;
        }

        private void CancelPendingAbilities()
        {
            CancelThunder();

            if (!barrageTasks.NullOrEmpty())
            {
                barrageTasks.Clear();
            }
        }

        private void LaunchBarrageProjectile(BarrageTask task)
        {
            Vector3 origin = task.caster.DrawPos;
            Vector3 forward = task.fireDirection;
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = GetFireDirection(task.caster, task.aimCell);
            }
            Vector3 perp = new(-forward.z, 0f, forward.x);

            float edgeDistance = GetDistanceToMapEdge(map, origin, forward);
            float maxDistance = task.maxDistance > 0f ? Mathf.Min(task.maxDistance, edgeDistance) : edgeDistance;
            if (maxDistance <= 1f)
            {
                return;
            }

            float minDistance = Mathf.Min(8f, maxDistance * 0.2f);
            float distance = Rand.Range(minDistance, maxDistance);
            float lateral = GetAntiClusterLateralOffset(task.firedShots, task.totalShots, task.spreadSeed, task.lateralSpread);

            Vector3 hitPos = origin + forward * distance + perp * lateral;
            IntVec3 hitCell = ClampToMap(hitPos.ToIntVec3(), map);

            Thing projectileThing = ThingMaker.MakeThing(task.projectileDef);
            Thing spawned = GenSpawn.Spawn(projectileThing, task.caster.Position, map);
            Projectile projectile = spawned as Projectile;
            if (projectile == null)
            {
                if (spawned != null && !spawned.Destroyed)
                {
                    spawned.Destroy(DestroyMode.Vanish);
                }
                return;
            }

            Thing equipment = task.caster.equipment != null ? task.caster.equipment.Primary : null;
            LocalTargetInfo usedTarget = new(hitCell);
            LocalTargetInfo intendedTarget = new(hitCell);
            projectile.Launch(task.caster, origin, usedTarget, intendedTarget, ProjectileHitFlags.All, preventFriendlyFire: false, equipment: equipment, targetCoverDef: null);
        }

        private static Vector3 GetFireDirection(Pawn caster, IntVec3 aimCell)
        {
            if (caster == null)
            {
                return Vector3.forward;
            }

            Vector3 origin = caster.DrawPos;
            Vector3 dir = aimCell.ToVector3Shifted() - origin;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
            {
                IntVec3 facing = caster.Rotation.FacingCell;
                dir = new Vector3(facing.x, 0f, facing.z);
            }

            dir.Normalize();
            return dir;
        }

        private static float GetDistanceToMapEdge(Map map, Vector3 origin, Vector3 dir)
        {
            if (map == null || dir.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            float minX = 0.5f;
            float maxX = map.Size.x - 0.5f;
            float minZ = 0.5f;
            float maxZ = map.Size.z - 0.5f;

            float tx = float.PositiveInfinity;
            if (Mathf.Abs(dir.x) > 0.0001f)
            {
                tx = dir.x > 0f ? (maxX - origin.x) / dir.x : (minX - origin.x) / dir.x;
            }

            float tz = float.PositiveInfinity;
            if (Mathf.Abs(dir.z) > 0.0001f)
            {
                tz = dir.z > 0f ? (maxZ - origin.z) / dir.z : (minZ - origin.z) / dir.z;
            }

            float t = Mathf.Min(tx, tz);
            if (float.IsInfinity(t) || float.IsNaN(t))
            {
                return 0f;
            }

            return Mathf.Max(0f, t);
        }

        private static float GetAntiClusterLateralOffset(int firedShots, int totalShots, int seed, float corridorWidth)
        {
            float half = corridorWidth * 0.5f;
            if (half <= 0.001f || totalShots <= 1)
            {
                return 0f;
            }

            int slotCount = Mathf.Max(4, Mathf.RoundToInt(corridorWidth / 0.55f));
            int safeShot = Mathf.Clamp(firedShots, 0, totalShots - 1);
            int cycle = safeShot / slotCount;
            int indexInCycle = safeShot % slotCount;

            int localSeed = seed ^ (cycle * 486187739);
            int start = PositiveMod(localSeed, slotCount);
            int step = GetCoprimeStep(slotCount, localSeed * 31 + 7);
            int permIndex = (start + indexInCycle * step) % slotCount;

            float t = (permIndex + 0.5f) / slotCount;
            float laneWidth = corridorWidth / slotCount;
            float jitter = (Hash01(localSeed + safeShot * 92821) - 0.5f) * laneWidth * 0.7f;
            float lateral = Mathf.Lerp(-half, half, t) + jitter;
            return Mathf.Clamp(lateral, -half, half);
        }

        private static int GetCoprimeStep(int modulo, int seed)
        {
            int step = PositiveMod(seed, modulo);
            if (step == 0) step = 1;
            while (Gcd(step, modulo) != 1)
            {
                step++;
                if (step >= modulo) step = 1;
            }
            return step;
        }

        private static int Gcd(int a, int b)
        {
            a = Mathf.Abs(a);
            b = Mathf.Abs(b);
            while (b != 0)
            {
                int t = a % b;
                a = b;
                b = t;
            }
            return a == 0 ? 1 : a;
        }

        private static int PositiveMod(int value, int mod)
        {
            int r = value % mod;
            return r < 0 ? r + mod : r;
        }

        private static float Hash01(int x)
        {
            unchecked
            {
                uint u = (uint)x;
                u ^= 2747636419u;
                u *= 2654435769u;
                u ^= u >> 16;
                u *= 2654435769u;
                u ^= u >> 16;
                return (u & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static IntVec3 ClampToMap(IntVec3 cell, Map map)
        {
            int x = Mathf.Clamp(cell.x, 0, map.Size.x - 1);
            int z = Mathf.Clamp(cell.z, 0, map.Size.z - 1);
            return new IntVec3(x, 0, z);
        }
    }

}
