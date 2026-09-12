using System.IO;
using BepInEx;
using FMOD.Studio;
using FMODUnity;

namespace CustomAssetLoader.Managers;

public static class BankManager
{
    private static readonly List<(string Path, Bank Bank)> LoadedBanks = new();
    private static bool _loadAttempted;

    public static int LoadedCount => LoadedBanks.Count;

    /// <summary>
    /// Unload previously loaded custom banks and rescan <c>custom_banks</c>.
    /// Same Lethe hot-reload window as <see cref="BundleManager.RequestReload"/>
    /// (<c>LoadUserDataAndSetScene</c> postfix). No-ops if FMOD is not ready yet.
    /// </summary>
    public static void RequestReload()
    {
        if (!TryGetStudioSystem(out _, logFailures: false))
        {
            BundleLog.Verbose("FMOD StudioSystem not ready; skip custom bank reload.");
            return;
        }

        UnloadModBanks();
        _loadAttempted = false;
        LoadModBanks();
    }

    /// <summary>
    /// Scan each Lethe mod's <c>custom_banks</c> folder and load every <c>*.bank</c>
    /// (including <c>*.strings.bank</c>) into the global FMOD Studio system.
    /// Safe to call more than once; a completed scan is not repeated until
    /// <see cref="RequestReload"/>.
    /// </summary>
    public static void LoadModBanks()
    {
        if (_loadAttempted)
        {
            return;
        }

        if (!TryGetStudioSystem(out FMOD.Studio.System fmodSystem, logFailures: true))
        {
            return;
        }

        _loadAttempted = true;

        string modsRoot = Path.Combine(Paths.PluginPath, MODS_ROOT);
        if (!Directory.Exists(modsRoot))
        {
            BundleLog.Verbose($"Mods root not found: {modsRoot}");
            return;
        }

        int found = 0;
        int loaded = 0;
        foreach (string modDir in Directory.GetDirectories(modsRoot))
        {
            string bankDir = Path.Combine(modDir, BANK_DIRECTORY);
            if (!Directory.Exists(bankDir))
            {
                continue;
            }

            string[] bankPaths = Directory.GetFiles(bankDir, "*.bank", SearchOption.TopDirectoryOnly);
            Array.Sort(bankPaths, CompareBanksStringsFirst);

            foreach (string bankPath in bankPaths)
            {
                found++;
                if (TryLoadBank(fmodSystem, bankPath))
                {
                    loaded++;
                }
            }
        }

        if (found == 0)
        {
            BundleLog.Verbose($"No .bank files found under {modsRoot}/*/{BANK_DIRECTORY}");
            return;
        }

        BundleLog.Startup($"Loaded {loaded}/{found} custom FMOD bank(s).");
    }

    /// <summary>
    /// Load immediately when FMOD is already initialized (plugin loaded late).
    /// </summary>
    public static void TryLoadIfStudioReady()
    {
        if (_loadAttempted)
        {
            return;
        }

        if (!TryGetStudioSystem(out _, logFailures: false))
        {
            return;
        }

        LoadModBanks();
    }

    private static void UnloadModBanks()
    {
        if (LoadedBanks.Count == 0)
        {
            return;
        }

        int unloaded = 0;
        // Reverse of load order: gameplay banks first, *.strings.bank last.
        for (int i = LoadedBanks.Count - 1; i >= 0; i--)
        {
            (string path, Bank bank) = LoadedBanks[i];
            try
            {
                if (!bank.isValid())
                {
                    continue;
                }

                FMOD.RESULT result = bank.unload();
                if (result != FMOD.RESULT.OK)
                {
                    BundleLog.Error($"Failed to unload bank: {result} - Path: {path}");
                    continue;
                }

                unloaded++;
            }
            catch (Exception ex)
            {
                BundleLog.VerboseWarn($"Bank.unload failed for {path}: {ex.Message}");
            }
        }

        LoadedBanks.Clear();
        BundleLog.Startup($"Unloaded {unloaded} custom FMOD bank(s).");
    }

    private static bool TryLoadBank(FMOD.Studio.System fmodSystem, string bankPath)
    {
        FMOD.RESULT result = fmodSystem.loadBankFile(
            bankPath,
            LOAD_BANK_FLAGS.NORMAL,
            out Bank bank);

        if (result != FMOD.RESULT.OK)
        {
            BundleLog.Error($"Failed to load bank: {result} - Path: {bankPath}");
            return false;
        }

        LoadedBanks.Add((bankPath, bank));
        BundleLog.Startup($"Successfully loaded bank {bankPath}");
        return true;
    }

    private static bool TryGetStudioSystem(out FMOD.Studio.System fmodSystem, bool logFailures)
    {
        fmodSystem = default;
        try
        {
            fmodSystem = RuntimeManager.StudioSystem;
        }
        catch (Exception ex)
        {
            if (logFailures)
            {
                BundleLog.Error($"FMOD StudioSystem unavailable: {ex.Message}");
            }

            return false;
        }

        if (!fmodSystem.isValid())
        {
            if (logFailures)
            {
                BundleLog.Error("FMOD StudioSystem is not valid; skip custom bank load.");
            }

            return false;
        }

        return true;
    }

    private static int CompareBanksStringsFirst(string left, string right)
    {
        bool leftStrings = IsStringsBank(left);
        bool rightStrings = IsStringsBank(right);
        if (leftStrings != rightStrings)
        {
            return leftStrings ? -1 : 1;
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStringsBank(string path) =>
        path.EndsWith(".strings.bank", StringComparison.OrdinalIgnoreCase);
}
