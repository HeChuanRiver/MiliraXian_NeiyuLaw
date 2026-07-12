using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    public class QingheSlashExtension : DefModExtension
    {
        public bool canGainSwordPressure = true;
        public float swordPressureGain = 25f;
        public float resonanceAccumulationMultiplier = 1f;
    }

    public class DamageWorker_QingheSlash : DamageWorker_AddInjury
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            DamageResult result = base.Apply(dinfo, thing);
            Pawn caster = dinfo.Instigator as Pawn;
            if (caster == null || result.totalDamageDealt <= 0f || !MX_QHCharacterUtility.IsQinghe(caster))
            {
                return result;
            }

            QingheSlashExtension extension = def.GetModExtension<QingheSlashExtension>();
            QingheSwordCombatUtility.NotifySlashHit(caster, thing, extension);
            return result;
        }
    }

    public static class QingheSwordCombatUtility
    {
        public static bool IsSwordMode(Pawn pawn)
        {
            return pawn?.equipment?.Primary?.def == MX_QHDefOf.MX_QH_Weapon_Sword;
        }

        public static bool IsBellMode(Pawn pawn)
        {
            return pawn?.equipment?.Primary?.def == MX_QHDefOf.MX_QH_Weapon_FlowerBell;
        }

        public static FlowerBellResonance ResonanceFor(Pawn pawn)
        {
            return MX_QH_HediffUtility.EnsureCombatState(pawn)?.Resonance ?? FlowerBellResonance.Spring;
        }

        public static void NotifySlashHit(Pawn caster, Thing target, QingheSlashExtension extension)
        {
            if (caster == null || target == null || !GenHostility.HostileTo(caster, target))
            {
                return;
            }

            FlowerBellResonance resonance = ResonanceFor(caster);
            bool canGain = extension?.canGainSwordPressure ?? true;
            if (canGain)
            {
                float gain = Mathf.Max(0f, extension?.swordPressureGain ?? 25f);
                if (resonance == FlowerBellResonance.Summer)
                {
                    gain *= 1.5f;
                }
                MX_QH_HediffUtility.EnsureSwordPressure(caster)?.AddProgress(gain);
            }

            ApplyResonanceEffect(caster, target as Pawn, resonance, Mathf.Max(0f, extension?.resonanceAccumulationMultiplier ?? 1f));
        }

        public static void ApplyResonanceEffect(Pawn caster, Pawn target, FlowerBellResonance resonance, float accumulationMultiplier)
        {
            if (caster == null)
            {
                return;
            }

            if (resonance == FlowerBellResonance.Spring)
            {
                ApplySpringBlessing(caster);
            }
            else if (resonance == FlowerBellResonance.Winter)
            {
                caster.GetComp<CompDivineProtectionShield>()?.RestoreEnergy(8f);
            }

            ThingDef projectile = ProjectileFor(resonance);
            CompProperties_FlowerBellStatusOnHit props = CompFlowerBellStatusOnHit.PropsFor(projectile);
            CompFlowerBellStatusOnHit.ApplyAbnormals(caster, target, props, accumulationMultiplier);
        }

        public static ThingDef ProjectileFor(FlowerBellResonance resonance)
        {
            switch (resonance)
            {
                case FlowerBellResonance.Summer:
                    return MX_QHDefOf.MX_Bullet_Qinghe_FlowerBell_Summer;
                case FlowerBellResonance.Autumn:
                    return MX_QHDefOf.MX_Bullet_Qinghe_FlowerBell_Autumn;
                case FlowerBellResonance.Winter:
                    return MX_QHDefOf.MX_Bullet_Qinghe_FlowerBell_Winter;
                default:
                    return MX_QHDefOf.MX_Bullet_Qinghe_FlowerBell_Spring;
            }
        }

        public static void ApplySlash(Pawn caster, Thing target, float damage, float armorPenetration, bool empowered)
        {
            if (caster == null || target == null || target.Destroyed)
            {
                return;
            }

            if (ResonanceFor(caster) == FlowerBellResonance.Autumn)
            {
                armorPenetration *= 1.5f;
            }

            DamageDef damageDef = empowered ? MX_QHDefOf.MX_QH_SlashSkill : MX_QHDefOf.MX_QH_Slash;
            target.TakeDamage(new DamageInfo(damageDef ?? DamageDefOf.Cut, damage, armorPenetration, -1f, caster, null, MX_QHDefOf.MX_QH_Weapon_Sword));
        }

        public static int ApplyCone(Pawn caster, IntVec3 center, IntVec3 targetCell, float radius, float angleDegrees, float damage, float armorPenetration, bool empowered)
        {
            Map map = caster?.MapHeld;
            if (map == null)
            {
                return 0;
            }

            HashSet<Thing> hit = new HashSet<Thing>();
            List<IntVec3> cells = new List<IntVec3>();
            FillConeCells(caster, center, targetCell, radius, angleDegrees, cells);
            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                IntVec3 cell = cells[cellIndex];
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing != caster && thing != null && thing.Spawned && GenHostility.HostileTo(caster, thing) && hit.Add(thing))
                    {
                        ApplySlash(caster, thing, damage, armorPenetration, empowered);
                    }
                }
            }

            return hit.Count;
        }

        public static void FillConeCells(Pawn caster, IntVec3 center, IntVec3 targetCell, float radius, float angleDegrees, List<IntVec3> cells)
        {
            cells?.Clear();
            Map map = caster?.MapHeld;
            if (map == null || cells == null)
            {
                return;
            }

            Vector3 forward = (targetCell - center).ToVector3().Yto0();
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = caster.Rotation.FacingCell.ToVector3();
            }
            forward.Normalize();

            float halfAngle = Mathf.Clamp(angleDegrees, 1f, 360f) * 0.5f;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Mathf.Max(0f, radius), true))
            {
                if (!cell.InBounds(map) || cell == center)
                {
                    continue;
                }

                Vector3 direction = (cell - center).ToVector3().Yto0();
                if (direction.sqrMagnitude >= 0.001f && Vector3.Angle(forward, direction.normalized) <= halfAngle)
                {
                    cells.Add(cell);
                }
            }
        }

        public static int ApplyRadius(Pawn caster, IntVec3 center, float radius, float damage, float armorPenetration, bool empowered, List<Thing> hitTargets = null)
        {
            Map map = caster?.MapHeld;
            if (map == null)
            {
                hitTargets?.Clear();
                return 0;
            }

            hitTargets?.Clear();
            HashSet<Thing> hit = new HashSet<Thing>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Mathf.Max(0f, radius), true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing != caster && thing != null && thing.Spawned && GenHostility.HostileTo(caster, thing) && hit.Add(thing))
                    {
                        hitTargets?.Add(thing);
                        ApplySlash(caster, thing, damage, armorPenetration, empowered);
                    }
                }
            }

            return hit.Count;
        }

        private static void ApplySpringBlessing(Pawn caster)
        {
            if (caster.health?.hediffSet == null || MX_QHDefOf.MX_QH_SpringFlow == null)
            {
                return;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_SpringFlow);
            if (hediff == null)
            {
                hediff = caster.health.AddHediff(MX_QHDefOf.MX_QH_SpringFlow);
            }
            hediff?.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
        }
    }

    public class StatPart_QingheSwordModeDodge : StatPart
    {
        public float rawOffset = 30f;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.HasThing && req.Thing is Pawn pawn && QingheSwordCombatUtility.IsSwordMode(pawn))
            {
                val += rawOffset;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.HasThing && req.Thing is Pawn pawn && QingheSwordCombatUtility.IsSwordMode(pawn))
            {
                return "MX_QH_SwordModeDodgeExplanation".Translate().ToString();
            }
            return null;
        }
    }
}
