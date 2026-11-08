using System.Collections.Generic;
using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Abilities.Zantetsuken
{
    public class JobDriver_Zantetsuken : JobDriver, IMotionPlaybackSource
    {
        private Vector3 aim = Vector3.right;
        private IntVec3 origin = IntVec3.Invalid;
        private IntVec3 dashEnd = IntVec3.Invalid;
        private IntVec3 lotusCell = IntVec3.Invalid;
        private Vector3 dashFrom;
        private List<Thing> dashStruck = new();
        private IntVec3 lastSafe = IntVec3.Invalid;
        private int dashStartTick = -1;
        private int dashTicks = 1;
        private int kickTick = -1;
        private IntVec3 takeoffCell = IntVec3.Invalid;
        private IntVec3 landCell = IntVec3.Invalid;
        private bool gaeshi;
        private float apexHeight;
        private bool tookOff;
        private bool landed;
        private int hitsDone;
        private bool gaeshiHitDone;
        private bool splitPlayed;
        private int markSeed;
        private List<Pawn> struckPawns = new();

        private BladeMotionClip clip;
        private List<ZantetsukenMarks.Mark> marks;
        private bool visualsSpawned;
        private bool flashSpawned;
        private bool trailSpawned;

        private const float EndTail = 0.1f;
        // Sounds, shakes and cuts of an event a load has already passed are skipped.
        private const float LateWindow = 0.2f;

        public override bool PlayerInterruptable => false;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private CompProperties_AbilityZantetsuken Props => job.ability.CompOfType<CompAbilityEffect_Zantetsuken>().Props;

        private bool Kicked => kickTick >= 0;

        private static int Now => Find.TickManager.TicksGame;

        private List<ZantetsukenMarks.Mark> Marks => marks ??= ZantetsukenMarks.Make(Props, markSeed);

        private BladeMotionClip Clip => clip ??= ZantetsukenMotion.Build(Props, gaeshi, apexHeight, ZantetsukenMotion.TurnTotal(aim));

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            CompProperties_AbilityZantetsuken props = Props;
            origin = pawn.Position;
            lastSafe = origin;
            dashEnd = job.GetTarget(TargetIndex.A).Cell;
            lotusCell = job.GetTarget(TargetIndex.B).Cell;
            dashFrom = origin.ToVector3Shifted();
            Vector3 d = (dashEnd.ToVector3Shifted() - origin.ToVector3Shifted()).Yto0();
            Vector3 toLotus = (lotusCell.ToVector3Shifted() - origin.ToVector3Shifted()).Yto0();
            aim = toLotus.sqrMagnitude > 0.01f ? toLotus.normalized : d.sqrMagnitude > 0.01f ? d.normalized : Vector3.right;
            dashTicks = Mathf.Max(props.dashMinTicks, Mathf.CeilToInt(d.magnitude / props.dashSpeedCellsPerSecond * 60f));
            dashStartTick = Now;
            gaeshi = Hediffs.MX_QH_HediffUtility.GetFlowerResonance(pawn).EffectiveNodeLevel(props.gaeshiNode) > 0;
            apexHeight = props.apexHeight;
            markSeed = Gen.HashCombineInt(dashStartTick, pawn.thingIDNumber);
            pawn.pather.StopDead();
            if (pawn.flight is { Flying: true } flight)
            {
                flight.ForceLand();
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = ToilMaker.MakeToil("Zantetsuken");
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;
            toil.tickAction = ZantetsukenTick;
            yield return toil;
        }

        private void ZantetsukenTick()
        {
            CompProperties_AbilityZantetsuken props = Props;
            Map map = pawn.Map;
            if (pawn.Downed)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            pawn.pather.StopDead();
            pawn.Rotation = Rot4.FromAngleFlat(aim.AngleFlat());
            if (!Kicked)
            {
                DashTick(props, map);
                return;
            }
            SpawnVisuals(props, map);
            float t = (Now - kickTick) / 60f;
            if (!tookOff && t >= props.takeoffSeconds)
            {
                tookOff = true;
                if (landCell != pawn.Position)
                {
                    pawn.Position = landCell;
                    pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: true);
                }
            }
            if (t >= props.fallSeconds && t < props.landSeconds + 0.1f && Now % 2 == 0)
            {
                AddAfterimage(map, props, 18, 0.28f);
            }
            if (!landed && t >= props.landSeconds)
            {
                landed = true;
                if (t < props.landSeconds + LateWindow)
                {
                    props.landSound.PlayOneShot(new TargetInfo(landCell, map));
                    Shake(map, props.landShake);
                }
            }
            if (!splitPlayed && t >= props.landSeconds + props.visuals.splitDelay)
            {
                splitPlayed = true;
                if (t < props.landSeconds + props.visuals.splitDelay + LateWindow)
                {
                    ZantetsukenMarks.SpawnSplit(map, props, landCell.ToVector3Shifted(), aim, apexHeight);
                }
            }
            while (hitsDone < props.landHits && t >= props.HitTime(hitsDone))
            {
                int i = hitsDone++;
                if (t < props.HitTime(i) + LateWindow)
                {
                    props.hitSound.PlayOneShot(new TargetInfo(landCell, map));
                    Shake(map, props.hitShake);
                    ZantetsukenMarks.Spawn(map, props, props.visuals.markVfx, lotusCell.ToVector3Shifted(), Marks[i]);
                }
                Burst(props, map, props.landDamage, props.hitPosture, props.landDamageDef);
            }
            if (gaeshi && !gaeshiHitDone && t >= props.ClickTime)
            {
                gaeshiHitDone = true;
                if (t < props.ClickTime + LateWindow)
                {
                    props.clickSound.PlayOneShot(new TargetInfo(landCell, map));
                    props.burstSound.PlayOneShot(new TargetInfo(landCell, map));
                    Shake(map, props.gaeshiShake);
                    Rand.PushState(markSeed ^ 0x6a5e);
                    foreach (ZantetsukenMarks.Mark m in Marks)
                    {
                        ZantetsukenMarks.Spawn(map, props, props.visuals.gaeshiMarkVfx, lotusCell.ToVector3Shifted(), m, Rand.Range(0f, 0.04f));
                    }
                    Rand.PopState();
                }
                Burst(props, map, props.gaeshiDamage, props.hitPosture + props.gaeshiExtraPosture, props.gaeshiDamageDef);
            }
            HoldBreaks(props);
            // The tail lets the sheathe flash start: it is dropped once the job is gone.
            if (t >= props.EndSeconds(gaeshi) + EndTail)
            {
                EndJobWith(JobCondition.Succeeded);
            }
        }

        private Vector3 DashPoint(float ticks)
        {
            float u = Mathf.Clamp01(ticks / dashTicks);
            return Vector3.Lerp(origin.ToVector3Shifted(), dashEnd.ToVector3Shifted(), 1f - (1f - u) * (1f - u));
        }

        private void DashTick(CompProperties_AbilityZantetsuken props, Map map)
        {
            int elapsed = Now - dashStartTick;
            IntVec3 from = DashPoint(elapsed - 1).ToIntVec3();
            IntVec3 to = DashPoint(elapsed).ToIntVec3();
            bool blocked = false;
            foreach (IntVec3 cell in GenSight.BresenhamCellsBetween(from, to))
            {
                if (cell == origin)
                {
                    continue;
                }
                if (!Passable(map, cell))
                {
                    blocked = true;
                    break;
                }
                lastSafe = cell;
            }
            Vector3 point = blocked ? lastSafe.ToVector3Shifted() : DashPoint(elapsed);
            DashHit(props, map, point);
            if (elapsed % 2 == 0)
            {
                AddAfterimage(map, props, 20, 0.32f);
            }
            if (blocked || elapsed >= dashTicks)
            {
                Arrive(props, map);
            }
        }

        private bool Passable(Map map, IntVec3 cell)
        {
            return cell.WalkableBy(map, pawn) && (cell.GetEdifice(map) is not Building_Door door || door.Open);
        }

        private void DashHit(CompProperties_AbilityZantetsuken props, Map map, Vector3 point)
        {
            AreaDamageBurst burst = new()
            {
                def = props.dashBurst,
                caster = pawn,
                weaponDef = pawn.equipment.Primary.def,
                shape = AreaDamageShape.Capsule,
                center = dashFrom,
                end = point,
                radius = props.dashCollisionRadius,
                damage = props.dashDamage * DamageFactor,
                armorPenetration = props.armorPenetration,
            };
            dashFrom = point;
            List<Thing> struck = new();
            burst.Fire(map, struck, dashStruck);
            foreach (Thing t in struck)
            {
                dashStruck.Add(t);
                props.dashHitSound.PlayOneShot(new TargetInfo(t.Position, map));
            }
        }

        /// <summary>The stun at the end of the dash; nothing in the lotus ends the move there.</summary>
        private void Arrive(CompProperties_AbilityZantetsuken props, Map map)
        {
            IntVec3 stop = FindLandingCell(map, lastSafe, lastSafe);
            if (stop != pawn.Position)
            {
                pawn.Position = stop;
                pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: true);
            }
            List<Thing> targets = new();
            MakeBurst(props, 0f, 0f, null).CollectTargets(map, targets);
            if (targets.Count == 0)
            {
                EndJobWith(JobCondition.Succeeded);
                return;
            }
            kickTick = Now;
            takeoffCell = pawn.Position;
            landCell = FindLandingCell(map, lotusCell, takeoffCell);
            // The roof is not broken: under one she leaps low on the same timeline.
            if (takeoffCell.Roofed(map) || landCell.Roofed(map))
            {
                apexHeight = Mathf.Min(apexHeight, props.roofedHeight);
                clip = null;
            }
            Hediffs.MX_QH_HediffUtility.GetSwordPressure(pawn).ConsumeAll();
            int stun = gaeshi ? props.gaeshiStunTicks : props.stunTicks;
            foreach (Thing t in targets)
            {
                if (t is Pawn p)
                {
                    p.stances.stunner.StunFor(stun, pawn, addBattleLog: true, showMote: true);
                }
            }
            SpawnVisuals(props, map);
        }

        private float DamageFactor => MX_QH_PerkUtility.GetSpellEffectFactor(pawn)
            * pawn.GetStatValue(StatDefOf.MeleeDamageFactor, cacheStaleAfterTicks: -1);

        private AreaDamageBurst MakeBurst(CompProperties_AbilityZantetsuken props, float damage, float posture, DamageDef damageDef)
        {
            return new AreaDamageBurst
            {
                def = props.burstDamage,
                caster = pawn,
                weaponDef = pawn.equipment.Primary.def,
                damageDef = damageDef,
                center = lotusCell.ToVector3Shifted(),
                radius = props.lotusRadius,
                damage = damage,
                armorPenetration = props.armorPenetration,
                posture = posture,
            };
        }

        private void Burst(CompProperties_AbilityZantetsuken props, Map map, float damage, float posture, DamageDef damageDef)
        {
            List<Thing> struck = new();
            MakeBurst(props, damage * DamageFactor, posture, damageDef).Fire(map, struck);
            foreach (Thing t in struck)
            {
                if (t is Pawn p && !struckPawns.Contains(p))
                {
                    struckPawns.Add(p);
                }
            }
        }

        /// <summary>
        /// Her guard (and so her execution) is off for the whole job: a break she caused lasts until the
        /// job ends and then its usual window, so the execution comes after the move.
        /// </summary>
        private void HoldBreaks(CompProperties_AbilityZantetsuken props)
        {
            int end = kickTick + Mathf.CeilToInt((props.EndSeconds(gaeshi) + EndTail) * 60f);
            struckPawns.RemoveAll(p => p.Dead || !p.Spawned);
            foreach (Pawn p in struckPawns)
            {
                Common.Combat.CombatPosture.Get(p)?.HoldBreakPast(pawn, end);
            }
        }

        private static void Shake(Map map, float magnitude)
        {
            if (map == Find.CurrentMap)
            {
                Find.CameraDriver.shaker.DoShake(magnitude);
            }
        }

        private IntVec3 FindLandingCell(Map map, IntVec3 desired, IntVec3 fallback)
        {
            desired = new IntVec3(Mathf.Clamp(desired.x, 0, map.Size.x - 1), 0, Mathf.Clamp(desired.z, 0, map.Size.z - 1));
            int count = GenRadial.NumCellsInRadius(3.9f);
            for (int i = 0; i < count; i++)
            {
                IntVec3 c = desired + GenRadial.RadialPattern[i];
                if (ValidLanding(map, c))
                {
                    return c;
                }
            }
            return fallback;
        }

        private bool ValidLanding(Map map, IntVec3 c)
        {
            if (!c.InBounds(map) || !JumpUtility.ValidJumpTarget(pawn, map, c))
            {
                return false;
            }
            foreach (Thing t in c.GetThingList(map))
            {
                if (t is Pawn p && p != pawn && !p.Dead)
                {
                    return false;
                }
            }
            return true;
        }

        private void AddAfterimage(Map map, CompProperties_AbilityZantetsuken props, int ticks, float alpha)
        {
            map.GetComponent<MapComponent_PawnAfterimages>().AddAfterimage(pawn, pawn.DrawPos, pawn.Rotation, ticks, alpha,
                props.visuals.afterimageTint);
        }

        /// <summary>Not saved: after a load the parts still to come are spawned again.</summary>
        private void SpawnVisuals(CompProperties_AbilityZantetsuken props, Map map)
        {
            int id = kickTick;
            bool StillOn() => pawn.Spawned && pawn.jobs.curDriver is JobDriver_Zantetsuken d && d.kickTick == id;
            if (!visualsSpawned)
            {
                visualsSpawned = true;
                if (ZantetsukenVfxRenderer.Find(map, pawn, id) == null)
                {
                    MapComponent_QingheVfx.Register(map, new ZantetsukenVfxRenderer(props, pawn, id, lotusCell, aim, gaeshi, StillOn));
                }
            }
            BladeMotionClip c = Clip;
            QingheBladeTrailExtension ext = pawn.equipment.Primary.def.GetModExtension<QingheBladeTrailExtension>();
            int now = Now;
            if (!flashSpawned)
            {
                flashSpawned = true;
                float flashTick = kickTick + c.flashTime * 60f;
                if (flashTick >= now)
                {
                    QingheBladeTrail.SpawnFlash(pawn, ext, c, aim, flashTick, StillOn, props.Projection);
                }
            }
            // A strip source must not start in the future: the trail waits for the scoop.
            if (!trailSpawned && gaeshi && now >= kickTick + c.trailStart * 60f)
            {
                trailSpawned = true;
                if (now <= kickTick + c.trailEnd * 60f)
                {
                    QingheBladeTrail.SpawnTrail(pawn, ext, c, aim, Mathf.RoundToInt(kickTick + c.trailStart * 60f), 1f,
                        QingheSwordCombatUtility.ResonanceFor(pawn), -1f, -1f, props.Projection);
                }
            }
        }

        public bool TryGetPlayback(out MotionPlayback playback)
        {
            CompProperties_AbilityZantetsuken props = Props;
            BladeMotionClip c = Clip;
            Vector3 travel = Vector3.zero;
            float seconds = 0f;
            if (!Kicked)
            {
                // Position stays on the origin while dashing.
                travel = DashPoint(VisualClock.TicksSince(dashStartTick)) - pawn.Position.ToVector3Shifted();
            }
            else
            {
                seconds = Mathf.Clamp(VisualClock.TicksSince(kickTick) / 60f, 0f, c.durationSeconds);
                if (tookOff && seconds < props.apexSeconds)
                {
                    // Position is already on the landing cell: the body is drawn back along the leap.
                    float u = Mathf.InverseLerp(props.takeoffSeconds, props.apexSeconds, seconds);
                    travel = (takeoffCell.ToVector3Shifted() - landCell.ToVector3Shifted()) * ((1f - u) * (1f - u));
                }
            }
            travel.y = 0f;
            playback = new MotionPlayback
            {
                clip = c,
                seconds = seconds,
                aim = aim,
                projection = props.Projection,
                scabbard = true,
                blendSeconds = 0.08f,
                source = c,
                sourceId = dashStartTick,
                travel = travel,
            };
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref aim, "mx_qh_zan_aim", Vector3.right);
            Scribe_Values.Look(ref origin, "mx_qh_zan_origin", IntVec3.Invalid);
            Scribe_Values.Look(ref dashEnd, "mx_qh_zan_dashEnd", IntVec3.Invalid);
            Scribe_Values.Look(ref lotusCell, "mx_qh_zan_lotus", IntVec3.Invalid);
            Scribe_Values.Look(ref dashFrom, "mx_qh_zan_dashFrom");
            Scribe_Collections.Look(ref dashStruck, "mx_qh_zan_dashStruck", LookMode.Reference);
            Scribe_Values.Look(ref lastSafe, "mx_qh_zan_lastSafe", IntVec3.Invalid);
            Scribe_Values.Look(ref dashStartTick, "mx_qh_zan_dashStart", -1);
            Scribe_Values.Look(ref dashTicks, "mx_qh_zan_dashTicks", 1);
            Scribe_Values.Look(ref kickTick, "mx_qh_zan_kick", -1);
            Scribe_Values.Look(ref takeoffCell, "mx_qh_zan_takeoff", IntVec3.Invalid);
            Scribe_Values.Look(ref landCell, "mx_qh_zan_land", IntVec3.Invalid);
            Scribe_Values.Look(ref gaeshi, "mx_qh_zan_gaeshi");
            Scribe_Values.Look(ref apexHeight, "mx_qh_zan_apex");
            Scribe_Values.Look(ref tookOff, "mx_qh_zan_tookOff");
            Scribe_Values.Look(ref landed, "mx_qh_zan_landed");
            Scribe_Values.Look(ref hitsDone, "mx_qh_zan_hits");
            Scribe_Values.Look(ref gaeshiHitDone, "mx_qh_zan_gaeshiHit");
            Scribe_Values.Look(ref splitPlayed, "mx_qh_zan_split");
            Scribe_Values.Look(ref markSeed, "mx_qh_zan_seed");
            Scribe_Collections.Look(ref struckPawns, "mx_qh_zan_struck", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                struckPawns.RemoveAll(p => p == null);
                dashStruck.RemoveAll(t => t == null);
            }
        }
    }
}
