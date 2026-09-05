using System;
using System.Collections.Generic;
using System.Reflection;
using AriandelLibrary;
using HarmonyLib;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    // Snapshots are taken after XML patches/translations. Reflection is initialization-only;
    // combat reads the cached level, and changing back restores the actual loaded values.
    internal sealed class CharacterPowerProfile
    {
        private readonly List<Action<CharacterPowerLevel>> tunings = new List<Action<CharacterPowerLevel>>();
        public CharacterPowerLevel Level { get; private set; }
        public int Revision { get; private set; }
        public bool Original => Level == CharacterPowerLevel.Original;
        public bool Balanced => Level == CharacterPowerLevel.Balanced;
        public bool Sealed => Level == CharacterPowerLevel.Decorative;

        public void SetLevel(CharacterPowerLevel level)
        {
            if (level < CharacterPowerLevel.Original || level > CharacterPowerLevel.Decorative)
                level = CharacterPowerLevel.Original;
            if (Level == level) return;
            Level = level;
            Revision++;
            Apply();
        }

        public void Apply()
        {
            for (int i = 0; i < tunings.Count; i++) tunings[i](Level);
        }

        public void Value<T>(Func<T> getter, Action<T> setter, T balanced, T decorative)
        {
            T original = getter();
            tunings.Add(level => setter(level == CharacterPowerLevel.Original ? original
                : level == CharacterPowerLevel.Balanced ? balanced : decorative));
        }

        public void Field(object target, string name, object balanced, object decorative)
        {
            if (target == null) throw new InvalidOperationException("Missing balance target for " + name);
            FieldInfo field = AccessTools.Field(target.GetType(), name);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            object b = ConvertValue(balanced, field.FieldType), d = ConvertValue(decorative, field.FieldType);
            Value(() => field.GetValue(target), value => field.SetValue(target, value), b, d);
        }

        private static object ConvertValue(object value, Type type)
        {
            return value == null || type.IsInstanceOfType(value) ? value : Convert.ChangeType(value, type);
        }

        public void ScaleField(object target, string name, float scale, object decorative, float neutral = 0f)
        {
            if (target == null) throw new InvalidOperationException("Missing balance target for " + name);
            FieldInfo field = AccessTools.Field(target.GetType(), name);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            Field(target, name, ConservativePowerTuning.Number(field.GetValue(target), scale, neutral), decorative);
        }

        public void KeepField(object target, string name, object decorative)
        {
            if (target == null) throw new InvalidOperationException("Missing balance target for " + name);
            FieldInfo field = AccessTools.Field(target.GetType(), name);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            Field(target, name, field.GetValue(target), decorative);
        }

        public void ScaleStat(ThingDef def, string name, float scale, float decorative, bool equipped = false)
        {
            List<StatModifier> list = equipped ? def.equippedStatOffsets : def.statBases;
            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(name);
            if (stat == null) return; // DLC-dependent stats.
            StatModifier modifier = list?.Find(item => item.stat == stat);
            if (modifier != null) ScaleField(modifier, "value", scale, decorative);
        }

        public void Weapon(string name, float decorativeDamage, float decorativeInterval)
        {
            ThingDef def = Thing(name);
            ScaleField(def.tools[0], "power", ConservativePowerTuning.Damage, decorativeDamage);
            ScaleField(def.tools[0], "armorPenetration", ConservativePowerTuning.Defense, .1f);
            KeepField(def.tools[0], "cooldownTime", decorativeInterval);
            ScaleStat(def, "MeleeWeapon_CooldownMultiplier", 1f, 1f);
        }

        public void Armor(string name)
        {
            ThingDef def = Thing(name);
            ScaleStat(def, "ArmorRating_Sharp", ConservativePowerTuning.Defense, .18f);
            ScaleStat(def, "ArmorRating_Blunt", ConservativePowerTuning.Defense, .08f);
            ScaleStat(def, "ArmorRating_Heat", ConservativePowerTuning.Defense, .1f);
            ScaleStat(def, "Insulation_Cold", 1f, 8f);
            ScaleStat(def, "Insulation_Heat", 1f, 5f);
        }

        public void Ability(string name, int cooldown, float range)
        {
            AbilityDef def = AbilityDef(name);
            ScaleField(def, "cooldownTicksRange", ConservativePowerTuning.Cooldown, new IntRange(cooldown, cooldown));
            KeepField(def.verbProperties, "range", range);
        }

        public void Description(Def def, string key)
        {
            string inactiveKey = def is AbilityDef ? "MX_Power_SealedDescription"
                : def is ThingDef ? "MX_Power_EquipmentInactive" : "MX_Power_PassiveInactive";
            Value(() => def.description, value => def.description = value,
                key.Translate().ToString(), inactiveKey.Translate().ToString());
        }

        public void LibraryPassives(string kindName)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed(kindName);
            var mental = kind.GetModExtension<AL_RefuseMentalBreak_Extension>();
            // These library caches are populated once. Editing XML extensions alone does not update them.
            bool runWild = AL_MentalBreak_Cache.RunWildImmuneKinds.Contains(kind);
            bool mapped = AL_MentalBreak_Cache.KindExtensionMapping.TryGetValue(kind, out var originalMental);
            Value(() => runWild, enabled => {
                if (enabled) AL_MentalBreak_Cache.RunWildImmuneKinds.Add(kind);
                else AL_MentalBreak_Cache.RunWildImmuneKinds.Remove(kind);
            }, runWild, false);
            Value(() => mapped, enabled => {
                if (enabled) AL_MentalBreak_Cache.KindExtensionMapping[kind] = originalMental;
                else AL_MentalBreak_Cache.KindExtensionMapping.Remove(kind);
            }, mapped, false);
            if (mental != null) KeepField(mental, "blockMentalBreak", false);
            bool voidKill = AL_Kill_Manager_Cache.VoidKillKinds.Contains(kind);
            Value(() => voidKill, enabled => {
                if (enabled) AL_Kill_Manager_Cache.VoidKillKinds.Add(kind);
                else AL_Kill_Manager_Cache.VoidKillKinds.Remove(kind);
            }, voidKill, false);
        }

        public static ThingDef Thing(string name) => DefDatabase<ThingDef>.GetNamed(name);
        public static HediffDef Hediff(string name) => DefDatabase<HediffDef>.GetNamed(name);
        public static AbilityDef AbilityDef(string name) => DefDatabase<AbilityDef>.GetNamed(name);
        public static T AbilityComp<T>(string name) where T : CompProperties_AbilityEffect => AbilityDef(name).comps.Find(c => c is T) as T;
        public static T HediffComp<T>(string name) where T : HediffCompProperties => Hediff(name).comps.Find(c => c is T) as T;
        public static T ThingComp<T>(string name) where T : CompProperties => Thing(name).comps.Find(c => c is T) as T;

        public static float HealOrdinaryInjuries(Pawn pawn, float budget)
        {
            float remaining = budget;
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return 0f;
            for (int i = hediffs.Count - 1; i >= 0 && remaining > 0f; i--)
            {
                if (!(hediffs[i] is Hediff_Injury injury) || injury.IsPermanent()) continue;
                float amount = Mathf.Min(remaining, injury.Severity);
                injury.Heal(amount);
                remaining -= amount;
            }
            return budget - remaining;
        }
    }

    public abstract class CompAbilityEffect_CharacterPowerLimited : CompAbilityEffect
    {
        protected abstract bool PowerSealed { get; }
        public override bool GizmoDisabled(out string reason)
        {
            if (PowerSealed) { reason = "MX_Power_AbilitiesSealed".Translate(); return true; }
            return base.GizmoDisabled(out reason);
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return !PowerSealed && base.Valid(target, throwMessages);
        }
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return !PowerSealed && base.CanApplyOn(target, dest);
        }
    }
}
