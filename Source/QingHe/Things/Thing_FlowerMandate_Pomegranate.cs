using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Things
{
    [StaticConstructorOnStartup]
    public class Thing_FlowerMandate_Pomegranate : Building, IAttackTarget
    {
        private const string OverlayTexPath = "Things/Mote/PsycastCast";
        private static readonly int DistortionTex = Shader.PropertyToID("_DistortionTex");
        private static readonly int ScrollSpeed = Shader.PropertyToID("_ScrollSpeed");
        private static readonly Material OverlayMaterial = CreateOverlayMaterial();

        Thing IAttackTarget.Thing => this;

        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

        public float TargetPriorityFactor => 1f;

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor)
        {
            CompTurretGun turretGun = this.TryGetComp<CompTurretGun>();
            return Destroyed || !Spawned || turretGun == null || turretGun.AttackVerb == null || !turretGun.AttackVerb.Available();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            DrawRealtimeOverlay(drawLoc);
        }

        private void DrawRealtimeOverlay(Vector3 drawLoc)
        {
            if (OverlayMaterial == null)
            {
                return;
            }

            float alpha = this.TryGetComp<CompFlowerMandate_PomegranateLifetime>()?.VisualAlpha ?? 1f;
            if (alpha <= 0.01f)
            {
                return;
            }

            Color color = new Color(1f, 0.55f, 0.78f, 0.42f * alpha);
            OverlayMaterial.color = color;
            Vector3 position = drawLoc;
            position.y = AltitudeLayer.MoteLow.AltitudeFor() + 0.05f;
            Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(2f, 1f, 2f));
            Graphics.DrawMesh(MeshPool.plane10, matrix, OverlayMaterial, 0);
        }

        private static Material CreateOverlayMaterial()
        {
            Texture2D texture = ContentFinder<Texture2D>.Get(OverlayTexPath, reportFailure: false);
            if (texture == null)
            {
                return null;
            }

            Material material = new Material(ShaderDatabase.MoteGlowDistorted)
            {
                mainTexture = texture,
                color = new Color(1f, 0.55f, 0.78f, 0.42f),
                renderQueue = 3600
            };

            Texture2D distortionTexture = ContentFinder<Texture2D>.Get("Other/Ripples", reportFailure: false);
            if (distortionTexture != null)
            {
                material.SetTexture(DistortionTex, distortionTexture);
            }

            material.SetFloat(ScrollSpeed, -0.25f);
            return material;
        }
    }

    public class Thing_FlowerMandate_PomegranateGun : ThingWithComps
    {
    }
}
