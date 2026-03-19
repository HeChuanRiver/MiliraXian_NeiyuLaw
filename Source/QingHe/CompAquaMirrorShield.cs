using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AquaMirrorShield : CompProperties
    {
        public float startingEnergy = 1f;
        public float energyLossPerDamage = 0.033f;
        
        public bool breakOnEmp = true;
        public bool playBreakEffects = true;

        // Shield hit VFX settings aligned with Neiyu shield implementation.
        public string absorbFleckDefName = "ExplosionFlash";
        public List<string> hurtFleckDefNames = new List<string>();
        public float absorbFleckScale = 1.2f;
        public string absorbEffecterDefName = null;

        // Custom active shield rendering settings.
        public bool drawActiveShield = true;
        public string activeShieldTexPath = "MiliraXianNeiyu/Effect/Neiyu_Shield/Shield";
        public Vector2 activeShieldDrawSize = new Vector2(3.6f, 3.6f);
        public float activeShieldAlpha = 0.35f;
        public float activeShieldAltitudeOffset = 0f;
        public float activeShieldPulseMin = 0.96f;
        public float activeShieldPulseMax = 1.06f;
        public int activeShieldPulseTicks = 75;

        public float minDrawSize = 1.2f;
        public float maxDrawSize = 1.55f;

        public CompProperties_AquaMirrorShield()
        {
            compClass = typeof(CompAquaMirrorShield);
        }
    }
    
    public class CompAquaMirrorShield : ThingComp
    {
        private static readonly Dictionary<string, Material> ShieldMaterialByPath = new Dictionary<string, Material>();

        private float energy = 100.0f;
        private float energyFactor = 1f;

        public CompProperties_AquaMirrorShield Props => (CompProperties_AquaMirrorShield)props;

        public float Energy => energy;

        public bool Broken => energy <= 0f;

        private Pawn PawnOwner => parent as Pawn;

        private bool ShouldDisplay
        {
            get
            {
                Pawn pawn = PawnOwner;
                return pawn != null && pawn.Spawned && !pawn.Dead;
            }
        }

        private float ActiveShieldPulseScale
        {
            get
            {
                int period = Mathf.Max(1, Props.activeShieldPulseTicks);
                float min = Mathf.Min(Props.activeShieldPulseMin, Props.activeShieldPulseMax);
                float max = Mathf.Max(Props.activeShieldPulseMin, Props.activeShieldPulseMax);
                float t = (Find.TickManager.TicksGame % period) / (float)period;
                float wave = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
                return Mathf.Lerp(min, max, wave);
            }
        }

        public void Init(float maxEnergyFactor)
        {
            energy *= maxEnergyFactor;
            energyFactor = maxEnergyFactor;
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            energy = Mathf.Max(0f, Props.startingEnergy * energyFactor);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref energy, "aquaMirrorEnergy", -1f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && energy < 0f)
            {
                energy = Mathf.Max(0f, Props.startingEnergy * energyFactor);
            }
        }

        // Intentionally do not provide shield status gizmos.
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield break;
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            Pawn owner = PawnOwner;
            if (owner == null || Broken)
            {
                return;
            }

            if (dinfo.Def == DamageDefOf.EMP)
            {
                if (Props.breakOnEmp)
                {
                    Break();
                }

                return;
            }

            if (dinfo.Def.ignoreShields)
            {
                return;
            }

            // Treat weapon-based or explicit-instigator damage as verb-driven damage.
            if (dinfo.Weapon == null && dinfo.Instigator == null)
            {
                return;
            }

            if (owner.Spawned && owner.Map != null)
            {
                SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(owner.Position, owner.Map));

                FleckDef fleck = null;
                if (Props.hurtFleckDefNames != null && Props.hurtFleckDefNames.Count > 0)
                {
                    int index = Rand.RangeInclusive(0, Props.hurtFleckDefNames.Count - 1);
                    string fleckName = Props.hurtFleckDefNames[index];
                    if (!fleckName.NullOrEmpty())
                    {
                        fleck = DefDatabase<FleckDef>.GetNamedSilentFail(fleckName);
                    }
                }

                if (fleck == null && !Props.absorbFleckDefName.NullOrEmpty())
                {
                    fleck = DefDatabase<FleckDef>.GetNamedSilentFail(Props.absorbFleckDefName);
                }

                if (fleck == null)
                {
                    fleck = FleckDefOf.ExplosionFlash;
                }

                FleckMaker.Static(owner.TrueCenter(), owner.Map, fleck, Mathf.Max(0.1f, Props.absorbFleckScale));

                if (!Props.absorbEffecterDefName.NullOrEmpty())
                {
                    EffecterDef effecterDef = DefDatabase<EffecterDef>.GetNamedSilentFail(Props.absorbEffecterDefName);
                    if (effecterDef != null)
                    {
                        Effecter effecter = effecterDef.Spawn(owner.Position, owner.Map);
                        TargetInfo t = new TargetInfo(owner.Position, owner.Map);
                        effecter.EffectTick(t, t);
                        effecter.Cleanup();
                    }
                }
            }

            float loss = Mathf.Max(0f, dinfo.Amount) * Mathf.Max(0f, Props.energyLossPerDamage);
            energy -= loss;
            if (energy <= 0f)
            {
                Break();
            }

            absorbed = true;
        }

        public override void PostDraw()
        {
            base.PostDraw();

            Pawn owner = PawnOwner;
            if (owner == null || Broken || !ShouldDisplay || !Props.drawActiveShield)
            {
                return;
            }

            Material shieldMat = null;
            if (!Props.activeShieldTexPath.NullOrEmpty())
            {
                var dynamicAlpha = Mathf.Clamp01(Props.activeShieldAlpha * (energy * 0.008f + 0.2f));
                string cacheKey = Props.activeShieldTexPath + "|" + dynamicAlpha.ToString("F3");
                if (!ShieldMaterialByPath.TryGetValue(cacheKey, out shieldMat))
                {
                    shieldMat = MaterialPool.MatFrom(
                        Props.activeShieldTexPath,
                        ShaderDatabase.Transparent,
                        new Color(0.8f, 0.9f, 1f, dynamicAlpha));
                    ShieldMaterialByPath[cacheKey] = shieldMat;
                }
            }

            if (shieldMat == null)
            {
                return;
            }
            Vector3 pos = owner.Drawer.DrawPos;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            pos += Altitudes.AltIncVect * Props.activeShieldAltitudeOffset;

            float pulseScale = ActiveShieldPulseScale;
            Vector2 drawSize = Props.activeShieldDrawSize;

            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.identity,
                new Vector3(drawSize.x * pulseScale, 1f, drawSize.y * pulseScale));

            Graphics.DrawMesh(MeshPool.plane10, matrix, shieldMat, 0);
        }

        public override bool CompAllowVerbCast(Verb verb)
        {
            return true;
        }

        private void Break()
        {
            float energyBeforeBreak = energy;
            energy = 0f;

            Pawn owner = PawnOwner;
            if (!Props.playBreakEffects || owner == null || !owner.Spawned)
            {
                return;
            }

            float ratio;
            if (Props.startingEnergy > 0f)
            {
                ratio = Mathf.Clamp01(energyBeforeBreak / (Props.startingEnergy * energyFactor));
            }
            else
            {
                ratio = 0f;
            }

            float scale = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, ratio);
            EffecterDefOf.Shield_Break.SpawnAttached(parent, parent.MapHeld, scale);
            FleckMaker.Static(owner.TrueCenter(), owner.Map, FleckDefOf.ExplosionFlash, 8f);
        }
    }
}