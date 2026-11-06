using System;
using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common;
using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters.Neiyu.Cultivation;

namespace MiliraXian.Characters.Neiyu
{
    public class CompProperties_ModeSwitchWeapon : CompProperties
    {
        public List<ThingDef> formWeaponDefs;

        public List<string> formLabels;
        public List<string> formIconPaths;

        public string commandLabel = "MX_ModeSwitch_CommandLabel";
        public string commandDesc = "MX_ModeSwitch_CommandDesc";

        public bool destroyOldWeapon = true;
        public bool requirePrimary = true;
        public bool reuseInventoryForm;
        public bool blockWhilePawnBusy;
        public int cooldownTicks = 0;

        public CompProperties_ModeSwitchWeapon()
        {
            compClass = typeof(Comp_ModeSwitchWeapon);
        }
    }

    public class Comp_ModeSwitchWeapon : ThingComp
    {
        internal enum HeldLocation
        {
            EquipmentPrimary,
            Inventory
        }

        internal struct SwitchContext
        {
            public Pawn Pawn;
            public ThingWithComps SourceThing;
            public Comp_ModeSwitchWeapon Comp;
            public HeldLocation Location;
            public int CurrentIndex;
        }

        private int lastToggleTick = -999999;
        private List<Texture2D> cachedIcons;

        private CompProperties_ModeSwitchWeapon Props => props as CompProperties_ModeSwitchWeapon;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastToggleTick, "mx_lastToggleTick", -999999);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            Pawn pawn = parent?.ParentHolder as Pawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (!TryGetSwitchContext(pawn, out SwitchContext context))
            {
                yield break;
            }

            if (context.SourceThing != parent)
            {
                yield break;
            }

