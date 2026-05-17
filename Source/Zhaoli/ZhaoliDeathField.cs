using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    public class CompProperties_AbilityZhaoliDeathField : CompProperties_AbilityEffect
    {
        public HediffDef fieldHediff;
        public float radius = 9f;

        public CompProperties_AbilityZhaoliDeathField()
        {
            compClass = typeof(CompAbilityEffect_ZhaoliDeathField);
        }
    }

    public class CompAbilityEffect_ZhaoliDeathField : CompAbilityEffect
    {
        private static readonly Color PreviewColor = new Color(0.48f, 0.08f, 0.1f);

        private new CompProperties_AbilityZhaoliDeathField Props => (CompProperties_AbilityZhaoliDeathField)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            if (caster == null || caster.health == null || !target.IsValid)
            {
                return;
            }

            HediffWithComps field = caster.health.hediffSet.GetFirstHediffOfDef(Props.fieldHediff) as HediffWithComps;
            if (field == null)
            {
                field = HediffMaker.MakeHediff(Props.fieldHediff, caster) as HediffWithComps;
                if (field == null)
                {
                    return;
                }

                caster.health.AddHediff(field);
            }

            HediffComp_ZhaoliDeathField comp = field.GetComp<HediffComp_ZhaoliDeathField>();
            comp?.ActivateAt(target.Cell);
            if (ZhaoliScenarioUtility.IsRaidState(caster))
            {
                ZhaoliKarmaUtility.AddKarma(caster, ZhaoliScenarioUtility.DeathFieldRaidBonusKarma);
            }

            if (caster.Spawned)
            {
                FleckMaker.Static(target.Cell, caster.Map, FleckDefOf.PsycastAreaEffect, Mathf.Max(1.5f, Props.radius * 0.65f));
                FleckMaker.Static(target.Cell, caster.Map, FleckDefOf.ExplosionFlash, 2.4f);
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return;
            }

            GenDraw.DrawRadiusRing(target.Cell, Props.radius, PreviewColor);
        }
    }

    public class HediffCompProperties_ZhaoliDeathField : HediffCompProperties
    {
        public float radius = 9f;
        public int fieldDurationTicks = 900;
        public int bleedIntervalTicks = 180;
        public float bleedDamage = 3f;
        public float bleedArmorPenetration = 0f;
        public DamageDef bleedDamageDef;
        public int executeStayTicks = 900;
        public HediffDef slowHediff;
        public int slowRefreshTicks = 90;
        public float karmaPerExecution = 1f;

        public HediffCompProperties_ZhaoliDeathField()
        {
            compClass = typeof(HediffComp_ZhaoliDeathField);
        }
    }

    public class HediffComp_ZhaoliDeathField : HediffComp
    {
        private const int FieldParticleIntervalTicks = 9;
        private const float FieldAreaRotationRate = 360f;

        private Dictionary<Pawn, int> stayTicks = new Dictionary<Pawn, int>();
        private Dictionary<Pawn, int> lastInsideTicks = new Dictionary<Pawn, int>();
        private readonly Dictionary<Pawn, Mote> markedPawns = new Dictionary<Pawn, Mote>();
        private readonly Dictionary<Pawn, int> lastDisplayedRemainingHits = new Dictionary<Pawn, int>();
        private readonly HashSet<Pawn> pawnsInsideNow = new HashSet<Pawn>();
        private readonly List<Pawn> pawnsToRemove = new List<Pawn>();
        private List<string> executedPawnLabels = new List<string>();

        private List<Pawn> tmpStayPawns;
        private List<int> tmpStayTicks;
        private List<Pawn> tmpLastInsidePawns;
        private List<int> tmpLastInsideTicks;

        private Mote fieldAreaMote;
        private int fieldCenterX;
        private int fieldCenterZ;
        private float activeRadius;
        private bool active;
        private int executionCount;
        private bool executionSummaryShown;

        private HediffCompProperties_ZhaoliDeathField PropsField => (HediffCompProperties_ZhaoliDeathField)props;

        private IntVec3 FieldCenter => new IntVec3(fieldCenterX, 0, fieldCenterZ);
        public float DefaultRadius => PropsField.radius;
        private float CurrentRadius => activeRadius > 0f ? activeRadius : PropsField.radius;

        public void ActivateAt(IntVec3 center, float radiusOverride = -1f)
        {
            fieldCenterX = center.x;
            fieldCenterZ = center.z;
            activeRadius = radiusOverride > 0f ? radiusOverride : PropsField.radius;
            active = true;
            stayTicks.Clear();
            lastInsideTicks.Clear();
            markedPawns.Clear();
            lastDisplayedRemainingHits.Clear();
            executedPawnLabels.Clear();
            executionCount = 0;
            executionSummaryShown = false;
            fieldAreaMote = null;
            parent.TryGetComp<HediffComp_Disappears>()?.SetDuration(PropsField.fieldDurationTicks);
        }

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref fieldCenterX, "fieldCenterX", 0);
            Scribe_Values.Look(ref fieldCenterZ, "fieldCenterZ", 0);
            Scribe_Values.Look(ref activeRadius, "activeRadius", 0f);
            Scribe_Values.Look(ref active, "active", false);
            Scribe_Values.Look(ref executionCount, "executionCount", 0);
            Scribe_Values.Look(ref executionSummaryShown, "executionSummaryShown", false);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                pawnsToRemove.Clear();
                foreach (KeyValuePair<Pawn, int> pair in stayTicks)
                {
                    if (pair.Key == null || pair.Key.Destroyed)
                    {
                        pawnsToRemove.Add(pair.Key);
                    }
                }

                for (int i = 0; i < pawnsToRemove.Count; i++)
                {
                    stayTicks.Remove(pawnsToRemove[i]);
                    lastInsideTicks.Remove(pawnsToRemove[i]);
                }
            }

            Scribe_Collections.Look(ref stayTicks, "stayTicks", LookMode.Reference, LookMode.Value, ref tmpStayPawns, ref tmpStayTicks);
            Scribe_Collections.Look(ref lastInsideTicks, "lastInsideTicks", LookMode.Reference, LookMode.Value, ref tmpLastInsidePawns, ref tmpLastInsideTicks);
            Scribe_Collections.Look(ref executedPawnLabels, "executedPawnLabels", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (stayTicks == null)
                {
                    stayTicks = new Dictionary<Pawn, int>();
                }

                if (lastInsideTicks == null)
                {
                    lastInsideTicks = new Dictionary<Pawn, int>();
                }

                if (executedPawnLabels == null)
                {
                    executedPawnLabels = new List<string>();
                }
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!active || Pawn == null || Pawn.Dead)
            {
                return;
            }

            Map map = Pawn.MapHeld;
            IntVec3 center = FieldCenter;
            if (map == null || !center.IsValid || !center.InBounds(map))
            {
                return;
            }

            MaintainFieldArea(map, center);
            MaintainFieldParticles(map, center);
            if (Find.TickManager != null && Find.TickManager.TicksGame % 60 == 0)
            {
                FleckDef deathPulse = ZhaoliEffectUtility.DeathRefusalPulseFleckDef;
                if (deathPulse != null)
                {
                    FleckMaker.Static(center, map, deathPulse, Mathf.Max(2f, CurrentRadius * 0.55f));
                }
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            pawnsInsideNow.Clear();
            IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                Pawn pawn = allPawnsSpawned[i];
                if (pawn == null || pawn == Pawn || pawn.Destroyed || pawn.Dead)
                {
                    continue;
                }

                if (!ZhaoliScenarioUtility.ShouldDeathFieldAffectTarget(Pawn, pawn))
                {
                    continue;
                }

                if (pawn.Position.InHorDistOf(center, CurrentRadius))
                {
                    pawnsInsideNow.Add(pawn);
                }
            }

            foreach (Pawn pawn in pawnsInsideNow)
            {
                int ticksPresent = 0;
                stayTicks.TryGetValue(pawn, out ticksPresent);
                if (!lastInsideTicks.TryGetValue(pawn, out int lastInsideTick) || lastInsideTick < currentTick - 1)
                {
                    ticksPresent = 0;
                    lastDisplayedRemainingHits.Remove(pawn);
                }

                ticksPresent++;
                stayTicks[pawn] = ticksPresent;
                lastInsideTicks[pawn] = currentTick;

                MaintainFieldMark(pawn);
                RefreshSlow(pawn);
                ShowCountdownIfNeeded(pawn, ticksPresent);

                if (PropsField.bleedIntervalTicks > 0 && ticksPresent % PropsField.bleedIntervalTicks == 0)
                {
                    ApplyBleed(pawn);
                }

                if (PropsField.executeStayTicks > 0 && ticksPresent >= PropsField.executeStayTicks)
                {
                    ZhaoliKarmaUtility.AddKarma(Pawn, PropsField.karmaPerExecution);
                    ZhaoliShieldLayerUtility.AddLayers(Pawn, ZhaoliShieldLayerUtility.ShieldLayersPerExecution);
                    ExecutePawn(pawn);
                    stayTicks.Remove(pawn);
                    lastInsideTicks.Remove(pawn);
                    markedPawns.Remove(pawn);
                    lastDisplayedRemainingHits.Remove(pawn);
                }
            }

            pawnsToRemove.Clear();
            foreach (KeyValuePair<Pawn, int> pair in stayTicks)
            {
                if (pair.Key == null || pair.Key.Destroyed || pair.Key.Dead || !pawnsInsideNow.Contains(pair.Key))
                {
                    pawnsToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < pawnsToRemove.Count; i++)
            {
                Pawn pawn = pawnsToRemove[i];
                stayTicks.Remove(pawn);
                lastInsideTicks.Remove(pawn);
                markedPawns.Remove(pawn);
                lastDisplayedRemainingHits.Remove(pawn);
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            ShowExecutionSummary();
        }

        private void RefreshSlow(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || PropsField.slowHediff == null)
            {
                return;
            }

            Hediff slow = pawn.health.hediffSet.GetFirstHediffOfDef(PropsField.slowHediff);
            if (slow == null)
            {
                slow = HediffMaker.MakeHediff(PropsField.slowHediff, pawn);
                pawn.health.AddHediff(slow);
            }

            slow.TryGetComp<HediffComp_Disappears>()?.SetDuration(PropsField.slowRefreshTicks);
        }

        private void ApplyBleed(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed)
            {
                return;
            }

            BodyPartRecord part = pawn.health?.hediffSet?.GetRandomNotMissingPart(DamageDefOf.Cut, BodyPartHeight.Undefined, BodyPartDepth.Outside);
            if (part == null)
            {
                return;
            }

            Hediff_Injury injury = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part) as Hediff_Injury;
            if (injury == null)
            {
                return;
            }

            injury.Severity = Mathf.Max(0.1f, PropsField.bleedDamage);
            pawn.health.AddHediff(injury, part);
            if (pawn.Spawned)
            {
                FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.FlashHollow, 1.1f);
                FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastAreaEffect, 0.9f);
            }
        }

        private void MaintainFieldArea(Map map, IntVec3 center)
        {
            ThingDef areaDef = ZhaoliEffectUtility.DeathFieldAreaMoteDef;
            if (areaDef == null)
            {
                return;
            }

            if (fieldAreaMote == null || fieldAreaMote.Destroyed)
            {
                fieldAreaMote = MoteMaker.MakeStaticMote(center, map, areaDef, 1f);
                if (fieldAreaMote != null)
                {
                    fieldAreaMote.exactRotation = Rand.Range(0f, 360f);
                    fieldAreaMote.rotationRate = FieldAreaRotationRate;
                }
            }

            fieldAreaMote?.Maintain();
        }

        private void MaintainFieldParticles(Map map, IntVec3 center)
        {
            if (Find.TickManager == null || Find.TickManager.TicksGame % FieldParticleIntervalTicks != 0)
            {
                return;
            }

            ThingDef particleDef = ZhaoliEffectUtility.RandomDeathFieldParticleMoteDef;
            if (particleDef == null)
            {
                return;
            }

            Vector3 loc = center.ToVector3Shifted() + Rand.InsideUnitCircleVec3 * Mathf.Max(0.1f, CurrentRadius * 0.92f);
            Mote mote = MoteMaker.MakeStaticMote(loc, map, particleDef, Rand.Range(0.85f, 1.25f), false, Rand.Range(0f, 360f));
            if (mote != null)
            {
                mote.rotationRate = Rand.Range(-45f, 45f);
            }
        }

        private void MaintainFieldMark(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned)
            {
                return;
            }

            ThingDef markDef = ZhaoliEffectUtility.DeathFieldMarkMoteDef;
            if (markDef == null)
            {
                return;
            }

            if (!markedPawns.TryGetValue(pawn, out Mote mote) || mote == null || mote.Destroyed)
            {
                mote = MoteMaker.MakeAttachedOverlay(pawn, markDef, Vector3.zero, 1f);
                markedPawns[pawn] = mote;
            }

            mote.Maintain();
        }

        private void ShowCountdownIfNeeded(Pawn pawn, int ticksPresent)
        {
            if (pawn == null || !pawn.Spawned)
            {
                return;
            }

            int totalHitsRequired = GetTotalBleedHitsRequired();
            if (totalHitsRequired <= 0)
            {
                return;
            }

            int hitsTaken = PropsField.bleedIntervalTicks > 0 ? ticksPresent / PropsField.bleedIntervalTicks : 0;
            int remainingHits = Mathf.Clamp(totalHitsRequired - hitsTaken, 1, totalHitsRequired);
            if (PropsField.executeStayTicks > 0 && ticksPresent >= PropsField.executeStayTicks)
            {
                return;
            }

            if (lastDisplayedRemainingHits.TryGetValue(pawn, out int lastShown) && lastShown == remainingHits)
            {
                return;
            }

            lastDisplayedRemainingHits[pawn] = remainingHits;
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, remainingHits.ToString(), new Color(0.94f, 0.26f, 0.26f), 1.1f);
        }

        private int GetTotalBleedHitsRequired()
        {
            if (PropsField.bleedIntervalTicks <= 0 || PropsField.executeStayTicks <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.CeilToInt((float)PropsField.executeStayTicks / PropsField.bleedIntervalTicks));
        }

        private void ExecutePawn(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed)
            {
                return;
            }

            string pawnLabel = pawn.LabelShortCap;
            Map map = pawn.MapHeld;
            IntVec3 position = pawn.PositionHeld;
            if (map != null && position.IsValid)
            {
                FleckDef soulFleck = ZhaoliEffectUtility.DeathRefusalBubbleFleckDef;
                if (soulFleck != null)
                {
                    FleckMaker.Static(position, map, soulFleck, 1.6f);
                }

                FleckMaker.Static(position, map, FleckDefOf.ExplosionFlash, 1.6f);
                FleckMaker.Static(position, map, FleckDefOf.FlashHollow, 1.4f);

                ThingDef soulPulseDef = ZhaoliEffectUtility.SoulAbsorbPulseMoteDef;
                if (soulPulseDef != null && Pawn != null && Pawn.Spawned && Pawn.MapHeld == map)
                {
                    MoteMaker.MakeInteractionOverlay(soulPulseDef, new TargetInfo(position, map), Pawn);
                    FleckMaker.Static(Pawn.Position, map, FleckDefOf.PsycastAreaEffect, 1.15f);
                }
            }

            DamageInfo dinfo = new DamageInfo(
                DamageDefOf.ExecutionCut,
                99999f,
                999f,
                -1f,
                Pawn,
                null,
                Pawn?.equipment?.Primary?.def,
                DamageInfo.SourceCategory.ThingOrUnknown,
                pawn,
                instigatorGuilty: false,
                spawnFilth: false,
                checkForJobOverride: false);
            dinfo.SetIgnoreArmor(true);
            dinfo.SetIgnoreInstantKillProtection(true);
            pawn.Kill(dinfo);
            DiscardExecutedPawn(pawn);
            RecordExecution(pawnLabel);
        }

        private static void DiscardExecutedPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            Corpse corpse = pawn.Corpse;
            if (corpse != null && !corpse.Destroyed)
            {
                corpse.InnerPawn = null;
                corpse.Destroy(DestroyMode.Vanish);
            }

            if (Find.WorldPawns != null && Find.WorldPawns.Contains(pawn))
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
            }
            else if (pawn.Destroyed && !pawn.Discarded)
            {
                pawn.Discard(silentlyRemoveReferences: true);
            }
        }

        private void RecordExecution(string pawnLabel)
        {
            if (executedPawnLabels == null)
            {
                executedPawnLabels = new List<string>();
            }

            executedPawnLabels.Add(pawnLabel.NullOrEmpty() ? "MX_Common_UnknownTarget".Translate().ToString() : pawnLabel);
            executionCount = executedPawnLabels.Count;
        }

        private void ShowExecutionSummary()
        {
            if (executionSummaryShown || executionCount <= 0 || Pawn == null)
            {
                return;
            }

            executionSummaryShown = true;
            string names = executedPawnLabels.NullOrEmpty() ? "MX_Common_None".Translate().ToString() : string.Join("MX_ZL_ListSeparator".Translate().ToString(), executedPawnLabels);
            MessageTypeDef messageType = Pawn.Faction == Faction.OfPlayer ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.ThreatSmall;
            Messages.Message("MX_ZL_DeathFieldExecuted".Translate(executionCount, names), Pawn, messageType);
        }
    }
}
