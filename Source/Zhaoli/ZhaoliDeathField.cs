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
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return;
            }

            GenDraw.DrawRadiusRing(target.Cell, Props.radius, Color.red);
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
        private Dictionary<Pawn, int> stayTicks = new Dictionary<Pawn, int>();
        private readonly HashSet<Pawn> pawnsInsideNow = new HashSet<Pawn>();
        private readonly List<Pawn> pawnsToRemove = new List<Pawn>();

        private List<Pawn> tmpStayPawns;
        private List<int> tmpStayTicks;

        private int fieldCenterX;
        private int fieldCenterZ;
        private bool active;

        private HediffCompProperties_ZhaoliDeathField PropsField => (HediffCompProperties_ZhaoliDeathField)props;

        private IntVec3 FieldCenter => new IntVec3(fieldCenterX, 0, fieldCenterZ);

        public void ActivateAt(IntVec3 center)
        {
            fieldCenterX = center.x;
            fieldCenterZ = center.z;
            active = true;
            stayTicks.Clear();
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
            Scribe_Values.Look(ref active, "active", false);
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
                }
            }

            Scribe_Collections.Look(ref stayTicks, "stayTicks", LookMode.Reference, LookMode.Value, ref tmpStayPawns, ref tmpStayTicks);
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

            pawnsInsideNow.Clear();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, PropsField.radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Pawn pawn = things[i] as Pawn;
                    if (pawn == null || pawn == Pawn || pawn.Destroyed || pawn.Dead)
                    {
                        continue;
                    }

                    pawnsInsideNow.Add(pawn);
                }
            }

            foreach (Pawn pawn in pawnsInsideNow)
            {
                int ticksPresent = 0;
                stayTicks.TryGetValue(pawn, out ticksPresent);
                ticksPresent++;
                stayTicks[pawn] = ticksPresent;

                RefreshSlow(pawn);

                if (PropsField.bleedIntervalTicks > 0 && ticksPresent % PropsField.bleedIntervalTicks == 0)
                {
                    ApplyBleed(pawn);
                }

                if (PropsField.executeStayTicks > 0 && ticksPresent >= PropsField.executeStayTicks)
                {
                    ZhaoliKarmaUtility.AddKarma(Pawn, PropsField.karmaPerExecution);
                    ExecutePawn(pawn);
                    stayTicks.Remove(pawn);
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
                stayTicks.Remove(pawnsToRemove[i]);
            }
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

            DamageDef damageDef = PropsField.bleedDamageDef ?? DamageDefOf.Cut;
            DamageInfo damageInfo = new DamageInfo(damageDef, PropsField.bleedDamage, PropsField.bleedArmorPenetration, -1f, Pawn);
            pawn.TakeDamage(damageInfo);
        }

        private static void ExecutePawn(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed)
            {
                return;
            }

            pawn.Destroy(DestroyMode.Vanish);
        }
    }
}
