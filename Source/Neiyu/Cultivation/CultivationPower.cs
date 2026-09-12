using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    internal static class CultivationPower
    {
        // Defs remain immutable per-pawn ceilings managed by the existing power settings.
        // All reductions are evaluated for the actual owner; shared Defs never store progress.
        private static readonly Dictionary<Tool, Vector2> Tools = new();
        private static readonly Dictionary<ProjectileProperties, Vector2> Projectiles = new();
        private static readonly HashSet<StatDef> InstalledParts = new();
        private static readonly AccessTools.FieldRef<ProjectileProperties, float> ProjectilePenetration =
            AccessTools.FieldRefAccess<ProjectileProperties, float>("armorPenetrationBase");
        internal static readonly Dictionary<ThingDef, Dictionary<StatDef, float>> StatFloors = new();
        internal static readonly Dictionary<ThingDef, HashSet<StatDef>> OffsetStats = new();

        internal static void RegisterTool(Tool tool, float damage, float penetration)
        {
            if (tool != null) Tools[tool] = new Vector2(damage, penetration);
        }

        internal static void RegisterProjectile(ProjectileProperties projectile, float damage, float penetration)
        {
            if (projectile != null) Projectiles[projectile] = new Vector2(damage, penetration);
        }

        internal static void RegisterStat(ThingDef def, StatDef stat, float floor, bool offset)
        {
            if (def == null || stat == null) return;
            if (!StatFloors.TryGetValue(def, out var floors)) StatFloors[def] = floors = new();
            floors[stat] = floor;
            if (offset)
            {
                if (!OffsetStats.TryGetValue(def, out var stats)) OffsetStats[def] = stats = new();
                stats.Add(stat);
            }
            if (InstalledParts.Add(stat))
            {
                stat.parts ??= new();
                // Apply before quality/other parts so they still scale the effective base.
                stat.parts.Insert(0, new StatPart_NeiyuCultivation { parentStat = stat });
                if (stat.immutable)
                {
                    stat.immutable = false;
                    stat.Worker.SetCacheability(false);
                }
            }
        }

        public static void InvalidateStats(Pawn pawn)
        {
            // One cold-path invalidation per successful unlock, including derived DPS
            // stats. Required because the window pauses ticks and cached gear values
            // would otherwise remain stale until time resumes.
            foreach (StatDef stat in DefDatabase<StatDef>.AllDefsListForReading)
            {
                stat.Worker.ClearCacheForThing(pawn);
                if (pawn.equipment?.Primary != null) stat.Worker.ClearCacheForThing(pawn.equipment.Primary);
                if (pawn.apparel != null)
                    foreach (Apparel apparel in pawn.apparel.WornApparel) stat.Worker.ClearCacheForThing(apparel);
            }
        }

        public static Pawn Owner(Thing thing) => thing is Apparel apparel ? apparel.Wearer :
            MXNeiyuShieldUtility.TryGetEquipmentOwnerPawn(thing);

        public static float Effective(float ceiling, float floor, float fraction) =>
            floor + (ceiling - floor) * Mathf.Clamp01(fraction);

        public static float MeleeFactor(Tool tool, Pawn pawn, bool penetration)
        {
            if (tool == null || !Tools.TryGetValue(tool, out Vector2 floors)) return 1f;
            float ceiling = penetration ? tool.armorPenetration : tool.power;
            float floor = penetration ? floors.y : floors.x;
            return ceiling <= 0f ? 1f : Effective(ceiling, floor,
                CultivationService.Fraction(pawn, CultivationBranch.Wing)) / ceiling;
        }

        public static void AdjustProjectileDamage(ProjectileProperties projectile, Thing weapon, ref int result)
        {
            if (!Projectiles.TryGetValue(projectile, out Vector2 floors)) return;
            // Unmultiplied overload avoids recursion into the existing shield postfix.
            float ceiling = projectile.GetDamageAmount(1f, null);
            if (ceiling <= 0f) return;
            float effective = Effective(ceiling, floors.x, CultivationService.Fraction(Owner(weapon), CultivationBranch.Wing));
            result = Mathf.Max(1, Mathf.RoundToInt(result * effective / ceiling));
        }

        public static void AdjustProjectilePenetration(ProjectileProperties projectile, Thing weapon, ref float result)
        {
            if (projectile.damageDef?.armorCategory == null || !Projectiles.TryGetValue(projectile, out Vector2 floors)) return;
            float multiplier = weapon?.GetStatValue(StatDefOf.RangedWeapon_ArmorPenetrationMultiplier) ?? 1f;
            float fraction = CultivationService.Fraction(Owner(weapon), CultivationBranch.Wing);
            result = Effective(result, floors.y * multiplier, fraction);
        }

        internal static float ProjectilePenetrationCeiling(ProjectileProperties projectile) => ProjectilePenetration(projectile);

        public static int SplitCount(Pawn pawn, int ceiling) => ArrowCount(CultivationService.Rank(pawn, CultivationBranch.Arrow), ceiling, 1, 4, 8);
        public static int BarrageCount(Pawn pawn, int ceiling) => ArrowCount(CultivationService.Rank(pawn, CultivationBranch.Arrow), ceiling, 4, 24, 54);
        public static int ArrowCount(int rank, int ceiling, int floor, int first, int second) =>
            Math.Max(1, Math.Min(ceiling, rank <= 0 ? floor : rank == 1 ? first : rank == 2 ? second : ceiling));
        public static bool ShieldEnabled(Pawn pawn) => !NeiyuPowerBalance.PassivesDisabled && CultivationService.Rank(pawn, CultivationBranch.Halo) > 0;
        public static bool StageThreeEnabled(Pawn pawn) => !NeiyuPowerBalance.PassivesDisabled && CultivationService.Rank(pawn, CultivationBranch.Halo) >= 3;
        public static int ShieldCapacity(Pawn pawn, int ceiling) => ShieldCapacity(CultivationService.Rank(pawn, CultivationBranch.Halo), ceiling);
        public static int ShieldCapacity(int rank, int ceiling) => Math.Max(0, Math.Min(ceiling, rank <= 0 ? 0 : rank == 1 ? 6 : rank == 2 ? 18 : ceiling));
    }

    public sealed class StatPart_NeiyuCultivation : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Def is not ThingDef def || !CultivationPower.StatFloors.TryGetValue(def, out var stats)
                || !stats.TryGetValue(parentStat, out float floor)) return;
            Pawn owner = req.HasThing ? CultivationPower.Owner(req.Thing) : null;
            CultivationBranch branch = def.IsWeapon ? CultivationBranch.Wing : CultivationBranch.Law;
            // For equipped offsets, preserve other mods' contributions and change only our Def offset.
            bool offset = CultivationPower.OffsetStats.TryGetValue(def, out var offsets) && offsets.Contains(parentStat);
            float ceiling = (offset ? def.equippedStatOffsets : def.statBases).GetStatOffsetFromList(parentStat);
            val += CultivationPower.Effective(ceiling, floor, CultivationService.Fraction(owner, branch)) - ceiling;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.Def is not ThingDef def || !CultivationPower.StatFloors.TryGetValue(def, out var stats)
                || !stats.ContainsKey(parentStat)) return null;
            float delta = 0f;
            TransformValue(req, ref delta);
            return CultivationText.Title + ": " + delta.ToStringByStyle(parentStat.ToStringStyleUnfinalized, ToStringNumberSense.Offset);
        }
    }

    [HarmonyPatch(typeof(ProjectileProperties), nameof(ProjectileProperties.GetArmorPenetration), typeof(Thing), typeof(StringBuilder))]
    internal static class Patch_NeiyuCultivation_ProjectilePenetration
    {
        private static void Postfix(ProjectileProperties __instance, Thing weapon, ref float __result) =>
            CultivationPower.AdjustProjectilePenetration(__instance, weapon, ref __result);
    }
}
