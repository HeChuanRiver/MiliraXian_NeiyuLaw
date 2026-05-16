using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.UI
{
    public static class FlowerMandateGizmoFactory
    {
        public static Command_Action BuildDisabledFlowerMandateCommand(HediffComp_SeasonResonance seasonResonance)
        {
            var props = seasonResonance?.Props;
            Command_Action command = new Command_Action
            {
                defaultLabel = props?.defaultFlowerMandateLabel ?? "飞花令",
                defaultDesc = props?.defaultFlowerMandateDesc ?? "清荷尚未调谐四时共鸣，暂时无法回应花神。",
                icon = TexCommand.Attack,
                action = delegate
                {
                }
            };

            command.Disable(props?.defaultFlowerMandateDisabledReason ?? "尚未调谐四时共鸣。");
            return command;
        }
    }
}
