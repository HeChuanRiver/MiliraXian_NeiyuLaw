using System.Collections.Generic;
using MiliraXian.Characters.Neiyu.Cultivation;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu;

public partial class NeiyuCombatMapComponent
{
    private sealed class CultivationField : IExposable
    {
        internal Pawn caster;
        internal IntVec3 center;
        internal float radius, healingFraction, poisonSeverity, berserkChance;
        internal int endTick, nextTick;
        internal List<Pawn> affected = new();
        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster"); Scribe_Values.Look(ref center, "center");
            Scribe_Values.Look(ref radius, "radius"); Scribe_Values.Look(ref healingFraction, "healingFraction");
            Scribe_Values.Look(ref poisonSeverity, "poisonSeverity"); Scribe_Values.Look(ref berserkChance, "berserkChance");
            Scribe_Values.Look(ref endTick, "endTick"); Scribe_Values.Look(ref nextTick, "nextTick");
            Scribe_Collections.Look(ref affected, "affected", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) affected ??= new();
        }
    }
    private List<CultivationField> cultivationFields = new();
    private List<BarrageVolley> cultivationVolleys = new();
    private int nextVolleyId;
    private sealed class BarrageVolley : IExposable
    {
        internal int id, expires;
        internal Dictionary<Pawn, int> hits = new();
        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id"); Scribe_Values.Look(ref expires, "expires");
            Scribe_Collections.Look(ref hits, "hits", LookMode.Reference, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) hits ??= new();
        }
    }
    private void ExposeCultivationData()
    {
        Scribe_Collections.Look(ref cultivationFields, "featherFields", LookMode.Deep);
        Scribe_Collections.Look(ref cultivationVolleys, "featherVolleys", LookMode.Deep);
        Scribe_Values.Look(ref nextVolleyId, "featherNextVolley");
        if (Scribe.mode == LoadSaveMode.PostLoadInit) { cultivationFields ??= new(); cultivationVolleys ??= new(); }
    }
    internal void AddCultivationField(Pawn caster, IntVec3 center, float radius, bool healing,
        IEnumerable<Pawn> alreadyAffected = null, float poisonSeverity = 0f, float berserkChance = 0f)
    {
        var state = CultivationService.State(caster);
        var effect = healing ? CultivationEffect.BlessAfterglow : CultivationEffect.ToxinAfterglow;
        if (state?.Has(effect) != true) return;
        var node = Hediff_NeiyuCultivation.Definition(effect);
        cultivationFields.RemoveAll(f => f.caster == caster && (f.healingFraction > 0f) == healing);
        cultivationFields.Add(new CultivationField
        {
            caster = caster, center = center, radius = radius, healingFraction = healing ? node.value : 0,
            poisonSeverity = poisonSeverity, berserkChance = berserkChance,
            affected = alreadyAffected == null ? new() : new(alreadyAffected),
            endTick = Find.TickManager.TicksGame + node.durationTicks,
            nextTick = Find.TickManager.TicksGame + (healing ? 60 : 15)
        });
    }
    private void TickCultivationFields()
    {
        int now = Find.TickManager.TicksGame;
        for (int i = cultivationFields.Count - 1; i >= 0; i--)
        {
            var f = cultivationFields[i];
            if (f.caster == null || f.caster.Dead || !f.caster.Spawned || f.caster.Map != map || now > f.endTick)
            { cultivationFields.RemoveAt(i); continue; }
            if (now < f.nextTick) continue;
            f.nextTick = now + (f.healingFraction > 0 ? 60 : 15);
            var state = CultivationService.State(f.caster);
            if (state?.Has(f.healingFraction > 0 ? CultivationEffect.BlessAfterglow : CultivationEffect.ToxinAfterglow) != true) continue;
            foreach (Pawn target in NeiyuFlowerSwordSkillUtility.CollectPawnsInRadius(map, f.center, f.radius))
            {
                if (f.healingFraction > 0)
                {
                    if (target.HostileTo(f.caster) || target.RaceProps.IsMechanoid) continue;
                    float healed = 0;
                    var hediffs = target.health.hediffSet.hediffs;
                    for (int h = hediffs.Count - 1; h >= 0; h--)
                        if (hediffs[h] is Hediff_Injury injury && injury.Severity > 0f)
                        { float amount = injury.Severity * f.healingFraction; injury.Heal(amount); healed += amount; }
                    state.NotifyHealing(healed);
                }
                else if (target.HostileTo(f.caster) && !f.affected.Contains(target))
                {
                    f.affected.Add(target);
                    CompAbilityEffect_NeiyuFlowerToxinField.ApplyToxin(target, f.poisonSeverity, f.berserkChance);
                }
            }
            if (now % 60 < 15) NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, f.center, "MXNL_ForFeatherCircle", .6f);
        }
        if (now % 120 == 0) cultivationVolleys.RemoveAll(v => now > v.expires);
    }
    private int BeginCultivationVolley()
    {
        int id = ++nextVolleyId;
        cultivationVolleys.Add(new BarrageVolley { id = id, expires = Find.TickManager.TicksGame + 7200 });
        return id;
    }
    internal float ExtraArrowFactor(int id, Pawn target, float successiveFactor)
    {
        var volley = cultivationVolleys.Find(v => v.id == id);
        if (volley == null || target == null) return 1f;
        int hits = volley.hits.GetValueOrDefault(target);
        volley.hits[target] = hits + 1;
        return Mathf.Pow(successiveFactor, hits);
    }
    internal static void CultivationSplash(Pawn caster, Map map, IntVec3 cell, float radius, float damage,
        DamageDef damageDef, float penetration, Thing exclude = null)
    {
        if (caster == null || map == null || NeiyuPowerBalance.AbilitiesDisabled || NeiyuPowerBalance.PassivesDisabled) return;
        foreach (Pawn target in NeiyuFlowerSwordSkillUtility.CollectPawnsInRadius(map, cell, radius))
            if (target != exclude && target.HostileTo(caster))
                target.TakeDamage(new DamageInfo(damageDef, damage, penetration, -1, caster));
        NeiyuFlowerSwordSkillUtility.PlayFleckAt(map, cell, "MXNL_ForFeatherCircle", .35f);
    }
}

