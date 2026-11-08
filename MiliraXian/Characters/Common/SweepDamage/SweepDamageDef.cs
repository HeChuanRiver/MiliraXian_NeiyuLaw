using System;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common.SweepDamage
{
    public enum SweepTargetFaction
    {
        /// <summary>Only things hostile to the caster.</summary>
        Hostile,
        /// <summary>Everything except the caster's own faction and its allies.</summary>
        HostileAndNeutral,
        /// <summary>Everything except the caster (friendly fire allowed).</summary>
        All,
    }

    /// <summary>
    /// The rules of one kind of sweeping damage area: who it may hit and how a contact turns into
    /// damage. Geometry and amounts come with each <see cref="SweepDamageSource"/>.
    /// </summary>
    public class SweepDamageDef : Def
    {
        public DamageDef damageDef;
        public SweepTargetFaction targetFaction = SweepTargetFaction.Hostile;
        public bool hitPawns = true;
        public bool hitBuildings = true;
        /// <summary>Blocked when the caster has no line of sight to the target.</summary>
        public bool requireLineOfSight = true;
        /// <summary>Downed pawns are left out unless the mod setting lets them be hit.</summary>
        public bool skipDowned;
        /// <summary>Contact is decided on the tick the sweep reaches the target; damage lands this many ticks later.</summary>
        public int damageDelayTicks;
        /// <summary>Applies the damage of one contact. Subclass it for character-specific hit effects.</summary>
        public Type workerClass = typeof(SweepDamageWorker);

        [Unsaved]
        private SweepDamageWorker workerInt;

        public SweepDamageWorker Worker
        {
            get
            {
                if (workerInt == null)
                {
                    workerInt = (SweepDamageWorker)Activator.CreateInstance(workerClass ?? typeof(SweepDamageWorker));
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        public override System.Collections.Generic.IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (workerClass != null && !typeof(SweepDamageWorker).IsAssignableFrom(workerClass))
            {
                yield return $"workerClass {workerClass} does not derive from {nameof(SweepDamageWorker)}";
            }
        }
    }
}
