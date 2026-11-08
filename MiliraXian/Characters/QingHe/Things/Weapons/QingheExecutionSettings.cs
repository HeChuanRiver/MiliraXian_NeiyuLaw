using System.Collections.Generic;
using MiliraXian.Characters.Common.Combat;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    public sealed class Hediff_QingheExecutionSettings : HediffWithComps
    {
        public ExecutionMode mode = ExecutionMode.Auto;
        public bool nonLethal;

        public override bool Visible => false;

        internal static Hediff_QingheExecutionSettings Get(Pawn pawn) =>
            pawn.health.hediffSet.GetFirstHediffOfDef(Defs.MX_QHDefOf.MX_QH_ExecutionSettings) as Hediff_QingheExecutionSettings;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }
            yield return new Command_Action
            {
                defaultLabel = ("MX_QH_ExecutionMode_" + mode).Translate(),
                defaultDesc = "MX_QH_ExecutionModeDesc".Translate(),
                icon = mode == ExecutionMode.None ? TexCommand.CannotShoot : TexCommand.AttackMelee,
                action = () => mode = mode switch
                {
                    ExecutionMode.Auto => ExecutionMode.Manual,
                    ExecutionMode.Manual => ExecutionMode.None,
                    _ => ExecutionMode.Auto,
                },
            };
            yield return new Command_Toggle
            {
                defaultLabel = "MX_QH_NonLethalExecutionLabel".Translate(),
                defaultDesc = "MX_QH_NonLethalExecutionDesc".Translate(),
                icon = Vfx.MX_QHRenderStatics.NonLethalExecutionIcon,
                isActive = () => nonLethal,
                toggleAction = () => nonLethal = !nonLethal,
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mode, "mode", ExecutionMode.Auto);
            Scribe_Values.Look(ref nonLethal, "nonLethal");
        }
    }
}
