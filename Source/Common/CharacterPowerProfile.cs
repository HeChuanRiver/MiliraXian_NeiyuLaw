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

        public void Stat(ThingDef def, string name, float balanced, float decorative, bool equipped = false)
        {
            List<StatModifier> list = equipped ? def.equippedStatOffsets : def.statBases;
            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(name);
            if (stat == null) return; // DLC-dependent stats.
            StatModifier modifier = list?.Find(item => item.stat == stat);
            if (modifier != null) Field(modifier, "value", balanced, decorative);
        }

        public void Weapon(string name, float damage, float penetration, float interval, float decorativeDamage)
        {
            ThingDef def = Thing(name);
            Field(def.tools[0], "power", damage, decorativeDamage);
            Field(def.tools[0], "armorPenetration", penetration, .1f);
            Field(def.tools[0], "cooldownTime", interval, interval + .3f);
            Stat(def, "MeleeWeapon_CooldownMultiplier", 1f, 1f);
        }

        public void Armor(string name, float sharp, float blunt, float heat)
        {
            ThingDef def = Thing(name);
            Stat(def, "ArmorRating_Sharp", sharp, .18f);
            Stat(def, "ArmorRating_Blunt", blunt, .08f);
            Stat(def, "ArmorRating_Heat", heat, .1f);
            Stat(def, "Insulation_Cold", 30f, 8f);
            Stat(def, "Insulation_Heat", 20f, 5f);
        }

        public void Ability(string name, int cooldown, float range)
        {
            AbilityDef def = AbilityDef(name);
            Field(def, "cooldownTicksRange", new IntRange(cooldown, cooldown), new IntRange(cooldown, cooldown));
            Field(def.verbProperties, "range", range, range);
        }

        public void Description(Def def, string key)
        {
            Value(() => def.description, value => def.description = value,
                key.Translate().ToString(), "MX_Power_SealedDescription".Translate().ToString());
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
            }, false, false);
            Value(() => mapped, enabled => {
                if (enabled) AL_MentalBreak_Cache.KindExtensionMapping[kind] = originalMental;
                else AL_MentalBreak_Cache.KindExtensionMapping.Remove(kind);
            }, false, false);
            if (mental != null) Field(mental, "blockMentalBreak", false, false);
            bool voidKill = AL_Kill_Manager_Cache.VoidKillKinds.Contains(kind);
            Value(() => voidKill, enabled => {
                if (enabled) AL_Kill_Manager_Cache.VoidKillKinds.Add(kind);
                else AL_Kill_Manager_Cache.VoidKillKinds.Remove(kind);
            }, false, false);
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
