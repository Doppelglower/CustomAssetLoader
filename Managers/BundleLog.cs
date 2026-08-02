namespace CustomBundleLoader.Managers;

/// <summary>
/// Central logging gate. Errors and one-line milestones always print;
/// everything else requires <see cref="VerboseLog"/>.
/// </summary>
public static class BundleLog
{
    public const bool VerboseLog = false;

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
