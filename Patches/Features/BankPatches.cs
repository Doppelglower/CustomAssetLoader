using System.Reflection;
using FMODUnity;
using HarmonyLib;

namespace CustomAssetLoader.Patches.Features;

public static class BankPatches
{
    public static void Apply(Harmony harmony)
    {
        MethodInfo initialize = AccessTools.Method(
            typeof(RuntimeManager),
            nameof(RuntimeManager.Initialize));
        if (initialize != null)
        {
            harmony.Patch(
                initialize,
                postfix: new HarmonyMethod(typeof(BankPatches), nameof(Postfix_Initialize)));
        }
        else
        {
            BundleLog.Error("Bank patch target missing: RuntimeManager.Initialize");
        }

        MethodInfo loadUserDataAndSetScene = AccessTools.Method(
            typeof(GlobalGameManager),
            nameof(GlobalGameManager.LoadUserDataAndSetScene));
        if (loadUserDataAndSetScene != null)
        {
            harmony.Patch(
                loadUserDataAndSetScene,
                postfix: new HarmonyMethod(typeof(BankPatches), nameof(Postfix_LoadUserDataAndSetScene)));
        }
        else
        {
            BundleLog.Error("Bank patch target missing: GlobalGameManager.LoadUserDataAndSetScene");
        }

        BankManager.TryLoadIfStudioReady();
    }

    public static void Postfix_Initialize()
    {
        BundleLog.Startup("FMOD inited, loading custom banks...");
        BankManager.LoadModBanks();
    }

    public static void Postfix_LoadUserDataAndSetScene()
    {
        BankManager.RequestReload();
    }
}
