using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common
{
    // Snapshots are taken after XML patches/translations. Reflection is initialization-only;
    // combat reads the cached seal, and changing back restores the actual loaded values.
    internal sealed class CharacterPowerProfile
    {
        private readonly List<Action<bool>> tunings = new();
        public int Revision { get; private set; }
        public bool Sealed { get; private set; }

        public void SetDecorative(bool decorative)
        {
            if (Sealed == decorative) return;
            Sealed = decorative;
            Revision++;
            Apply();
        }

        public void Apply()
        {
            for (int i = 0; i < tunings.Count; i++) tunings[i](Sealed);
        }

        // Old enum names are read as strings so removing Balanced never reaches Enum.Parse.
        // Only the new boolean is written; its presence takes precedence over legacy data.
        public static bool LookDecorative(ref bool decorative, string label, string legacyLabel)
        {
            bool readLegacy = Scribe.mode == LoadSaveMode.LoadingVars
                && Scribe.loader.curXmlParent[label] == null;
            Scribe_Values.Look(ref decorative, label, false, forceSave: true);
            if (!readLegacy) return false;

            string legacy = null;
            Scribe_Values.Look(ref legacy, legacyLabel);
            decorative = legacy == "Decorative" || legacy == "2";
            return legacy == "Balanced" || legacy == "1";
        }

        public void Value<T>(Func<T> getter, Action<T> setter, T decorative)
        {
            T original = getter();
            tunings.Add(sealedPower => setter(sealedPower ? decorative : original));
        }

        public void Field(object target, string name, object decorative)
        {
            if (target == null) throw new InvalidOperationException("Missing balance target for " + name);
            FieldInfo field = AccessTools.Field(target.GetType(), name);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            object value = ConvertValue(decorative, field.FieldType);
            Value(() => field.GetValue(target), next => field.SetValue(target, next), value);
        }

        private static object ConvertValue(object value, Type type)
        {
            return value == null || type.IsInstanceOfType(value) ? value : Convert.ChangeType(value, type);
        }

        public void Stat(ThingDef def, string name, float decorative, bool equipped = false)
        {
            List<StatModifier> list = equipped ? def.equippedStatOffsets : def.statBases;
            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(name);
            if (stat == null) return; // DLC-dependent stats.
            StatModifier modifier = list?.Find(item => item.stat == stat);
            if (modifier != null) Field(modifier, "value", decorative);
        }

        public void Weapon(string name, float decorativeDamage, float decorativeInterval)
        {
            ThingDef def = Thing(name);
            Field(def.tools[0], "power", decorativeDamage);
            Field(def.tools[0], "armorPenetration", .1f);
            Field(def.tools[0], "cooldownTime", decorativeInterval);
            Stat(def, "MeleeWeapon_CooldownMultiplier", 1f);
        }

        public void Armor(string name)
        {
            ThingDef def = Thing(name);
            Stat(def, "ArmorRating_Sharp", .18f);
            Stat(def, "ArmorRating_Blunt", .08f);
            Stat(def, "ArmorRating_Heat", .1f);
            Stat(def, "Insulation_Cold", 8f);
            Stat(def, "Insulation_Heat", 5f);
        }

        public void Ability(string name, int cooldown, float range)
        {
            AbilityDef def = AbilityDef(name);
            Field(def, "cooldownTicksRange", new IntRange(cooldown, cooldown));
            Field(def.verbProperties, "range", range);
        }

        public void Description(Def def)
        {
            string inactiveKey = def is AbilityDef ? "MX_Power_SealedDescription"
                : def is ThingDef ? "MX_Power_EquipmentInactive" : "MX_Power_PassiveInactive";
            Value(() => def.description, value => def.description = value, inactiveKey.Translate().ToString());
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
