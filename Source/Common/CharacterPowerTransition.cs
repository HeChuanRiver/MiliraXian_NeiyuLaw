using System.Collections.Generic;
using HarmonyLib;
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
        private static readonly AccessTools.FieldRef<Ability, int> CooldownDuration =
            AccessTools.FieldRefAccess<Ability, int>("cooldownDuration");
        private int neiyuRevision = -1, zhaoliRevision = -1, mingyuanRevision = -1;
        private CharacterPowerLevel lastNeiyu, lastZhaoli, lastMingyuan;

        public GameComponent_CharacterPowerTransition(Game game) { }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref lastNeiyu, "power_lastNeiyu", CharacterPowerLevel.Original);
            Scribe_Values.Look(ref lastZhaoli, "power_lastZhaoli", CharacterPowerLevel.Original);
            Scribe_Values.Look(ref lastMingyuan, "power_lastMingyuan", CharacterPowerLevel.Original);
        }

        public override void GameComponentTick()
        {
            if (neiyuRevision == NeiyuPowerBalance.Revision && zhaoliRevision == ZhaoliPowerBalance.Profile.Revision
                && mingyuanRevision == MingyuanPowerBalance.Profile.Revision) return;
            bool n = neiyuRevision != NeiyuPowerBalance.Revision && (!NeiyuPowerBalance.IsOriginal || lastNeiyu != CharacterPowerLevel.Original);
            bool z = zhaoliRevision != ZhaoliPowerBalance.Profile.Revision && (!ZhaoliPowerBalance.IsOriginal || lastZhaoli != CharacterPowerLevel.Original);
            bool m = mingyuanRevision != MingyuanPowerBalance.Profile.Revision && (!MingyuanPowerBalance.IsOriginal || lastMingyuan != CharacterPowerLevel.Original);
            bool zChanged = lastZhaoli != ZhaoliPowerBalance.Profile.Level;
            bool mChanged = lastMingyuan != MingyuanPowerBalance.Profile.Level;
            bool nChanged = lastNeiyu != NeiyuPowerBalance.CurrentLevel;
            neiyuRevision = NeiyuPowerBalance.Revision;
            zhaoliRevision = ZhaoliPowerBalance.Profile.Revision;
            mingyuanRevision = MingyuanPowerBalance.Profile.Revision;
            if (n || z || m)
            {
                var seen = new HashSet<Pawn>();
                foreach (Map map in Find.Maps)
                    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                        if (seen.Add(pawn)) Refresh(pawn, n, z, m, nChanged, zChanged, mChanged);
                if (Find.WorldPawns != null)
                    foreach (Pawn pawn in Find.WorldPawns.AllPawnsAlive)
                        if (seen.Add(pawn)) Refresh(pawn, n, z, m, nChanged, zChanged, mChanged);
            }
            lastZhaoli = ZhaoliPowerBalance.Profile.Level;
            lastMingyuan = MingyuanPowerBalance.Profile.Level;
            lastNeiyu = NeiyuPowerBalance.CurrentLevel;
        }

        private static void Refresh(Pawn pawn, bool n, bool z, bool m, bool nChanged, bool zChanged, bool mChanged)
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead) return;
            bool zPawn = z && ZhaoliKarmaUtility.IsZhaoli(pawn);
            bool mPawn = m && MingyuanUtility.IsMingyuan(pawn);
            bool nPawn = n && NeiyuEquipmentUtility.IsNeiyu(pawn);
            if (z && ZhaoliKarmaUtility.IsZhaoli(pawn.MentalState?.causedByPawn)
                && pawn.MentalState.def.defName == "WanderConfused")
            {
                if (ZhaoliPowerBalance.Sealed) pawn.MentalState.RecoverFromState();
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
            }
            if (nPawn || zPawn || mPawn)
            {
                pawn.health.hediffSet.DirtyCache();
                pawn.needs?.AddOrRemoveNeedsAsAppropriate();
                bool changed = nPawn ? nChanged : zPawn ? zChanged : mChanged;
                bool balanced = nPawn ? NeiyuPowerBalance.IsBalanced : zPawn ? ZhaoliPowerBalance.IsBalanced : MingyuanPowerBalance.IsBalanced;
                string prefix = nPawn ? "MX_Neiyu_" : zPawn ? "MX_Zhaoli_" : "MX_Mingyuan_";
                if (changed)
                {
                    // Stop only this character's active cast/weapon warmup, not unrelated work.
                    if (pawn.CurJob?.ability?.def?.defName.StartsWith(prefix) == true)
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    pawn.equipment?.PrimaryEq?.PrimaryVerb?.Reset();
                }
                // Also migrate old tier-two saves whose selected enum has not changed.
                if ((changed || balanced) && pawn.abilities != null)
                    foreach (Ability ability in pawn.abilities.AllAbilitiesForReading)
                    {
                        if (!ability.def.defName.StartsWith(prefix)) continue;
                        if (changed) ability.verb?.Reset();
                        if (ability.CooldownTicksRemaining > 0 && ability.HasCooldown)
                        {
                            int total = ability.def.cooldownTicksRange.max;
                            if (total == ability.CooldownTicksTotal) continue;
                            int remaining = ConservativePowerTuning.RemapCooldown(ability.CooldownTicksRemaining, ability.CooldownTicksTotal, total);
                            ability.StartCooldown(remaining);
                            // StartCooldown uses the remaining time as its denominator; restore the
                            // full duration so another toggle does not restart the entire cooldown.
                            CooldownDuration(ability) = total;
                        }
                    }
            }
        }
    }
}
