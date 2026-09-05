
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Neiyu
{
    public class CompProperties_AbilityNeiyuThunderSigil : CompProperties_AbilityEffect
    {
        public float radius = 3f;
        public IntRange strikeCountRange = new(3, 5);
        public IntRange firstDelayTicksRange = new(120, 180);
        public IntRange strikeIntervalTicksRange = new(45, 75);
        public int damageAmount = 60;
        public int empDamageAmount = 20;
        public HediffDef markerHediff;

        public CompProperties_AbilityNeiyuThunderSigil()
        {
            compClass = typeof(CompAbilityEffect_NeiyuThunderSigil);
        }
    }

    public class CompAbilityEffect_NeiyuThunderSigil : CompAbilityEffect
    {
        private new CompProperties_AbilityNeiyuThunderSigil Props
        {
            get { return (CompProperties_AbilityNeiyuThunderSigil)props; }
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (NeiyuPowerBalance.AbilitiesDisabled)
            {
                reason = NeiyuPowerBalance.AbilitiesDisabledReason;
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
            if (caster == null || map == null || !target.IsValid)
            {
                return;
            }

            NeiyuCombatMapComponent component = map.GetComponent<NeiyuCombatMapComponent>();
            if (component == null)
            {
                return;
            }

            int marked = component.ScheduleThunderMarks(caster, target.Cell, Props);
            if (marked <= 0)
            {
                Messages.Message("MX_NL_NoMarkTargetsInRange".Translate(), caster, MessageTypeDefOf.RejectInput);
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return;
            }

            GenDraw.DrawRadiusRing(target.Cell, Props.radius, Color.cyan);
        }
    }

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

    public class NeiyuCombatMapComponent : MapComponent
    {
        private class ThunderTask : IExposable
        {
            public Pawn caster;
            public Pawn target;
            public int remainingStrikes;
            public int nextStrikeTick;
            public int intervalMin;
            public int intervalMax;
            public int damageAmount;
            public int empDamageAmount;
            public HediffDef markerHediff;

            public void ExposeData()
            {
                Scribe_References.Look(ref caster, "caster");
                Scribe_References.Look(ref target, "target");
                Scribe_Values.Look(ref remainingStrikes, "remainingStrikes", 0);
                Scribe_Values.Look(ref nextStrikeTick, "nextStrikeTick", 0);
                Scribe_Values.Look(ref intervalMin, "intervalMin", 45);
                Scribe_Values.Look(ref intervalMax, "intervalMax", 75);
                Scribe_Values.Look(ref damageAmount, "damageAmount", 60);
                Scribe_Values.Look(ref empDamageAmount, "empDamageAmount", 20);
                Scribe_Defs.Look(ref markerHediff, "markerHediff");
            }
        }

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

        private List<ThunderTask> thunderTasks = new();
        private List<BarrageTask> barrageTasks = new();

        public NeiyuCombatMapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref thunderTasks, "mxnl_thunderTasks", LookMode.Deep);
            Scribe_Collections.Look(ref barrageTasks, "mxnl_barrageTasks", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                thunderTasks ??= new();

                barrageTasks ??= new();
            }
        }

        public int ScheduleThunderMarks(Pawn caster, IntVec3 center, CompProperties_AbilityNeiyuThunderSigil props)
        {
            if (caster == null || caster.MapHeld != map || props == null)
            {
                return 0;
            }

            HashSet<Pawn> affected = new();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, props.radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Pawn pawn = things[i] as Pawn;
                    if (pawn == null || pawn.Dead || pawn.Destroyed || pawn == caster)
                    {
                        continue;
                    }

                    if (caster.Faction != null && pawn.Faction == caster.Faction)
                    {
                        continue;
                    }

                    if (!affected.Add(pawn))
                    {
                        continue;
                    }

                    TryApplyMarker(pawn, props.markerHediff);

                    ThunderTask task = new();
                    task.caster = caster;
                    task.target = pawn;
                    task.remainingStrikes = Mathf.Max(1, props.strikeCountRange.RandomInRange);
                    task.nextStrikeTick = Find.TickManager.TicksGame + Mathf.Max(1, props.firstDelayTicksRange.RandomInRange);
                    task.intervalMin = Mathf.Max(1, props.strikeIntervalTicksRange.min);
                    task.intervalMax = Mathf.Max(task.intervalMin, props.strikeIntervalTicksRange.max);
                    task.damageAmount = Mathf.Max(0, props.damageAmount);
                    task.empDamageAmount = Mathf.Max(0, props.empDamageAmount);
                    task.markerHediff = props.markerHediff;
                    thunderTasks.Add(task);
                }
            }

            return affected.Count;
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
            task.totalShots = Mathf.Max(1, props.shotCount);
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

            if (!thunderTasks.NullOrEmpty())
            {
                for (int i = thunderTasks.Count - 1; i >= 0; i--)
                {
                    if (thunderTasks[i] == null || !ProcessThunderTask(thunderTasks[i]))
                    {
                        thunderTasks.RemoveAt(i);
                    }
                }
            }

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

        private bool ProcessThunderTask(ThunderTask task)
        {
            if (task.target == null || task.target.Destroyed || task.target.Dead || !task.target.Spawned || task.target.MapHeld != map)
            {
                TryRemoveMarker(task.target, task.markerHediff);
                return false;
            }

            int now = Find.TickManager.TicksGame;
            if (now < task.nextStrikeTick)
            {
                return true;
            }

            task.remainingStrikes = Mathf.Min(task.remainingStrikes, NeiyuPowerBalance.ThunderStrikeCap);
            task.damageAmount = Mathf.Min(task.damageAmount, NeiyuPowerBalance.ThunderDamageCap);
            task.empDamageAmount = Mathf.Min(task.empDamageAmount, NeiyuPowerBalance.ThunderEmpDamageCap);

            IntVec3 strikeCell = task.target.Position;
            PlayLightningVisual(strikeCell);

            if (task.damageAmount > 0)
            {
                DamageInfo main = new(DamageDefOf.Burn, task.damageAmount, 999f, -1f, task.caster);
                main.SetIgnoreArmor(true);

                DamageWorker.DamageResult result = task.target.TakeDamage(main);


                if (result == null || result.totalDamageDealt <= 0.01f)
                {
                    DamageInfo fallback = new(DamageDefOf.Bomb, task.damageAmount, 999f, -1f, task.caster);
                    fallback.SetIgnoreArmor(true);
                    task.target.TakeDamage(fallback);
                }
            }

            if (task.empDamageAmount > 0)
            {
                DamageInfo emp = new(DamageDefOf.EMP, task.empDamageAmount, 0f, -1f, task.caster);
                task.target.TakeDamage(emp);
            }

            task.remainingStrikes--;
            if (task.remainingStrikes <= 0)
            {
                TryRemoveMarker(task.target, task.markerHediff);
                return false;
            }

            task.nextStrikeTick = now + Rand.RangeInclusive(task.intervalMin, task.intervalMax);
            return true;
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
            if (!thunderTasks.NullOrEmpty())
            {
                for (int index = 0; index < thunderTasks.Count; index++)
                {
                    ThunderTask task = thunderTasks[index];
                    if (task != null)
                    {
                        TryRemoveMarker(task.target, task.markerHediff);
                    }
                }
                thunderTasks.Clear();
            }

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

        private void PlayLightningVisual(IntVec3 strikeLoc)
        {
            if (!strikeLoc.InBounds(map))
            {
                return;
            }

            map.weatherManager.eventHandler.AddEvent(new WeatherEvent_NeiyuLightningVisual(map, strikeLoc));

            SoundDefOf.Thunder_OffMap.PlayOneShotOnCamera(map);
            SoundInfo info = SoundInfo.InMap(new TargetInfo(strikeLoc, map));
            SoundDefOf.Thunder_OnMap.PlayOneShot(info);

            Vector3 loc = strikeLoc.ToVector3Shifted();
            for (int i = 0; i < 4; i++)
            {
                FleckMaker.ThrowSmoke(loc, map, 1.2f);
                FleckMaker.ThrowMicroSparks(loc, map);
                FleckMaker.ThrowLightningGlow(loc, map, 1.2f);
            }

            FleckMaker.Static(strikeLoc, map, FleckDefOf.ExplosionFlash, 1f);
        }

        private static void TryApplyMarker(Pawn pawn, HediffDef marker)
        {
            if (pawn == null || marker == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            if (pawn.health.hediffSet.HasHediff(marker))
            {
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(marker, pawn);
            if (hediff != null)
            {
                pawn.health.AddHediff(hediff);
            }
        }

        private static void TryRemoveMarker(Pawn pawn, HediffDef marker)
        {
            if (pawn == null || marker == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(marker);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private static IntVec3 ClampToMap(IntVec3 cell, Map map)
        {
            int x = Mathf.Clamp(cell.x, 0, map.Size.x - 1);
            int z = Mathf.Clamp(cell.z, 0, map.Size.z - 1);
            return new IntVec3(x, 0, z);
        }
    }

    [StaticConstructorOnStartup]
    public class WeatherEvent_NeiyuLightningVisual : WeatherEvent_LightningFlash
    {
        private readonly IntVec3 strikeLoc;
        private Mesh boltMesh;

        private static readonly Material LightningMat = MatLoader.LoadMat("Weather/LightningBolt");

        public WeatherEvent_NeiyuLightningVisual(Map map, IntVec3 strikeLoc) : base(map)
        {
            this.strikeLoc = strikeLoc;
        }

        public override void FireEvent()
        {
            boltMesh = LightningBoltMeshPool.RandomBoltMesh;
        }

        public override void WeatherEventDraw()
        {
            if (!strikeLoc.IsValid || boltMesh == null)
            {
                return;
            }

            Graphics.DrawMesh(
                boltMesh,
                strikeLoc.ToVector3ShiftedWithAltitude(AltitudeLayer.Weather),
                Quaternion.identity,
                FadedMaterialPool.FadedVersionOf(LightningMat, LightningBrightness),
                0);
        }
    }

}
