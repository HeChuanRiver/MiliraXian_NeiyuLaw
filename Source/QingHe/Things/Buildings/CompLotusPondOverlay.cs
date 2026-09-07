using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Buildings
{
    public class CompProperties_LotusPondOverlay : CompProperties
    {
        public string texPath = "MiliraXianQinghe/Buildings/lotus_pond_flowers";
        public Vector2 drawSize = new(3f, 3f);

        public CompProperties_LotusPondOverlay()
        {
            compClass = typeof(CompLotusPondOverlay);
        }
    }

    public class CompLotusPondOverlay : ThingComp
    {
        private Graphic overlayGraphic;

        public CompProperties_LotusPondOverlay Props => (CompProperties_LotusPondOverlay)props;

        private Graphic OverlayGraphic => overlayGraphic ??= GraphicDatabase.Get<Graphic_Single>(
            Props.texPath,
            ShaderDatabase.Cutout,
            Props.drawSize,
            Color.white);

        public override void PostDraw()
        {
            base.PostDraw();
            Vector3 drawPos = parent.DrawPos;
            drawPos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            OverlayGraphic.Draw(drawPos, Rot4.North, parent);
        }
    }
}