public sealed class Projectile_NeiyuBarrageArrow : Bullet
{
    internal int volleyId;
    internal bool extra;
    internal float successiveFactor = 1f, splashFactor, splashRadius;
    private float impactFactor = 1f;
    public override int DamageAmount => Mathf.RoundToInt(base.DamageAmount * impactFactor);
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref volleyId, "featherVolley"); Scribe_Values.Look(ref extra, "featherExtra");
        Scribe_Values.Look(ref successiveFactor, "featherSuccessiveFactor", 1f);
        Scribe_Values.Look(ref splashFactor, "featherSplashFactor"); Scribe_Values.Look(ref splashRadius, "featherSplashRadius");
    }
    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        Map map = Map; Pawn caster = launcher as Pawn; IntVec3 cell = Position;
        if (extra && hitThing is Pawn victim && caster != null && victim.HostileTo(caster))
            impactFactor = map.GetComponent<NeiyuCombatMapComponent>().ExtraArrowFactor(volleyId, victim, successiveFactor);
        float damage = DamageAmount, penetration = ArmorPenetration;
        bool splash = !blockedByShield && splashFactor > 0 && hitThing is Pawn target && caster != null && target.HostileTo(caster);
        base.Impact(hitThing, blockedByShield);
        if (splash && CultivationService.State(caster)?.Has(CultivationEffect.BarrageSplash) == true)
            NeiyuCombatMapComponent.CultivationSplash(caster, map, cell, splashRadius, damage * splashFactor, def.projectile.damageDef, penetration, hitThing);
    }
}
