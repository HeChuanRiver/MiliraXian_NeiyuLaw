using RimWorld;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public static class MX_QH_HediffUtility
    {
        public static HediffComp_SkillTreeState EnsureFlowerResonance(Pawn pawn)
        {
            return EnsureHediffComp<HediffComp_SkillTreeState>(pawn, MX_QHDefOf.MX_QH_FlowerResonance);
        }

        public static HediffComp_SkillTreeState GetFlowerResonance(Pawn pawn)
        {
            return GetHediffComp<HediffComp_SkillTreeState>(pawn, MX_QHDefOf.MX_QH_FlowerResonance);
        }

        public static HediffComp_FlowerDecree EnsureFlowerDecree(Pawn pawn)
        {
            return PawnSpecialResourceUtility.EnsureSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
        }

        public static HediffComp_FlowerDecree GetFlowerDecree(Pawn pawn)
        {
            return PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
        }

        public static HediffComp_MeditativeStillness EnsureMeditativeStillness(Pawn pawn)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn))
            {
                return null;
            }

            return EnsureHediffComp<HediffComp_MeditativeStillness>(pawn, MX_QHDefOf.MX_QH_MeditativeStillness);
        }

        public static void AddMeditativeStillnessFromLotusPond(Pawn pawn, Building lotusPond)
        {
            HediffComp_MeditativeStillness stillness = EnsureMeditativeStillness(pawn);
            if (stillness == null || lotusPond == null)
            {
                return;
            }

            float gain = stillness.PropsStillness.baseGainPerDay / 60000f * ResolveLotusPondRoomStillnessFactor(lotusPond);
            stillness.AddStillness(gain);
        }

        public static void AddMeditativeStillnessFromReading(Pawn pawn, int delta, float roomBonusFactor)
        {
            HediffComp_MeditativeStillness stillness = EnsureMeditativeStillness(pawn);
            if (stillness == null || delta <= 0)
            {
                return;
            }

            float gain = stillness.PropsStillness.baseGainPerDay / 60000f * delta * Mathf.Max(0.1f, roomBonusFactor);
            stillness.AddStillness(gain);
        }

        public static void ApplyMeditativeStillnessQualityBonus(Pawn pawn, ref QualityCategory quality)
        {
            HediffComp_MeditativeStillness stillness = GetHediffComp<HediffComp_MeditativeStillness>(pawn, MX_QHDefOf.MX_QH_MeditativeStillness);
            int bonusLevels = stillness?.ConsumeForQualityBonus() ?? 0;
            if (bonusLevels > 0)
            {
                quality = (QualityCategory)Mathf.Clamp((int)quality + bonusLevels, 0, 6);
            }
        }

        public static HediffComp_DivineFortune GetDivineFortune(Pawn pawn)
        {
            return GetHediffComp<HediffComp_DivineFortune>(pawn, MX_QHDefOf.MX_QH_DivineFortune);
        }

        public static void SyncDivineGrace(Pawn pawn, HediffComp_SkillTreeState state = null)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn) || pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_DivineGrace == null)
            {
                return;
            }

            SkillNodeDef node = MX_QHSkillNodeDefOf.MX_QH_Node_DivineGrace;
            int level = (state ?? GetFlowerResonance(pawn))?.GetNodeLevel(node) ?? 0;
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_DivineGrace);
            if (level <= 0)
            {
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                }
                return;
            }

            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_DivineGrace, pawn);
                pawn.health.AddHediff(hediff);
            }

            (hediff as HediffWithComps)?.GetComp<HediffComp_DivineGrace>()?.SetLevel(level, node?.MaxLevel ?? 24);
        }

        public static void EnsureCoreHediffs(Pawn pawn)
        {
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_DivineBlessing);
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_DivineProtection);
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_DivineFortune);
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_Trickle);
            EnsureFlowerResonance(pawn);
            EnsureFlowerDecree(pawn);
            EnsureMeditativeStillness(pawn);
            SyncDivineGrace(pawn);

            GetHediffComp<HediffComp_DivineProtection>(pawn, MX_QHDefOf.MX_QH_DivineProtection)?.EnsureShieldBound();
        }

        private static T EnsureHediffComp<T>(Pawn pawn, HediffDef hediffDef) where T : HediffComp
        {
            Hediff hediff = EnsureHediff(pawn, hediffDef);
            return (hediff as HediffWithComps)?.GetComp<T>();
        }

        private static T GetHediffComp<T>(Pawn pawn, HediffDef hediffDef) where T : HediffComp
        {
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            return (hediff as HediffWithComps)?.GetComp<T>();
        }

        private static Hediff EnsureHediff(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                pawn.health.AddHediff(hediff);
            }

            return hediff;
        }

        private static float ResolveLotusPondRoomStillnessFactor(Building lotusPond)
        {
            Room room = lotusPond.GetRoom();
            if (room == null)
            {
                return 1f;
            }

            float beauty = room.GetStat(RoomStatDefOf.Beauty);
            float cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
            return Mathf.Lerp(0.75f, 1.5f, Mathf.InverseLerp(-5f, 20f, beauty))
                * Mathf.Lerp(0.75f, 1.25f, Mathf.InverseLerp(-2f, 1f, cleanliness));
        }
    }
}


