using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.NeiyuLaw
{
    public class CompProperties_ModeSwitchWeapon : CompProperties
    {
        public List<ThingDef> formWeaponDefs;

        public List<string> formLabels;
        public List<string> formIconPaths;

        public string commandLabel = "切换形态";
        public string commandDesc = "切换为其他形态";

        public bool destroyOldWeapon = true;
        public bool requirePrimary = true;
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

            Command_Action cmd = new Command_Action
            {
                defaultLabel = Props.commandLabel + "：" + GetFormLabel(context.CurrentIndex),
                defaultDesc = Props.commandDesc + "\n\n当前：" + GetFormLabel(context.CurrentIndex) + "\n点击后选择目标形态。",
                icon = GetFormIcon(previewIndex) ?? GetFormIcon(context.CurrentIndex) ?? TexCommand.Attack,
                Disabled = onCooldown
            };

            if (onCooldown)
            {
                int ticksLeft = Props.cooldownTicks - (Find.TickManager.TicksGame - lastToggleTick);
                cmd.disabledReason = "冷却中：" + (ticksLeft / 60f).ToString("F1") + "s";
            }

            cmd.action = delegate
            {
                OpenSwitchMenu(context);
            };

            return cmd;
        }

        private void OpenSwitchMenu(SwitchContext context)
        {
            if (Props == null || Props.formWeaponDefs == null)
            {
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
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
                Messages.Message("没有可切换的目标形态。", context.Pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new FloatMenu(options));
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
                return Props.formLabels[index];
            }

            ThingDef def = Props?.formWeaponDefs != null && index >= 0 && index < Props.formWeaponDefs.Count
                ? Props.formWeaponDefs[index]
                : null;
            return def != null ? def.label.CapitalizeFirst() : "未知";
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

            ThingDef targetDef = Props.formWeaponDefs[targetIndex];
            if (targetDef == null)
            {
                Messages.Message("目标形态未配置。", pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            ThingDef stuff = targetDef.MadeFromStuff ? sourceThing.Stuff : null;
            ThingWithComps newThing = ThingMaker.MakeThing(targetDef, stuff) as ThingWithComps;
            if (newThing == null)
            {
                Messages.Message("目标形态不是有效物品：" + targetDef.defName, pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            newThing.HitPoints = Math.Min(sourceThing.HitPoints, newThing.MaxHitPoints);

            CompQuality qOld = sourceThing.TryGetComp<CompQuality>();
            CompQuality qNew = newThing.TryGetComp<CompQuality>();
            if (qOld != null && qNew != null)
            {
                qNew.SetQuality(qOld.Quality, ArtGenerationContext.Colony);
            }

            if (context.Location == HeldLocation.EquipmentPrimary)
            {
                if (pawn.equipment == null)
                {
                    Messages.Message("该角色没有装备栏。", pawn, MessageTypeDefOf.RejectInput, false);
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
                    Messages.Message("该角色没有物品栏。", pawn, MessageTypeDefOf.RejectInput, false);
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
