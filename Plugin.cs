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
        BundleLog.Startup($"{PLUGIN_NAME} v{VERSION} loaded.");

        var harmony = new Harmony(GUID);
        BundlePatches.Apply(harmony);
        CustomBundleMotionPatches.Apply(harmony);
    }
}
