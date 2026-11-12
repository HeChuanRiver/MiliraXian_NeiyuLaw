using System.Collections.Generic;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilitySummerRain : CompProperties_AbilityEffect
    {
        /// <summary>
        /// The wall def, by name. Resolved on use rather than through a DefOf field: CompProperties
        /// are constructed while the XML is being loaded, which is before DefOf injection, so a
        /// DefOf reference here can be null and quietly make the whole ability do nothing.
        /// </summary>
        public string wallDefName = "MX_QH_SummerRainWall";

        public HediffDef resourceCostDef;
        public float resourceCost = 2f;
        public string missingResourceMessage = "MX_QH_FlowerDecreeNotEnough";

        /// <summary>Half the wall's length and thickness, only for drawing the preview.</summary>
        public float previewHalfLength = 7.5f;
        public float previewHalfThickness = 1.5f;

        public CompProperties_AbilitySummerRain()
        {
            compClass = typeof(CompAbilityEffect_SummerRain);
        }
    }

    /// <summary>
    /// Lotus Rain (RangedRework.md 2.3). The wall is spawned when the cast succeeds — the rain does
    /// not fall during the warm-up; that was tried and it left the player with no feedback while
    /// winding up, so the effect now begins at the moment it actually happens.
    /// </summary>
    public class CompAbilityEffect_SummerRain : CompAbilityEffect
    {
        private static readonly Color PreviewColor = new(0.60f, 0.95f, 0.50f, 0.60f);

        private readonly List<IntVec3> previewCells = new();

        public new CompProperties_AbilitySummerRain Props => (CompProperties_AbilitySummerRain)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (Props.resourceCostDef != null
                && Props.resourceCost > 0f
                && PawnSpecialResourceUtility.GetCurrentResource(parent.pawn, Props.resourceCostDef) < Props.resourceCost)
            {
                reason = Props.missingResourceMessage.Translate();
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            Map map = caster?.Map;
            if (map == null)
            {
                return;
            }

            ThingDef wallDef = ResolveWallDef();
            if (wallDef == null)
            {
                Log.ErrorOnce("[MiliraXian] Lotus Rain: wall def '" + Props.wallDefName + "' not found.", 0x71f0a2);
                return;
            }

            IntVec3 cell = target.Cell;
            if (!cell.IsValid || !cell.InBounds(map))
            {
                return;
            }

            if (Props.resourceCostDef != null && Props.resourceCost > 0f)
            {
                PawnSpecialResourceUtility.TryConsumeResource(caster, Props.resourceCostDef, Props.resourceCost);
            }

            Thing wall = GenSpawn.Spawn(wallDef, cell, map);
            CompSummerRainWall comp = wall?.TryGetComp<CompSummerRainWall>();
            if (comp == null)
            {
                Log.ErrorOnce("[MiliraXian] Lotus Rain: the wall def has no CompSummerRainWall.", 0x71f0a3);
                return;
            }
            comp.Init(caster);
        }

        private ThingDef ResolveWallDef()
        {
            return Props.wallDefName.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(Props.wallDefName);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            Map map = Find.CurrentMap;
            if (map == null || !target.Cell.IsValid)
            {
                return;
            }

            BuildPreviewCells(target.Cell, map);
            if (previewCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(previewCells, PreviewColor);
            }
        }

        /// <summary>The 15x3 band the wall will occupy, perpendicular to the caster -> target line.</summary>
        private void BuildPreviewCells(IntVec3 centre, Map map)
        {
            previewCells.Clear();
            Pawn caster = parent?.pawn;
            Vector2 toTarget = caster != null
                ? new Vector2(centre.x - caster.Position.x, centre.z - caster.Position.z)
                : Vector2.right;
            if (toTarget.sqrMagnitude <= 0.01f)
            {
                toTarget = Vector2.right;
            }
            toTarget.Normalize();
            Vector2 axisLength = new(-toTarget.y, toTarget.x);

            int halfLen = Mathf.CeilToInt(Props.previewHalfLength);
            int halfThick = Mathf.CeilToInt(Props.previewHalfThickness);
            for (int a = -halfLen; a <= halfLen; a++)
            {
                for (int t = -halfThick; t <= halfThick; t++)
                {
                    Vector2 offset = axisLength * a + toTarget * t;
                    IntVec3 cell = new(centre.x + Mathf.RoundToInt(offset.x), 0, centre.z + Mathf.RoundToInt(offset.y));
                    if (!cell.InBounds(map))
                    {
                        continue;
                    }
                    if (Mathf.Abs(a) <= Props.previewHalfLength && Mathf.Abs(t) <= Props.previewHalfThickness)
                    {
                        previewCells.Add(cell);
                    }
                }
            }
        }
    }
}
