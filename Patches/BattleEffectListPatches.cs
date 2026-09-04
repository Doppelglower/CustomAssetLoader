using System.Reflection;
using HarmonyLib;

namespace CustomBundleLoader.Patches;

/// <summary>
/// Loads manifest <c>assetType=BattleEffectList</c> ScriptableObjects and appends them
/// to <see cref="BattleEffectManager.battleEffectLists_next"/> after official Init.
/// Does not register Addressable keys.
/// </summary>
public static class BattleEffectListPatches
{
    public static void Apply(Harmony harmony)
    {
        MethodInfo init = AccessTools.Method(
            typeof(BattleEffectManager),
            nameof(BattleEffectManager.Init));
        if (init == null)
        {
            BundleLog.Error("BattleEffectList patch target missing: BattleEffectManager.Init");
            return;
        }

        harmony.Patch(
            init,
            postfix: new HarmonyMethod(typeof(BattleEffectListPatches), nameof(Postfix_Init)));
    }

    public static void Postfix_Init(BattleEffectManager __instance)
    {
        BundleManager.InjectBattleEffectLists(__instance);
    }
}
