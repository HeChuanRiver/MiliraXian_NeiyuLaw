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

    public class CompAbilityEffect_ZhaoliMinshen : CompAbilityEffect_ZhaoliPowerLimited
    {
        private const string DazedMentalStateDefName = "WanderConfused";
        private static readonly Color PreviewColor = new Color(0.44f, 0.12f, 0.16f);

        private readonly HashSet<Pawn> tmpTargets = new HashSet<Pawn>();
        private readonly List<IntVec3> tmpPreviewCells = new List<IntVec3>();

        private new CompProperties_AbilityZhaoliMinshen Props => (CompProperties_AbilityZhaoliMinshen)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (base.GizmoDisabled(out reason)) return true;
            ZhaoliKarmaUtility.ResetNoCooldownAbilityLock(parent);
            reason = null;
            return false;
        }

        public override IEnumerable<PreCastAction> GetPreCastActions()
        {
            int warmupTicks = GetWarmupTicks();
            if (warmupTicks <= 0)
            {
                yield break;
            }

            yield return new PreCastAction
            {
                ticksAwayFromCast = warmupTicks,
                action = delegate(LocalTargetInfo target, LocalTargetInfo dest)
                {
                    Pawn caster = parent?.pawn;
                    if (caster?.Map == null || !target.IsValid)
                    {
                        return;
                    }

                    float areaScale = Mathf.Max(2.2f, Mathf.Max(Props.areaWidth, Props.areaHeight) * 0.34f);
                    bool usingUnityVfx = MiliraXian.Characters.CharacterUnityVfxRuntime.TryPlayWorld(
                        MiliraXian.Characters.CharacterUnityVfxKind.ZhaoliMinshen,
                        caster.Map,
                        target.Cell,
                        1f,
                        Mathf.Max(1, warmupTicks));
                    if (!usingUnityVfx)
                    {
                        SpawnMinshenAreaPulse(target.Cell, caster.Map);
                        FleckMaker.Static(target.Cell, caster.Map, FleckDefOf.PsycastAreaEffect, areaScale);
                    }
                    FleckMaker.Static(caster.Position, caster.Map, FleckDefOf.FeedbackShoot, 1f);
                }
            };

            int midWarmupTicks = Mathf.Max(1, warmupTicks * 2 / 3);
            if (midWarmupTicks < warmupTicks)
            {
                yield return new PreCastAction
                {
                    ticksAwayFromCast = midWarmupTicks,
                    action = delegate(LocalTargetInfo target, LocalTargetInfo dest)
                    {
                        Pawn caster = parent?.pawn;
                        if (caster?.Map == null || !target.IsValid)
                        {
                            return;
                        }

                        if (!MiliraXian.Characters.CharacterUnityVfxRuntime.IsAvailable(
                                MiliraXian.Characters.CharacterUnityVfxKind.ZhaoliMinshen))
                        {
                            SpawnMinshenAreaPulse(target.Cell, caster.Map);
                        }
                    }
                };
            }

            int particleWarmupTicks = Mathf.Max(1, warmupTicks / 3);
            if (particleWarmupTicks < midWarmupTicks)
            {
                yield return new PreCastAction
                {
                    ticksAwayFromCast = particleWarmupTicks,
                    action = delegate(LocalTargetInfo target, LocalTargetInfo dest)
                    {
                        Pawn caster = parent?.pawn;
                        if (caster?.Map == null || !target.IsValid)
                        {
                            return;
                        }

                        if (!MiliraXian.Characters.CharacterUnityVfxRuntime.IsAvailable(
                                MiliraXian.Characters.CharacterUnityVfxKind.ZhaoliMinshen))
                        {
                            SpawnMinshenParticles(target.Cell, caster.Map, 7);
                        }
                    }
                };
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (ZhaoliPowerBalance.Sealed) return;
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
                    Messages.Message("MX_ZL_NotEnoughKarmaMinshen".Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
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

            if (caster.Spawned)
            {
                bool usingUnityVfx = MiliraXian.Characters.CharacterUnityVfxRuntime.TryPlayWorld(
                    MiliraXian.Characters.CharacterUnityVfxKind.ZhaoliMinshenImpact,
                    caster.Map,
                    target.Cell,
                    1f,
                    36);
                if (!usingUnityVfx)
                {
                    float areaScale = Mathf.Max(2.5f, Mathf.Max(Props.areaWidth, Props.areaHeight) * 0.35f);
                    FleckMaker.Static(target.Cell, caster.Map, FleckDefOf.PsycastAreaEffect, areaScale);
                    FleckMaker.Static(target.Cell, caster.Map, FleckDefOf.ExplosionFlash, 1.8f);
                    SpawnMinshenParticles(target.Cell, caster.Map, 10);
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

                ApplyLifeLoss(caster, pawn);

                bool applyDazed = !pawn.Downed && pawn.Awake() && Rand.Chance(Props.dazeChance);
                if (applyDazed && TryApplyDazed(caster, pawn))
                {
                    continue;
                }

                ApplySlow(pawn);
            }

            if (caster.Spawned)
            {
                MoteMaker.ThrowText(caster.DrawPos, caster.Map, "MX_ZL_MinshenMote".Translate().ToString(), 3.65f);
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
                    Messages.Message("MX_ZL_NotEnoughKarmaMinshen".Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
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

            GenDraw.DrawFieldEdges(tmpPreviewCells, PreviewColor);
        }

        private void SpawnMinshenAreaPulse(IntVec3 center, Map map)
        {
            if (map == null)
            {
                return;
            }

            ThingDef warnAreaDef = ZhaoliEffectUtility.MinshenWarnAreaMoteDef;
            if (warnAreaDef != null)
            {
                MoteMaker.MakeStaticMote(center, map, warnAreaDef, 1f);
            }
        }

        private void SpawnMinshenParticles(IntVec3 center, Map map, int count)
        {
            if (map == null || count <= 0)
            {
                return;
            }

            float radius = Mathf.Max(1f, Mathf.Max(Props.areaWidth, Props.areaHeight) * 0.48f);
            for (int i = 0; i < count; i++)
            {
                ThingDef particleDef = ZhaoliEffectUtility.RandomDeathFieldParticleMoteDef;
                if (particleDef == null)
                {
                    return;
                }

                Vector3 loc = center.ToVector3Shifted() + Rand.InsideUnitCircleVec3 * radius;
                Mote mote = MoteMaker.MakeStaticMote(loc, map, particleDef, Rand.Range(0.9f, 1.35f), false, Rand.Range(0f, 360f));
                if (mote != null)
                {
                    mote.rotationRate = Rand.Range(-35f, 35f);
                }
            }
        }

        private int GetWarmupTicks()
        {
            return parent?.def?.verbProperties != null ? GenTicks.SecondsToTicks(parent.def.verbProperties.warmupTime) : 0;
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

            if (pawn.Spawned)
            {
                FleckMaker.AttachedOverlay(pawn, FleckDefOf.PsycastAreaEffect, Vector3.zero, 1.05f);
            }
        }

        private void ApplyLifeLoss(Pawn caster, Pawn pawn)
        {
            if (Props.damageHediff == null)
            {
                return;
            }

            Hediff hediff = pawn.health.GetOrAddHediff(Props.damageHediff);
            hediff?.TryGetComp<HediffComp_Disappears>()?.SetDuration(Props.damageDurationTicks);
            HediffComp_ZhaoliMinshenDamage damageComp = hediff?.TryGetComp<HediffComp_ZhaoliMinshenDamage>();
            damageComp?.SetCaster(caster);
            damageComp?.ResetTimer();
            if (hediff != null)
            {
                pawn.health.Notify_HediffChanged(hediff);
            }

            if (pawn.Spawned)
            {
                FleckMaker.AttachedOverlay(pawn, FleckDefOf.FlashHollow, Vector3.zero, 0.95f);
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

            if (!handler.TryStartMentalState(stateDef, "MX_ZL_MinshenMentalStateReason".Translate().ToString(), forced: true, forceWake: true, causedByMood: false, otherPawn: caster, transitionSilently: false, causedByDamage: false, causedByPsycast: false))
            {
                return false;
            }

            if (handler.CurState != null)
            {
                handler.CurState.forceRecoverAfterTicks = Props.mentalStateDurationTicks;
                handler.CurState.sourceFaction = caster.Faction;
                if (!ZhaoliPowerBalance.IsOriginal) handler.CurState.causedByPawn = caster;
            }

            if (pawn.Spawned)
            {
                FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastAreaEffect, 1f);
            }

            return true;
        }

        private void ApplyEmp(Pawn caster, Pawn pawn)
        {
            pawn.TakeDamage(new DamageInfo(DamageDefOf.EMP, Props.empDamage, 2f, -1f, caster));
            if (pawn.Spawned)
            {
                FleckMaker.AttachedOverlay(pawn, FleckDefOf.MicroSparksFast, Vector3.zero, 1.1f);
                FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.ExplosionFlash, 1.1f);
            }
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
        private Pawn caster;
        private int ticksUntilDamage;

        private HediffCompProperties_ZhaoliMinshenDamage PropsDamage => (HediffCompProperties_ZhaoliMinshenDamage)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            ResetTimer();
        }

        public override void CompExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref ticksUntilDamage, "ticksUntilDamage", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (ZhaoliPowerBalance.Sealed) { Pawn?.health?.RemoveHediff(parent); return; }
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

        public void SetCaster(Pawn pawn)
        {
            caster = pawn;
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

            DamageInfo dinfo = new DamageInfo(DamageDefOf.Blunt, PropsDamage.damagePerTick, 999f, -1f, caster, torso, null, DamageInfo.SourceCategory.ThingOrUnknown, Pawn, instigatorGuilty: false, spawnFilth: false);
            dinfo.SetIgnoreArmor(true);
            if (!ZhaoliPowerBalance.IsOriginal)
            {
                dinfo = new DamageInfo(DamageDefOf.Blunt, PropsDamage.damagePerTick, .1f, -1f, caster);
            }
            Pawn.TakeDamage(dinfo);
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
