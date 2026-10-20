using System;
using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Mingyuan;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Common
{
    // One pass on a setting change/load, not a per-tick pawn/Def search.
    public sealed class GameComponent_CharacterPowerTransition : GameComponent
    {
        private static readonly AccessTools.FieldRef<Ability, int> CooldownDuration =
            AccessTools.FieldRefAccess<Ability, int>("cooldownDuration");
        private int neiyuRevision = -1, zhaoliRevision = -1, mingyuanRevision = -1;
        private bool lastNeiyu, lastZhaoli, lastMingyuan;
        private bool legacyNeiyu, legacyZhaoli, legacyMingyuan;

        public GameComponent_CharacterPowerTransition(Game game) { }
        public override void ExposeData()
        {
            legacyNeiyu |= CharacterPowerProfile.LookDecorative(ref lastNeiyu, "power_neiyuDecorative", "power_lastNeiyu");
            legacyZhaoli |= CharacterPowerProfile.LookDecorative(ref lastZhaoli, "power_zhaoliDecorative", "power_lastZhaoli");
            legacyMingyuan |= CharacterPowerProfile.LookDecorative(ref lastMingyuan, "power_mingyuanDecorative", "power_lastMingyuan");
        }

        public override void GameComponentTick()
        {
            if (neiyuRevision == NeiyuPowerBalance.Revision && zhaoliRevision == ZhaoliPowerBalance.Profile.Revision
                && mingyuanRevision == MingyuanPowerBalance.Profile.Revision) return;
            bool n = neiyuRevision != NeiyuPowerBalance.Revision;
            bool z = zhaoliRevision != ZhaoliPowerBalance.Profile.Revision;
            bool m = mingyuanRevision != MingyuanPowerBalance.Profile.Revision;
            bool zChanged = lastZhaoli != ZhaoliPowerBalance.Sealed || legacyZhaoli;
            bool mChanged = lastMingyuan != MingyuanPowerBalance.Sealed || legacyMingyuan;
            bool nChanged = lastNeiyu != NeiyuPowerBalance.PassivesDisabled || legacyNeiyu;
            neiyuRevision = NeiyuPowerBalance.Revision;
            zhaoliRevision = ZhaoliPowerBalance.Profile.Revision;
            mingyuanRevision = MingyuanPowerBalance.Profile.Revision;
            if (n || z || m)
            {
                // Snapshot the shared finder result before jobs or effects can query it again.
                var pawns = new HashSet<Pawn>(PawnsFinder.AllMapsWorldAndTemporary_Alive);
                foreach (Pawn pawn in pawns) Refresh(pawn, n, z, m, nChanged, zChanged, mChanged);
            }
            lastZhaoli = ZhaoliPowerBalance.Sealed;
            lastMingyuan = MingyuanPowerBalance.Sealed;
            lastNeiyu = NeiyuPowerBalance.PassivesDisabled;
            legacyNeiyu = legacyZhaoli = legacyMingyuan = false;
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
                string prefix = nPawn ? "MX_Neiyu_" : zPawn ? "MX_Zhaoli_" : "MX_Mingyuan_";
                if (changed)
                {
                    // Stop only this character's active cast/weapon warmup, not unrelated work.
                    if (pawn.CurJob?.ability?.def?.defName.StartsWith(prefix) == true)
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    pawn.equipment?.PrimaryEq?.PrimaryVerb?.Reset();
                }
                // Normalize saved cooldown totals on load as well as on a mode change.
                if (pawn.abilities != null)
                    foreach (Ability ability in pawn.abilities.AllAbilitiesForReading)
                    {
                        if (!ability.def.defName.StartsWith(prefix)) continue;
                        if (changed) ability.verb?.Reset();
                        if (ability.CooldownTicksRemaining > 0 && ability.HasCooldown)
                        {
                            int total = ability.def.cooldownTicksRange.max;
                            if (total == ability.CooldownTicksTotal) continue;
                            double fraction = Math.Clamp(ability.CooldownTicksRemaining / (double)Math.Max(1, ability.CooldownTicksTotal), 0d, 1d);
                            int remaining = (int)Math.Ceiling(fraction * Math.Max(0, total));
                            int charges = ability.RemainingCharges;
                            ability.StartCooldown(remaining);
                            ability.RemainingCharges = charges;
                            // StartCooldown uses the remaining time as its denominator; restore the
                            // full duration so another toggle does not restart the entire cooldown.
                            CooldownDuration(ability) = total;
                        }
                    }
            }
        }
    }
}
