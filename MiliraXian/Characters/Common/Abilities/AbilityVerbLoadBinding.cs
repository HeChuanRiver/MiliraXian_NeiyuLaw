using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common.Abilities;

[HarmonyPatch(typeof(Ability), nameof(Ability.ExposeData))]
internal static class AbilityVerbLoadBinding
{
    private static void Prefix(Ability __instance)
    {
        if (Scribe.mode != LoadSaveMode.PostLoadInit)
            return;

        AbilityDef def = __instance.def;
        ModContentPack content = NeiyuLawMod.Instance?.Content;
        if (content == null || !ReferenceEquals(def?.modContentPack, content))
            return;

        // VerbTracker may have replaced the Verb after Ability.Initialize bound it.
        Verb verb = null;
        string stage = "读取施法追踪器";
        try
        {
            VerbTracker tracker = __instance.VerbTracker;
            IVerbOwner owner = tracker.directOwner;
            if (!ReferenceEquals(owner, __instance))
            {
                string ownerId = (owner as Ability)?.GetUniqueLoadID() ?? "未知";
                Log.Error($"{Context()} 施法追踪器归属异常，预期为当前技能实例（{__instance.GetType().FullName}），"
                    + $"实际归属类型为 {owner?.GetType().FullName ?? "空"}，归属 ID 为 {ownerId}。无法恢复绑定。");
                return;
            }

            stage = "取得主施法对象";
            verb = tracker.PrimaryVerb;
            if (verb == null)
            {
                Log.Error($"{Context()} 施法追踪器缺少主 Verb。无法恢复绑定。");
                return;
            }

            if (verb is not IAbilityVerb abilityVerb)
            {
                Log.Error($"{Context()} 主 Verb 缺少 {nameof(IAbilityVerb)} 接口。无法恢复绑定。");
                return;
            }

            stage = "读取技能绑定";
            Ability previous = abilityVerb.Ability;
            if (ReferenceEquals(previous, __instance))
                return;

            stage = "恢复技能绑定";
            abilityVerb.Ability = __instance;
            stage = "确认技能绑定";
            Ability current = abilityVerb.Ability;
            if (!ReferenceEquals(current, __instance))
            {
                Log.Error($"{Context()} 写入后绑定仍为 {current?.GetUniqueLoadID() ?? "空"}。无法恢复绑定。");
                return;
            }

            string reason = previous == null
                ? "读档后施法对象缺少技能绑定"
                : $"读档后施法对象指向其他技能实例 {previous.GetUniqueLoadID()}（{previous.def?.defName ?? "Def 缺失"}）";
            Log.Warning($"{Context()} {reason}，已绑定到当前技能实例。");
        }
        catch (Exception exception)
        {
            Log.Error($"{Context()} 读档时{stage}发生异常。\n{exception}");
        }

        string Context() => $"[MiliraXian] 技能 {def.defName}（{__instance.GetUniqueLoadID()}），"
            + $"Verb ID 为 {verb?.loadID ?? "未知"}，实际类型为 {verb?.GetType().FullName ?? "无"}，"
            + $"预期类型为 {def.verbProperties?.verbClass?.FullName ?? "未配置"}。";
    }
}
