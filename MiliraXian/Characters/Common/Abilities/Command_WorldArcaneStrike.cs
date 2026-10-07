using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Common.Abilities
{
    public sealed class Command_WorldArcaneStrike : Command_Ability
    {
        public Command_WorldArcaneStrike(Ability ability, Pawn pawn) : base(ability, pawn)
        {
        }

        protected override void DisabledCheck()
        {
            base.DisabledCheck();
            if (!disabled && !WorldArcaneStrikeTargeter.CasterCanStart(ability, out string reason))
            {
                DisableWithReason(reason);
            }
        }

        public override void ProcessInput(Event ev)
        {
            DisabledCheck();
            if (disabled)
            {
                return;
            }

            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            new WorldArcaneStrikeTargeter(ability).BeginTargeting();
        }

        public override void GizmoUpdateOnMouseover()
        {
            // The impact radius belongs to the world preview, not a local-map ring.
            ability.OnGizmoUpdate();
        }
    }
}
