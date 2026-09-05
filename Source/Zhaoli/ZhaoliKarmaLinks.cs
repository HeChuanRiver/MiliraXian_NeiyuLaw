using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    public class HediffCompProperties_ZhaoliKarmaLinks : HediffCompProperties
    {
        public int maxLinks = 20;
        public int linkDurationTicks = 1800000;

        public HediffCompProperties_ZhaoliKarmaLinks()
        {
            compClass = typeof(HediffComp_ZhaoliKarmaLinks);
        }
    }

    public class HediffComp_ZhaoliKarmaLinks : HediffComp
    {
        private List<Pawn> linkedPawns = new List<Pawn>();
        private int nextBalancedSubstituteTick;
        private int balancedRewardWindow;
        private int balancedRewards;
        private int balanceRevision = -1;

        public bool TryUseBalancedSubstitute()
        {
            if (ZhaoliPowerBalance.Sealed) return false;
            if (ZhaoliPowerBalance.IsOriginal) return true;
            int tick = Find.TickManager.TicksGame;
            if (tick < nextBalancedSubstituteTick) return false;
            nextBalancedSubstituteTick = tick + 60000;
            return true;
        }

        public void RewardBalancedSentence()
        {
            if (!ZhaoliPowerBalance.IsBalanced) return;
            int tick = Find.TickManager.TicksGame;
            if (tick >= balancedRewardWindow + 600) { balancedRewardWindow = tick; balancedRewards = 0; }
            if (balancedRewards >= 3) return;
            balancedRewards++;
            ZhaoliKarmaUtility.AddKarma(Pawn, 1f);
            ZhaoliShieldLayerUtility.AddLayers(Pawn, 1);
        }

        public HediffCompProperties_ZhaoliKarmaLinks PropsLinks => (HediffCompProperties_ZhaoliKarmaLinks)props;

        public int ActiveLinkCount
        {
            get
            {
                CleanupInvalidLinks();
                return linkedPawns.Count;
            }
        }

        public override string CompLabelInBracketsExtra => "MX_ZL_LinkLabelExtra".Translate(ActiveLinkCount, PropsLinks.maxLinks).ToString();

        public override string CompTipStringExtra => "MX_ZL_LinkTipExtra".Translate(ActiveLinkCount, PropsLinks.maxLinks).ToString();

        public override string CompDescriptionExtra => BuildLinkSummary();

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref nextBalancedSubstituteTick, "power_nextSubstitute", 0);
            Scribe_Values.Look(ref balancedRewardWindow, "power_rewardWindow", 0);
            Scribe_Values.Look(ref balancedRewards, "power_rewards", 0);
            Scribe_Collections.Look(ref linkedPawns, "linkedPawns", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                CleanupInvalidLinks();
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (balanceRevision != ZhaoliPowerBalance.Profile.Revision)
            {
                balanceRevision = ZhaoliPowerBalance.Profile.Revision;
                if (!ZhaoliPowerBalance.IsOriginal)
                {
                    // Expire surplus old links on the setting change only, never scan the map each tick.
                    while (linkedPawns.Count > PropsLinks.maxLinks)
                    {
                        Pawn target = linkedPawns[linkedPawns.Count - 1];
                        if (target == null) linkedPawns.RemoveAt(linkedPawns.Count - 1);
                        else BreakLink(target);
                    }
                    foreach (Pawn target in linkedPawns)
                    {
                        var duration = target?.health?.hediffSet?.GetFirstHediffOfDef(
                            DefDatabase<HediffDef>.GetNamed(ZhaoliKarmaUtility.LinkTargetHediffDefName))?.TryGetComp<HediffComp_Disappears>();
                        if (duration != null && duration.ticksToDisappear > PropsLinks.linkDurationTicks)
                            duration.SetDuration(PropsLinks.linkDurationTicks);
                    }
                }
            }
            if (ZhaoliPowerBalance.Sealed) return;
            if (Pawn != null && Pawn.IsHashIntervalTick(250))
            {
                CleanupInvalidLinks();
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
        }

        public bool CanLinkTarget(Pawn target, out string failureReason)
        {
            failureReason = null;
            if (ZhaoliPowerBalance.Sealed) { failureReason = "MX_Power_AbilitiesSealed".Translate(); return false; }
            CleanupInvalidLinks();
            if (target == null || target.Dead || target.Destroyed)
            {
                failureReason = "MX_ZL_LinkTargetInvalid".Translate().ToString();
                return false;
            }

            if (target == Pawn)
            {
                failureReason = "MX_ZL_LinkCannotTargetSelf".Translate().ToString();
                return false;
            }

            HediffComp_ZhaoliKarmaLinkTarget targetComp = ZhaoliKarmaUtility.GetLinkTargetComp(target);
            if (targetComp != null && targetComp.Zhaoli != null && targetComp.Zhaoli != Pawn)
            {
                failureReason = "MX_ZL_LinkTargetAlreadyLinkedOther".Translate().ToString();
                return false;
            }

            if (targetComp != null && targetComp.Zhaoli == Pawn)
            {
                return true;
            }

            if (linkedPawns.Count >= PropsLinks.maxLinks)
            {
                failureReason = "MX_ZL_LinkLimitReached".Translate().ToString();
                return false;
            }

            return true;
        }

        public bool TryAddOrRefreshLink(Pawn target, out bool createdNewLink, out string failureReason)
        {
            createdNewLink = false;
            if (!CanLinkTarget(target, out failureReason))
            {
                return false;
            }

            HediffDef linkDef = DefDatabase<HediffDef>.GetNamedSilentFail(ZhaoliKarmaUtility.LinkTargetHediffDefName);
            if (linkDef == null || target?.health == null)
            {
                failureReason = "MX_ZL_LinkHediffDefMissing".Translate().ToString();
                return false;
            }

            Hediff hediff = target.health.GetOrAddHediff(linkDef);
            HediffWithComps hediffWithComps = hediff as HediffWithComps;
            if (hediffWithComps == null)
            {
                failureReason = "MX_ZL_LinkHediffCreateFailed".Translate().ToString();
                return false;
            }

            HediffComp_ZhaoliKarmaLinkTarget linkTargetComp = hediffWithComps.GetComp<HediffComp_ZhaoliKarmaLinkTarget>();
            if (linkTargetComp == null)
            {
                failureReason = "MX_ZL_LinkHediffCompMissing".Translate().ToString();
                return false;
            }

            linkTargetComp.SetZhaoli(Pawn);
            hediffWithComps.GetComp<HediffComp_Disappears>()?.SetDuration(PropsLinks.linkDurationTicks);
            target.health.Notify_HediffChanged(hediff);

            if (!linkedPawns.Contains(target))
            {
                linkedPawns.Add(target);
                createdNewLink = true;
            }

            return true;
        }

        public void RemoveLinkReference(Pawn target)
        {
            if (target == null)
            {
                return;
            }

            linkedPawns.RemoveAll(pawn => pawn == null || pawn == target);
        }

        public void BreakLink(Pawn target)
        {
            if (target == null)
            {
                return;
            }

            RemoveLinkReference(target);
            ZhaoliKarmaUtility.RemoveTargetLinkHediff(target, Pawn);
            ZhaoliKarmaUtility.RemoveOverflowBurden(target);
        }

        public bool TryDistributeOverflow(int overflowCount)
        {
            if (overflowCount <= 0)
            {
                return true;
            }

            List<Pawn> eligiblePawns = GetEligibleOverflowPawns();
            if (eligiblePawns.Count < overflowCount)
            {
                return false;
            }

            for (int i = 0; i < overflowCount; i++)
            {
                int index = Rand.Range(0, eligiblePawns.Count);
                Pawn selectedPawn = eligiblePawns[index];
                eligiblePawns.RemoveAt(index);
                ZhaoliKarmaUtility.ApplyOverflowBurden(selectedPawn);
            }

            return true;
        }

        public Pawn GetRandomLiveLinkedPawn()
        {
            if (ZhaoliPowerBalance.Sealed) return null;
            List<Pawn> liveLinkedPawns = new List<Pawn>();
            CleanupInvalidLinks();
            for (int i = 0; i < linkedPawns.Count; i++)
            {
                Pawn linkedPawn = linkedPawns[i];
                if (linkedPawn == null || linkedPawn.Dead || linkedPawn.Destroyed)
                {
                    continue;
                }

                liveLinkedPawns.Add(linkedPawn);
            }

            if (liveLinkedPawns.Count == 0)
            {
                return null;
            }

            return liveLinkedPawns[Rand.Range(0, liveLinkedPawns.Count)];
        }

        private List<Pawn> GetEligibleOverflowPawns()
        {
            List<Pawn> eligiblePawns = new List<Pawn>();
            CleanupInvalidLinks();
            for (int i = 0; i < linkedPawns.Count; i++)
            {
                Pawn linkedPawn = linkedPawns[i];
                if (linkedPawn == null || linkedPawn.Dead || linkedPawn.Destroyed)
                {
                    continue;
                }

                if (ZhaoliKarmaUtility.HasOverflowBurden(linkedPawn))
                {
                    continue;
                }

                eligiblePawns.Add(linkedPawn);
            }

            return eligiblePawns;
        }

        private void CleanupInvalidLinks()
        {
            HashSet<Pawn> seenPawns = new HashSet<Pawn>();
            for (int i = linkedPawns.Count - 1; i >= 0; i--)
            {
                Pawn linkedPawn = linkedPawns[i];
                if (linkedPawn == null || linkedPawn.Destroyed || !seenPawns.Add(linkedPawn) || !ZhaoliKarmaUtility.HasLinkFrom(linkedPawn, Pawn))
                {
                    if (linkedPawn != null && !linkedPawn.Destroyed)
                    {
                        ZhaoliKarmaUtility.RemoveOverflowBurden(linkedPawn);
                    }

                    linkedPawns.RemoveAt(i);
                }
            }
        }

        private string BuildLinkSummary()
        {
            CleanupInvalidLinks();
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("MX_ZL_LinkTipExtra".Translate(ActiveLinkCount, PropsLinks.maxLinks).ToString());
            if (linkedPawns.Count == 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append("MX_ZL_LinkListNone".Translate().ToString());
                return stringBuilder.ToString();
            }

            stringBuilder.AppendLine();
            stringBuilder.Append("MX_ZL_LinkListHeader".Translate().ToString());
            for (int i = 0; i < linkedPawns.Count; i++)
            {
                Pawn linkedPawn = linkedPawns[i];
                if (linkedPawn == null)
                {
                    continue;
                }

                stringBuilder.AppendLine();
                stringBuilder.Append("  - ");
                stringBuilder.Append(linkedPawn.LabelShortCap);
                if (ZhaoliKarmaUtility.HasOverflowBurden(linkedPawn))
                {
                    stringBuilder.Append("MX_ZL_LinkOverflowBurdenSuffix".Translate().ToString());
                }
            }

            return stringBuilder.ToString();
        }
    }

    public class HediffCompProperties_ZhaoliKarmaLinkTarget : HediffCompProperties
    {
        public HediffCompProperties_ZhaoliKarmaLinkTarget()
        {
            compClass = typeof(HediffComp_ZhaoliKarmaLinkTarget);
        }
    }

    public class HediffComp_ZhaoliKarmaLinkTarget : HediffComp
    {
        private Pawn zhaoli;

        public Pawn Zhaoli => zhaoli;

        public override string CompLabelInBracketsExtra => zhaoli?.LabelShortCap;

        public override string CompTipStringExtra
        {
            get
            {
                if (zhaoli == null)
                {
                    return "MX_ZL_LinkTargetTipGeneric".Translate().ToString();
                }

                return "MX_ZL_LinkTargetTipNamed".Translate(zhaoli.LabelShortCap).ToString();
            }
        }

        public override void CompExposeData()
        {
            Scribe_References.Look(ref zhaoli, "zhaoli");
        }

        public override void CompPostPostRemoved()
        {
            ZhaoliKarmaUtility.GetLinkComp(zhaoli)?.RemoveLinkReference(Pawn);
            ZhaoliKarmaUtility.RemoveOverflowBurden(Pawn);
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            ZhaoliKarmaUtility.GetLinkComp(zhaoli)?.RemoveLinkReference(Pawn);
        }

        public void SetZhaoli(Pawn pawn)
        {
            zhaoli = pawn;
        }
    }

    public class GameComponent_ZhaoliKarma : GameComponent
    {
        private List<Pawn> pendingResurrectionPawns = new List<Pawn>();
        private List<ZhaoliPendingDingshuLink> pendingDingshuLinks = new List<ZhaoliPendingDingshuLink>();
        private List<ZhaoliPendingRebirth> pendingRebirths = new List<ZhaoliPendingRebirth>();
        private int nextRebirthCheckTick = int.MaxValue;

        public GameComponent_ZhaoliKarma(Game game)
        {
        }

        public void RegisterPendingResurrection(Pawn pawn)
        {
            if (pendingResurrectionPawns == null)
            {
                pendingResurrectionPawns = new List<Pawn>();
            }

            if (pawn == null || pendingResurrectionPawns.Contains(pawn))
            {
                return;
            }

            pendingResurrectionPawns.Add(pawn);
        }

        public void RegisterPendingDingshuLink(Pawn zhaoli, Pawn targetPawn, int expireTick)
        {
            if (pendingDingshuLinks == null)
            {
                pendingDingshuLinks = new List<ZhaoliPendingDingshuLink>();
            }

            if (zhaoli == null || targetPawn == null)
            {
                return;
            }

            for (int i = 0; i < pendingDingshuLinks.Count; i++)
            {
                ZhaoliPendingDingshuLink pendingLink = pendingDingshuLinks[i];
                if (pendingLink?.zhaoli == zhaoli && pendingLink.targetPawn == targetPawn)
                {
                    pendingLink.expireTick = expireTick;
                    return;
                }
            }

            pendingDingshuLinks.Add(new ZhaoliPendingDingshuLink(zhaoli, targetPawn, expireTick));
        }

        public bool IsPending(Pawn pawn)
        {
            if (pendingRebirths == null)
            {
                return false;
            }

            if (pawn == null)
            {
                return false;
            }

            for (int i = 0; i < pendingRebirths.Count; i++)
            {
                if (pendingRebirths[i]?.pawn == pawn)
                {
                    return true;
                }
            }

            return false;
        }

        public void RegisterPendingRebirth(Pawn pawn, int rebirthTick)
        {
            if (pendingRebirths == null)
            {
                pendingRebirths = new List<ZhaoliPendingRebirth>();
            }

            if (pawn == null)
            {
                return;
            }

            for (int i = 0; i < pendingRebirths.Count; i++)
            {
                if (pendingRebirths[i]?.pawn == pawn)
                {
                    pendingRebirths[i].rebirthTick = rebirthTick;
                    nextRebirthCheckTick = System.Math.Min(nextRebirthCheckTick, rebirthTick);
                    return;
                }
            }

            pendingRebirths.Add(new ZhaoliPendingRebirth(pawn, rebirthTick));
            nextRebirthCheckTick = System.Math.Min(nextRebirthCheckTick, rebirthTick);
        }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            if (pendingResurrectionPawns == null)
            {
                pendingResurrectionPawns = new List<Pawn>();
            }

            if (pendingDingshuLinks == null)
            {
                pendingDingshuLinks = new List<ZhaoliPendingDingshuLink>();
            }

            if (pendingRebirths == null)
            {
                pendingRebirths = new List<ZhaoliPendingRebirth>();
            }

            if (pendingResurrectionPawns.Count == 0 && pendingDingshuLinks.Count == 0 && pendingRebirths.Count == 0)
            {
                return;
            }

            for (int i = pendingResurrectionPawns.Count - 1; i >= 0; i--)
            {
                Pawn pawn = pendingResurrectionPawns[i];
                pendingResurrectionPawns.RemoveAt(i);
                ProcessPendingResurrection(pawn);
            }

            int currentTick = Find.TickManager.TicksGame;
            for (int i = pendingDingshuLinks.Count - 1; i >= 0; i--)
            {
                ZhaoliPendingDingshuLink pendingLink = pendingDingshuLinks[i];
                if (pendingLink?.zhaoli == null || pendingLink.targetPawn == null || pendingLink.zhaoli.Discarded || pendingLink.targetPawn.Discarded)
                {
                    pendingDingshuLinks.RemoveAt(i);
                    continue;
                }

                if (currentTick > pendingLink.expireTick)
                {
                    pendingDingshuLinks.RemoveAt(i);
                    continue;
                }

                if (pendingLink.targetPawn.Dead || pendingLink.targetPawn.Destroyed)
                {
                    continue;
                }

                HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.GetLinkComp(pendingLink.zhaoli);
                if (linkComp == null)
                {
                    pendingDingshuLinks.RemoveAt(i);
                    continue;
                }

                linkComp.TryAddOrRefreshLink(pendingLink.targetPawn, out _, out _);
                pendingDingshuLinks.RemoveAt(i);
            }

            if (pendingRebirths.Count == 0 || currentTick < nextRebirthCheckTick)
            {
                return;
            }

            nextRebirthCheckTick = int.MaxValue;
            for (int i = pendingRebirths.Count - 1; i >= 0; i--)
            {
                ZhaoliPendingRebirth pendingRebirth = pendingRebirths[i];
                if (pendingRebirth?.pawn == null || pendingRebirth.pawn.Discarded)
                {
                    pendingRebirths.RemoveAt(i);
                    continue;
                }

                if (!pendingRebirth.pawn.Dead)
                {
                    pendingRebirths.RemoveAt(i);
                    continue;
                }

                if (currentTick < pendingRebirth.rebirthTick)
                {
                    nextRebirthCheckTick = System.Math.Min(nextRebirthCheckTick, pendingRebirth.rebirthTick);
                    continue;
                }

                ZhaoliRebirthUtility.PreparePawnForPendingRebirth(pendingRebirth.pawn);

                if (!ZhaoliRebirthUtility.TryFindRebirthLocation(out Map map, out IntVec3 cell))
                {
                    nextRebirthCheckTick = System.Math.Min(nextRebirthCheckTick, currentTick + 1);
                    continue;
                }

                if (!ResurrectionUtility.TryResurrect(pendingRebirth.pawn, new ResurrectionParams
                {
                    gettingScarsChance = 0f,
                    canKidnap = false,
                    canTimeoutOrFlee = false,
                    sappers = false,
                    useAvoidGridSmart = true,
                    canSteal = false,
                    breachers = false,
                    canPickUpOpportunisticWeapons = false,
                    restoreMissingParts = true,
                    noLord = true,
                    dontSpawn = true,
                    invisibleStun = true,
                    removeDiedThoughts = false
                }))
                {
                    nextRebirthCheckTick = System.Math.Min(nextRebirthCheckTick, currentTick + 1);
                    continue;
                }

                if (pendingRebirth.pawn.IsWorldPawn())
                {
                    Find.WorldPawns.RemovePawn(pendingRebirth.pawn);
                }

                GenSpawn.Spawn(pendingRebirth.pawn, cell, map);
                ZhaoliRebirthUtility.FinalizeReturnedPawn(pendingRebirth.pawn);
                ZhaoliScenarioUtility.EnsureDefaultLoadout(pendingRebirth.pawn);
                ZhaoliRebirthUtility.NotifyApparelResurrected(pendingRebirth.pawn);
                Messages.Message("MX_ZL_RebirthReturned".Translate(), pendingRebirth.pawn, MessageTypeDefOf.PositiveEvent);
                pendingRebirths.RemoveAt(i);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingResurrectionPawns, "pendingResurrectionPawns", LookMode.Reference);
            Scribe_Collections.Look(ref pendingDingshuLinks, "pendingDingshuLinks", LookMode.Deep);
            Scribe_Collections.Look(ref pendingRebirths, "pendingRebirths", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pendingResurrectionPawns == null)
                {
                    pendingResurrectionPawns = new List<Pawn>();
                }

                if (pendingDingshuLinks == null)
                {
                    pendingDingshuLinks = new List<ZhaoliPendingDingshuLink>();
                }

                if (pendingRebirths == null)
                {
                    pendingRebirths = new List<ZhaoliPendingRebirth>();
                }

                pendingResurrectionPawns.RemoveAll(pawn => pawn == null || pawn.Discarded);
                pendingDingshuLinks.RemoveAll(entry => entry == null || entry.zhaoli == null || entry.targetPawn == null || entry.zhaoli.Discarded || entry.targetPawn.Discarded);
                pendingRebirths.RemoveAll(entry => entry == null || entry.pawn == null || entry.pawn.Discarded);
                RecalculateNextRebirthCheckTick();
            }
        }

        private void RecalculateNextRebirthCheckTick()
        {
            nextRebirthCheckTick = int.MaxValue;
            for (int index = 0; index < pendingRebirths.Count; index++)
            {
                ZhaoliPendingRebirth entry = pendingRebirths[index];
                if (entry != null)
                {
                    nextRebirthCheckTick = System.Math.Min(nextRebirthCheckTick, entry.rebirthTick);
                }
            }
        }

        private static void ProcessPendingResurrection(Pawn pawn)
        {
            if (ZhaoliPowerBalance.Sealed) return;
            if (pawn == null || pawn.Discarded || !pawn.Dead)
            {
                return;
            }

            HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.GetLinkComp(pawn);
            Pawn sacrificePawn = linkComp?.GetRandomLiveLinkedPawn();
            if (sacrificePawn == null)
            {
                return;
            }

            if (!linkComp.TryUseBalancedSubstitute()) return;
            linkComp.BreakLink(sacrificePawn);
            sacrificePawn.Kill(null);

            if (ResurrectionUtility.TryResurrect(pawn, new ResurrectionParams
            {
                gettingScarsChance = 0f,
                canKidnap = false,
                canTimeoutOrFlee = false,
                useAvoidGridSmart = true,
                canSteal = false,
                noLord = true,
                invisibleStun = true,
                removeDiedThoughts = true
            }))
            {
                ZhaoliRebirthUtility.FinalizeReturnedPawn(pawn);
                ZhaoliScenarioUtility.EnsureDefaultLoadout(pawn);
                ZhaoliRebirthUtility.NotifyApparelResurrected(pawn);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill_ZhaoliSubstitute
    {
        private static readonly Dictionary<Pawn, Pawn> pendingSubstitutePawns = new Dictionary<Pawn, Pawn>();

        public static bool HasPendingSubstitute(Pawn pawn)
        {
            return pawn != null && pendingSubstitutePawns.ContainsKey(pawn);
        }

        public static void Prefix(Pawn __instance)
        {
            if (__instance == null || __instance.Dead || !ZhaoliKarmaUtility.IsZhaoli(__instance))
            {
                return;
            }

            HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.GetLinkComp(__instance);
            Pawn sacrificePawn = linkComp?.GetRandomLiveLinkedPawn();
            if (sacrificePawn == null || sacrificePawn.Dead || sacrificePawn.Destroyed)
            {
                pendingSubstitutePawns.Remove(__instance);
                return;
            }

            if (!linkComp.TryUseBalancedSubstitute()) return;

            pendingSubstitutePawns[__instance] = sacrificePawn;
        }

        public static void Postfix(Pawn __instance)
        {
            if (__instance == null || !__instance.Dead || !ZhaoliKarmaUtility.IsZhaoli(__instance))
            {
                pendingSubstitutePawns.Remove(__instance);
                return;
            }

            if (pendingSubstitutePawns.TryGetValue(__instance, out Pawn sacrificePawn))
            {
                pendingSubstitutePawns.Remove(__instance);
                bool isRaidState = ZhaoliScenarioUtility.IsRaidState(__instance);

                HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.GetLinkComp(__instance);
                linkComp.BreakLink(sacrificePawn);
                if (!sacrificePawn.Dead && !sacrificePawn.Destroyed)
                {
                    sacrificePawn.Kill(null);
                }

                if (ResurrectionUtility.TryResurrect(__instance, new ResurrectionParams
                {
                    gettingScarsChance = 0f,
                    canKidnap = false,
                    canTimeoutOrFlee = false,
                    useAvoidGridSmart = true,
                    canSteal = false,
                    noLord = true,
                    invisibleStun = true,
                    removeDiedThoughts = true
                }))
                {
                    ZhaoliScenarioUtility.EnsureDefaultLoadout(__instance);
                    ZhaoliRebirthUtility.NotifyApparelResurrected(__instance);
                    if (isRaidState)
                    {
                        ZhaoliRebirthUtility.RemoveRebirthHediff(__instance);
                        ZhaoliScenarioUtility.GetRaidStateComp(__instance)?.NotifySubstituteTriggered();
                        return;
                    }

                    ZhaoliRebirthUtility.RegisterRecruitGrowthDeath(__instance);
                    return;
                }

                if (isRaidState)
                {
                    return;
                }

                ZhaoliRebirthUtility.TryScheduleRebirth(__instance);
            }
        }
    }
}
