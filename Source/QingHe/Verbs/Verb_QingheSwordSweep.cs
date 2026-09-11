using System;
using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.QingHe.Things.Weapons;
using RimWorld;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Verbs
{
    public class Verb_QingheSwordSweep : Verb_MeleeAttackDamage
    {
        private static readonly Func<Verb_MeleeAttackDamage, LocalTargetInfo, IEnumerable<DamageInfo>> DamageInfosForTarget =
            (Func<Verb_MeleeAttackDamage, LocalTargetInfo, IEnumerable<DamageInfo>>)Delegate.CreateDelegate(
                typeof(Func<Verb_MeleeAttackDamage, LocalTargetInfo, IEnumerable<DamageInfo>>),
                AccessTools.Method(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply"));
        private static readonly Func<Verb_MeleeAttack, LocalTargetInfo, float> NonMissChance =
            (Func<Verb_MeleeAttack, LocalTargetInfo, float>)Delegate.CreateDelegate(
                typeof(Func<Verb_MeleeAttack, LocalTargetInfo, float>),
                AccessTools.Method(typeof(Verb_MeleeAttack), "GetNonMissChance"));
        private static readonly Func<Verb_MeleeAttack, LocalTargetInfo, float> DodgeChance =
            (Func<Verb_MeleeAttack, LocalTargetInfo, float>)Delegate.CreateDelegate(
                typeof(Func<Verb_MeleeAttack, LocalTargetInfo, float>),
                AccessTools.Method(typeof(Verb_MeleeAttack), "GetDodgeChance"));
        private static readonly Func<Verb_MeleeAttack, Thing, SoundDef> DodgeSound =
            (Func<Verb_MeleeAttack, Thing, SoundDef>)Delegate.CreateDelegate(
                typeof(Func<Verb_MeleeAttack, Thing, SoundDef>),
                AccessTools.Method(typeof(Verb_MeleeAttack), "SoundDodge"));

        private Dictionary<Thing, List<DamageInfo>> swingDamage;

        protected override bool TryCastShot()
        {
            Pawn pawn = CasterPawn;
            LocalTargetInfo originalTarget = currentTarget;
            Thing primary = originalTarget.Thing;
            if (pawn == null || !pawn.Spawned || pawn.stances.FullBodyBusy
                || primary == null || !primary.Spawned || primary.MapHeld != pawn.MapHeld)
            {
                return false;
            }

            if (!QingheSwordCombatUtility.IsSwordMode(pawn))
            {
                return base.TryCastShot();
            }

            Map map = pawn.MapHeld;
            List<Thing> targets = new() { primary };
            HashSet<Thing> seen = new() { primary };
            List<IntVec3> cells = new();
            QingheSwordCombatUtility.FillConeCells(pawn, pawn.Position, primary.Position, verbProps.range, 120f, cells);
            foreach (IntVec3 cell in cells)
            {
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (thing == pawn || !thing.Spawned || thing.Destroyed
                        || thing is not Pawn && thing is not Building
                        || thing is Pawn victim && victim.Dead
                        || !GenHostility.HostileTo(pawn, thing)
                        || !CanHitTarget(thing) || !seen.Add(thing))
                    {
                        continue;
                    }

                    targets.Add(thing);
                }
            }

            Dictionary<Thing, List<DamageInfo>> previousDamage = swingDamage;
            swingDamage = new Dictionary<Thing, List<DamageInfo>>();
            try
            {
                // Materialize the vanilla damage rolls before any hit changes pressure or buffs.
                foreach (Thing target in targets)
                {
                    swingDamage.Add(target, new List<DamageInfo>(DamageInfosForTarget(this, target)));
                }

                bool hit = base.TryCastShot();
                for (int i = 1; i < targets.Count; i++)
                {
                    Thing target = targets[i];
                    if (!pawn.Spawned || pawn.Dead || pawn.MapHeld != map)
                    {
                        break;
                    }
                    if (target.Destroyed || !target.Spawned || target.MapHeld != map
                        || target is Pawn victim && victim.Dead
                        || !GenHostility.HostileTo(pawn, target) || !CanHitTarget(target))
                    {
                        continue;
                    }

                    currentTarget = target;
                    hit |= TryHitSweepTarget(target, map);
                }

                return hit;
            }
            finally
            {
                currentTarget = originalTarget;
                swingDamage = previousDamage;
            }
        }

        private bool TryHitSweepTarget(Thing target, Map map)
        {
            Pawn victim = target as Pawn;
            if (victim != null)
            {
                victim.mindState.meleeThreat = CasterPawn;
                victim.mindState.lastMeleeThreatHarmTick = Find.TickManager.TicksGame;
                victim.stances.stagger.StaggerFor(95);
            }

            if (!Rand.Chance(NonMissChance(this, target)))
            {
                CreateCombatLog(m => m.combatLogRulesMiss, alwaysShow: false);
                return false;
            }
            if (Rand.Chance(DodgeChance(this, target)))
            {
                DodgeSound(this, target)?.PlayOneShot(new TargetInfo(target.Position, map));
                MoteMaker.ThrowText(target.DrawPos, map, "TextMote_Dodge".Translate(), 1.9f);
                CreateCombatLog(m => m.combatLogRulesDodge, alwaysShow: false);
                return false;
            }

            BattleLogEntry_MeleeCombat entry = CreateCombatLog(m => m.combatLogRulesHit, alwaysShow: true);
            DamageWorker.DamageResult result = ApplyMeleeDamageToTarget(target);
            if (entry != null)
            {
                if (result.stunned && result.parts.NullOrEmpty())
                {
                    Find.BattleLog.RemoveEntry(entry);
                }
                else
                {
                    result.AssociateWithLog(entry);
                    if (result.deflected)
                    {
                        entry.RuleDef = maneuver.combatLogRulesDeflect;
                        entry.alwaysShowInCompact = false;
                    }
                }
            }

            return true;
        }

        protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)
        {
            if (swingDamage == null || !swingDamage.TryGetValue(target.Thing, out List<DamageInfo> damageInfos))
            {
                return base.ApplyMeleeDamageToTarget(target);
            }

            DamageWorker.DamageResult result = new();
            foreach (DamageInfo damage in damageInfos)
            {
                if (target.ThingDestroyed)
                {
                    break;
                }
                result = target.Thing.TakeDamage(damage);
            }

            return result;
        }
    }
}
