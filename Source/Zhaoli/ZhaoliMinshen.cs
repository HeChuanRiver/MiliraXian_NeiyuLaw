using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Zhaoli
{
    public class CompProperties_AbilityZhaoliMinshen : CompProperties_AbilityEffect
    {
        public float karmaCost = 3f;
        public int areaWidth = 13;
        public int areaHeight = 13;
        public float dazeChance = 0.1f;
        public int mentalStateDurationTicks = 1800;
        public int damageDurationTicks = 300;
        public float empDamage = 15f;

        public HediffDef slowHediff;
        public HediffDef damageHediff;

        public CompProperties_AbilityZhaoliMinshen()
        {
            compClass = typeof(CompAbilityEffect_ZhaoliMinshen);
        }
    }

    public class CompAbilityEffect_ZhaoliMinshen : CompAbilityEffect
    {
        private const string DazedMentalStateDefName = "WanderConfused";

        private readonly HashSet<Pawn> tmpTargets = new HashSet<Pawn>();
        private readonly List<IntVec3> tmpPreviewCells = new List<IntVec3>();

        private new CompProperties_AbilityZhaoliMinshen Props => (CompProperties_AbilityZhaoliMinshen)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            if (caster == null || caster.Map == null || !target.IsValid)
            {
                return;
            }

            if (!ZhaoliKarmaUtility.TryConsumeKarma(caster, Props.karmaCost))
            {
                if (caster.Faction == Faction.OfPlayer)
                {
                    Messages.Message("因果不足，无法施放泯神。", caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return;
            }

            tmpTargets.Clear();
            CellRect area = CellRect.CenteredOn(target.Cell, Props.areaWidth, Props.areaHeight).ClipInsideMap(caster.Map);
            foreach (IntVec3 cell in area)
            {
                List<Thing> things = cell.GetThingList(caster.Map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn pawn && pawn != caster && !pawn.Dead && !pawn.Destroyed)
                    {
                        tmpTargets.Add(pawn);
                    }
                }
            }

            foreach (Pawn pawn in tmpTargets)
            {
                if (!pawn.HostileTo(caster))
                {
                    continue;
                }

                if (!pawn.RaceProps.IsFlesh)
                {
                    ApplyEmp(caster, pawn);
                    continue;
                }

                ApplyLifeLoss(pawn);

                bool applyDazed = !pawn.Downed && pawn.Awake() && Rand.Chance(Props.dazeChance);
                if (applyDazed && TryApplyDazed(caster, pawn))
                {
                    continue;
                }

                ApplySlow(pawn);
            }

            if (caster.Spawned)
            {
                MoteMaker.ThrowText(caster.DrawPos, caster.Map, "泯神", 3.65f);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !target.IsValid)
            {
                return false;
            }

            if (ZhaoliKarmaUtility.GetCurrentKarma(caster) < Props.karmaCost)
            {
                if (throwMessages)
                {
                    Messages.Message("因果不足，无法施放泯神。", caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            Pawn caster = parent?.pawn;
            if (caster?.Map == null || !target.IsValid)
            {
                return;
            }

            tmpPreviewCells.Clear();
            CellRect area = CellRect.CenteredOn(target.Cell, Props.areaWidth, Props.areaHeight).ClipInsideMap(caster.Map);
            foreach (IntVec3 cell in area)
            {
                tmpPreviewCells.Add(cell);
            }

            GenDraw.DrawFieldEdges(tmpPreviewCells, Color.red);
        }

        private void ApplySlow(Pawn pawn)
        {
            if (Props.slowHediff == null)
            {
                return;
            }

            Hediff hediff = pawn.health.GetOrAddHediff(Props.slowHediff);
            hediff?.TryGetComp<HediffComp_Disappears>()?.SetDuration(Props.mentalStateDurationTicks);
            if (hediff != null)
            {
                pawn.health.Notify_HediffChanged(hediff);
            }
        }

        private void ApplyLifeLoss(Pawn pawn)
        {
            if (Props.damageHediff == null)
            {
                return;
            }

            Hediff hediff = pawn.health.GetOrAddHediff(Props.damageHediff);
            hediff?.TryGetComp<HediffComp_Disappears>()?.SetDuration(Props.damageDurationTicks);
            HediffComp_ZhaoliMinshenDamage damageComp = hediff?.TryGetComp<HediffComp_ZhaoliMinshenDamage>();
            damageComp?.ResetTimer();
            if (hediff != null)
            {
                pawn.health.Notify_HediffChanged(hediff);
            }
        }

        private bool TryApplyDazed(Pawn caster, Pawn pawn)
        {
            MentalStateDef stateDef = DefDatabase<MentalStateDef>.GetNamedSilentFail(DazedMentalStateDefName);
            MentalStateHandler handler = pawn.mindState?.mentalStateHandler;
            if (stateDef == null || handler == null)
            {
                return false;
            }

            if (!handler.TryStartMentalState(stateDef, "昭离的泯神使其失神。", forced: true, forceWake: true, causedByMood: false, otherPawn: caster, transitionSilently: false, causedByDamage: false, causedByPsycast: false))
            {
                return false;
            }

            if (handler.CurState != null)
            {
                handler.CurState.forceRecoverAfterTicks = Props.mentalStateDurationTicks;
                handler.CurState.sourceFaction = caster.Faction;
            }

            return true;
        }

        private void ApplyEmp(Pawn caster, Pawn pawn)
        {
            pawn.TakeDamage(new DamageInfo(DamageDefOf.EMP, Props.empDamage, 2f, -1f, caster));
        }
    }

    public class HediffCompProperties_ZhaoliMinshenDamage : HediffCompProperties
    {
        public int damageIntervalTicks = 60;
        public float damagePerTick = 1f;
        public HediffDef injuryHediff;

        public HediffCompProperties_ZhaoliMinshenDamage()
        {
            compClass = typeof(HediffComp_ZhaoliMinshenDamage);
        }
    }

    public class HediffComp_ZhaoliMinshenDamage : HediffComp
    {
        private int ticksUntilDamage;

        private HediffCompProperties_ZhaoliMinshenDamage PropsDamage => (HediffCompProperties_ZhaoliMinshenDamage)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            ResetTimer();
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref ticksUntilDamage, "ticksUntilDamage", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            ticksUntilDamage--;
            if (ticksUntilDamage > 0)
            {
                return;
            }

            ticksUntilDamage = PropsDamage.damageIntervalTicks;
            ApplyTorsoDamage();
        }

        public void ResetTimer()
        {
            ticksUntilDamage = PropsDamage.damageIntervalTicks;
        }

        private void ApplyTorsoDamage()
        {
            if (PropsDamage.injuryHediff == null || Pawn?.health?.hediffSet == null)
            {
                return;
            }

            BodyPartRecord torso = GetTorsoPart(Pawn);
            if (torso == null)
            {
                return;
            }

            Hediff_Injury injury = HediffMaker.MakeHediff(PropsDamage.injuryHediff, Pawn, torso) as Hediff_Injury;
            if (injury == null)
            {
                return;
            }

            injury.Severity = PropsDamage.damagePerTick;
            Pawn.health.AddHediff(injury, torso);
        }

        private static BodyPartRecord GetTorsoPart(Pawn pawn)
        {
            List<BodyPartRecord> torsoParts = pawn.RaceProps?.body?.GetPartsWithDef(BodyPartDefOf.Torso);
            if (torsoParts == null)
            {
                return null;
            }

            for (int i = 0; i < torsoParts.Count; i++)
            {
                BodyPartRecord part = torsoParts[i];
                if (part != null && !pawn.health.hediffSet.PartIsMissing(part))
                {
                    return part;
                }
            }

            return null;
        }
    }
}
