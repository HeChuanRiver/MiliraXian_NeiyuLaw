using System;
using System.Collections.Generic;
using MiliraXian.Characters.Biography;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    public enum CultivationBranch { Wing, Arrow, Halo, Law }

    // Source-language content lives in Defs; translations can be added after content review.
    public sealed class NeiyuCultivationNodeDef : Def
    {
        public CultivationBranch branch;
        public int rank;
        public NeiyuCultivationNodeDef prerequisite;
        public List<ThingDefCountClass> costs = new();
        public BiographyUnlockCondition condition;
        [MustTranslate] public string story;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (!Enum.IsDefined(typeof(CultivationBranch), branch)) yield return defName + ": invalid branch.";
            if (rank < 1 || rank > 3) yield return defName + ": rank must be 1–3.";
            if (rank > 1 && (prerequisite == null || prerequisite.branch != branch || prerequisite.rank != rank - 1))
                yield return defName + ": prerequisite must be the preceding rank in this branch.";
            if (rank == 1 && prerequisite != null) yield return defName + ": first rank cannot have a prerequisite.";
            var seen = new HashSet<ThingDef>();
            foreach (ThingDefCountClass cost in costs)
                if (cost?.thingDef == null || cost.count <= 0 || !seen.Add(cost.thingDef))
                    yield return defName + ": invalid or duplicate material cost.";
            if (condition != null)
                foreach (string error in condition.ConfigErrors(null, null, defName + ".condition")) yield return error;
            foreach (NeiyuCultivationNodeDef other in DefDatabase<NeiyuCultivationNodeDef>.AllDefsListForReading)
                if (other != this && other.branch == branch && other.rank == rank)
                    yield return defName + ": duplicate branch/rank.";
        }
    }

    internal static class CultivationText
    {
        public static string Get(string id, string source)
        {
            string key = "MX_Cultivation_" + id;
            return key.CanTranslate() ? key.Translate().ToString() : source;
        }

        public static string Title => Get("Title", "修行");
        public static string Branch(CultivationBranch branch) => branch switch
        {
            CultivationBranch.Wing => Get("Wing", "羽 · 兵刃"),
            CultivationBranch.Arrow => Get("Arrow", "霁 · 箭羽"),
            CultivationBranch.Halo => Get("Halo", "环 · 庇护"),
            _ => Get("Law", "律 · 衣装")
        };
    }
}
