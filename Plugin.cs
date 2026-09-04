using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using CustomBundleLoader.Patches;
using HarmonyLib;

namespace CustomBundleLoader;

[BepInPlugin(GUID, PLUGIN_NAME, VERSION)]
[BepInDependency("Lethe", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BasePlugin
{
    public static new ManualLogSource Log { get; private set; } = null!;

    public override void Load()
    {
        Log = base.Log;
        BundleLog.Bind(Config);

        var harmony = new Harmony(GUID);
        BundlePatches.Apply(harmony);
        BattleEffectListPatches.Apply(harmony);
        CustomBundleMotionPatches.Apply(harmony);

        BundleLog.Loaded($"{PLUGIN_NAME} v{VERSION} loaded.");
    }
}
