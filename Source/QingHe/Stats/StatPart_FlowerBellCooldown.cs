using MiliraXian.Characters.QingHe.Verbs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Stats
{
    public class StatPart_FlowerBellCooldown : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            FlowerBellMandateVerbProperties settings = CurrentSettings(req);
            if (settings != null && settings.cooldownTime >= 0f)
            {
                val = settings.cooldownTime;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            FlowerBellMandateVerbProperties settings = CurrentSettings(req);
            if (settings == null || settings.cooldownTime < 0f)
            {
                return null;
            }

            return "当前飞花令: " + settings.cooldownTime.ToString("0.##") + "s";
        }

        private static FlowerBellMandateVerbProperties CurrentSettings(StatRequest req)
        {
            ThingWithComps equipment = req.Thing as ThingWithComps;
            if (equipment?.def != MX_QHDefOf.MX_QH_Weapon_FlowerBell)
            {
                return null;
            }

            Verb_ShootFlowerBell verb = equipment.TryGetComp<CompEquippable>()?.PrimaryVerb as Verb_ShootFlowerBell;
            return verb?.CurrentSettings();
        }
    }
}
