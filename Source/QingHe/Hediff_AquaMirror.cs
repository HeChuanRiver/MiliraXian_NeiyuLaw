using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class Hediff_AquaMirror : HediffWithComps
    {
        public override string Label
        {
            get
            {
                string baseLabel = base.Label;
                var aquaComp = this.TryGetComp<HediffComp_AquaMirror>();
                if (aquaComp?.shieldInspected != null)
                {
                    float current = aquaComp.shieldInspected.Energy;
                    float max = aquaComp.shieldInspected.Props.startingEnergy;
                    return $"{baseLabel} ({current:F0}/{max:F0})";
                }
                return baseLabel;
            }
        }
    }
}
