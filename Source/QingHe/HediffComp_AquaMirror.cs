using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_AquaMirror : HediffCompProperties
    {
        public int healAmount = 10;
        public float explosionRadius = 2.0f;
        public float explosionDamage = 10.0f;
        public float tempestPerMirror = 5.0f;
        public float shieldRadius = 1.0f;
        public float shieldAmount = 100.0f;
        
        public HediffCompProperties_AquaMirror()
        {
            compClass = typeof(HediffComp_AquaMirror);
        }
    }
    
    public class HediffComp_AquaMirror : HediffComp
    {
        public HediffCompProperties_AquaMirror Props => (HediffCompProperties_AquaMirror)props;

        public Pawn caster;

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            // 视觉效果
            Log.Message("Creating mote"); // TODO: Remove debug msg
            MoteMaker.MakeStaticMote(parent.pawn.TrueCenter(), parent.pawn.Map, MX_QHDefOf.Mote_AquaMirrorExplode, 2.0f);
            // 伤害附近的敌人
            foreach (var thing in GenRadial.RadialDistinctThingsAround(parent.pawn.Position, parent.pawn.Map, Props.explosionRadius, true))
            {
                if (thing is Pawn pawn && !pawn.Dead && pawn.HostileTo(caster))
                {
                    var dinfo = new DamageInfo(MX_QHDefOf.MX_Dehydrate, Props.explosionDamage,
                        armorPenetration: 1000.0f, instigator: caster);
                    dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Inside);
                    dinfo.SetIgnoreArmor(true);
                    dinfo.SetApplyAllDamage(true);
                    pawn.TakeDamage(dinfo);
                }
            }
            // 治疗效果
            parent.pawn.HitPoints += Props.healAmount;
            var hediffs = new List<Hediff>();
            hediffs.AddRange(parent.pawn.health.hediffSet.hediffs);
            foreach (var h in hediffs)
            {
                if (h is Hediff_Injury injury && !injury.IsPermanent())
                {
                    parent.pawn.health.RemoveHediff(h);
                }
                if (h is Hediff_MissingPart missingPart)
                {
                    parent.pawn.health.RestorePart(missingPart.Part);
                }
            }
            // 积累激流
            PawnSpecialResourceUtility.AddResource(caster, MX_QHDefOf.MXQH_Tempest, Props.tempestPerMirror);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref caster, "caster");
        }
    }
}