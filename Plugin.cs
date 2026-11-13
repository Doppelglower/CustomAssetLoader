using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using CustomAssetLoader.Patches.Features;
using HarmonyLib;
using ModularSkillScripts;

namespace CustomAssetLoader;

[BepInPlugin(GUID, PLUGIN_NAME, VERSION)]
[BepInDependency("Lethe", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("GlitchGames.ModularSkillScripts")]
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
        BankPatches.Apply(harmony);
        SpriteIdlePatches.Apply(harmony);
        TimelineSfxPatches.Apply(harmony);

        // combo conseq
        harmony.PatchAll(typeof(CombosAndMore));
        MainClass.consequenceDict["comboinput"] = new Conseq_ComboTimelineInput();
        MainClass.consequenceDict["comborefreshdir"] = new Conseq_ComboTimelineRefreshDirection();

        BundleLog.Loaded($"{PLUGIN_NAME} v{VERSION} loaded.");
    }
}
