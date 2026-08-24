using System.Collections.Generic;
using MiliraXian.Characters.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_LunarMirrorShield : CompProperties
    {
        public float radius = 4f;
        public float startingEnergy = 160f;
        public float energyLossPerDamage = 1f;
        public int durationTicks = 900;
        public int fadeInTicks = 30;
        public int fadeOutTicks = 45;
        public bool breakOnEmp = true;
        public bool interceptGroundProjectiles = true;
        public bool interceptAirProjectiles = true;
        public bool interceptOutgoingProjectiles = false;
        public string shieldTexPath = "MiliraXianNeiyu/Effect/Neiyu_Shield/Shield";
        public float shieldDrawSizeFactor = 1.1601562f;
        public float shieldAlpha = 0.34f;
        public Color shieldColor = new(0.70f, 0.90f, 1f, 1f);
        public string absorbFleckDefName = "ExplosionFlash";
        public float absorbFleckScale = 1.0f;
        public float breakEffectScale = 3.6f;
        public float breakFlashScale = 8f;
        public ThingDef enhancedRetaliationProjectileDef;

        public CompProperties_LunarMirrorShield()
        {
            compClass = typeof(CompLunarMirrorShield);
        }
    }

    public class CompLunarMirrorShield : ThingComp
    {
        private static readonly Dictionary<string, Material> ShieldMaterialByPath = new();

        private Pawn caster;
        private Faction casterFaction;
        private float energy;
        private int ticksLeft;
        private int ageTicks;
        private bool enhanced;

        public CompProperties_LunarMirrorShield Props => (CompProperties_LunarMirrorShield)props;

        public bool Active => parent != null && parent.Spawned && energy > 0f && ticksLeft > 0;

        public float VisualAlpha
        {
            get
            {
                float alpha = 1f;
                if (Props.fadeInTicks > 0)
                {
                    alpha = Mathf.Min(alpha, ageTicks / (float)Props.fadeInTicks);
                }
                if (Props.fadeOutTicks > 0)
                {
                    alpha = Mathf.Min(alpha, ticksLeft / (float)Props.fadeOutTicks);
                }
                return Mathf.Clamp01(alpha);
            }
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            energy = Props.startingEnergy;
            ticksLeft = Props.durationTicks;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Destroyed)
            {
                return;
            }

            ageTicks++;
            ticksLeft--;
            if (ticksLeft <= 0 || energy <= 0f)
            {
                parent.Destroy(DestroyMode.Vanish);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster", false);
            Scribe_References.Look(ref casterFaction, "casterFaction", false);
            Scribe_Values.Look(ref energy, "energy", Props.startingEnergy);
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", Props.durationTicks);
            Scribe_Values.Look(ref ageTicks, "ageTicks", 0);
            Scribe_Values.Look(ref enhanced, "enhanced", false);
        }

        public override Color? ForceColor()
        {
            Color color = parent?.def?.graphicData?.color ?? Color.white;
            color.a *= VisualAlpha;
            return color;
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!Active || Props.shieldTexPath.NullOrEmpty())
            {
                return;
            }

            float alpha = Props.shieldAlpha * VisualAlpha;
            if (alpha <= 0.01f)
            {
                return;
            }

            if (!ShieldMaterialByPath.TryGetValue(Props.shieldTexPath, out Material shieldMat))
            {
                shieldMat = MaterialPool.MatFrom(Props.shieldTexPath, ShaderDatabase.Transparent, Color.white);
                ShieldMaterialByPath[Props.shieldTexPath] = shieldMat;
            }

            Vector3 pos = parent.DrawPos;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            float drawSize = Props.radius * 2f * Mathf.Max(0.01f, Props.shieldDrawSizeFactor);
            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.identity,
                new Vector3(drawSize, 1f, drawSize));
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            Color color = Props.shieldColor;
            color.a = alpha;
            block.SetColor(ShaderPropertyIDs.Color, color);
            Graphics.DrawMesh(MeshPool.plane10, matrix, shieldMat, 0, null, 0, block);
            block.Clear();
        }

        public void Init(Pawn newCaster, int duration)
        {
            caster = newCaster;
            casterFaction = newCaster?.Faction;
            ticksLeft = duration > 0 ? duration : Props.durationTicks;
            energy = Props.startingEnergy * MiliraXian.Characters.QingHe.MX_QHSkillUtility.GetSpecialAbilityEffectFactor(caster);
            ageTicks = 0;
        }

        public void SetEnhanced(bool value)
        {
            enhanced = value;
        }

        public bool TryInterceptProjectile(Projectile projectile, Vector3 lastExactPos, Vector3 newExactPos)
        {
            if (!Active || projectile == null || projectile.Destroyed || parent.Map == null)
            {
                return false;
            }

            if (!InterceptsProjectile(projectile))
            {
                return false;
            }

            if (!ProjectileIsHostile(projectile))
            {
                return false;
            }

            Vector3 center = parent.Position.ToVector3Shifted();
            float radiusWithSpeed = Props.radius + projectile.def.projectile.SpeedTilesPerTick + 0.1f;
            if ((newExactPos.x - center.x) * (newExactPos.x - center.x) + (newExactPos.z - center.z) * (newExactPos.z - center.z) > radiusWithSpeed * radiusWithSpeed)
            {
                return false;
            }

            if (!Props.interceptOutgoingProjectiles
                && ProjectileStartedInside(lastExactPos, center)
                && !ProjectileIsHostile(projectile))
            {
                return false;
            }

            if (!GenGeo.IntersectLineCircleOutline(
                    new Vector2(center.x, center.z),
                    Props.radius,
                    new Vector2(lastExactPos.x, lastExactPos.z),
                    new Vector2(newExactPos.x, newExactPos.z)))
            {
                return false;
            }

            Absorb(projectile, lastExactPos, newExactPos);
            return true;
        }

        private bool InterceptsProjectile(Projectile projectile)
        {
            if (Props.interceptGroundProjectiles && Props.interceptAirProjectiles)
            {
                return true;
            }
            if (Props.interceptGroundProjectiles)
            {
                return !projectile.def.projectile.flyOverhead;
            }
            return Props.interceptAirProjectiles && projectile.def.projectile.flyOverhead;
        }

        private bool ProjectileStartedInside(Vector3 lastExactPos, Vector3 center)
        {
            return (new Vector2(center.x, center.z) - new Vector2(lastExactPos.x, lastExactPos.z)).sqrMagnitude <= Props.radius * Props.radius;
        }

        private bool ProjectileIsHostile(Projectile projectile)
        {
            Thing launcher = projectile.Launcher;
            if (launcher == null)
            {
                return false;
            }

            Faction shieldFaction = casterFaction ?? caster?.Faction;
            if (shieldFaction == null)
            {
                return true;
            }

            if (launcher.Spawned)
            {
                return launcher.HostileTo(shieldFaction);
            }

            return launcher.Faction != null && launcher.Faction.HostileTo(shieldFaction);
        }

        private void Absorb(Projectile projectile, Vector3 lastExactPos, Vector3 newExactPos)
        {
            Map map = parent.Map;
            Vector3 center = parent.TrueCenter();

            if (projectile.DamageDef == DamageDefOf.EMP && Props.breakOnEmp)
            {
                Break();
            }
            else
            {
                energy -= Mathf.Max(0f, projectile.DamageAmount) * Mathf.Max(0f, Props.energyLossPerDamage);
                if (energy <= 0f)
                {
                    Break();
                }
            }

            if (map == null)
            {
                return;
            }

            SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(parent.Position, map));
            FleckDef fleck = null;
            if (!Props.absorbFleckDefName.NullOrEmpty())
            {
                fleck = DefDatabase<FleckDef>.GetNamedSilentFail(Props.absorbFleckDefName);
            }
            if (fleck == null)
            {
                fleck = FleckDefOf.ExplosionFlash;
            }

            Vector3 loc = GetImpactPointOnShield(lastExactPos, newExactPos, center);
            FleckMaker.Static(loc, map, fleck, Mathf.Max(0.1f, Props.absorbFleckScale));
            TryFireEnhancedRetaliation(projectile, loc);
        }

        private void TryFireEnhancedRetaliation(Projectile absorbedProjectile, Vector3 launchPos)
        {
            if (!enhanced || Props.enhancedRetaliationProjectileDef == null || parent?.Spawned != true || parent.Map == null)
            {
                return;
            }

            Thing target = absorbedProjectile?.Launcher;
            if (target == null || target.Destroyed || target.MapHeld != parent.Map)
            {
                return;
            }

            IntVec3 launchCell = launchPos.ToIntVec3();
            if (!launchCell.InBounds(parent.Map))
            {
                launchCell = parent.Position;
            }

            Projectile retaliation = GenSpawn.Spawn(Props.enhancedRetaliationProjectileDef, launchCell, parent.Map) as Projectile;
            if (retaliation == null)
            {
                return;
            }

            Thing launcher = caster ?? parent;
            retaliation.Launch(
                launcher,
                launchPos,
                target,
                target,
                ProjectileHitFlags.IntendedTarget,
                preventFriendlyFire: false,
                equipment: null);
        }

        private Vector3 GetImpactPointOnShield(Vector3 lastExactPos, Vector3 newExactPos, Vector3 center)
        {
            Vector2 circleCenter = new(center.x, center.z);
            Vector2 start = new(lastExactPos.x, lastExactPos.z);
            Vector2 end = new(newExactPos.x, newExactPos.z);
            Vector2 movement = end - start;
            float movementSqr = movement.sqrMagnitude;
            if (movementSqr <= 0.0001f)
            {
                return center;
            }

            Vector2 fromCenter = start - circleCenter;
            float a = movementSqr;
            float b = 2f * Vector2.Dot(fromCenter, movement);
            float c = fromCenter.sqrMagnitude - Props.radius * Props.radius;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return newExactPos;
            }

            float sqrt = Mathf.Sqrt(discriminant);
            float t1 = (-b - sqrt) / (2f * a);
            float t2 = (-b + sqrt) / (2f * a);
            float t = t1 >= 0f && t1 <= 1f ? t1 : t2;
            t = Mathf.Clamp01(t);
            Vector2 hit = start + movement * t;
            return new Vector3(hit.x, AltitudeLayer.MoteOverhead.AltitudeFor(), hit.y);
        }

        private void Break()
        {
            energy = 0f;
            if (parent?.Spawned != true)
            {
                return;
            }

            EffecterDefOf.Shield_Break.SpawnAttached(parent, parent.MapHeld, Mathf.Max(1f, Props.breakEffectScale));
            FleckMaker.Static(parent.TrueCenter(), parent.Map, FleckDefOf.ExplosionFlash, Mathf.Max(1f, Props.breakFlashScale));
        }
    }
}
