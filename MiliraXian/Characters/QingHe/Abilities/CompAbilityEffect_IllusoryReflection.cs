using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Abilities
{
    /// <summary>
    /// Mirror Flower, Water Moon. Times are game ticks (60 = 1 s) and copy the preview script's
    /// constants (.cache/mirror_proto/mirror_preview.py): WINDUP 0.5, SHATTER 0.5, CUT_DELAY 0.2,
    /// CUT 0.4, REFORM_DELAY 0.5, RECOVER 0.5 (WEAPON_BACK 0.15), EXIT 0.5.
    /// </summary>
    public class CompProperties_AbilityIllusoryReflection : CompProperties_AbilityEffect
    {
        /// <summary>Windup plus stance, the longest she waits for a hit.</summary>
        public int durationTicks = 300;
        public int windupTicks = 30;
        public int shatterTicks = 30;
        public int cutDelayTicks = 12;
        public int cutTicks = 24;
        public int reformDelayTicks = 30;
        public int recoverTicks = 30;
        /// <summary>Last part of the recovery in which the weapon comes back.</summary>
        public int weaponBackTicks = 9;
        /// <summary>No hit: the water drains and the weapon returns over this long.</summary>
        public int exitTicks = 30;

        public SweepDamageDef sweepDamage;
        /// <summary>Wound and combat log name of the cut.</summary>
        public DamageDef cutDamageDef;
        public float damage = 96f;
        public float armorPenetration = 1.25f;
        /// <summary>Damage reach: the whole disc, near targets included (the trail is drawn as its outer band).</summary>
        public float cutRadius = 4.5f;
        /// <summary>Aim-relative yaw the trail starts at (+ = right); −135 = back-left, the ring's low side.</summary>
        public float cutStartYaw = -135f;
        /// <summary>Posture each struck pawn takes.</summary>
        public float posture = 15f;
        /// <summary>Sword pressure when the cut strikes anything.</summary>
        public float swordPressureGain = 1f;

        public SoundDef stanceSound;
        public SoundDef slashSound;
        /// <summary>The mirror breaking, at the moment she is hit.</summary>
        public SoundDef shatterSound;
        /// <summary>Water, body, reflection and trail; null = the defaults (the preview's constants).</summary>
        public Vfx.MirrorVisualProps visuals;

        public CompProperties_AbilityIllusoryReflection()
        {
            compClass = typeof(CompAbilityEffect_IllusoryReflection);
        }
    }

    public class CompAbilityEffect_IllusoryReflection : CompAbilityEffect
    {
        private static readonly Color PreviewColor = new(0.72f, 0.9f, 1f, 0.7f);

        public new CompProperties_AbilityIllusoryReflection Props => (CompProperties_AbilityIllusoryReflection)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            return caster != null
                && target.IsValid
                && target.Cell != caster.Position
                && base.Valid(target, throwMessages);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned)
            {
                return;
            }
            GenDraw.DrawRadiusRing(caster.Position, Props.cutRadius, PreviewColor);
        }

        /// <summary>
        /// Only reached when something activates the ability directly (the player's cast runs the
        /// job straight from the ability's jobDef): the cooldown vanilla just started is dropped,
        /// it starts when the stance ends.
        /// </summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            if (caster?.jobs == null
                || MX_QHDefOf.MX_QH_IllusoryReflection == null
                || !target.IsValid
                || target.Cell == caster.Position)
            {
                return;
            }

            parent.ResetCooldown();
            caster.rotationTracker?.FaceCell(target.Cell);
            Job stanceJob = JobMaker.MakeJob(MX_QHDefOf.MX_QH_IllusoryReflection);
            stanceJob.ability = parent;
            stanceJob.verbToUse = parent.verb;
            stanceJob.SetTarget(TargetIndex.A, target.Cell);
            stanceJob.playerForced = true;
            caster.jobs.StartJob(
                stanceJob,
                JobCondition.InterruptForced,
                cancelBusyStances: false,
                tag: JobTag.Misc);
        }
    }
}
