using System.Collections.Generic;
using MiliraXian.Characters.Mingyuan;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters
{
    // One pass on a setting change/load, not a per-tick pawn/Def search.
    public sealed class GameComponent_CharacterPowerTransition : GameComponent
    {
        private int zhaoliRevision = -1, mingyuanRevision = -1;
        private CharacterPowerLevel lastZhaoli, lastMingyuan;

        public GameComponent_CharacterPowerTransition(Game game) { }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref lastZhaoli, "power_lastZhaoli", CharacterPowerLevel.Original);
            Scribe_Values.Look(ref lastMingyuan, "power_lastMingyuan", CharacterPowerLevel.Original);
        }

        public override void GameComponentTick()
        {
            if (zhaoliRevision == ZhaoliPowerBalance.Profile.Revision && mingyuanRevision == MingyuanPowerBalance.Profile.Revision) return;
            bool z = zhaoliRevision != ZhaoliPowerBalance.Profile.Revision && (!ZhaoliPowerBalance.IsOriginal || lastZhaoli != CharacterPowerLevel.Original);
            bool m = mingyuanRevision != MingyuanPowerBalance.Profile.Revision && (!MingyuanPowerBalance.IsOriginal || lastMingyuan != CharacterPowerLevel.Original);
            bool zChanged = lastZhaoli != ZhaoliPowerBalance.Profile.Level;
            bool mChanged = lastMingyuan != MingyuanPowerBalance.Profile.Level;
            zhaoliRevision = ZhaoliPowerBalance.Profile.Revision;
            mingyuanRevision = MingyuanPowerBalance.Profile.Revision;
            if (z || m)
            {
                var seen = new HashSet<Pawn>();
                foreach (Map map in Find.Maps)
                    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                        if (seen.Add(pawn)) Refresh(pawn, z, m, zChanged, mChanged);
                if (Find.WorldPawns != null)
                    foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive)
                        if (seen.Add(pawn)) Refresh(pawn, z, m, zChanged, mChanged);
            }
            lastZhaoli = ZhaoliPowerBalance.Profile.Level;
            lastMingyuan = MingyuanPowerBalance.Profile.Level;
        }

        private static void Refresh(Pawn pawn, bool z, bool m, bool zChanged, bool mChanged)
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead) return;
            bool zPawn = z && ZhaoliKarmaUtility.IsZhaoli(pawn);
            bool mPawn = m && MingyuanUtility.IsMingyuan(pawn);
            if (z && ZhaoliKarmaUtility.IsZhaoli(pawn.MentalState?.causedByPawn)
                && pawn.MentalState.def.defName == "WanderConfused")
            {
                if (ZhaoliPowerBalance.Sealed) pawn.MentalState.RecoverFromState();
                else if (ZhaoliPowerBalance.IsBalanced && pawn.MentalState.forceRecoverAfterTicks > 360)
                    pawn.MentalState.forceRecoverAfterTicks = 360;
            }
            var hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                Hediff h = hediffs[i];
                string name = h.def.defName;
                bool removeZ = z && ZhaoliPowerBalance.Sealed && (name == "MXZL_ZhaoliDeathFieldActive"
                    || name == "MXZL_ZhaoliMinghuo" || name == "MXZL_ZhaoliMinshenSlow" || name == "MXZL_ZhaoliMinshenDamage"
                    || name == "MX_AbnormalDeathSentence" || name == "MXZL_ZhaoliKarmaLink" || name == "MXZL_ZhaoliOverflowKarma");
                bool removeM = m && MingyuanPowerBalance.Sealed && name == "MX_Mingyuan_LifeBurn";
                if (zPawn && ZhaoliPowerBalance.Sealed && !ZhaoliScenarioUtility.IsHideoutState(pawn) && name == "MXZL_ZhaoliDormancy") removeZ = true;
                if (removeZ || removeM) { pawn.health.RemoveHediff(h); continue; }
                if (m && MingyuanPowerBalance.IsBalanced && name == "MX_Mingyuan_LifeBurn") h.Severity = Mathf.Min(h.Severity, 100f);
                if (z && ZhaoliPowerBalance.IsBalanced && name == "MXZL_ZhaoliKarmaLink")
                {
                    var duration = h.TryGetComp<HediffComp_Disappears>();
                    if (duration != null && duration.ticksToDisappear > 300000) duration.SetDuration(300000);
                }
                if (z && ZhaoliPowerBalance.IsBalanced && name == "MXZL_ZhaoliMinshenSlow")
                {
                    var duration = h.TryGetComp<HediffComp_Disappears>();
                    if (duration != null && duration.ticksToDisappear > 360) duration.SetDuration(360);
                }
            }
            if (zPawn || mPawn)
            {
                pawn.health.hediffSet.DirtyCache();
                pawn.needs?.AddOrRemoveNeedsAsAppropriate();
                bool changed = zPawn ? zChanged : mChanged;
                if (changed)
                {
                    // Stop only this character's active cast/weapon warmup, not unrelated work.
                    if (pawn.CurJob?.ability?.def?.defName.StartsWith(zPawn ? "MX_Zhaoli_" : "MX_Mingyuan_") == true)
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    pawn.equipment?.PrimaryEq?.PrimaryVerb?.Reset();
                    if (pawn.abilities != null)
                        foreach (Ability ability in pawn.abilities.AllAbilitiesForReading)
                        {
                            if (!ability.def.defName.StartsWith(zPawn ? "MX_Zhaoli_" : "MX_Mingyuan_")) continue;
                            ability.verb?.Reset();
                            if (ability.CooldownTicksRemaining > 0 && ability.HasCooldown)
                            {
                                int remaining = Mathf.CeilToInt(ability.CooldownTicksRemaining / (float)Mathf.Max(1, ability.CooldownTicksTotal)
                                    * ability.def.cooldownTicksRange.max);
                                ability.StartCooldown(remaining);
                            }
                        }
                }
            }
        }
    }
}
