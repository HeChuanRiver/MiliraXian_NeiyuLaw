using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using Verse;

namespace MiliraXian.CharacterLib
{
    /// <summary>
    /// One texture swap on a race's render tree, written as
    /// <c>&lt;li source="race/path" prefix="true"&gt;character/path&lt;/li&gt;</c>.
    /// The source is the path the tree itself renders from: a functional field rather than a
    /// debug label, unique within the tree, and present on child nodes too (a "behind" node
    /// carries no bodyPartLabel).
    /// </summary>
    public class RacePartSwap
    {
        public string source;
        public string target;

        /// <summary>
        /// Matches any node whose path starts with <see cref="source"/>, appending the remainder
        /// to <see cref="target"/>. Lets one entry cover a part's front and behind nodes.
        /// </summary>
        public bool prefix;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            source = xmlRoot.Attributes?["source"]?.Value;
            prefix = xmlRoot.Attributes?["prefix"]?.Value?.ToLowerInvariant() == "true";
            target = xmlRoot.FirstChild?.Value;
        }
    }

    /// <summary>
    /// One animation swap on a race's part, written as
    /// <c>&lt;li source="Race_Anim"&gt;Character_Anim&lt;/li&gt;</c>. A part's animation is not
    /// reachable by swapping textures: while animating, a node takes its graphic from the
    /// animation's keyframes instead of from GraphicFor.
    /// </summary>
    public class RacePartAnimationSwap
    {
        public AnimationDef source;
        public AnimationDef target;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            // Both ends are Defs, and a custom loader replaces the automatic resolution that
            // child elements would have received, so each reference is registered by hand.
            string sourceName = xmlRoot.Attributes?["source"]?.Value;
            if (!sourceName.NullOrEmpty())
                DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "source", sourceName);

            string targetName = xmlRoot.FirstChild?.Value;
            if (!targetName.NullOrEmpty())
                DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "target", targetName);
        }
    }

    /// <summary>
    /// Replaces the parts a race hardcodes into its render tree. Such nodes have no vanilla Def
    /// of their own — the tree names their texture directly — so a character sharing its race's
    /// tree cannot vary them through XML alone.
    /// </summary>
    public class CharacterRacePartExtension : DefModExtension
    {
        public List<RacePartSwap> parts = new();

        public List<RacePartAnimationSwap> animations = new();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            foreach (RacePartSwap part in parts)
            {
                if (part.source.NullOrEmpty()) yield return "a part entry has no source attribute.";
                if (part.target.NullOrEmpty()) yield return "a part entry has no target path.";
            }
            foreach (RacePartAnimationSwap swap in animations)
            {
                if (swap.source == null) yield return "an animation entry has no source attribute.";
                if (swap.target == null) yield return "an animation entry has no target animation.";
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class CharacterRaceParts
    {
        private static readonly Dictionary<PawnKindDef, CharacterRacePartExtension> kinds = new();
        private static readonly Dictionary<(PawnKindDef, string), Graphic> graphics = new();

        /// <summary>
        /// Lets a character override the declared replacement at runtime, in either direction:
        /// a Unity-driven clip when one can run, a plainer animation when it cannot. Returning
        /// null keeps what XML declares. This library cannot tell which applies, so the choice
        /// is delegated while the mapping itself stays here.
        /// </summary>
        public static Func<Pawn, AnimationDef, AnimationDef> AnimationOverride;

        static CharacterRaceParts()
        {
            foreach (PawnKindDef kind in DefDatabase<PawnKindDef>.AllDefsListForReading)
                if (kind.GetModExtension<CharacterRacePartExtension>() is { } ext) kinds.Add(kind, ext);
            ReportUnmatchedPaths();
        }

        /// <summary>
        /// A mistyped source path would otherwise fail silently: the part simply keeps its race
        /// texture, which is easy to miss and hard to trace. Both places a part's texture can be
        /// declared are checked, since an animated part takes its frames from a GraphicStateDef
        /// rather than from the node.
        /// </summary>
        private static void ReportUnmatchedPaths()
        {
            var known = new HashSet<string>();
            foreach (PawnRenderTreeDef tree in DefDatabase<PawnRenderTreeDef>.AllDefsListForReading)
                Collect(tree.root, known);
            foreach (GraphicStateDef state in DefDatabase<GraphicStateDef>.AllDefsListForReading)
            {
                string texPath = state.defaultGraphicData?.texPath;
                if (!texPath.NullOrEmpty()) known.Add(texPath);
            }

            foreach (var (kind, ext) in kinds)
                foreach (RacePartSwap part in ext.parts)
                {
                    if (part.source.NullOrEmpty()) continue;
                    bool matched = false;
                    foreach (string path in known)
                        if (part.prefix ? path.StartsWith(part.source) : path == part.source)
                        {
                            matched = true;
                            break;
                        }
                    if (!matched)
                        Log.Error($"[MiliraXian] {kind.defName} wants to replace part texture "
                            + $"'{part.source}', which no render tree or graphic state uses.");
                }
        }

        private static void Collect(PawnRenderNodeProperties props, HashSet<string> paths)
        {
            if (props == null) return;
            if (!props.texPath.NullOrEmpty()) paths.Add(props.texPath);
            if (props.children != null)
                foreach (PawnRenderNodeProperties child in props.children) Collect(child, paths);
        }

        public static Graphic GraphicFor(Pawn pawn, string nodeTexPath)
        {
            if (nodeTexPath.NullOrEmpty() || pawn?.kindDef == null
                || !kinds.TryGetValue(pawn.kindDef, out var ext)) return null;

            var key = (pawn.kindDef, nodeTexPath);
            if (graphics.TryGetValue(key, out Graphic cached)) return cached;

            Graphic graphic = null;
            foreach (RacePartSwap part in ext.parts)
            {
                string path = PathFor(part, nodeTexPath);
                if (path == null) continue;
                graphic = GraphicDatabase.Get<Graphic_Multi>(path, ShaderDatabase.Cutout, Vector2.one, Color.white);
                break;
            }
            graphics[key] = graphic;
            return graphic;
        }

        /// <summary>
        /// The same swap applied to an already-built graphic, keeping its class and draw data.
        /// Used for animation frames, whose graphics the race declares on its GraphicStateDefs
        /// rather than on a render node, and which are single-image rather than four-way.
        /// </summary>
        public static Graphic GraphicLike(Pawn pawn, Graphic original)
        {
            if (original?.path == null || pawn?.kindDef == null
                || !kinds.TryGetValue(pawn.kindDef, out var ext)) return null;

            var key = (pawn.kindDef, original.path);
            if (graphics.TryGetValue(key, out Graphic cached)) return cached;

            Graphic graphic = null;
            foreach (RacePartSwap part in ext.parts)
            {
                string path = PathFor(part, original.path);
                if (path == null) continue;
                graphic = GraphicDatabase.Get(original.GetType(), path, original.Shader, original.drawSize,
                    original.Color, original.ColorTwo, original.data, null);
                break;
            }
            graphics[key] = graphic;
            return graphic;
        }

        /// <summary>
        /// Where a character keeps its own copy of a race texture, or null if it declares no
        /// replacement for that path. Lets a character's own code reach the same mapping instead
        /// of repeating the destination as a second constant.
        /// </summary>
        public static string TexPathFor(Pawn pawn, string raceTexPath)
        {
            if (pawn?.kindDef == null || !kinds.TryGetValue(pawn.kindDef, out var ext)) return null;
            foreach (RacePartSwap part in ext.parts)
            {
                string path = PathFor(part, raceTexPath);
                if (path != null) return path;
            }
            return null;
        }

        private static string PathFor(RacePartSwap part, string nodeTexPath)
        {
            if (part.source.NullOrEmpty() || part.target.NullOrEmpty()) return null;
            if (!part.prefix) return nodeTexPath == part.source ? part.target : null;
            return nodeTexPath.StartsWith(part.source)
                ? part.target + nodeTexPath.Substring(part.source.Length)
                : null;
        }

        /// <summary>
        /// The replacement declared in XML for <paramref name="raceAnimation"/>, ignoring
        /// <see cref="AnimationOverride"/>.
        /// </summary>
        public static AnimationDef DeclaredAnimation(Pawn pawn, AnimationDef raceAnimation)
        {
            if (pawn?.kindDef == null || raceAnimation == null
                || !kinds.TryGetValue(pawn.kindDef, out var ext)) return null;
            foreach (RacePartAnimationSwap swap in ext.animations)
                if (swap.source == raceAnimation) return swap.target;
            return null;
        }

        /// <summary>
        /// The animation to play in place of <paramref name="raceAnimation"/>, or null to keep
        /// the race's own. An override applies even where XML declares no replacement: a part
        /// whose frames are swapped by texture still plays the race's animation, and a character
        /// may have a richer version of it to offer.
        /// </summary>
        public static AnimationDef AnimationFor(Pawn pawn, AnimationDef raceAnimation)
        {
            if (raceAnimation == null) return null;
            return AnimationOverride?.Invoke(pawn, raceAnimation) ?? DeclaredAnimation(pawn, raceAnimation);
        }
    }
}