            Command_Action cmd = BuildSwitchCommand(context);
            if (cmd != null)
            {
                yield return cmd;
            }
        }

        internal static bool TryGetSwitchContext(Pawn pawn, out SwitchContext context)
        {
            context = default(SwitchContext);
            if (pawn == null)
            {
                return false;
            }

            ThingWithComps primary = pawn.equipment?.Primary;
            if (primary != null && TryGetContextFromThing(pawn, primary, HeldLocation.EquipmentPrimary, out context))
            {
                return true;
            }

            ThingOwner inventory = pawn.inventory?.innerContainer;
            if (inventory != null)
            {
                for (int i = 0; i < inventory.Count; i++)
                {
                    ThingWithComps thing = inventory[i] as ThingWithComps;
                    if (thing == null || thing == primary)
                    {
                        continue;
                    }

                    if (TryGetContextFromThing(pawn, thing, HeldLocation.Inventory, out context))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetContextFromThing(Pawn pawn, ThingWithComps thing, HeldLocation location, out SwitchContext context)
        {
            context = default(SwitchContext);
            if (thing == null)
            {
                return false;
            }

            Comp_ModeSwitchWeapon comp = thing.TryGetComp<Comp_ModeSwitchWeapon>();
            if (comp == null)
            {
                return false;
            }

            if (location == HeldLocation.Inventory
                && thing.TryGetComp<CompWeaponDropProtection>()?.BoundOwner is { } owner && owner != pawn)
                return false;

            int currentIndex = comp.GetCurrentFormIndex(thing);
            if (currentIndex < 0)
            {
                return false;
            }

            context = new SwitchContext
            {
                Pawn = pawn,
                SourceThing = thing,
                Comp = comp,
                Location = location,
                CurrentIndex = currentIndex
            };
            return true;
        }

        internal Command_Action BuildSwitchCommand(SwitchContext context)
        {
            if (Props == null || Props.formWeaponDefs == null || Props.formWeaponDefs.Count < 2)
            {
                return null;
            }

            EnsureIconsLoaded();

            int previewIndex = -1;
            for (int i = 0; i < Props.formWeaponDefs.Count; i++)
            {
                if (i != context.CurrentIndex)
                {
                    previewIndex = i;
                    break;
                }
            }

            bool onCooldown = Props.cooldownTicks > 0 && Find.TickManager.TicksGame - lastToggleTick < Props.cooldownTicks;
            bool pawnBusy = Props.blockWhilePawnBusy && PawnUnavailableForSwitch(context.Pawn);
            bool directSwitch = Props.formWeaponDefs.Count == 2;
            if (directSwitch && FindStoredProtectedForm(context.Pawn, Props.formWeaponDefs[previewIndex]) != null)
                return null;

            bool disabledWhileHunting = IsPrimaryWeaponLockedByHunting(context);

            string commandLabel = ResolveText(Props.commandLabel, "MX_ModeSwitch_CommandLabel");
            string commandDesc = ResolveText(Props.commandDesc, "MX_ModeSwitch_CommandDesc");
            string currentFormLabel = GetFormLabel(context.CurrentIndex);

            Command_Action cmd = new()
            {
                defaultLabel = directSwitch
                    ? commandLabel
                    : "MX_ModeSwitch_CommandLabelWithForm".Translate(commandLabel, currentFormLabel).ToString(),
                defaultDesc = directSwitch
                    ? commandDesc
                    : "MX_ModeSwitch_CommandFullDesc".Translate(commandDesc, currentFormLabel).ToString(),
                icon = GetFormIcon(previewIndex) ?? GetFormIcon(context.CurrentIndex) ?? TexCommand.Attack,
                Disabled = onCooldown || pawnBusy || disabledWhileHunting
            };

            if (disabledWhileHunting)
            {
                cmd.disabledReason = "MX_ModeSwitch_DisabledWhileHunting".Translate().ToString();
            }
            else if (pawnBusy)
            {
                cmd.disabledReason = "MX_ModeSwitch_PawnBusy".Translate().ToString();
            }
            else if (onCooldown)
            {
                int ticksLeft = Props.cooldownTicks - (Find.TickManager.TicksGame - lastToggleTick);
                cmd.disabledReason = "MX_ModeSwitch_Cooldown".Translate((ticksLeft / 60f).ToString("F1")).ToString();
            }

            cmd.action = delegate
            {
                if (directSwitch)
                {
                    TrySwitchTo(context, previewIndex);
                }
                else
                {
                    OpenSwitchMenu(context);
                }
            };

            return cmd;
        }

        private void OpenSwitchMenu(SwitchContext context)
        {
            if (Props == null || Props.formWeaponDefs == null)
            {
                return;
            }

            List<FloatMenuOption> options = new();
            for (int i = 0; i < Props.formWeaponDefs.Count; i++)
            {
                if (i == context.CurrentIndex)
                {
                    continue;
                }

                ThingDef targetDef = Props.formWeaponDefs[i];
                if (targetDef == null)
                {
                    continue;
                }

                int targetIndex = i;
                string label = GetFormLabel(targetIndex);
                options.Add(new FloatMenuOption(label, delegate
                {
                    TrySwitchTo(context, targetIndex);
                }));
            }

            if (options.Count == 0)
            {
                Messages.Message("MX_ModeSwitch_NoTargetForm".Translate(), context.Pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string ResolveText(string text, string fallbackKey)
        {
            string value = text.NullOrEmpty() ? fallbackKey : text;
            if (LanguageDatabase.activeLanguage != null
                && (value.StartsWith("MX_") || value.StartsWith("MiliraXian.")))
            {
                return value.Translate().ToString();
            }
            return value;
        }

        private int GetCurrentFormIndex(Thing sourceThing)
        {
            if (Props?.formWeaponDefs == null || sourceThing == null)
            {
                return -1;
            }

            for (int i = 0; i < Props.formWeaponDefs.Count; i++)
            {
                if (Props.formWeaponDefs[i] == sourceThing.def)
                {
                    return i;
                }
            }

            return -1;
        }

        private string GetFormLabel(int index)
        {
            if (Props?.formLabels != null
                && index >= 0
                && index < Props.formLabels.Count
                && !string.IsNullOrEmpty(Props.formLabels[index]))
            {
                return ResolveText(Props.formLabels[index], Props.formLabels[index]);
            }

            ThingDef def = Props?.formWeaponDefs != null && index >= 0 && index < Props.formWeaponDefs.Count
                ? Props.formWeaponDefs[index]
                : null;
            return def != null ? def.label.CapitalizeFirst() : "MX_Common_Unknown".Translate().ToString();
        }

        private void EnsureIconsLoaded()
        {
            if (cachedIcons != null)
            {
                return;
            }

            cachedIcons = new List<Texture2D>();
            int count = Props?.formWeaponDefs?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                Texture2D tex = null;
                if (Props.formIconPaths != null
                    && i < Props.formIconPaths.Count
                    && !string.IsNullOrEmpty(Props.formIconPaths[i]))
                {
                    tex = ContentFinder<Texture2D>.Get(Props.formIconPaths[i], true);
                }
                cachedIcons.Add(tex);
            }
        }

        private Texture2D GetFormIcon(int index)
        {
            if (cachedIcons == null || index < 0 || index >= cachedIcons.Count)
            {
                return null;
            }
            return cachedIcons[index];
        }

        private void TrySwitchTo(SwitchContext context, int targetIndex)
        {
            Pawn pawn = context.Pawn;
            ThingWithComps sourceThing = context.SourceThing;
            if (pawn == null || sourceThing == null || sourceThing.Destroyed)
            {
                return;
            }

            if (Props == null || Props.formWeaponDefs == null || Props.formWeaponDefs.Count < 2)
            {
                return;
            }

            if (targetIndex < 0 || targetIndex >= Props.formWeaponDefs.Count)
            {
                return;
            }

            if (Props.cooldownTicks > 0 && Find.TickManager.TicksGame - lastToggleTick < Props.cooldownTicks)
            {
                return;
            }

            if (IsPrimaryWeaponLockedByHunting(context))
            {
                Messages.Message("MX_ModeSwitch_DisabledWhileHunting".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (Props.blockWhilePawnBusy && PawnUnavailableForSwitch(pawn))
            {
                Messages.Message("MX_ModeSwitch_PawnBusy".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            ThingDef targetDef = Props.formWeaponDefs[targetIndex];
            if (targetDef == null)
            {
                Messages.Message("MX_ModeSwitch_TargetFormMissing".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            ThingOwner heldInventory = pawn.inventory?.innerContainer;
            if (context.Location == HeldLocation.EquipmentPrimary
                ? pawn.equipment?.Primary != sourceThing
                : heldInventory?.Contains(sourceThing) != true)
                return;

            CompWeaponDropProtection sourceProtection = sourceThing.TryGetComp<CompWeaponDropProtection>();
            if (sourceProtection?.Transferring == true
                || (sourceProtection?.BoundOwner is { } owner && owner != pawn))
                return;

            if (FindStoredProtectedForm(pawn, targetDef, allowUnbound: true) is { } storedForm)
            {
                WeaponDropProtectionUtility.EquipOrReject(pawn, storedForm, allowUnbound: true);
                return;
            }

            ThingWithComps newThing = null;
            if (Props.reuseInventoryForm && context.Location == HeldLocation.EquipmentPrimary && heldInventory != null)
            {
                for (int i = 0; i < heldInventory.Count; i++)
                {
                    if (heldInventory[i] is ThingWithComps alternate && alternate.def == targetDef
                        && !alternate.HasComp<CompWeaponDropProtection>() && heldInventory.Remove(alternate))
                    {
                        newThing = alternate;
                        break;
                    }
                }
            }

            if (newThing == null)
            {
                ThingDef stuff = targetDef.MadeFromStuff ? sourceThing.Stuff : null;
                newThing = ThingMaker.MakeThing(targetDef, stuff) as ThingWithComps;
                if (newThing == null)
                {
                    Messages.Message("MX_ModeSwitch_TargetFormInvalidThing".Translate(targetDef.defName), pawn, MessageTypeDefOf.RejectInput, false);
                    return;
                }

                newThing.HitPoints = Math.Min(sourceThing.HitPoints, newThing.MaxHitPoints);

                CompQuality qOld = sourceThing.TryGetComp<CompQuality>();
                CompQuality qNew = newThing.TryGetComp<CompQuality>();
                if (qOld != null && qNew != null)
                {
                    qNew.SetQuality(qOld.Quality, ArtGenerationContext.Colony);
                }
            }

            if (Props.destroyOldWeapon && newThing.TryGetComp<CompWeaponDropProtection>() is { } newProtection)
            {
                newProtection.InheritFormOwner(sourceThing, pawn);
                if (!TryReplaceProtectedForm(context, sourceProtection, newProtection))
                {
                    Messages.Message(WeaponDropProtectionUtility.Text("SwapFailed", "未能完成换装。"),
                        pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
            }
            else if (context.Location == HeldLocation.EquipmentPrimary)
            {
                if (pawn.equipment == null)
                {
                    Messages.Message("MX_ModeSwitch_NoEquipmentTracker".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                    newThing.Destroy(DestroyMode.Vanish);
                    return;
                }

                pawn.equipment.Remove(sourceThing);
                if (Props.destroyOldWeapon && !sourceThing.Destroyed)
                {
                    sourceThing.Destroy(DestroyMode.Vanish);
                }

                if (newThing.TryGetComp<CompEquippable>() != null)
                {
                    pawn.equipment.AddEquipment(newThing);
                }
                else
                {
                    AddToInventoryOrDrop(pawn, newThing);
                }
            }
            else
            {
                ThingOwner inventory = pawn.inventory?.innerContainer;
                if (inventory == null)
                {
                    Messages.Message("MX_ModeSwitch_NoInventoryTracker".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                    newThing.Destroy(DestroyMode.Vanish);
                    return;
                }

                inventory.Remove(sourceThing);
                if (Props.destroyOldWeapon && !sourceThing.Destroyed)
                {
                    sourceThing.Destroy(DestroyMode.Vanish);
                }

                AddToInventoryOrDrop(pawn, newThing);
            }

            Comp_ModeSwitchWeapon newComp = newThing.TryGetComp<Comp_ModeSwitchWeapon>();
            int tick = Find.TickManager.TicksGame;
            lastToggleTick = tick;
            if (newComp != null)
            {
                newComp.lastToggleTick = tick;
            }
        }

        private ThingWithComps FindStoredProtectedForm(Pawn pawn, ThingDef targetDef, bool allowUnbound = false)
        {
            if (!Props.reuseInventoryForm || pawn.inventory?.innerContainer is not { } inventory)
                return null;

            foreach (Thing item in inventory)
            {
                if (item is ThingWithComps form && form.def == targetDef
                    && WeaponDropProtectionUtility.IsStoredWeapon(form, pawn, allowUnbound))
                    return form;
            }
            return null;
        }

        private static bool TryReplaceProtectedForm(SwitchContext context, CompWeaponDropProtection sourceProtection,
            CompWeaponDropProtection newProtection)
        {
            Pawn pawn = context.Pawn;
            ThingWithComps source = context.SourceThing;
            ThingWithComps replacement = newProtection.parent;
            bool equipped = context.Location == HeldLocation.EquipmentPrimary;
            ThingOwner holder = equipped ? pawn.equipment.GetDirectlyHeldThings() : pawn.inventory.innerContainer;
            if (sourceProtection != null)
                sourceProtection.Transferring = true;
            newProtection.Transferring = true;

            try
            {
                try
                {
                    if ((!equipped || (replacement.HasComp<CompEquippable>() && EquipmentUtility.CanEquip(replacement, pawn)))
                        && holder.Remove(source) && !source.Spawned && source.ParentHolder == null)
                    {
                        if (equipped)
                            pawn.equipment.AddEquipment(replacement);
                        else
                            holder.TryAdd(replacement, canMergeWithExistingStacks: false);
                    }
                }
                catch (Exception exception)
                {
                    Log.Error($"[MiliraXian] Changing form of {source} for {pawn} raised an exception: {exception}");
                }

                if (equipped ? pawn.equipment.Primary == replacement : holder.Contains(replacement))
                {
                    if (source.Destroyed)
                        return true;
                    if (!source.Spawned && source.ParentHolder == null)
                    {
                        source.Destroy(DestroyMode.Vanish);
                        return true;
                    }

                    // Another handler moved the old form during removal. Keep it in its new holder.
                    replacement.Destroy(DestroyMode.Vanish);
                    return false;
                }

                if (!replacement.Destroyed && !replacement.Spawned && replacement.ParentHolder == null)
                    replacement.Destroy(DestroyMode.Vanish);
                WeaponDropProtectionUtility.RestoreIfLoose(source, holder, pawn);
                return false;
            }
            finally
            {
                newProtection.Transferring = false;
                if (sourceProtection != null)
                    sourceProtection.Transferring = false;
            }
        }

        private static bool IsPrimaryWeaponLockedByHunting(SwitchContext context)
        {
            return context.Location == HeldLocation.EquipmentPrimary
                && context.Pawn?.jobs?.curJob?.def == JobDefOf.Hunt;
        }

        private static void AddToInventoryOrDrop(Pawn pawn, ThingWithComps thing)
        {
            ThingOwner inventory = pawn.inventory?.innerContainer;
            if (inventory != null && inventory.TryAdd(thing, canMergeWithExistingStacks: true))
            {
                return;
            }

            if (pawn.Spawned && pawn.Map != null)
            {
                GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
            else
            {
                thing.Destroy(DestroyMode.Vanish);
            }
        }

        private static bool PawnUnavailableForSwitch(Pawn pawn)
        {
            return pawn == null
                || pawn.Downed
                || pawn.Dead
                || pawn.stances?.stunner?.Stunned == true
                || pawn.jobs?.curDriver?.PlayerInterruptable == false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos_ModeSwitchWeapon
    {
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }

            if (__instance == null || __instance.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            if (NeiyuEquipmentUtility.IsNeiyu(__instance) && !__instance.Dead)
            {
                yield return new Command_Action
                {
                    defaultLabel = Cultivation.CultivationText.Title,
                    defaultDesc = Cultivation.CultivationText.Get("OpenDescription", "查看修行分支、所需材料与当前力量。"),
                    icon = Cultivation.CultivationArt.Emblem,
                    action = () => Find.WindowStack.Add(new Cultivation.Dialog_NeiyuCultivation(__instance))
                };

                if (Prefs.DevMode && DebugSettings.ShowDevGizmos)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = CultivationText.Get("DevCompleteAllLabel", "DEV：修行全部修满"),
                        defaultDesc = CultivationText.Get("DevCompleteAllDescription", "立即领悟全部修行，跳过材料与经历要求。进度保存到当前角色，力量封印仍然生效。"),
                        icon = CultivationArt.Emblem,
                        action = () =>
                        {
                            bool success = CultivationService.TryCompleteAllForDev(__instance, out string message);
                            Messages.Message(message, __instance,
                                success ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput, historical: false);
                        }
                    };
                }
            }

            if (!Comp_ModeSwitchWeapon.TryGetSwitchContext(__instance, out Comp_ModeSwitchWeapon.SwitchContext context))
            {
                yield break;
            }

            Command_Action cmd = context.Comp.BuildSwitchCommand(context);
            if (cmd != null)
            {
                yield return cmd;
            }
        }
    }
}
