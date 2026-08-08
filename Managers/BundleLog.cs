using BepInEx.Configuration;

namespace CustomBundleLoader.Managers;

/// <summary>
/// Central logging gate. Errors, Warn, and one-line milestones always print;
/// everything else requires <see cref="VerboseLog"/>.
/// <see cref="Startup"/> / <see cref="Loaded"/> — plugin load, bundle reload, catalog ready.
/// <see cref="Verbose"/> / <see cref="VerboseWarn"/> — shader remap, replaceTex, asset serve, probes.
/// </summary>
public static class BundleLog
{
    private static ConfigFile _config = null!;

    private static ConfigEntry<bool> verboseToggle = null!;

    public static bool VerboseLog => verboseToggle.Value;

    public static void Bind(ConfigFile config)
    {
        _config = config;
        verboseToggle = config.Bind(
            CONFIG_SECTION_LOGGING,
            CONFIG_VERBOSE_LOG,
            false,
            "Emit detailed bundle and shader-remap logs. Reloaded on each bundle reload.");
    }

    public static void ReloadConfig() => _config.Reload();

    public static void Startup(string message) => Plugin.Log.LogInfo(message);

    public static void Loaded(string message) => Plugin.Log.LogInfo(message);

    public static void Error(string message) => Plugin.Log.LogError(message);

    public static void Warn(string message) => Plugin.Log.LogWarning(message);

    public static void Verbose(string message)
    {
        if (VerboseLog)
        {
            Plugin.Log.LogInfo(message);
        }
    }

    public static void VerboseWarn(string message)
    {
        if (VerboseLog)
        {
            Plugin.Log.LogWarning(message);
        }
    }
}
