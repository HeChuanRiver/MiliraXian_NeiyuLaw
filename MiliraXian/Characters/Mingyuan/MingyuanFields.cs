using System.Collections.Generic;
using MiliraXian.Characters.Common;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

public class Thing_MingyuanAshesField : ThingWithComps
{
    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        var field = GetComp<CompMingyuanBurningField>();
        if (field.Active)
            MingyuanFireVfx.DrawAshesField(field.Caster.DrawPos, field.PropsField.radius, field.RemainingTicks);
    }
}

public class CompProperties_MingyuanBurningField : CompProperties
{
    public float radius = 4f;
    public int durationTicks = 1800;
    public int pulseIntervalTicks = 30;
    public float damageAmount = 1f;
    public float lifeBurnLayers = 150f;
    public float bloodLossRecovery = 0.03f;
    public Color previewRingColor = new(1f, 0.75f, 0.34f, 0.64f);

    public CompProperties_MingyuanBurningField() => compClass = typeof(CompMingyuanBurningField);
}

public class CompMingyuanBurningField : ThingComp
{
    private Pawn caster;
    private int expireTick;
    private int ticksToPulse;

    public CompProperties_MingyuanBurningField PropsField => (CompProperties_MingyuanBurningField)props;
    internal Pawn Caster => caster;
    internal int RemainingTicks => expireTick - Find.TickManager.TicksGame;
    internal bool Active => !MingyuanPowerBalance.Sealed && RemainingTicks > 0
        && caster is { Spawned: true, Dead: false, Destroyed: false } && caster.Map == parent.Map;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_References.Look(ref caster, "caster");
        Scribe_Values.Look(ref expireTick, "expireTick", 0);
        Scribe_Values.Look(ref ticksToPulse, "ticksToPulse", 0);
    }

    public void Init(Pawn newCaster)
    {
        caster = newCaster;
        expireTick = Find.TickManager.TicksGame + PropsField.durationTicks;
        ticksToPulse = 1;
    }

    public override void CompTick()
    {
        if (!Active)
        {
            parent.Destroy(DestroyMode.Vanish);
            return;
        }

        if (parent.Position != caster.Position) parent.Position = caster.Position;
        if (--ticksToPulse > 0) return;

        ticksToPulse = Mathf.Max(1, PropsField.pulseIntervalTicks);
        Pulse();
    }

    public override string CompInspectStringExtra() =>
        "MX_Mingyuan_AshesField_Inspect".Translate(
            PropsField.radius.ToString("0.#"), PropsField.damageAmount.ToString("0.#"),
            PropsField.lifeBurnLayers.ToString("0.#"), Mathf.CeilToInt(Mathf.Max(0, RemainingTicks) / 60f));

    private void Pulse()
    {
        Map map = parent.Map;
        IntVec3 center = caster.Position;
        List<Thing> targets = CombatTargetSnapshot.Rent(GenRadial.RadialDistinctThingsAround(center, map, PropsField.radius, true));
        try
        {
            foreach (Thing thing in targets)
            {
                if (!Active) break;
                if (!MingyuanUtility.IsHostilePawn(thing, caster, out Pawn target)
                    || !target.Spawned || target.Map != map || !center.InHorDistOf(target.Position, PropsField.radius)) continue;

                DamageWorker.DamageResult result = MingyuanUtility.ApplyTrueDamage(
                    target, DamageDefOf.Burn, PropsField.damageAmount, caster, scaleWithSelfBurn: true);
                if (result?.totalDamageDealt > 0f)
                {
                    MingyuanUtility.AddLifeBurn(target, caster, PropsField.lifeBurnLayers, scaleWithOverburn: true);
                    if (!Active) break;
                    MingyuanUtility.HealOneInjury(caster);
                    float loss = MingyuanSurvival.BloodLoss(caster);
                    if (loss > 0f)
                        HealthUtility.AdjustSeverity(caster, HediffDefOf.BloodLoss, -Mathf.Min(loss, PropsField.bloodLossRecovery));
                }
            }
        }
        finally
        {
            CombatTargetSnapshot.Return(targets);
        }
    }
}
