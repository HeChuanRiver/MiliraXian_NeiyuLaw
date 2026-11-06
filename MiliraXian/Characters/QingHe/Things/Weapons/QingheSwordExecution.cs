using System.Collections.Generic;
using MiliraXian.Characters.Common.Combat;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>
    /// Qinghe's execution of an enemy whose posture she broke: an iaido in place. She sheathes and
    /// gathers, draws in one flash, follows through and returns, turns aside as she sheathes again, and
    /// when the blade clicks home the enemy takes the blow (one hit on its core part). The motion is the
    /// sword's attack clip replayed piece by piece (<see cref="QingheSwordGuardDef.executionMotion"/>).
    /// Blows meanwhile are parried (see <see cref="QingheSwordBlocker"/>).
    /// </summary>
    public sealed class JobDriver_QingheSwordExecution : JobDriver, IPostureExecutionDriver, IMotionPlaybackSource
    {
        private int executionStart = -1;
        private Vector3 aim = Vector3.right;
        private bool drawPlayed;
        private bool hitDone;
        // The dash through the enemy: Position moves to dashCell at once, the body is drawn back
        // along the way (MotionPlayback.travel) and catches up over the dash piece.
        private bool dashed;
        private IntVec3 dashOrigin = IntVec3.Invalid;
        private IntVec3 dashCell = IntVec3.Invalid;
        // Aim of the pieces after the dash, fixed when it lands (the enemy may die meanwhile).
        private Vector3 backAim = Vector3.left;
        // Visual only, not saved: pieces whose trail and flash have been spawned (after a load the
        // piece in progress spawns again).
        private int visualMask;

        private static QingheSwordGuardDef C => QingheSwordGuard.Config;
        private static int Now => Find.TickManager.TicksGame;

        public Pawn Victim => job?.targetA.Pawn;
        private float Elapsed => (Now - executionStart) / 60f;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private BladeMotionSetExtension Set => pawn.equipment?.Primary?.def.GetModExtension<BladeMotionSetExtension>();
        private BladeMotionClip Clip => Set?.Attack(C.executionAttack);

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            executionStart = Now;
            Vector3 d = (Victim?.DrawPos ?? pawn.DrawPos + pawn.Rotation.FacingCell.ToVector3()) - pawn.DrawPos;
            d.y = 0f;
            aim = d.sqrMagnitude > 0.001f ? d.normalized : pawn.Rotation.FacingCell.ToVector3();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref executionStart, "mxExecutionStart", -1);
            Scribe_Values.Look(ref aim, "mxExecutionAim", Vector3.right);
            Scribe_Values.Look(ref drawPlayed, "mxExecutionDrawn");
            Scribe_Values.Look(ref hitDone, "mxExecutionHit");
            Scribe_Values.Look(ref dashed, "mxExecutionDashed");
            Scribe_Values.Look(ref dashOrigin, "mxExecutionDashOrigin", IntVec3.Invalid);
            Scribe_Values.Look(ref dashCell, "mxExecutionDashCell", IntVec3.Invalid);
            Scribe_Values.Look(ref backAim, "mxExecutionBackAim", -aim);
            base.ExposeData();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(Finish);
            Toil execution = ToilMaker.MakeToil("QingheSwordExecution");
            execution.defaultCompleteMode = ToilCompleteMode.Never;
            execution.handlingFacing = true;
            execution.tickAction = TickExecution;
            yield return execution;
        }

        private bool Valid()
        {
            if (executionStart < 0 || Clip == null || !pawn.Spawned || pawn.Downed)
            {
                return false;
            }
            if (hitDone)
            {
                return true;
            }
            Pawn victim = Victim;
            return QingheSwordGuard.CanGuard(pawn, out _) && QingheSwordGuard.ExecutionPair(pawn, victim)
                && (victim.Downed || victim.CurJobDef == CombatPosture.Config.brokenJob)
                && CombatPosture.Get(victim)?.Owns(pawn) == true;
        }

        private void TickExecution()
        {
            if (!Valid())
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            pawn.pather.StopDead();
            float t = Elapsed;
            if (!drawPlayed && t >= C.executionCutSeconds)
            {
                drawPlayed = true;
                C.executionDrawSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
            if (!dashed && DashSegment is { } dash && t >= dash.start)
            {
                dashed = true;
                if (!Dash())
                {
                    return;
                }
            }
            // Each piece's trail and flash when it starts: its aim is known only then (after the dash).
            List<QingheExecutionSegment> m = C.executionMotion;
            for (int i = 0; i < m.Count && i < 32; i++)
            {
                if ((visualMask & (1 << i)) == 0 && t >= m[i].start)
                {
                    visualMask |= 1 << i;
                    if (t < m[i].end)
                    {
                        SpawnVisuals(m[i]);
                    }
                }
            }
            if (!hitDone && t >= C.executionHitSeconds)
            {
                Pawn victim = Victim;
                HediffComp_CombatPosture posture = CombatPosture.Get(victim);
                // Saved before the damage: a death callback may save, throw or end this job.
                hitDone = true;
                try
                {
                    Strike(victim);
                }
                catch (System.Exception ex)
                {
                    Log.ErrorOnce("[MiliraXian] Qinghe execution blow failed, the target is released: " + ex, 0x51c7a5);
                    EndJobWith(JobCondition.Errored);
                    return;
                }
                finally
                {
                    posture?.ReleaseBreak();
                }
            }
            if (t >= C.executionSeconds)
            {
                EndJobWith(JobCondition.Succeeded);
            }
        }

        private static QingheExecutionSegment DashSegment => C.executionMotion.Find(s => s.dash);

        /// <summary>True once the dash has carried Qinghe past the enemy (pieces marked aimBack face it again).</summary>
        private bool Past => dashed && dashCell.IsValid && dashCell != dashOrigin;

        /// <summary>
        /// Pieces marked aimBack face the enemy again from where the dash landed: behind it that is the
        /// way back, from a lunge that landed beside it (the cell behind was taken) it is toward it.
        /// </summary>
        private Vector3 AimFor(QingheExecutionSegment s) => s.aimBack && Past ? backAim : aim;

        /// <summary>
        /// Where the dash lands. The cell just behind the enemy along the cut wins; if there is none, she
        /// cuts in place when she already stands next to it, else lands on any free cell next to it (the
        /// execution reaches up to <see cref="QingheSwordGuardDef.executionRange"/>, so the dash is the
        /// lunge that closes the distance). Invalid when no cell next to the enemy is free.
        /// </summary>
        private IntVec3 FindDashCell(Pawn victim)
        {
            IntVec3 behind = FindAdjacentCell(victim, 0.3f);
            if (behind.IsValid)
            {
                return behind;
            }
            if (pawn.Position.AdjacentTo8WayOrInside(victim.Position))
            {
                return pawn.Position;
            }
            return FindAdjacentCell(victim, float.NegativeInfinity);
        }

        /// <summary>
        /// The cell next to the enemy most directly behind it along the cut, scoring above
        /// <paramref name="minScore"/>: standable, free, not a door, not Qinghe's own, in her sight.
        /// </summary>
        private IntVec3 FindAdjacentCell(Pawn victim, float minScore)
        {
            Map map = pawn.Map;
            IntVec3 best = IntVec3.Invalid;
            float bestScore = minScore;
            for (int i = 0; i < 8; i++)
            {
                IntVec3 c = victim.Position + GenAdj.AdjacentCells[i];
                if (!c.InBounds(map) || !c.Standable(map) || c.GetDoor(map) != null || c == pawn.Position)
                {
                    continue;
                }
                Pawn other = c.GetFirstPawn(map);
                if (other != null && other != pawn)
                {
                    continue;
                }
                Vector3 d = c.ToVector3Shifted() - victim.Position.ToVector3Shifted();
                float score = Vector3.Dot(d.normalized, aim);
                if (score > bestScore && GenSight.LineOfSight(pawn.Position, c, map))
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }

        /// <summary>
        /// The draw: Qinghe is through the enemy and behind it. Position moves now, the drawn body follows.
        /// False: the execution ended here.
        /// </summary>
        private bool Dash()
        {
            Pawn victim = Victim;
            dashOrigin = pawn.Position;
            dashCell = victim != null ? FindDashCell(victim) : IntVec3.Invalid;
            if (!dashCell.IsValid)
            {
                // Out of reach with no free cell beside the enemy: no cut into the air. The break is
                // released by Finish (this job owns it).
                dashCell = dashOrigin;
                EndJobWith(JobCondition.Incompletable);
                return false;
            }
            Map map = pawn.Map;
            Vector3 startDraw = pawn.DrawPos;
            // Behind the enemy this is the way back; landed beside it (the cell behind was taken) it
            // faces the enemy.
            Vector3 back = victim.Position.ToVector3Shifted() - dashCell.ToVector3Shifted();
            back.y = 0f;
            backAim = back.sqrMagnitude > 0.001f ? back.normalized : -aim;
            if (dashCell != dashOrigin)
            {
                pawn.Position = dashCell;
                pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: true);
            }
            try
            {
                if (victim != null && C.executionDashVfx != null)
                {
                    QingheImpactVfx.Spawn(map, C.executionDashVfx, victim.DrawPos, aim, victim.Position);
                }
                Common.Vfx.MapComponent_PawnAfterimages ghosts = map.GetComponent<Common.Vfx.MapComponent_PawnAfterimages>();
                if (Past && ghosts != null)
                {
                    Vector3 end = dashCell.ToVector3Shifted();
                    end.y = startDraw.y;
                    int n = Mathf.Max(0, C.executionAfterimages);
                    for (int i = 0; i < n; i++)
                    {
                        float k = (i + 0.5f) / n;
                        ghosts.AddAfterimage(pawn, Vector3.Lerp(startDraw, end, k), pawn.Rotation, 14 + i * 3, 0.22f + 0.1f * k,
                            new Color(0.85f, 0.92f, 1f, 1f));
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce("[MiliraXian] Qinghe execution dash effects failed: " + ex, 0x51c7a6);
            }
            return true;
        }

        /// <summary>The blow: one hit of the execution damage on the target's core part (the torso of a human).</summary>
        private void Strike(Pawn victim)
        {
            Vector3 toward = victim.DrawPos - pawn.DrawPos;
            QingheImpactVfx.Spawn(victim.Map, C.executionVfx, victim.DrawPos, toward, victim.Position);
            BodyPartRecord core = victim.RaceProps?.body?.corePart;
            if (core != null && victim.health.hediffSet.PartIsMissing(core))
            {
                core = null;
            }
            DamageInfo blow = new(C.executionDamageDef, C.executionDamage, C.executionArmorPenetration,
                toward.AngleFlat(), pawn, core, pawn.equipment?.Primary?.def);
            blow.SetTool(TechniqueDamage.ToolFor(C.executionDamageDef));
            // The pressure first, fixed: the blow's own damage def gains none, so a kill or a
            // death callback that ends this job cannot drop or scale it. A negated blow still gives it.
            if (C.executionSwordPressure > 0f)
            {
                Hediffs.MX_QH_HediffUtility.GetSwordPressure(pawn)?.AddValue(C.executionSwordPressure);
            }
            // The vanilla execution cut under the scabbard click (the click is the hit effect's sound).
            SoundDefOf.Execute_Cut.PlayOneShot(new TargetInfo(victim.Position, victim.Map));
            // The one exception: a life-like charge (Qinghe's Flowing Form) takes the whole execution.
            if (ExecutionGuard.TryNegate(victim, blow))
            {
                return;
            }
            BattleLogEntry_MeleeCombat entry = TechniqueDamage.CreateLog(pawn, victim, C.executionDamageDef, blow.Weapon);
            // Past every shield, guard and damage reduction, and through armour: the execution lands as dealt.
            DamageWorker.DamageResult result = DirectDamage.Apply(victim, blow);
            TechniqueDamage.FinishLog(entry, result, C.executionDamageDef);
        }

        /// <summary>The scabbard flash and blade trail of one forward piece, where it passes the clip's flash or cut.</summary>
        private void SpawnVisuals(QingheExecutionSegment s)
        {
            BladeMotionClip clip = Clip;
            QingheBladeTrailExtension ext = pawn.equipment?.Primary?.def.GetModExtension<QingheBladeTrailExtension>();
            if (clip == null || pawn.Map == null || ext == null || s.clipTo <= s.clipFrom)
            {
                return;
            }
            int id = executionStart;
            Vector3 pieceAim = AimFor(s);
            float speed = (s.clipTo - s.clipFrom) / (s.end - s.start);
            if (clip.flashTime > s.clipFrom && clip.flashTime <= s.clipTo)
            {
                float at = executionStart + (s.start + (clip.flashTime - s.clipFrom) / speed) * 60f;
                if (at >= Now)
                {
                    QingheBladeTrail.SpawnFlash(pawn, ext, clip, pieceAim, at,
                        () => pawn.Spawned && pawn.jobs?.curDriver is JobDriver_QingheSwordExecution d && d.executionStart == id);
                }
            }
            // The trail is drawn on the moving body, so the dash itself has the slash mark instead.
            if (!s.dash && clip.HasTrail && s.clipFrom < clip.trailEnd && s.clipTo > clip.trailStart)
            {
                float from = Mathf.Max(clip.trailStart, s.clipFrom);
                int trailTick = Mathf.RoundToInt(executionStart + (s.start + (from - s.clipFrom) / speed) * 60f);
                QingheBladeTrail.SpawnTrail(pawn, ext, clip, pieceAim, trailTick, speed,
                    QingheSwordCombatUtility.ResonanceFor(pawn), from, s.clipTo);
            }
        }

        public bool TryGetPlayback(out MotionPlayback playback)
        {
            playback = default;
            BladeMotionSetExtension set = Set;
            BladeMotionClip clip = set?.Attack(C.executionAttack);
            if (clip?.blade == null || executionStart < 0 || C.executionMotion.NullOrEmpty())
            {
                return false;
            }
            float t = Mathf.Max(0f, (Common.Vfx.VisualClock.Ticks - executionStart) / 60f);
            List<QingheExecutionSegment> m = C.executionMotion;
            QingheExecutionSegment seg = m[m.Count - 1];
            for (int i = 0; i < m.Count; i++)
            {
                if (t < m[i].end)
                {
                    seg = m[i];
                    break;
                }
            }
            // During the dash Position is already behind the enemy: draw the body back along the way.
            Vector3 travel = Vector3.zero;
            if (seg.dash && dashed && Past && t < seg.end)
            {
                float k = Mathf.InverseLerp(seg.start, seg.end, t);
                float ease = 1f - (1f - k) * (1f - k) * (1f - k);
                travel = (dashOrigin.ToVector3Shifted() - dashCell.ToVector3Shifted()) * (1f - ease);
                travel.y = 0f;
            }
            playback = new MotionPlayback
            {
                clip = clip,
                seconds = seg.ClipAt(t),
                aim = AimFor(seg),
                projection = set.Projection,
                scabbardSet = set,
                blendSeconds = seg.blend > 0f ? seg.blend : set.blendSeconds,
                source = seg,
                sourceId = executionStart,
                travel = travel,
            };
            return true;
        }

        private void Finish(JobCondition condition)
        {
            HediffComp_CombatPosture posture = CombatPosture.Get(Victim);
            if (posture?.Executioner == pawn)
            {
                posture.ReleaseBreak();
            }
        }
    }
}
