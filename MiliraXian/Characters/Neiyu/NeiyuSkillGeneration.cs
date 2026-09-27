using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    [HarmonyPatch(typeof(PawnGenerator), "GenerateSkills")]
    internal static class NeiyuSkillGeneration
    {
        private static void Postfix(Pawn pawn)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || pawn.skills == null)
                return;

            foreach (SkillRecord skill in pawn.skills.skills)
            {
                int level;
                if (skill.def == SkillDefOf.Shooting || skill.def == SkillDefOf.Melee)
                    level = 4;
                else if (skill.def == SkillDefOf.Construction || skill.def == SkillDefOf.Plants
                    || skill.def == SkillDefOf.Crafting || skill.def == SkillDefOf.Artistic
                    || skill.def == SkillDefOf.Medicine || skill.def == SkillDefOf.Social
                    || skill.def == SkillDefOf.Intellectual)
                    level = 2;
                else
                    continue;

                // Add backstory gains to the fixed base without vanilla age/random multipliers.
                if (pawn.story != null)
                {
                    foreach (BackstoryDef backstory in pawn.story.AllBackstories)
                    {
                        if (backstory?.skillGains == null)
                            continue;
                        foreach (SkillGain gain in backstory.skillGains)
                            if (gain.skill == skill.def)
                                level += gain.amount;
                    }
                }

                // Keep trait gains; genes and other aptitudes are added by SkillRecord.GetLevel.
                if (pawn.story?.traits != null)
                {
                    foreach (Trait trait in pawn.story.traits.allTraits)
                    {
                        if (trait.Suppressed || trait.CurrentData.skillGains == null)
                            continue;
                        foreach (SkillGain gain in trait.CurrentData.skillGains)
                            if (gain.skill == skill.def)
                                level += gain.amount;
                    }
                }

                // Generation only: never reset learned levels on spawn or save/load.
                skill.Level = level;
            }
        }
    }
}
