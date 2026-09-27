using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class CompProperties_PawnKindEquipmentDurability : CompProperties
    {
        public List<PawnKindDef> protectedPawnKinds = new();

        public CompProperties_PawnKindEquipmentDurability()
        {
            compClass = typeof(CompPawnKindEquipmentDurability);
        }
    }

    public class CompPawnKindEquipmentDurability : ThingComp
    {
        public CompProperties_PawnKindEquipmentDurability Props => (CompProperties_PawnKindEquipmentDurability)props;

        public bool PreventsDurabilityLoss
        {
            get
            {
                // Stored or dropped equipment stays protected; only an unlisted wearer permits damage.
                Pawn wearer = parent.ParentHolder switch
                {
                    Pawn_ApparelTracker apparel => apparel.pawn,
                    Pawn_EquipmentTracker equipment => equipment.pawn,
                    _ => null,
                };
                return wearer == null || Props.protectedPawnKinds.Contains(wearer.kindDef);
            }
        }
    }
}
