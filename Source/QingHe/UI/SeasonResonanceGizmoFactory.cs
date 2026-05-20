using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.UI
{
    public static class SeasonResonanceGizmoFactory
    {
        public static IEnumerable<Gizmo> BuildDevCommands(HediffComp_SeasonResonance resonance)
        {
            if (!Prefs.DevMode || resonance?.Pawn == null)
            {
                yield break;
            }

            yield return BuildSeasonMenuCommand(resonance);
            yield return BuildResourceMenuCommand(resonance);
            yield return BuildAttunementMenuCommand(resonance);
        }

        private static Command_Action BuildResourceMenuCommand(HediffComp_SeasonResonance resonance)
        {
            return new Command_Action
            {
                defaultLabel = "DEV: 资源调试",
                defaultDesc = "开发者测试用：调整清荷的花令与花令恢复进度。",
                action = delegate
                {
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        BuildResourceOption("花令 +1", delegate { PawnSpecialResourceUtility.AddResource(resonance.Pawn, MX_QHDefOf.MX_QH_FlowerDecree, 1f); }),
                        BuildResourceOption("花令 -1", delegate { PawnSpecialResourceUtility.TryConsumeResource(resonance.Pawn, MX_QHDefOf.MX_QH_FlowerDecree, 1f); }),
                        BuildResourceOption("花令进度 +25", delegate { FlowerCourtUtility.AddFlowerDecreeRecoveryProgress(resonance.Pawn, 25f); }),
                        BuildResourceOption("花令进度 -25", delegate { FlowerCourtUtility.AddFlowerDecreeRecoveryProgress(resonance.Pawn, -25f); })
                    }));
                }
            };
        }

        private static Command_Action BuildAttunementMenuCommand(HediffComp_SeasonResonance resonance)
        {
            return new Command_Action
            {
                defaultLabel = "DEV: 调谐度",
                defaultDesc = "开发者测试用：调整四季调谐度。",
                action = delegate
                {
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        BuildAttunementOption(resonance, AttunedSeason.Spring, 10f),
                        BuildAttunementOption(resonance, AttunedSeason.Spring, -10f),
                        BuildAttunementOption(resonance, AttunedSeason.Summer, 10f),
                        BuildAttunementOption(resonance, AttunedSeason.Summer, -10f),
                        BuildAttunementOption(resonance, AttunedSeason.Autumn, 10f),
                        BuildAttunementOption(resonance, AttunedSeason.Autumn, -10f),
                        BuildAttunementOption(resonance, AttunedSeason.Winter, 10f),
                        BuildAttunementOption(resonance, AttunedSeason.Winter, -10f)
                    }));
                }
            };
        }

        private static FloatMenuOption BuildResourceOption(string label, System.Action action)
        {
            return new FloatMenuOption(label, action);
        }

        private static FloatMenuOption BuildAttunementOption(HediffComp_SeasonResonance resonance, AttunedSeason season, float delta)
        {
            string sign = delta > 0f ? "+" : string.Empty;
            return new FloatMenuOption(GetSeasonLabel(season) + " " + sign + delta.ToString("F0"), delegate
            {
                resonance.AddAttunement(season, delta);
            });
        }

        private static Command_Action BuildSeasonMenuCommand(HediffComp_SeasonResonance resonance)
        {
            return new Command_Action
            {
                defaultLabel = "DEV: 共鸣-" + GetSeasonLabel(resonance.CurrentAttunedSeason),
                defaultDesc = "开发者测试用：切换清荷当前调谐时节。",
                action = delegate
                {
                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        BuildSeasonOption(resonance, AttunedSeason.None),
                        BuildSeasonOption(resonance, AttunedSeason.Spring),
                        BuildSeasonOption(resonance, AttunedSeason.Summer),
                        BuildSeasonOption(resonance, AttunedSeason.Autumn),
                        BuildSeasonOption(resonance, AttunedSeason.Winter)
                    }));
                }
            };
        }

        private static FloatMenuOption BuildSeasonOption(HediffComp_SeasonResonance resonance, AttunedSeason season)
        {
            string label = GetSeasonLabel(season);
            if (resonance.CurrentAttunedSeason == season)
            {
                label += "（当前）";
            }

            return new FloatMenuOption(label, delegate
            {
                resonance.SetAttunedSeason(season);
            });
        }

        private static string GetSeasonLabel(AttunedSeason season)
        {
            switch (season)
            {
                case AttunedSeason.Spring:
                    return "春";
                case AttunedSeason.Summer:
                    return "夏";
                case AttunedSeason.Autumn:
                    return "秋";
                case AttunedSeason.Winter:
                    return "冬";
                default:
                    return "未调谐";
            }
        }
    }
}
